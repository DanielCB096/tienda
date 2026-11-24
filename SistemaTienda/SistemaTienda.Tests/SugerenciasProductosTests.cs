using SistemaTienda.Core;

namespace SistemaTienda.Tests;

public sealed class SugerenciasProductosTests
{
    private static Producto Agregar(BaseDatosPrueba bd, string codigo, string nombre,
        long precio = 2_000, int stock = 10)
    {
        var producto = new Producto
        {
            CodigoBarras = codigo,
            Nombre = nombre,
            Costo = 1_000,
            Precio = precio,
            Stock = stock
        };

        var resultado = bd.Inventario.AgregarProducto(producto);
        Assert.True(resultado.IsSuccess, resultado.Message);
        return producto;
    }

    [Fact]
    public void SugerirProductos_TextoVacio_DevuelveListaVacia()
    {
        using var bd = new BaseDatosPrueba();
        Agregar(bd, "A1", "Arroz 1 kg");

        Assert.Empty(bd.Inventario.SugerirProductos(""));
        Assert.Empty(bd.Inventario.SugerirProductos("   "));
    }

    [Fact]
    public void SugerirProductos_PrefijoDeNombreVaAntesQueCoincidenciaInterna()
    {
        using var bd = new BaseDatosPrueba();
        Agregar(bd, "C1", "Chocolate con leche");
        Agregar(bd, "L1", "Leche entera 1 L");

        var sugerencias = bd.Inventario.SugerirProductos("leche");

        Assert.Equal(2, sugerencias.Count);
        Assert.Equal("Leche entera 1 L", sugerencias[0].Nombre);
        Assert.Equal("Chocolate con leche", sugerencias[1].Nombre);
    }

    [Fact]
    public void SugerirProductos_CodigoExactoVaPrimero()
    {
        using var bd = new BaseDatosPrueba();
        Agregar(bd, "7501000000018", "Refresco cola 600 ml");
        Agregar(bd, "7501000000099", "Refresco toronja 2 L");
        Agregar(bd, "X1", "Refresco de temporada");

        var sugerencias = bd.Inventario.SugerirProductos("7501000000099");

        Assert.NotEmpty(sugerencias);
        Assert.Equal("Refresco toronja 2 L", sugerencias[0].Nombre);
    }

    [Fact]
    public void SugerirProductos_ExcluyeInactivos()
    {
        using var bd = new BaseDatosPrueba();
        var activo = Agregar(bd, "A1", "Arroz 1 kg");
        var inactivo = Agregar(bd, "A2", "Arroz integral 1 kg");

        var desactivado = bd.Inventario.DesactivarProducto(inactivo.Id);
        Assert.True(desactivado.IsSuccess, desactivado.Message);

        var sugerencias = bd.Inventario.SugerirProductos("arroz");

        Assert.Single(sugerencias);
        Assert.Equal(activo.Id, sugerencias[0].Id);
    }

    [Fact]
    public void SugerirProductos_RespetaElMaximo()
    {
        using var bd = new BaseDatosPrueba();
        for (var i = 1; i <= 12; i++)
            Agregar(bd, $"B{i:00}", $"Bebida sabor {i}");

        var sugerencias = bd.Inventario.SugerirProductos("bebida", maximo: 5);

        Assert.Equal(5, sugerencias.Count);
    }

    [Fact]
    public void SugerirProductos_IgnoraAcentosYMayusculas()
    {
        using var bd = new BaseDatosPrueba();
        Agregar(bd, "AZ1", "Azúcar 1 kg");

        var sugerencias = bd.Inventario.SugerirProductos("AZUCAR");

        Assert.Single(sugerencias);
        Assert.Equal("Azúcar 1 kg", sugerencias[0].Nombre);
    }

    [Fact]
    public void SugerirProductos_CatalogoGrande_DevuelveSoloElMaximo()
    {
        using var bd = new BaseDatosPrueba();
        for (var i = 0; i < 1_000; i++)
            Agregar(bd, $"G{i:0000}", $"Producto genérico {i:0000}");

        var sugerencias = bd.Inventario.SugerirProductos("producto generico 09", maximo: 8);

        Assert.Equal(8, sugerencias.Count);
        Assert.All(sugerencias, p => Assert.Contains("09", p.Nombre));
    }
}
