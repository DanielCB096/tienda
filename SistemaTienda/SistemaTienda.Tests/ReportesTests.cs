using SistemaTienda.Application.Services;
using SistemaTienda.Core;

namespace SistemaTienda.Tests;

public sealed class ReportesTests
{
    private static Producto CrearProducto(BaseDatosPrueba bd, string codigo,
        long precio, long costo, int stock)
    {
        var producto = new Producto
        {
            CodigoBarras = codigo,
            Nombre = $"Producto {codigo}",
            Costo = costo,
            Precio = precio,
            Stock = stock
        };

        Assert.True(bd.Inventario.AgregarProducto(producto).IsSuccess);
        return producto;
    }

    private static ProcessResult<Venta> Vender(BaseDatosPrueba bd, PagoSolicitado pago,
        params (Producto Producto, int Cantidad)[] lineas)
    {
        var solicitud = lineas
            .Select(l => new LineaVentaSolicitada { ProductoId = l.Producto.Id, Cantidad = l.Cantidad })
            .ToList();

        return new VentaService(bd.Db).RegistrarVenta(solicitud, pago, "Cajero");
    }

    private static ReporteUtilidad DelDia(BaseDatosPrueba bd) =>
        new ReporteUtilidadService(bd.Db).Obtener(DateTime.Today, DateTime.Today).Value;

    [Fact]
    public void Obtener_ConVentaEnEfectivo_UsaElCostoCongelado()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 1_500, costo: 1_000, stock: 10);

        Assert.True(Vender(bd, new PagoSolicitado { Efectivo = 3_000 }, (producto, 2)).IsSuccess);

        var reporte = DelDia(bd);

        Assert.Equal(1, reporte.NumeroVentas);
        Assert.Equal(3_000, reporte.VentasTotal);
        Assert.Equal(2_000, reporte.CostoVentas);
        Assert.Equal(0, reporte.ComisionTarjeta);
        Assert.Equal(1_000, reporte.UtilidadBruta);
        Assert.Equal(1_000, reporte.UtilidadNeta);
    }

    [Fact]
    public void Obtener_TrasCambiarElCostoDelCatalogo_ConservaElCostoDeLaVenta()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 1_500, costo: 1_000, stock: 10);
        Assert.True(Vender(bd, new PagoSolicitado { Efectivo = 1_500 }, (producto, 1)).IsSuccess);

        producto.Costo = 1_200;
        Assert.True(bd.Inventario.ActualizarProducto(producto).IsSuccess);

        Assert.Equal(1_000, DelDia(bd).CostoVentas);
    }

    [Fact]
    public void Obtener_ConPagoTarjeta_RestaLaComision()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 10_000, costo: 6_000, stock: 10);

        var venta = Vender(bd, new PagoSolicitado { Tarjeta = 10_000 }, (producto, 1));
        Assert.True(venta.IsSuccess, venta.Message);

        var reporte = DelDia(bd);

        Assert.Equal(350, reporte.ComisionTarjeta);
        Assert.Equal(10_000, reporte.VentasTotal);
        Assert.Equal(3_650, reporte.UtilidadNeta);
    }

    [Fact]
    public void Obtener_ConMermaYRestaSuCosto()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 1_500, costo: 1_000, stock: 10);

        Assert.True(bd.Inventario.RegistrarMerma(producto.Id, 2, "Caducado", "Dueño").IsSuccess);

        var reporte = DelDia(bd);

        Assert.Equal(2_000, reporte.Mermas);
        Assert.Equal(-2_000, reporte.UtilidadNeta);
    }

    [Fact]
    public void Obtener_ConGastosDeCajaYExternos_RestaAmbos()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        Assert.True(bd.Config.AgregarGasto(new Gasto
        {
            Concepto = "Luz", Monto = 500, Origen = OrigenGasto.Caja
        }).IsSuccess);
        Assert.True(bd.Config.AgregarGasto(new Gasto
        {
            Concepto = "Renta", Monto = 300, Origen = OrigenGasto.Externo
        }).IsSuccess);

        var reporte = DelDia(bd);

        Assert.Equal(800, reporte.Gastos);
        Assert.Equal(-800, reporte.UtilidadNeta);
    }

    [Fact]
    public void Obtener_ConVentaFueraDelRango_NoLaCuenta()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 1_500, costo: 1_000, stock: 10);
        Assert.True(Vender(bd, new PagoSolicitado { Efectivo = 1_500 }, (producto, 1)).IsSuccess);

        var ayer = DateTime.Today.AddDays(-1);
        var reporte = new ReporteUtilidadService(bd.Db).Obtener(ayer, ayer).Value;

        Assert.Equal(0, reporte.NumeroVentas);
        Assert.Equal(0, reporte.VentasTotal);
        Assert.Equal(0, reporte.UtilidadNeta);
    }

    [Fact]
    public void Obtener_ConRangoInvertido_Falla()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var resultado = new ReporteUtilidadService(bd.Db)
            .Obtener(DateTime.Today, DateTime.Today.AddDays(-1));

        Assert.True(resultado.IsError);
    }
}
