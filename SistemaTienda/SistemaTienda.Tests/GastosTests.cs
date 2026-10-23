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
            Monto = 0.01m,
            MetodoPago = "Efectivo",
            Origen = OrigenGasto.Caja
        });

        Assert.True(alta.IsSuccess, alta.Message);
        Assert.Equal(0.01m, bd.Config.ObtenerTotalGastos().Value);
        Assert.Single(bd.Config.ObtenerTodosLosGastos().Value);
    }

    [Fact]
    public void ObtenerTotalGastos_SumaCentavosSinPerderPrecision()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        Assert.True(bd.Config.AgregarGasto(new Gasto { Concepto = "Uno", Monto = 0.10m }).IsSuccess);
        Assert.True(bd.Config.AgregarGasto(new Gasto { Concepto = "Dos", Monto = 0.20m }).IsSuccess);
        Assert.True(bd.Config.AgregarGasto(new Gasto { Concepto = "Tres", Monto = 0.01m }).IsSuccess);

        Assert.Equal(0.31m, bd.Config.ObtenerTotalGastos().Value);
    }

    [Fact]
    public void AgregarGasto_ConMontoCeroONegativo_NoRegistraNiAlteraElTotal()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var cero = bd.Config.AgregarGasto(new Gasto { Concepto = "Cero", Monto = 0m });
        var negativo = bd.Config.AgregarGasto(new Gasto { Concepto = "Negativo", Monto = -0.01m });

        Assert.True(cero.IsError);
        Assert.True(negativo.IsError);
        Assert.Empty(bd.Config.ObtenerTodosLosGastos().Value);
        Assert.Equal(0m, bd.Config.ObtenerTotalGastos().Value);
    }
}
