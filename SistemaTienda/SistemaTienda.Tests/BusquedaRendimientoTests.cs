using System.Diagnostics;
using Xunit.Abstractions;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;

namespace SistemaTienda.Tests;

/// <summary>
/// H6.3: la búsqueda del mostrador debe responder con 3000 productos, ignorar
/// acentos y no perder dígitos cuando el escáner escribe el código completo.
/// </summary>
[CollectionDefinition("Rendimiento", DisableParallelization = true)]
public sealed class RendimientoCollection
{
}

[Collection("Rendimiento")]
public sealed class BusquedaRendimientoTests
{
    private readonly ITestOutputHelper _salida;

    public BusquedaRendimientoTests(ITestOutputHelper salida)
    {
        _salida = salida;
    }

    private static void Sembrar(BaseDatosPrueba bd, int cantidad)
    {
        var productos = new List<Producto>();
        for (var i = 0; i < cantidad; i++)
        {
            productos.Add(new Producto
            {
                CodigoBarras = $"H63-{i:D5}",
                Nombre = $"Producto genérico {i:D5}",
                Costo = 1_000,
                Precio = 2_000,
                Stock = 1
            });
        }

        var importado = bd.Db.ImportarProductos(productos, "bench.csv", "hash", "Dueño",
            cantidad, 0);
        Assert.True(importado.IsSuccess, importado.Message);
    }

    [Fact]
    public void Busqueda_3000Productos_RespondeRapido()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        Sembrar(bd, 3_000);

        var reloj = Stopwatch.StartNew();
        for (var i = 0; i < 20; i++)
        {
            var sugerencias = bd.Inventario.SugerirProductos("producto generico 0149", 8);
            Assert.NotEmpty(sugerencias);
        }
        var promedioSugerencias = reloj.Elapsed.TotalMilliseconds / 20;

        reloj.Restart();
        for (var i = 0; i < 20; i++)
        {
            var encontrados = bd.Inventario.BuscarPorNombre("producto generico 015");
            Assert.Equal(100, encontrados.Count);
        }
        var promedioNombres = reloj.Elapsed.TotalMilliseconds / 20;

        reloj.Restart();
        for (var i = 0; i < 50; i++)
            Assert.NotNull(bd.Inventario.BuscarPorCodigo("H63-01499"));
        var promedioCodigo = reloj.Elapsed.TotalMilliseconds / 50;

        _salida.WriteLine($"Búsqueda 3000: sugerencias {promedioSugerencias:F2} ms, " +
                          $"nombres {promedioNombres:F2} ms, código {promedioCodigo:F2} ms");

        Assert.True(promedioSugerencias < 250,
            $"Las sugerencias tardaron {promedioSugerencias:F2} ms en promedio");
        Assert.True(promedioNombres < 250,
            $"La búsqueda por nombre tardó {promedioNombres:F2} ms en promedio");
        Assert.True(promedioCodigo < 100,
            $"La búsqueda por código tardó {promedioCodigo:F2} ms en promedio");
    }

    [Fact]
    public void BuscarPorNombre_RespetaElMaximo()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        Sembrar(bd, 30);

        var encontrados = bd.Inventario.BuscarPorNombre("Producto", maximo: 10);

        Assert.Equal(10, encontrados.Count);
    }

    [Fact]
    public void Escaner_ConCadaPrefijo_EncuentraElProductoYElCodigoExactoVaPrimero()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = new Producto
        {
            CodigoBarras = "7501234567890",
            Nombre = "Producto escaneado",
            Costo = 1_000,
            Precio = 2_000,
            Stock = 5
        };
        Assert.True(bd.Inventario.AgregarProducto(producto).IsSuccess);

        for (var longitud = 1; longitud <= producto.CodigoBarras.Length; longitud++)
        {
            var prefijo = producto.CodigoBarras[..longitud];
            var sugerencias = bd.Inventario.SugerirProductos(prefijo, 8);
            Assert.Contains(sugerencias, sugerencia => sugerencia.Id == producto.Id);
        }

        var final = bd.Inventario.SugerirProductos(producto.CodigoBarras, 8);
        Assert.Equal(producto.Id, final[0].Id);
        Assert.Single(final, sugerencia => sugerencia.Id == producto.Id);
        Assert.Equal(producto.Id, bd.Inventario.BuscarPorCodigo(producto.CodigoBarras).Id);
    }

    [Fact]
    public void Sugerencias_PrefijoDeCodigoVaAntesQueElNombre()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var porCodigo = new Producto
        {
            CodigoBarras = "750999",
            Nombre = "Producto común",
            Precio = 1_000
        };
        var porNombre = new Producto
        {
            CodigoBarras = "123",
            Nombre = "750999 especial",
            Precio = 1_000
        };
        Assert.True(bd.Inventario.AgregarProducto(porCodigo).IsSuccess);
        Assert.True(bd.Inventario.AgregarProducto(porNombre).IsSuccess);

        var sugerencias = bd.Inventario.SugerirProductos("7509", 8);

        Assert.Equal(porCodigo.Id, sugerencias[0].Id);
        Assert.Contains(sugerencias, sugerencia => sugerencia.Id == porNombre.Id);
    }

    [Fact]
    public void Busqueda_IgnoraAcentosYMayusculasEnProductos()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = new Producto
        {
            CodigoBarras = "H63-CAFE",
            Nombre = "CAFÉ SOLUBLE 200 g",
            Precio = 1_000
        };
        Assert.True(bd.Inventario.AgregarProducto(producto).IsSuccess);

        Assert.Contains(bd.Inventario.BuscarPorNombre("cafe"),
            encontrado => encontrado.Id == producto.Id);
        Assert.Contains(bd.Inventario.BuscarPorNombre("CAFÉ"),
            encontrado => encontrado.Id == producto.Id);
        Assert.Equal(producto.Id, bd.Inventario.SugerirProductos("cafe")[0].Id);
    }
}
