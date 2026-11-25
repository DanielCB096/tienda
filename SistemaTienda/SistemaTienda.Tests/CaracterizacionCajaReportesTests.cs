using SistemaTienda.Application.Services;
using SistemaTienda.Core;

namespace SistemaTienda.Tests;

/// <summary>
/// CaracterizaciÃ³n de caja y reportes antes de H4: fija los importes de un
/// escenario completo y la regla de que un gasto de caja solo descuenta
/// efectivo si se pagÃ³ en efectivo.
/// </summary>
public sealed class CaracterizacionCajaReportesTests
{
    private static (Producto A, Producto B, ClienteFiado Cliente) Preparar(BaseDatosPrueba bd)
    {
        bd.ConfigurarTienda();

        var a = new Producto
        {
            CodigoBarras = "A",
            Nombre = "Producto A",
            Costo = 6_000,
            Precio = 10_000,
            Stock = 50
        };
        var b = new Producto
        {
            CodigoBarras = "B",
            Nombre = "Producto B",
            Costo = 3_000,
            Precio = 5_000,
            Stock = 50
        };
        var cliente = new ClienteFiado { Nombre = "Cliente" };

        Assert.True(bd.Inventario.AgregarProducto(a).IsSuccess);
        Assert.True(bd.Inventario.AgregarProducto(b).IsSuccess);
        Assert.True(bd.Config.AgregarClienteFiado(cliente).IsSuccess);

        return (a, b, cliente);
    }

    private static Venta Vender(BaseDatosPrueba bd, PagoSolicitado pago,
        params (Producto Producto, int Cantidad)[] lineas)
    {
        var venta = new VentaService(bd.Db).RegistrarVenta(
            lineas.Select(l => new LineaVentaSolicitada
            {
                ProductoId = l.Producto.Id,
                Cantidad = l.Cantidad
            }).ToList(),
            pago, "DueÃ±o");

        Assert.True(venta.IsSuccess, venta.Message);
        return venta.Value;
    }

    private static void EscenarioCompleto(BaseDatosPrueba bd, Producto a, Producto b,
        ClienteFiado cliente)
    {
        Vender(bd, new PagoSolicitado { Efectivo = 20_000, EfectivoRecibido = 25_000 },
            (a, 1), (b, 2));
        Vender(bd, new PagoSolicitado { Tarjeta = 20_000 }, (a, 2));
        Vender(bd, new PagoSolicitado { Efectivo = 4_000, Tarjeta = 6_000 }, (a, 1));
        Vender(bd, new PagoSolicitado { Fiado = 20_000, ClienteId = cliente.Id }, (b, 4));

        Assert.True(bd.Config.RegistrarAbonoCliente(cliente.Id, 5_000, "DueÃ±o").IsSuccess);

        var caja = new CorteCajaService(bd.Db);
        Assert.True(caja.RegistrarMovimiento(TipoMovimientoCaja.Ingreso, 3_000, "Cambio", "DueÃ±o").IsSuccess);
        Assert.True(caja.RegistrarMovimiento(TipoMovimientoCaja.Retiro, 2_000, "Servicio", "DueÃ±o").IsSuccess);

        Assert.True(bd.Config.AgregarGasto(new Gasto
        {
            Concepto = "Bolsas",
            Monto = 1_500,
            MetodoPago = MetodoPagoAbono.Efectivo,
            Origen = OrigenGasto.Caja
        }).IsSuccess);

        Assert.True(bd.Config.AgregarGasto(new Gasto
        {
            Concepto = "Renta",
            Monto = 2_500,
            MetodoPago = MetodoPagoAbono.Transferencia,
            Origen = OrigenGasto.Externo
        }).IsSuccess);
    }

    [Fact]
    public void Corte_ConVentasAbonosGastosYMovimientos_CalculaElEsperado()
    {
        using var bd = new BaseDatosPrueba();
        var (a, b, cliente) = Preparar(bd);
        EscenarioCompleto(bd, a, b, cliente);

        var pendiente = new CorteCajaService(bd.Db).ObtenerResumenPendiente();

        Assert.True(pendiente.IsSuccess, pendiente.Message);
        var corte = pendiente.Value;
        Assert.Equal(20_000, corte.FondoInicial);
        Assert.Equal(24_000, corte.VentasEfectivo);
        Assert.Equal(5_000, corte.AbonosEfectivo);
        Assert.Equal(3_000, corte.IngresosCaja);
        Assert.Equal(2_000, corte.RetirosCaja);
        Assert.Equal(1_500, corte.GastosCaja);
        Assert.Equal(48_500, corte.EfectivoEsperado);
        Assert.Equal(4, corte.NumeroVentas);
        Assert.Equal(70_000, corte.TotalVentas);
    }

    [Fact]
    public void Reporte_NoDuplicaVentaMixtaYDesglosaLosPagos()
    {
        using var bd = new BaseDatosPrueba();
        var (a, b, cliente) = Preparar(bd);
        EscenarioCompleto(bd, a, b, cliente);

        var reporte = new ReporteUtilidadService(bd.Db)
            .Obtener(DateTime.Today, DateTime.Today);

        Assert.True(reporte.IsSuccess, reporte.Message);
        var r = reporte.Value;
        Assert.Equal(4, r.NumeroVentas);
        Assert.Equal(70_000, r.VentasTotal);
        Assert.Equal(42_000, r.CostoVentas);
        Assert.Equal(910, r.ComisionTarjeta);
        Assert.Equal(27_090, r.UtilidadBruta);
        Assert.Equal(4_000, r.Gastos);
        Assert.Equal(23_090, r.UtilidadNeta);

        var efectivo = r.DesglosePagos.Single(d => d.Metodo == MetodoPagoVenta.Efectivo);
        Assert.Equal(24_000, efectivo.Monto);
        Assert.Equal(2, efectivo.NumeroVentas);

        var tarjeta = r.DesglosePagos.Single(d => d.Metodo == MetodoPagoVenta.Tarjeta);
        Assert.Equal(26_000, tarjeta.Monto);
        Assert.Equal(2, tarjeta.NumeroVentas);

        var fiado = r.DesglosePagos.Single(d => d.Metodo == MetodoPagoVenta.Fiado);
        Assert.Equal(20_000, fiado.Monto);
        Assert.Single(r.DesglosePagos, d => d.Metodo == MetodoPagoVenta.Fiado);
    }

    [Fact]
    public void GastoDeCajaConTarjeta_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var resultado = bd.Config.AgregarGasto(new Gasto
        {
            Concepto = "Luz",
            Monto = 10_000,
            MetodoPago = MetodoPagoAbono.Tarjeta,
            Origen = OrigenGasto.Caja
        });

        Assert.True(resultado.IsError);
        Assert.Contains("caja", resultado.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GastoDeCajaConTarjetaHistorico_NoReduceElEfectivoEsperado()
    {
        using var bd = new BaseDatosPrueba();
        var (a, b, cliente) = Preparar(bd);
        EscenarioCompleto(bd, a, b, cliente);

        // Simula un registro viejo inconsistente escrito antes de la correcciÃ³n.
        bd.Db.AgregarGasto(new Gasto
        {
            Concepto = "Luz pagada con tarjeta",
            Monto = 9_000,
            Fecha = DateTime.Now,
            MetodoPago = MetodoPagoAbono.Tarjeta,
            Origen = OrigenGasto.Caja
        });

        var corte = new CorteCajaService(bd.Db).ObtenerResumenPendiente().Value;

        Assert.Equal(1_500, corte.GastosCaja);
        Assert.Equal(48_500, corte.EfectivoEsperado);
    }

    [Fact]
    public void CorteCerrado_GuardaElEsperadoYDejaDiferenciaCero()
    {
        using var bd = new BaseDatosPrueba();
        var (a, b, cliente) = Preparar(bd);
        EscenarioCompleto(bd, a, b, cliente);

        var caja = new CorteCajaService(bd.Db);
        var pendiente = caja.ObtenerResumenPendiente().Value;
        var cierre = caja.RegistrarCorte(pendiente.EfectivoEsperado, pendiente.FondoInicial,
            "DueÃ±o", "Corte de caracterizaciÃ³n");

        Assert.True(cierre.IsSuccess, cierre.Message);
        Assert.Equal(0, cierre.Value.Diferencia);
        Assert.Equal(48_500, cierre.Value.EfectivoContado);

        var ultimo = caja.ObtenerUltimoCorte();
        Assert.True(ultimo.IsSuccess);
        Assert.Equal(48_500, ultimo.Value.EfectivoEsperado);
        Assert.Equal(0, ultimo.Value.Diferencia);
    }
}

