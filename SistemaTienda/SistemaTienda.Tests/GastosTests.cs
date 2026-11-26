using Microsoft.Data.Sqlite;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;

namespace SistemaTienda.Tests;

public sealed class GastosTests
{
    [Fact]
    public void EditarGasto_CambiaCamposConservaFechaYDejaBitacora()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var fecha = new DateTime(2026, 11, 10, 9, 30, 0);
        Assert.True(bd.Config.AgregarGasto(new Gasto
        {
            Concepto = "Luz",
            Monto = 10_000,
            Fecha = fecha,
            MetodoPago = MetodoPagoAbono.Efectivo,
            Origen = OrigenGasto.Caja
        }).IsSuccess);
        var gasto = Assert.Single(bd.Config.ObtenerTodosLosGastos().Value);

        var resultado = bd.Config.EditarGasto(gasto.Id, "  Luz de noviembre  ",
            12_000, MetodoPagoAbono.Efectivo, OrigenGasto.Externo, "  Corregido  ");

        Assert.True(resultado.IsSuccess, resultado.Message);
        var actualizado = Assert.Single(bd.Config.ObtenerTodosLosGastos().Value);
        Assert.Equal("Luz de noviembre", actualizado.Concepto);
        Assert.Equal(12_000, actualizado.Monto);
        Assert.Equal(OrigenGasto.Externo, actualizado.Origen);
        Assert.Equal("Corregido", actualizado.Observaciones);
        Assert.Equal(fecha, actualizado.Fecha);
        Assert.Equal(1, bd.Db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Bitacora WHERE Accion = @accion",
            new SqliteParameter("@accion", AccionBitacora.EditarGasto)));
    }

    [Fact]
    public void EditarGasto_ConCorteCerrado_SeBloquea()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        Assert.True(bd.Config.AgregarGasto(new Gasto
        {
            Concepto = "Luz",
            Monto = 10_000,
            MetodoPago = MetodoPagoAbono.Efectivo,
            Origen = OrigenGasto.Caja
        }).IsSuccess);
        var gasto = Assert.Single(bd.Config.ObtenerTodosLosGastos().Value);

        var corte = new CorteCajaService(bd.Db).RegistrarCorte(0, 0, "Dueño",
            "Cierre de prueba");
        Assert.True(corte.IsSuccess, corte.Message);

        var resultado = bd.Config.EditarGasto(gasto.Id, "Luz corregida", 12_000,
            MetodoPagoAbono.Efectivo, OrigenGasto.Caja, string.Empty);

        Assert.True(resultado.IsError);
        Assert.Contains("corte cerrado", resultado.Message);
        Assert.Equal(10_000, Assert.Single(bd.Config.ObtenerTodosLosGastos().Value).Monto);
    }

    [Fact]
    public void EditarGasto_ConCajaYTarjeta_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        Assert.True(bd.Config.AgregarGasto(new Gasto
        {
            Concepto = "Luz",
            Monto = 10_000,
            MetodoPago = MetodoPagoAbono.Efectivo,
            Origen = OrigenGasto.Caja
        }).IsSuccess);
        var gasto = Assert.Single(bd.Config.ObtenerTodosLosGastos().Value);

        var resultado = bd.Config.EditarGasto(gasto.Id, "Luz", 10_000,
            "Tarjeta", OrigenGasto.Caja, string.Empty);

        Assert.True(resultado.IsError);
        Assert.Contains("efectivo", resultado.Message);
    }

    [Fact]
    public void EditarGasto_EmpleadoSinPermiso_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        Assert.True(bd.Config.AgregarGasto(new Gasto { Concepto = "Luz", Monto = 1_000 }).IsSuccess);
        var gasto = Assert.Single(bd.Config.ObtenerTodosLosGastos().Value);
        bd.IniciarSesion(RolUsuario.Empleado, "Empleado");

        var resultado = bd.Config.EditarGasto(gasto.Id, "Luz", 2_000,
            MetodoPagoAbono.Efectivo, OrigenGasto.Caja, string.Empty);

        Assert.True(resultado.IsError);
    }

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
    public void AgregarGasto_RecortaConceptoYObservaciones()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var alta = bd.Config.AgregarGasto(new Gasto
        {
            Concepto = "   Luz   ",
            Monto = 1_000,
            Observaciones = "   Recibo 12   "
        });

        Assert.True(alta.IsSuccess);
        var gasto = Assert.Single(bd.Config.ObtenerTodosLosGastos().Value);
        Assert.Equal("Luz", gasto.Concepto);
        Assert.Equal("Recibo 12", gasto.Observaciones);
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
