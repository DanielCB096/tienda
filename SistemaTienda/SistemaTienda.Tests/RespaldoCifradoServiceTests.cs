using SistemaTienda.Application.Services;
using SistemaTienda.Core;

namespace SistemaTienda.Tests;

public sealed class RespaldoCifradoServiceTests
{
    private static Producto CrearProducto(BaseDatosPrueba bd, string codigo)
    {
        var producto = new Producto
        {
            CodigoBarras = codigo,
            Nombre = $"Producto {codigo}",
            Costo = 1_000,
            Precio = 2_000,
            Stock = 5
        };

        var alta = bd.Inventario.AgregarProducto(producto);
        Assert.True(alta.IsSuccess, alta.Message);
        return producto;
    }

    [Fact]
    public void CrearRespaldoConClave_DejaSoloElArchivoCifrado()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var clave = CifradoRespaldo.GenerarClave();
        var respaldos = new RespaldoService(bd.Db);

        var creado = respaldos.CrearRespaldo("prueba", clave);

        Assert.True(creado.IsSuccess, creado.Message);
        Assert.EndsWith(CifradoRespaldo.Extension, creado.Value);
        Assert.True(File.Exists(creado.Value));
        Assert.True(CifradoRespaldo.EsCifrado(File.ReadAllBytes(creado.Value)));

        var sinCifrar = creado.Value[..^CifradoRespaldo.Extension.Length];
        Assert.False(File.Exists(sinCifrar));

        var lista = respaldos.ListarRespaldos().Value;
        Assert.Contains(lista, r => r.Ruta == creado.Value && r.Cifrado);
    }

    [Fact]
    public void CrearRespaldoSinClave_SigueSiendoPlano()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var respaldos = new RespaldoService(bd.Db);

        var creado = respaldos.CrearRespaldo("plano");

        Assert.True(creado.IsSuccess, creado.Message);
        Assert.EndsWith(".db", creado.Value);
        Assert.False(CifradoRespaldo.EsCifrado(File.ReadAllBytes(creado.Value)));
    }

    [Fact]
    public void RestaurarConClaveCorrecta_RecuperaElContenido()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        CrearProducto(bd, "ANTES");

        var clave = CifradoRespaldo.GenerarClave();
        var respaldos = new RespaldoService(bd.Db);
        var creado = respaldos.CrearRespaldo("prueba", clave);
        Assert.True(creado.IsSuccess, creado.Message);

        CrearProducto(bd, "DESPUES");

        var restaurado = respaldos.Restaurar(creado.Value, confirmado: true,
            claveRespaldo: clave);

        Assert.True(restaurado.IsSuccess, restaurado.Message);
        var productos = bd.Inventario.ObtenerProductos();
        Assert.Contains(productos, p => p.CodigoBarras == "ANTES");
        Assert.DoesNotContain(productos, p => p.CodigoBarras == "DESPUES");
    }

    [Fact]
    public void RestaurarSinClave_FallaSinTocarLaBase()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        CrearProducto(bd, "ORIGINAL");

        var clave = CifradoRespaldo.GenerarClave();
        var respaldos = new RespaldoService(bd.Db);
        var creado = respaldos.CrearRespaldo("prueba", clave);

        var resultado = respaldos.Restaurar(creado.Value, confirmado: true);

        Assert.True(resultado.IsError);
        Assert.Contains("cifrado", resultado.Message);
        Assert.Contains(bd.Inventario.ObtenerProductos(),
            p => p.CodigoBarras == "ORIGINAL");
    }

    [Fact]
    public void RestaurarConClaveIncorrecta_FallaSinTocarLaBase()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        CrearProducto(bd, "ORIGINAL");

        var clave = CifradoRespaldo.GenerarClave();
        var respaldos = new RespaldoService(bd.Db);
        var creado = respaldos.CrearRespaldo("prueba", clave);

        var otra = CifradoRespaldo.GenerarClave();
        var resultado = respaldos.Restaurar(creado.Value, confirmado: true,
            claveRespaldo: otra);

        Assert.True(resultado.IsError);
        Assert.Contains("clave", resultado.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(bd.Inventario.ObtenerProductos(),
            p => p.CodigoBarras == "ORIGINAL");
    }
}
