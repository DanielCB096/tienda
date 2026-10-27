using SistemaTienda.Core;

namespace SistemaTienda.Tests;

public sealed class ConteoEfectivoTests
{
    [Fact]
    public void Denominaciones_CubrenBilletesYMonedasDeMayorAMenor()
    {
        var denominaciones = ConteoEfectivo.Denominaciones;

        Assert.Equal(11, denominaciones.Count);
        Assert.Equal(100_000, denominaciones[0].ValorCentavos);
        Assert.Equal(50, denominaciones[^1].ValorCentavos);
        Assert.Equal(
            denominaciones.Select(d => d.ValorCentavos).OrderByDescending(v => v),
            denominaciones.Select(d => d.ValorCentavos));
        Assert.Equal(
            denominaciones.Count,
            denominaciones.Select(d => d.ValorCentavos).Distinct().Count());
        Assert.All(denominaciones, d =>
            Assert.Contains(d.Grupo, new[] { "Billetes", "Monedas" }));
    }

    [Fact]
    public void Subtotal_MultiplicaValorPorCantidad()
    {
        var quinientos = Buscar(50_000);
        var cincuentaCentavos = Buscar(50);

        Assert.Equal(150_000, quinientos.Subtotal(3));
        Assert.Equal(1_250, cincuentaCentavos.Subtotal(25));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Subtotal_IgnoraCantidadesNoPositivas(int cantidad)
    {
        Assert.Equal(0, Buscar(100).Subtotal(cantidad));
    }

    [Fact]
    public void CalcularTotal_SumaBilletesYMonedas()
    {
        var total = ConteoEfectivo.CalcularTotal(new[]
        {
            (Buscar(100_000), 2),
            (Buscar(20_000), 3),
            (Buscar(1_000), 4),
            (Buscar(50), 1)
        });

        Assert.Equal(264_050, total);
    }

    [Fact]
    public void CalcularTotal_IgnoraLineasSinCantidad()
    {
        var veinte = Buscar(2_000);

        Assert.Equal(0, ConteoEfectivo.CalcularTotal(new[] { (veinte, 0), (veinte, -1) }));
    }

    private static DenominacionEfectivo Buscar(long valorCentavos) =>
        ConteoEfectivo.Denominaciones.Single(d => d.ValorCentavos == valorCentavos);
}
