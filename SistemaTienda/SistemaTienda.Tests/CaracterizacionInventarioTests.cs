using SistemaTienda.Application.Services;
using SistemaTienda.Core;

namespace SistemaTienda.Tests;

/// <summary>
/// CaracterizaciÃ³n del inventario: despuÃ©s de cualquier operaciÃ³n, el stock en
/// cachÃ© de cada producto debe ser exactamente la suma de sus movimientos.
/// El log de movimientos es la verdad; la columna Stock es una cachÃ©.
/// </summary>
public sealed class CaracterizacionInventarioTests
{
    private static Producto CrearProducto(BaseDatosPrueba bd, string codigo,
        int stock = 10, long costo = 1_000, long precio = 2_000)
    {
        var producto = new Producto
        {
            CodigoBarras = codigo,
            Nombre = $"Producto {codigo}",
            Costo = costo,
            Precio = precio,
            Stock = stock
        };

        var alta = bd.Inventario.AgregarProducto(producto);
        Assert.True(alta.IsSuccess, alta.Message);
        return producto;
    }

    private static Venta Vender(BaseDatosPrueba bd, params (Producto Producto, int Cantidad)[] lineas)
    {
        var solicitadas = lineas
            .Select(l => new LineaVentaSolicitada
            {
                ProductoId = l.Producto.Id,
                Cantidad = l.Cantidad
            })
            .ToList();

        var total = lineas.Sum(l => l.Producto.Precio * l.Cantidad);
        var venta = new VentaService(bd.Db).RegistrarVenta(solicitadas,
            new PagoSolicitado { Efectivo = total }, "DueÃ±o");

        Assert.True(venta.IsSuccess, venta.Message);
        return venta.Value;
    }

    private static void AssertCoherente(BaseDatosPrueba bd)
    {
        var descuadres = bd.Db.ProductosDescuadrados();

        Assert.True(descuadres.Count == 0,
            "Productos con stock distinto a sus movimientos: " +
            string.Join(", ", descuadres.Select(d =>
                $"{d.Nombre} stock={d.Stock} movimientos={d.Movimientos}")));

        foreach (var producto in bd.Inventario.ObtenerProductos())
            Assert.Equal(producto.Stock, bd.Db.ObtenerStock(producto.Id));
    }

    [Fact]
    public void AltaConStockInicial_QuedaComoMovimiento()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", stock: 7);

        AssertCoherente(bd);
        Assert.Equal(7, bd.Db.ObtenerStock(producto.Id));
        Assert.Single(bd.Db.ObtenerHistorialMovimientos(producto.Id));
    }

    [Fact]
    public void EntradaMermaYAjuste_SeReflejanEnElStock()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A2", stock: 10);

        Assert.True(bd.Inventario.RegistrarEntrada(producto.Id, 5, 1_200, "DueÃ±o").IsSuccess);
        AssertCoherente(bd);

        Assert.True(bd.Inventario.RegistrarMerma(producto.Id, 2, "Roto", "DueÃ±o").IsSuccess);
        AssertCoherente(bd);

        Assert.True(bd.Inventario.AjustarStock(producto.Id, 30, "Conteo", "DueÃ±o").IsSuccess);
        AssertCoherente(bd);

        Assert.True(bd.Inventario.AjustarStock(producto.Id, 4, "Conteo", "DueÃ±o").IsSuccess);
        AssertCoherente(bd);

        Assert.Equal(4, bd.Db.ObtenerStock(producto.Id));
    }

    [Fact]
    public void VentaYAnulacion_DevuelvenElStock()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A3", stock: 10);

        var venta = Vender(bd, (producto, 3));
        AssertCoherente(bd);
        Assert.Equal(7, bd.Db.ObtenerStock(producto.Id));

        var anulacion = new VentaService(bd.Db).AnularVenta(venta.Id, "Prueba", "DueÃ±o");
        Assert.True(anulacion.IsSuccess, anulacion.Message);

        AssertCoherente(bd);
        Assert.Equal(10, bd.Db.ObtenerStock(producto.Id));
    }

    [Fact]
    public void VentaFiada_ReduceStockYNoDuplicaMovimientos()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A4", stock: 10);
        var cliente = new ClienteFiado { Nombre = "Cliente" };
        Assert.True(bd.Config.AgregarClienteFiado(cliente).IsSuccess);

        var venta = new VentaService(bd.Db).RegistrarVenta(
            new[] { new LineaVentaSolicitada { ProductoId = producto.Id, Cantidad = 2 } },
            new PagoSolicitado { Fiado = producto.Precio * 2, ClienteId = cliente.Id },
            "DueÃ±o");

        Assert.True(venta.IsSuccess, venta.Message);
        AssertCoherente(bd);
        Assert.Equal(8, bd.Db.ObtenerStock(producto.Id));
        Assert.Equal(producto.Precio * 2,
            bd.Config.ObtenerTodosLosClientes().Value.Single(c => c.Id == cliente.Id).SaldoActual);
    }

    [Fact]
    public void CompraAProveedor_AumentaStockYActualizaCosto()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A5", stock: 5, costo: 1_000);

        var proveedor = new Proveedor { Nombre = "Proveedor", Clave = "P1" };
        Assert.True(bd.Config.AgregarProveedor(proveedor).IsSuccess);

        var compra = bd.Config.RegistrarCompraProveedor(proveedor.Id, new[]
        {
            new LineaCompraSolicitada
            {
                ProductoId = producto.Id,
                Cantidad = 8,
                CostoUnitario = 1_500
            }
        }, "ReposiciÃ³n");

        Assert.True(compra.IsSuccess, compra.Message);
        AssertCoherente(bd);

        Assert.Equal(13, bd.Db.ObtenerStock(producto.Id));
        Assert.Equal(1_500, bd.Db.ObtenerProductoPorId(producto.Id).Costo);
    }

    [Fact]
    public void SecuenciaCompleta_MantieneElInventarioCuadrado()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var arroz = CrearProducto(bd, "B1", stock: 20, costo: 2_100, precio: 3_200);
        var leche = CrearProducto(bd, "B2", stock: 12, costo: 1_900, precio: 2_800);
        var cliente = new ClienteFiado { Nombre = "Cliente" };
        Assert.True(bd.Config.AgregarClienteFiado(cliente).IsSuccess);

        Vender(bd, (arroz, 2), (leche, 1));
        AssertCoherente(bd);

        var fiado = new VentaService(bd.Db).RegistrarVenta(
            new[] { new LineaVentaSolicitada { ProductoId = leche.Id, Cantidad = 3 } },
            new PagoSolicitado { Fiado = leche.Precio * 3, ClienteId = cliente.Id },
            "DueÃ±o");
        Assert.True(fiado.IsSuccess, fiado.Message);
        AssertCoherente(bd);

        Assert.True(new VentaService(bd.Db)
            .AnularVenta(fiado.Value.Id, "Cancelada", "DueÃ±o").IsSuccess);
        AssertCoherente(bd);

        Assert.True(bd.Inventario.RegistrarMerma(arroz.Id, 1, "Humedad", "DueÃ±o").IsSuccess);
        AssertCoherente(bd);

        Assert.True(bd.Inventario.RegistrarEntrada(leche.Id, 6, 2_000, "DueÃ±o").IsSuccess);
        AssertCoherente(bd);

        Assert.True(bd.Inventario.AjustarStock(arroz.Id, 25, "Conteo fÃ­sico", "DueÃ±o").IsSuccess);
        AssertCoherente(bd);

        Assert.Empty(bd.Db.ProductosDescuadrados());
        Assert.Equal(25, bd.Db.ObtenerStock(arroz.Id));
        Assert.Equal(17, bd.Db.ObtenerStock(leche.Id));
    }
}

