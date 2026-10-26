using SistemaTienda.Application.Services;
using SistemaTienda.Core;

namespace SistemaTienda.Tests;

public sealed class AnulacionVentasTests
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

        var resultado = bd.Inventario.AgregarProducto(producto);
        Assert.True(resultado.IsSuccess, resultado.Message);
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

    private static ProcessResult Anular(BaseDatosPrueba bd, int ventaId, string motivo = "Cliente devolvió") =>
        new VentaService(bd.Db).AnularVenta(ventaId, motivo, "Encargado");

    [Fact]
    public void AnularVenta_ConDosLineas_DevuelveStockYMarcaLaVenta()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var a = CrearProducto(bd, "A1", precio: 2_500, costo: 1_000, stock: 10);
        var b = CrearProducto(bd, "B1", precio: 1_500, costo: 600, stock: 5);

        var venta = Vender(bd,
            new PagoSolicitado { Efectivo = 6_500 },
            (a, 2), (b, 1));
        Assert.True(venta.IsSuccess, venta.Message);
        Assert.Equal(8, bd.Inventario.BuscarPorId(a.Id).Stock);
        Assert.Equal(4, bd.Inventario.BuscarPorId(b.Id).Stock);

        var anulacion = Anular(bd, venta.Value.Id);

        Assert.True(anulacion.IsSuccess, anulacion.Message);
        Assert.Equal(10, bd.Inventario.BuscarPorId(a.Id).Stock);
        Assert.Equal(5, bd.Inventario.BuscarPorId(b.Id).Stock);
        Assert.Equal(10, bd.Db.ObtenerStock(a.Id));

        var recargada = bd.Db.ObtenerVentaPorId(venta.Value.Id);
        Assert.True(recargada.Anulada);
        Assert.Equal(6_500, recargada.Total);
        Assert.Equal(2, bd.Db.ObtenerLineasVenta(venta.Value.Id).Count);

        var entradas = bd.Inventario.HistorialMovimientos(a.Id)
            .Where(m => m.Tipo == MovimientoTipo.Entrada && m.Motivo.Contains("Anulación"))
            .ToList();
        var entrada = Assert.Single(entradas);
        Assert.Equal(2, entrada.Cantidad);
        Assert.Equal(1_000, entrada.CostoUnitario);

        var registro = Assert.Single(bd.Db.ObtenerBitacora(accion: AccionBitacora.AnulacionVenta));
        Assert.Equal(venta.Value.Id.ToString(), registro.EntidadId);
        Assert.Equal("Cliente devolvió", registro.Motivo);

        var anulacionGuardada = bd.Db.ObtenerAnulacionVenta(venta.Value.Id);
        Assert.NotNull(anulacionGuardada);
        Assert.Equal("Encargado", anulacionGuardada.Usuario);
        Assert.Equal("Cliente devolvió", anulacionGuardada.Motivo);
        Assert.Equal(1, bd.Db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'AnulacionesVenta'"));
    }

    [Fact]
    public void AnularVenta_Fiado_RevierteElSaldoDelCliente()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda(fiado: true);
        var producto = CrearProducto(bd, "A1", precio: 2_500, costo: 1_000, stock: 10);
        var cliente = bd.Config.AgregarClienteFiado(new ClienteFiado
        {
            Nombre = "Cliente",
            Telefono = "5550000000"
        });
        Assert.True(cliente.IsSuccess, cliente.Message);

        var venta = Vender(bd,
            new PagoSolicitado { Fiado = 5_000, ClienteId = cliente.Value.ClienteId },
            (producto, 2));
        Assert.True(venta.IsSuccess, venta.Message);
        Assert.Equal(5_000, bd.Db.ObtenerClienteFiadoPorId(cliente.Value.ClienteId).SaldoActual);

        var anulacion = Anular(bd, venta.Value.Id, "Cobro equivocado");

        Assert.True(anulacion.IsSuccess, anulacion.Message);
        Assert.Equal(0, bd.Db.ObtenerClienteFiadoPorId(cliente.Value.ClienteId).SaldoActual);
        Assert.Equal(10, bd.Inventario.BuscarPorId(producto.Id).Stock);
    }

    [Fact]
    public void AnularVenta_ConCorteCerrado_FallaYNoDevuelveStock()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 2_500, costo: 1_000, stock: 10);

        var venta = Vender(bd, new PagoSolicitado { Efectivo = 2_500 }, (producto, 1));
        Assert.True(venta.IsSuccess, venta.Message);

        var corte = new CorteCajaService(bd.Db).RegistrarCorte(22_500, 20_000, "Cajero");
        Assert.True(corte.IsSuccess, corte.Message);

        var anulacion = Anular(bd, venta.Value.Id);

        Assert.True(anulacion.IsError);
        Assert.Contains("corte", anulacion.Message);
        Assert.False(bd.Db.ObtenerVentaPorId(venta.Value.Id).Anulada);
        Assert.Equal(9, bd.Inventario.BuscarPorId(producto.Id).Stock);
        Assert.Empty(bd.Db.ObtenerBitacora(accion: AccionBitacora.AnulacionVenta));
    }

    [Fact]
    public void AnularVenta_ConCorteAbierto_ExcluyeLaVentaDelCortePendiente()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 2_500, costo: 1_000, stock: 10);

        var venta = Vender(bd, new PagoSolicitado { Efectivo = 2_500 }, (producto, 1));
        Assert.True(venta.IsSuccess, venta.Message);
        Assert.Equal(1, bd.Db.ObtenerResumenCajaPendiente().NumeroVentas);

        Assert.True(Anular(bd, venta.Value.Id).IsSuccess);

        var pendiente = bd.Db.ObtenerResumenCajaPendiente();
        Assert.Equal(0, pendiente.NumeroVentas);
        Assert.Equal(0, pendiente.TotalVentas);
        Assert.Equal(0, pendiente.VentasEfectivo);
    }

    [Fact]
    public void AnularVenta_DosVeces_FallaYNoDuplicaMovimientos()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 2_500, costo: 1_000, stock: 10);
        var venta = Vender(bd, new PagoSolicitado { Efectivo = 2_500 }, (producto, 1));
        Assert.True(venta.IsSuccess, venta.Message);

        Assert.True(Anular(bd, venta.Value.Id).IsSuccess);
        var movimientos = bd.Inventario.HistorialMovimientos(producto.Id).Count;

        var segunda = Anular(bd, venta.Value.Id);

        Assert.True(segunda.IsError);
        Assert.Contains("ya está anulada", segunda.Message);
        Assert.Equal(movimientos, bd.Inventario.HistorialMovimientos(producto.Id).Count);
        Assert.Equal(10, bd.Inventario.BuscarPorId(producto.Id).Stock);
        Assert.Single(bd.Db.ObtenerBitacora(accion: AccionBitacora.AnulacionVenta));
    }

    [Fact]
    public void AnularVenta_EmpleadoSinPermiso_Falla()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 2_500, costo: 1_000, stock: 10);
        var venta = Vender(bd, new PagoSolicitado { Efectivo = 2_500 }, (producto, 1));
        Assert.True(venta.IsSuccess, venta.Message);

        bd.IniciarSesion(RolUsuario.Empleado);

        var anulacion = Anular(bd, venta.Value.Id);

        Assert.True(anulacion.IsError);
        Assert.Contains("permiso", anulacion.Message);
        Assert.False(bd.Db.ObtenerVentaPorId(venta.Value.Id).Anulada);
        Assert.Equal(9, bd.Inventario.BuscarPorId(producto.Id).Stock);
    }

    [Fact]
    public void AnularVenta_SinMotivo_Falla()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 2_500, costo: 1_000, stock: 10);
        var venta = Vender(bd, new PagoSolicitado { Efectivo = 2_500 }, (producto, 1));
        Assert.True(venta.IsSuccess, venta.Message);

        var anulacion = Anular(bd, venta.Value.Id, motivo: "   ");

        Assert.True(anulacion.IsError);
        Assert.False(bd.Db.ObtenerVentaPorId(venta.Value.Id).Anulada);
        Assert.Equal(9, bd.Inventario.BuscarPorId(producto.Id).Stock);
    }

    [Fact]
    public void ReporteUtilidad_ExcluyeVentasAnuladas()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 2_500, costo: 1_000, stock: 10);
        var venta = Vender(bd, new PagoSolicitado { Efectivo = 10_000 }, (producto, 4));
        Assert.True(venta.IsSuccess, venta.Message);

        Assert.True(Anular(bd, venta.Value.Id).IsSuccess);

        var reporte = new ReporteUtilidadService(bd.Db)
            .Obtener(DateTime.Today, DateTime.Today).Value;

        Assert.Equal(0, reporte.NumeroVentas);
        Assert.Equal(0, reporte.VentasTotal);
        Assert.Equal(0, reporte.CostoVentas);
        Assert.Equal(0, reporte.UtilidadNeta);
    }
}
