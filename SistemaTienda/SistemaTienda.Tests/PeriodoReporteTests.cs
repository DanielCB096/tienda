using SistemaTienda.Core;

namespace SistemaTienda.Tests;

public sealed class PeriodoReporteTests
{
    [Fact]
    public void Hoy_IgnoraLaHoraDeLaReferencia()
    {
        var referencia = new DateTime(2026, 11, 12, 18, 45, 30);

        var periodo = PeriodoReporte.Hoy(referencia);

        Assert.Equal(new DateTime(2026, 11, 12), periodo.Desde);
        Assert.Equal(new DateTime(2026, 11, 12), periodo.Hasta);
    }

    [Fact]
    public void MesActual_EmpiezaElPrimeroYTerminaHoy()
    {
        var referencia = new DateTime(2026, 11, 12, 9, 30, 0);

        var periodo = PeriodoReporte.MesActual(referencia);

        Assert.Equal(new DateTime(2026, 11, 1), periodo.Desde);
        Assert.Equal(new DateTime(2026, 11, 12), periodo.Hasta);
    }

    [Fact]
    public void MesActual_EnEnero_NoSeSaleDelAnio()
    {
        var referencia = new DateTime(2027, 1, 5);

        var periodo = PeriodoReporte.MesActual(referencia);

        Assert.Equal(new DateTime(2027, 1, 1), periodo.Desde);
        Assert.Equal(new DateTime(2027, 1, 5), periodo.Hasta);
    }

    [Fact]
    public void Entre_NormalizaLasHorasDeAmbasFechas()
    {
        var desde = new DateTime(2026, 3, 1, 23, 50, 0);
        var hasta = new DateTime(2026, 3, 31, 0, 10, 0);

        var periodo = PeriodoReporte.Entre(desde, hasta);

        Assert.Equal(new DateTime(2026, 3, 1), periodo.Desde);
        Assert.Equal(new DateTime(2026, 3, 31), periodo.Hasta);
    }
}
