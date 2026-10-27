// Catálogo de billetes y monedas para el conteo físico de la caja en pesos mexicanos
namespace SistemaTienda.Core;

public sealed record DenominacionEfectivo(long ValorCentavos, string Etiqueta, string Grupo)
{
    public long Subtotal(int cantidad) =>
        cantidad <= 0 ? 0 : checked(ValorCentavos * cantidad);
}

public static class ConteoEfectivo
{
    public static IReadOnlyList<DenominacionEfectivo> Denominaciones { get; } =
        new List<DenominacionEfectivo>
        {
            new(100_000, "$1000", "Billetes"),
            new(50_000, "$500", "Billetes"),
            new(20_000, "$200", "Billetes"),
            new(10_000, "$100", "Billetes"),
            new(5_000, "$50", "Billetes"),
            new(2_000, "$20", "Billetes"),
            new(1_000, "$10", "Monedas"),
            new(500, "$5", "Monedas"),
            new(200, "$2", "Monedas"),
            new(100, "$1", "Monedas"),
            new(50, "$0.50", "Monedas")
        }.AsReadOnly();

    public static long CalcularTotal(
        IEnumerable<(DenominacionEfectivo Denominacion, int Cantidad)> conteo)
    {
        long total = 0;
        foreach (var (denominacion, cantidad) in conteo)
            total = checked(total + denominacion.Subtotal(cantidad));

        return total;
    }
}
