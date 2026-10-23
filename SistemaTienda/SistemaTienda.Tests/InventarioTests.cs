using SistemaTienda.Core;

namespace SistemaTienda.Tests;

public sealed class InventarioTests
{
    private static Producto Producto(string codigo = "7501000000010", string nombre = "Refresco",
        long costo = 1_000, long precio = 1_500, int stock = 10, int minimo = 5) => new()
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
    public void AgregarProducto_RegistraStockInicialComoEntrada()
    {
        using var bd = new BaseDatosPrueba();

        var resultado = bd.Inventario.AgregarProducto(Producto(stock: 12));

        Assert.True(resultado.IsSuccess, resultado.Message);

        var producto = Assert.Single(bd.Inventario.ObtenerProductos());
        Assert.Equal(12, producto.Stock);
        Assert.Equal(12, bd.Db.ObtenerStock(producto.Id));

        var movimientos = bd.Inventario.HistorialMovimientos(producto.Id);
        var entrada = Assert.Single(movimientos);
        Assert.Equal(MovimientoTipo.Entrada, entrada.Tipo);
        Assert.Equal(12, entrada.Cantidad);
    }

    [Fact]
    public void AgregarProducto_ConCodigoDuplicado_FallaYNoDuplica()
    {
        using var bd = new BaseDatosPrueba();
        Assert.True(bd.Inventario.AgregarProducto(Producto()).IsSuccess);

        var duplicado = bd.Inventario.AgregarProducto(Producto(nombre: "Otro producto", stock: 1));

        Assert.True(duplicado.IsError);
        Assert.Single(bd.Inventario.ObtenerProductos());
    }

    [Theory]
    [InlineData("", "Nombre", 10)]
    [InlineData("123", "", 10)]
    [InlineData("123", "Nombre", -1)]
    public void AgregarProducto_ConDatosInvalidos_Falla(string codigo, string nombre, int stock)
    {
        using var bd = new BaseDatosPrueba();

        var producto = Producto(stock: stock);
        producto.CodigoBarras = codigo;
        producto.Nombre = nombre;

        Assert.True(bd.Inventario.AgregarProducto(producto).IsError);
    }

    [Fact]
    public void ReducirStockVenta_DescuentaStockYRegistraSalida()
    {
        using var bd = new BaseDatosPrueba();
        bd.Inventario.AgregarProducto(Producto(stock: 10));
        var producto = Assert.Single(bd.Inventario.ObtenerProductos());

        var venta = bd.Inventario.ReducirStockVenta(producto.Id, 3, "Cajero");

        Assert.True(venta.IsSuccess, venta.Message);
        Assert.Equal(7, bd.Inventario.BuscarPorId(producto.Id).Stock);
        Assert.Equal(7, bd.Db.ObtenerStock(producto.Id));

        var salida = bd.Inventario.HistorialMovimientos(producto.Id)
            .Single(m => m.Tipo == MovimientoTipo.Salida);
        Assert.Equal(-3, salida.Cantidad);
    }

    [Fact]
    public void ReducirStockVenta_SinStockSuficiente_FallaYNoModificaNada()
    {
        using var bd = new BaseDatosPrueba();
        bd.Inventario.AgregarProducto(Producto(stock: 2));
        var producto = Assert.Single(bd.Inventario.ObtenerProductos());

        var venta = bd.Inventario.ReducirStockVenta(producto.Id, 5, "Cajero");

        Assert.True(venta.IsError);
        Assert.Equal(2, bd.Inventario.BuscarPorId(producto.Id).Stock);
        Assert.Single(bd.Inventario.HistorialMovimientos(producto.Id));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void ReducirStockVenta_ConCantidadNoPositiva_Falla(int cantidad)
    {
        using var bd = new BaseDatosPrueba();
        bd.Inventario.AgregarProducto(Producto());
        var producto = Assert.Single(bd.Inventario.ObtenerProductos());

        Assert.True(bd.Inventario.ReducirStockVenta(producto.Id, cantidad, "Cajero").IsError);
    }

    [Fact]
    public void RegistrarMerma_ReduceStockYReportaCostoPerdido()
    {
        using var bd = new BaseDatosPrueba();
        bd.Inventario.AgregarProducto(Producto(costo: 1_000, stock: 10));
        var producto = Assert.Single(bd.Inventario.ObtenerProductos());

        var merma = bd.Inventario.RegistrarMerma(producto.Id, 2, "Caducado", "Dueño");

        Assert.True(merma.IsSuccess, merma.Message);
        Assert.Contains("20", merma.Message);
        Assert.Equal(8, bd.Inventario.BuscarPorId(producto.Id).Stock);

        var movimiento = bd.Inventario.HistorialMovimientos(producto.Id)
            .Single(m => m.Tipo == MovimientoTipo.Salida);
        Assert.Contains("Caducado", movimiento.Motivo);
    }

    [Fact]
    public void RegistrarMerma_SinMotivo_Falla()
    {
        using var bd = new BaseDatosPrueba();
        bd.Inventario.AgregarProducto(Producto());
        var producto = Assert.Single(bd.Inventario.ObtenerProductos());

        Assert.True(bd.Inventario.RegistrarMerma(producto.Id, 1, " ", "Dueño").IsError);
    }

    [Fact]
    public void RegistrarMerma_SinStockSuficiente_Falla()
    {
        using var bd = new BaseDatosPrueba();
        bd.Inventario.AgregarProducto(Producto(stock: 1));
        var producto = Assert.Single(bd.Inventario.ObtenerProductos());

        Assert.True(bd.Inventario.RegistrarMerma(producto.Id, 2, "Roto", "Dueño").IsError);
        Assert.Equal(1, bd.Inventario.BuscarPorId(producto.Id).Stock);
    }

    [Fact]
    public void RegistrarEntrada_AumentaStockYRegistraMovimiento()
    {
        using var bd = new BaseDatosPrueba();
        bd.Inventario.AgregarProducto(Producto(stock: 5));
        var producto = Assert.Single(bd.Inventario.ObtenerProductos());

        var entrada = bd.Inventario.RegistrarEntrada(producto.Id, 7, 950, "Dueño", "Compra");

        Assert.True(entrada.IsSuccess, entrada.Message);
        Assert.Equal(12, bd.Inventario.BuscarPorId(producto.Id).Stock);
        Assert.Equal(12, bd.Db.ObtenerStock(producto.Id));
    }

    [Fact]
    public void AjustarStock_RegistraSoloLaDiferencia()
    {
        using var bd = new BaseDatosPrueba();
        bd.Inventario.AgregarProducto(Producto(stock: 10));
        var producto = Assert.Single(bd.Inventario.ObtenerProductos());

        var ajuste = bd.Inventario.AjustarStock(producto.Id, 7, "Conteo físico", "Dueño");

        Assert.True(ajuste.IsSuccess, ajuste.Message);
        Assert.Equal(7, bd.Inventario.BuscarPorId(producto.Id).Stock);

        var movimiento = bd.Inventario.HistorialMovimientos(producto.Id)
            .Single(m => m.Tipo == MovimientoTipo.Ajuste);
        Assert.Equal(-3, movimiento.Cantidad);
        Assert.Equal(7, bd.Db.ObtenerStock(producto.Id));
    }

    [Fact]
    public void AjustarStock_SinCambios_NoRegistraMovimiento()
    {
        using var bd = new BaseDatosPrueba();
        bd.Inventario.AgregarProducto(Producto(stock: 10));
        var producto = Assert.Single(bd.Inventario.ObtenerProductos());

        var ajuste = bd.Inventario.AjustarStock(producto.Id, 10, "Conteo físico", "Dueño");

        Assert.True(ajuste.IsSuccess, ajuste.Message);
        Assert.Single(bd.Inventario.HistorialMovimientos(producto.Id));
    }

    [Theory]
    [InlineData(-1)]
    public void AjustarStock_ConStockNegativo_Falla(int nuevoStock)
    {
        using var bd = new BaseDatosPrueba();
        bd.Inventario.AgregarProducto(Producto());
        var producto = Assert.Single(bd.Inventario.ObtenerProductos());

        Assert.True(bd.Inventario.AjustarStock(producto.Id, nuevoStock, "Conteo", "Dueño").IsError);
    }

    [Fact]
    public void ProductosStockBajo_DevuelveSoloLosQueEstanEnElMinimoOMenos()
    {
        using var bd = new BaseDatosPrueba();
        bd.Inventario.AgregarProducto(Producto(codigo: "A1", nombre: "Escaso", stock: 3, minimo: 5));
        bd.Inventario.AgregarProducto(Producto(codigo: "A2", nombre: "Justo", stock: 5, minimo: 5));
        bd.Inventario.AgregarProducto(Producto(codigo: "A3", nombre: "Suficiente", stock: 50, minimo: 5));

        var bajos = bd.Inventario.ProductosStockBajo();

        Assert.Equal(2, bajos.Count);
        Assert.Contains(bajos, p => p.Nombre == "Escaso");
        Assert.Contains(bajos, p => p.Nombre == "Justo");
    }

    [Fact]
    public void BuscarPorNombre_EsParcialYNoDistingueMayusculas()
    {
        using var bd = new BaseDatosPrueba();
        bd.Inventario.AgregarProducto(Producto(codigo: "A1", nombre: "Refresco Cola"));
        bd.Inventario.AgregarProducto(Producto(codigo: "A2", nombre: "Galletas"));

        var encontrados = bd.Inventario.BuscarPorNombre("refresco");

        var producto = Assert.Single(encontrados);
        Assert.Equal("Refresco Cola", producto.Nombre);
    }

    [Fact]
    public void BuscarPorNombre_EscapaComodinesDelLike()
    {
        using var bd = new BaseDatosPrueba();
        bd.Inventario.AgregarProducto(Producto(codigo: "A1", nombre: "Refresco Cola"));

        Assert.Empty(bd.Inventario.BuscarPorNombre("%"));
    }

    [Fact]
    public void ActualizarProducto_NoModificaStock()
    {
        using var bd = new BaseDatosPrueba();
        bd.Inventario.AgregarProducto(Producto(stock: 10));
        var producto = Assert.Single(bd.Inventario.ObtenerProductos());

        producto.Nombre = "Refresco light";
        producto.Precio = 1_800;
        producto.Stock = 999;
        var actualizacion = bd.Inventario.ActualizarProducto(producto);

        Assert.True(actualizacion.IsSuccess, actualizacion.Message);

        var recargado = bd.Inventario.BuscarPorId(producto.Id);
        Assert.Equal("Refresco light", recargado.Nombre);
        Assert.Equal(1_800, recargado.Precio);
        Assert.Equal(10, recargado.Stock);
    }

    [Fact]
    public void ObtenerValorizadoInventario_MultiplicaStockPorCosto()
    {
        using var bd = new BaseDatosPrueba();
        bd.Inventario.AgregarProducto(Producto(codigo: "A1", costo: 1_000, stock: 10));
        bd.Inventario.AgregarProducto(Producto(codigo: "A2", costo: 250, stock: 4));

        Assert.Equal(11_000, bd.Inventario.ObtenerValorizadoInventario());
    }

    [Fact]
    public void DespuesDeVariasOperaciones_StockCoincideConSumaDeMovimientos()
    {
        using var bd = new BaseDatosPrueba();
        bd.Inventario.AgregarProducto(Producto(stock: 10));
        var producto = Assert.Single(bd.Inventario.ObtenerProductos());

        bd.Inventario.ReducirStockVenta(producto.Id, 3, "Cajero");
        bd.Inventario.RegistrarEntrada(producto.Id, 5, 1_000, "Dueño");
        bd.Inventario.RegistrarMerma(producto.Id, 2, "Roto", "Dueño");
        bd.Inventario.AjustarStock(producto.Id, 20, "Conteo", "Dueño");

        var almacenado = bd.Inventario.BuscarPorId(producto.Id).Stock;
        var sumaMovimientos = bd.Db.ObtenerStock(producto.Id);

        Assert.Equal(20, almacenado);
        Assert.Equal(almacenado, sumaMovimientos);
    }
}
