using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Tests;

public sealed class ImpuestoVentasTests
{
    private static Producto CrearProducto(BaseDatosPrueba bd, string codigo,
        long precio, decimal impuesto, int stock = 10)
    {
        var producto = new Producto
        {
            CodigoBarras = codigo,
            Nombre = $"Producto {codigo}",
            Costo = 5_000,
            Precio = precio,
            Stock = stock,
            Impuesto = impuesto
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

    [Fact]
    public void Venta_GuardaElImpuestoCongeladoSinCambiarElTotal()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 11_600, impuesto: 16);

        var venta = Vender(bd, new PagoSolicitado { Efectivo = 11_600 }, (producto, 1));
        Assert.True(venta.IsSuccess, venta.Message);

        var linea = Assert.Single(bd.Db.ObtenerLineasVenta(venta.Value.Id));
        Assert.Equal(11_600, venta.Value.Total);
        Assert.Equal(16m, linea.ImpuestoPorcentaje);
        Assert.Equal(1_600, linea.ImpuestoImporte);
    }

    [Fact]
    public void Venta_ConProductoExento_NoGeneraImpuesto()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 10_000, impuesto: 0);

        var venta = Vender(bd, new PagoSolicitado { Efectivo = 10_000 }, (producto, 1));
        Assert.True(venta.IsSuccess, venta.Message);

        var linea = Assert.Single(bd.Db.ObtenerLineasVenta(venta.Value.Id));
        Assert.Equal(0m, linea.ImpuestoPorcentaje);
        Assert.Equal(0, linea.ImpuestoImporte);
    }

    [Fact]
    public void Reporte_SumaElImpuestoYExcluyeLasVentasAnuladas()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var gravado = CrearProducto(bd, "A1", precio: 11_600, impuesto: 16);
        var exento = CrearProducto(bd, "B1", precio: 1_000, impuesto: 0);

        var venta = Vender(bd, new PagoSolicitado { Efectivo = 12_600 }, (gravado, 1), (exento, 1));
        Assert.True(venta.IsSuccess, venta.Message);

        var reporte = new ReporteUtilidadService(bd.Db).Obtener(DateTime.Today, DateTime.Today).Value;
        Assert.Equal(1_600, reporte.ImpuestoIncluido);
        Assert.Equal(12_600, reporte.VentasTotal);

        Assert.True(new VentaService(bd.Db)
            .AnularVenta(venta.Value.Id, "Cliente devolvió", "Encargado").IsSuccess);

        reporte = new ReporteUtilidadService(bd.Db).Obtener(DateTime.Today, DateTime.Today).Value;
        Assert.Equal(0, reporte.ImpuestoIncluido);
    }

    [Fact]
    public void Migracion_DesdeV13_AgregaElImpuestoALasLineas()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        bd.Db.ExecuteCommand("ALTER TABLE VentaLineas DROP COLUMN ImpuestoImporte");
        bd.Db.ExecuteCommand("ALTER TABLE VentaLineas DROP COLUMN ImpuestoPorcentaje");
        bd.Db.ExecuteCommand("PRAGMA user_version = 13");

        bd.Db.InicializarBaseDeDatos();

        Assert.Equal(Migraciones.VersionMaxima(Migraciones.ObtenerTodas()),
            bd.Db.ExecuteScalar<int>("PRAGMA user_version"));
        Assert.Equal(1, bd.Db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM pragma_table_info('VentaLineas') WHERE name = 'ImpuestoPorcentaje'"));
        Assert.Equal(1, bd.Db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM pragma_table_info('VentaLineas') WHERE name = 'ImpuestoImporte'"));
        Assert.Contains(RespaldoLocal.Listar(bd.Db.DirectorioRespaldos),
            r => r.Nombre.Contains("antes-migrar"));
    }
}
