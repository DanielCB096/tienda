using SistemaTienda.Core;

namespace SistemaTienda.Tests;

public sealed class InventarioBordeTests
{
    private static Producto Producto(string codigo = "7501000000010", string nombre = "Refresco",
        decimal costo = 10m, decimal precio = 15m, int stock = 10, int minimo = 5) => new()
        {
            CodigoBarras = codigo,
            Nombre = nombre,
            Costo = costo,
            Precio = precio,
            Stock = stock,
            StockMinimo = minimo,
            Categoria = "Bebidas",
            Impuesto = 16m
        };

    [Fact]
    public void ReducirStockVenta_ConCantidadIgualAlStock_DejaCeroYSegundoIntentoFalla()
    {
        using var bd = new BaseDatosPrueba();
        bd.Inventario.AgregarProducto(Producto(stock: 3));
        var producto = Assert.Single(bd.Inventario.ObtenerProductos());

        var venta = bd.Inventario.ReducirStockVenta(producto.Id, 3, "Cajero");

        Assert.True(venta.IsSuccess, venta.Message);
        Assert.Equal(0, bd.Inventario.BuscarPorId(producto.Id).Stock);

        var segundaVenta = bd.Inventario.ReducirStockVenta(producto.Id, 1, "Cajero");

        Assert.True(segundaVenta.IsError);
        Assert.Equal(0, bd.Inventario.BuscarPorId(producto.Id).Stock);
        Assert.Equal(0, bd.Db.ObtenerStock(producto.Id));
        Assert.Equal(2, bd.Inventario.HistorialMovimientos(producto.Id).Count);
    }

    [Fact]
    public void RegistrarEntrada_ConCostoNegativo_FallaYNoModificaStock()
    {
        using var bd = new BaseDatosPrueba();
        bd.Inventario.AgregarProducto(Producto(stock: 5));
        var producto = Assert.Single(bd.Inventario.ObtenerProductos());

        var entrada = bd.Inventario.RegistrarEntrada(producto.Id, 2, -0.01m, "Dueño");

        Assert.True(entrada.IsError);
        Assert.Equal(5, bd.Inventario.BuscarPorId(producto.Id).Stock);
        Assert.Single(bd.Inventario.HistorialMovimientos(producto.Id));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void RegistrarEntrada_ConCantidadNoPositiva_Falla(int cantidad)
    {
        using var bd = new BaseDatosPrueba();
        bd.Inventario.AgregarProducto(Producto(stock: 5));
        var producto = Assert.Single(bd.Inventario.ObtenerProductos());

        var entrada = bd.Inventario.RegistrarEntrada(producto.Id, cantidad, 8m, "Dueño");

        Assert.True(entrada.IsError);
        Assert.Equal(5, bd.Inventario.BuscarPorId(producto.Id).Stock);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RegistrarMerma_ConCantidadNoPositiva_Falla(int cantidad)
    {
        using var bd = new BaseDatosPrueba();
        bd.Inventario.AgregarProducto(Producto(stock: 5));
        var producto = Assert.Single(bd.Inventario.ObtenerProductos());

        var merma = bd.Inventario.RegistrarMerma(producto.Id, cantidad, "Roto", "Dueño");

        Assert.True(merma.IsError);
        Assert.Equal(5, bd.Inventario.BuscarPorId(producto.Id).Stock);
    }

    [Fact]
    public void AgregarProducto_ConCodigoDuplicadoTrasRecortar_FallaSinMovimientoExtra()
    {
        using var bd = new BaseDatosPrueba();
        Assert.True(bd.Inventario.AgregarProducto(Producto(codigo: "ABC123", stock: 5)).IsSuccess);

        var duplicado = bd.Inventario.AgregarProducto(Producto(codigo: "  ABC123  ", stock: 7));

        Assert.True(duplicado.IsError);

        var producto = Assert.Single(bd.Inventario.ObtenerProductos());
        Assert.Equal("ABC123", producto.CodigoBarras);
        Assert.Equal(5, producto.Stock);
        Assert.Single(bd.Inventario.HistorialMovimientos(producto.Id));
    }

    [Fact]
    public void BuscarPorNombre_ConComodinesLiterales_DevuelveSoloCoincidenciasExactas()
    {
        using var bd = new BaseDatosPrueba();
        bd.Inventario.AgregarProducto(Producto(codigo: "A1", nombre: "Promo_50%"));
        bd.Inventario.AgregarProducto(Producto(codigo: "A2", nombre: "PromoX50Y"));
        bd.Inventario.AgregarProducto(Producto(codigo: "A3", nombre: @"Ruta\Especial"));

        var guionBajo = Assert.Single(bd.Inventario.BuscarPorNombre("_"));
        Assert.Equal("A1", guionBajo.CodigoBarras);

        var porcentaje = Assert.Single(bd.Inventario.BuscarPorNombre("%"));
        Assert.Equal("A1", porcentaje.CodigoBarras);

        var diagonal = Assert.Single(bd.Inventario.BuscarPorNombre("\\"));
        Assert.Equal("A3", diagonal.CodigoBarras);

        var combinado = Assert.Single(bd.Inventario.BuscarPorNombre("Promo_50%"));
        Assert.Equal("A1", combinado.CodigoBarras);
    }
}
