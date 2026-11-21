// Catálogo de billetes y monedas para el conteo físico de la caja en pesos mexicanos
namespace SistemaTienda.Core;

public sealed record DenominacionEfectivo(long ValorCentavos, string Etiqueta, string Grupo)
{
    public long Subtotal(int cantidad) =>
        cantidad <= 0 ? 0 : checked(ValorCentavos * cantidad);
}

public static class ConteoEfectivo
{
    public const int MaximoCantidad = 100_000;

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

    /// <summary>
    /// Valida el conteo capturado y lo convierte en renglones persistibles.
    /// Se descartan las cantidades en cero y se rechazan denominaciones
    /// desconocidas, repetidas o cantidades fuera de rango.
    /// </summary>
    public static ProcessResult<List<CorteCajaDesglose>> CrearDesglose(
        IEnumerable<(DenominacionEfectivo Denominacion, int Cantidad)> conteo)
    {
        var renglones = new List<CorteCajaDesglose>();
        if (conteo == null)
            return ProcessResult<List<CorteCajaDesglose>>.OK(renglones);

        var vistas = new HashSet<long>();
        foreach (var (denominacion, cantidad) in conteo)
        {
            if (denominacion == null)
                return ProcessResult<List<CorteCajaDesglose>>.Error(
                    "El desglose incluye una denominación vacía");

            var catalogo = Denominaciones.FirstOrDefault(
                d => d.ValorCentavos == denominacion.ValorCentavos);
            if (catalogo == null)
                return ProcessResult<List<CorteCajaDesglose>>.Error(
                    $"La denominación {denominacion.Etiqueta} no está en el catálogo de efectivo");

            if (cantidad < 0 || cantidad > MaximoCantidad)
                return ProcessResult<List<CorteCajaDesglose>>.Error(
                    $"La cantidad de {catalogo.Etiqueta} debe ser un entero entre 0 y {MaximoCantidad}");

            if (!vistas.Add(catalogo.ValorCentavos))
                return ProcessResult<List<CorteCajaDesglose>>.Error(
                    $"La denominación {catalogo.Etiqueta} está repetida en el conteo");

            if (cantidad == 0)
                continue;

            renglones.Add(new CorteCajaDesglose
            {
                ValorCentavos = catalogo.ValorCentavos,
                Cantidad = cantidad,
                Subtotal = catalogo.Subtotal(cantidad)
            });
        }

        return ProcessResult<List<CorteCajaDesglose>>.OK(renglones);
    }

    /// <summary>Etiqueta legible de un valor en centavos; si no es del catálogo, lo formatea.</summary>
    public static string Etiqueta(long valorCentavos) =>
        Denominaciones.FirstOrDefault(d => d.ValorCentavos == valorCentavos)?.Etiqueta
        ?? $"${Dinero.Formatear(valorCentavos)}";
}
