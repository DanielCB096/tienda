using SistemaTienda.Core;

namespace SistemaTienda.Tests;

public sealed class GastosTests
{
    [Fact]
    public void AgregarGasto_ConMontoDeUnCentavo_SeAceptaYTotaliza()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var alta = bd.Config.AgregarGasto(new Gasto
        {
            Concepto = "Ajuste de caja",
            Monto = 1,
            MetodoPago = "Efectivo",
            Origen = OrigenGasto.Caja
        });

        Assert.True(alta.IsSuccess, alta.Message);
        Assert.Equal(1L, bd.Config.ObtenerTotalGastos().Value);
        Assert.Single(bd.Config.ObtenerTodosLosGastos().Value);
    }

    [Fact]
    public void ObtenerTotalGastos_SumaCentavosSinPerderPrecision()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        Assert.True(bd.Config.AgregarGasto(new Gasto { Concepto = "Uno", Monto = 10 }).IsSuccess);
        Assert.True(bd.Config.AgregarGasto(new Gasto { Concepto = "Dos", Monto = 20 }).IsSuccess);
        Assert.True(bd.Config.AgregarGasto(new Gasto { Concepto = "Tres", Monto = 1 }).IsSuccess);

        Assert.Equal(31L, bd.Config.ObtenerTotalGastos().Value);
    }

    [Fact]
    public void AgregarGasto_ConMontoCeroONegativo_NoRegistraNiAlteraElTotal()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var cero = bd.Config.AgregarGasto(new Gasto { Concepto = "Cero", Monto = 0 });
        var negativo = bd.Config.AgregarGasto(new Gasto { Concepto = "Negativo", Monto = -1 });

        Assert.True(cero.IsError);
        Assert.True(negativo.IsError);
        Assert.Empty(bd.Config.ObtenerTodosLosGastos().Value);
        Assert.Equal(0L, bd.Config.ObtenerTotalGastos().Value);
    }

    [Fact]
    public void AgregarGasto_GuardaMetodoOrigenObservacionesYFecha()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var fecha = new DateTime(2026, 11, 10, 9, 30, 0);

        Assert.True(bd.Config.AgregarGasto(new Gasto
        {
            Concepto = "Servicio de internet",
            Monto = 8_500,
            Fecha = fecha,
            MetodoPago = "Transferencia",
            Origen = OrigenGasto.Externo,
            Observaciones = "Factura A-1"
        }).IsSuccess);

        var gasto = Assert.Single(bd.Config.ObtenerTodosLosGastos().Value);
        Assert.Equal("Servicio de internet", gasto.Concepto);
        Assert.Equal(8_500L, gasto.Monto);
        Assert.Equal(fecha, gasto.Fecha);
        Assert.Equal("Transferencia", gasto.MetodoPago);
        Assert.Equal(OrigenGasto.Externo, gasto.Origen);
        Assert.Equal("Factura A-1", gasto.Observaciones);
    }

    [Fact]
    public void ObtenerTodosLosGastos_OrdenaDelMasRecienteAlMasAntiguo()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        Assert.True(bd.Config.AgregarGasto(new Gasto
        {
            Concepto = "Viejo", Monto = 100, Fecha = new DateTime(2026, 11, 1, 9, 0, 0)
        }).IsSuccess);
        Assert.True(bd.Config.AgregarGasto(new Gasto
        {
            Concepto = "Nuevo", Monto = 200, Fecha = new DateTime(2026, 11, 3, 9, 0, 0)
        }).IsSuccess);
        Assert.True(bd.Config.AgregarGasto(new Gasto
        {
            Concepto = "Medio", Monto = 300, Fecha = new DateTime(2026, 11, 2, 9, 0, 0)
        }).IsSuccess);

        var conceptos = bd.Config.ObtenerTodosLosGastos().Value.Select(g => g.Concepto);

        Assert.Equal(new[] { "Nuevo", "Medio", "Viejo" }, conceptos);
    }

    [Fact]
    public void ObtenerResumenGastos_SeparaCajaYExternos()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        Assert.True(bd.Config.AgregarGasto(new Gasto
        {
            Concepto = "Bolsas", Monto = 100, Origen = OrigenGasto.Caja
        }).IsSuccess);
        Assert.True(bd.Config.AgregarGasto(new Gasto
        {
            Concepto = "Luz", Monto = 250, Origen = OrigenGasto.Caja
        }).IsSuccess);
        Assert.True(bd.Config.AgregarGasto(new Gasto
        {
            Concepto = "Renta", Monto = 400, Origen = OrigenGasto.Externo
        }).IsSuccess);

        var resumen = bd.Config.ObtenerResumenGastos().Value;

        Assert.Equal(3, resumen.Cantidad);
        Assert.Equal(750L, resumen.Total);
        Assert.Equal(350L, resumen.DeCaja);
        Assert.Equal(400L, resumen.Externos);
    }

    [Fact]
    public void ObtenerResumenGastos_SinGastos_DevuelveCeros()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var resumen = bd.Config.ObtenerResumenGastos().Value;

        Assert.Equal(0, resumen.Cantidad);
        Assert.Equal(0L, resumen.Total);
        Assert.Equal(0L, resumen.DeCaja);
        Assert.Equal(0L, resumen.Externos);
    }
}
