using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Tests;

public sealed class DescuentosTests
{
    private static Producto CrearProducto(BaseDatosPrueba bd, string codigo,
        long precio, long costo, int stock, decimal impuesto = 0m)
    {
        var producto = new Producto
        {
            CodigoBarras = codigo,
            Nombre = $"Producto {codigo}",
            Costo = costo,
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
    public void Repartir_ReparteTodosLosCentavosSinPerderNinguno()
    {
        var reparto = DescuentoVenta.Repartir(100, new long[] { 333, 333, 334 });

        Assert.Equal(100, reparto.Sum());
        Assert.All(reparto, parte => Assert.True(parte >= 0));
        for (var i = 0; i < reparto.Count; i++)
            Assert.True(reparto[i] <= new long[] { 333, 333, 334 }[i]);
    }

    [Fact]
    public void RegistrarVenta_ConPorcentaje_CobraElTotalNeto()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "D1", precio: 1_000, costo: 600, stock: 10);

        var venta = Vender(bd, new PagoSolicitado
        {
            Efectivo = 900,
            DescuentoPorcentaje = 10m
        }, (producto, 1));

        Assert.True(venta.IsSuccess, venta.Message);
        Assert.Equal(900, venta.Value.Total);
        Assert.Equal(1_000, venta.Value.Subtotal);
        Assert.Equal(100, venta.Value.Descuento);
        Assert.Equal(10m, venta.Value.DescuentoPorcentaje);

        var guardada = bd.Db.ObtenerVentaPorId(venta.Value.Id);
        Assert.Equal(900, guardada.Total);
        Assert.Equal(100, guardada.Descuento);

        var linea = Assert.Single(bd.Db.ObtenerLineasVenta(venta.Value.Id));
        Assert.Equal(900, linea.Importe);
        Assert.Equal(100, linea.Descuento);
        Assert.Equal(1_000, linea.Subtotal);
    }

    [Fact]
    public void RegistrarVenta_ConMonto_ReparteElDescuentoEntreLasLineas()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var primero = CrearProducto(bd, "D2", precio: 1_000, costo: 600, stock: 10);
        var segundo = CrearProducto(bd, "D3", precio: 2_000, costo: 1_200, stock: 10);

        var venta = Vender(bd, new PagoSolicitado
        {
            Efectivo = 2_700,
            DescuentoMonto = 300
        }, (primero, 1), (segundo, 1));

        Assert.True(venta.IsSuccess, venta.Message);
        Assert.Equal(2_700, venta.Value.Total);
        Assert.Equal(300, venta.Value.Descuento);

        var lineas = bd.Db.ObtenerLineasVenta(venta.Value.Id);
        Assert.Equal(300, lineas.Sum(l => l.Descuento));
        Assert.Equal(2_700, lineas.Sum(l => l.Importe));
        Assert.Equal(100, lineas.Single(l => l.ProductoId == primero.Id).Descuento);
        Assert.Equal(200, lineas.Single(l => l.ProductoId == segundo.Id).Descuento);
    }

    [Fact]
    public void RegistrarVenta_ConDescuentoDelCienPorCiento_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "D4", precio: 1_000, costo: 600, stock: 10);

        var venta = Vender(bd, new PagoSolicitado
        {
            Efectivo = 0,
            DescuentoPorcentaje = 100m
        }, (producto, 1));

        Assert.True(venta.IsError);
        Assert.Empty(bd.Db.ObtenerVentasDelDia(DateTime.Today));
    }

    [Fact]
    public void RegistrarVenta_ConMontoIgualAlSubtotal_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "D5", precio: 1_000, costo: 600, stock: 10);

        var venta = Vender(bd, new PagoSolicitado
        {
            Efectivo = 0,
            DescuentoMonto = 1_000
        }, (producto, 1));

        Assert.True(venta.IsError);
        Assert.Empty(bd.Db.ObtenerVentasDelDia(DateTime.Today));
    }

    [Fact]
    public void RegistrarVenta_EmpleadoSinPermiso_NoAplicaDescuento()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "D6", precio: 1_000, costo: 600, stock: 10);
        bd.IniciarSesion(RolUsuario.Empleado, "Empleado");

        var venta = Vender(bd, new PagoSolicitado
        {
            Efectivo = 900,
            DescuentoPorcentaje = 10m
        }, (producto, 1));

        Assert.True(venta.IsError);
        Assert.Contains("descuento", venta.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(bd.Db.ObtenerVentasDelDia(DateTime.Today));
    }

    [Fact]
    public void RegistrarVenta_ConDescuentoYIVA_CalculaElImpuestoSobreElNeto()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "D7", precio: 1_160, costo: 800, stock: 10,
            impuesto: 16m);

        var venta = Vender(bd, new PagoSolicitado
        {
            Efectivo = 580,
            DescuentoPorcentaje = 50m
        }, (producto, 1));

        Assert.True(venta.IsSuccess, venta.Message);
        Assert.Equal(580, venta.Value.Total);

        var linea = Assert.Single(bd.Db.ObtenerLineasVenta(venta.Value.Id));
        Assert.Equal(580, linea.Importe);
        Assert.Equal(80, linea.ImpuestoImporte);
    }

    [Fact]
    public void RegistrarVenta_ConDescuento_ReportaVentasNetasPorProducto()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "D8", precio: 1_000, costo: 600, stock: 10,
            impuesto: 16m);

        Assert.True(Vender(bd, new PagoSolicitado
        {
            Efectivo = 900,
            DescuentoPorcentaje = 10m
        }, (producto, 1)).IsSuccess);

        var reporte = new ReporteUtilidadService(bd.Db)
            .Obtener(DateTime.Today, DateTime.Today).Value;

        Assert.Equal(900, reporte.VentasTotal);
        Assert.Equal(900, Assert.Single(reporte.Productos).Ventas);
        Assert.Equal(300, Assert.Single(reporte.Productos).Utilidad);
    }

    [Fact]
    public void RegistrarVenta_ConDescuentoYFiado_CargaElNetoAlCliente()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "D9", precio: 1_000, costo: 600, stock: 10);
        var cliente = new ClienteFiado { Nombre = "Cliente prueba" };
        Assert.True(bd.Config.AgregarClienteFiado(cliente).IsSuccess);
        var guardado = bd.Config.ObtenerTodosLosClientes().Value.Single();

        var venta = Vender(bd, new PagoSolicitado
        {
            Fiado = 900,
            ClienteId = guardado.Id,
            DescuentoPorcentaje = 10m
        }, (producto, 1));

        Assert.True(venta.IsSuccess, venta.Message);
        Assert.Equal(900, bd.Config.ObtenerTodosLosClientes().Value.Single().SaldoActual);
    }

    [Fact]
    public void Ticket_ConDescuento_MuestraSubtotalYDescuento()
    {
        var venta = new Venta
        {
            Id = 7,
            Fecha = new DateTime(2026, 11, 26, 10, 0, 0),
            Usuario = "Cajera",
            MetodoPago = MetodoPagoVenta.Efectivo,
            Total = 900,
            Descuento = 100,
            DescuentoPorcentaje = 10m,
            PagoEfectivo = 900,
            CostoTotal = 600
        };
        var linea = new VentaLinea
        {
            ProductoId = 1,
            Nombre = "Producto",
            Cantidad = 1,
            PrecioUnitario = 1_000,
            CostoUnitario = 600,
            Importe = 900,
            Descuento = 100
        };
        var configuracion = new ConfiguracionTienda { NombreTienda = "Mi tienda" };

        var ticket = new TicketService().Generar(venta,
            new[] { linea }, configuracion, efectivoRecibido: 1_000);

        Assert.Contains("Subtotal: $10.00", ticket.Texto);
        Assert.Contains("Descuento (10%): -$1.00", ticket.Texto);
        Assert.Contains("TOTAL: $9.00", ticket.Texto);
        Assert.Contains("$10.00 c/u = $10.00", ticket.Texto);
    }

    [Fact]
    public void Migracion_DesdeV19_AgregaElDescuento()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        bd.Db.ExecuteCommand("ALTER TABLE VentaLineas DROP COLUMN Descuento");
        bd.Db.ExecuteCommand("ALTER TABLE Ventas DROP COLUMN Descuento");
        bd.Db.ExecuteCommand("ALTER TABLE Ventas DROP COLUMN DescuentoPorcentaje");
        bd.Db.ExecuteCommand("PRAGMA user_version = 19");

        bd.Db.InicializarBaseDeDatos();

        Assert.Equal(Migraciones.VersionMaxima(Migraciones.ObtenerTodas()),
            bd.Db.ExecuteScalar<int>("PRAGMA user_version"));
        Assert.Equal(1, bd.Db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM pragma_table_info('Ventas') WHERE name = 'Descuento'"));
        Assert.Equal(1, bd.Db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM pragma_table_info('VentaLineas') WHERE name = 'Descuento'"));
    }
}
