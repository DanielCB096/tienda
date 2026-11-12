using SistemaTienda.Core;

namespace SistemaTienda.Tests;

public sealed class DineroTests
{
    [Theory]
    [InlineData(0.0, 0L)]
    [InlineData(1.0, 100L)]
    [InlineData(12.34, 1_234L)]
    [InlineData(0.005, 1L)]
    [InlineData(0.015, 2L)]
    [InlineData(0.004, 0L)]
    public void DePesos_RedondeaAlCentavoMasCercano(decimal pesos, long centavosEsperados)
    {
        Assert.Equal(centavosEsperados, Dinero.DePesos(pesos));
    }

    [Theory]
    [InlineData(0L, 0)]
    [InlineData(5L, 0.05)]
    [InlineData(12_345L, 123.45)]
    [InlineData(-1_000L, -10.00)]
    public void APesos_DevuelveElImporteEnPesos(long centavos, decimal pesosEsperados)
    {
        Assert.Equal(pesosEsperados, Dinero.APesos(centavos));
    }

    [Fact]
    public void Multiplicar_UsaCentavosUnitarios()
    {
        Assert.Equal(7_500, Dinero.Multiplicar(2_500, 3));
        Assert.Equal(0, Dinero.Multiplicar(999, 0));
        Assert.Throws<OverflowException>(() => Dinero.Multiplicar(long.MaxValue, 2));
    }

    [Theory]
    [InlineData(10_000, 3.5, 350L)]
    [InlineData(333, 3.5, 12L)]
    [InlineData(100, 2.45, 2L)]
    [InlineData(300, 2.45, 7L)]
    public void Porcentaje_RedondeaAlCentavoMasCercano(long centavos, decimal porcentaje, long esperado)
    {
        Assert.Equal(esperado, Dinero.Porcentaje(centavos, porcentaje));
    }

    [Theory]
    [InlineData(0L, "0.00")]
    [InlineData(5L, "0.05")]
    [InlineData(12_345L, "123.45")]
    [InlineData(-1_000L, "-10.00")]
    public void Formatear_EsInvarianteAlaCultura(long centavos, string esperado)
    {
        Assert.Equal(esperado, Dinero.Formatear(centavos));
    }
}
