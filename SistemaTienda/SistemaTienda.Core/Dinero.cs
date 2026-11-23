// Utilidades de dinero: todos los importes se guardan en centavos enteros
using System.Globalization;

namespace SistemaTienda.Core;

/// <summary>
/// Operaciones con importes expresados en centavos. No se usa decimal para dinero:
/// así no hay errores de redondeo acumulados y las sumas son exactas.
/// Los porcentajes (impuestos, comisiones) sí se guardan como decimal.
/// </summary>
public static class Dinero
{
    public const long CentavosPorPeso = 100;

    /// <summary>Convierte pesos capturados por el usuario a centavos, redondeando al centavo.</summary>
    public static long DePesos(decimal pesos) =>
        checked((long)decimal.Round(pesos * CentavosPorPeso, 0, MidpointRounding.AwayFromZero));

    public static decimal APesos(long centavos) => centavos / (decimal)CentavosPorPeso;

    public static long Multiplicar(long centavosUnitarios, int cantidad) =>
        checked(centavosUnitarios * cantidad);

    /// <summary>Calcula un porcentaje del importe, redondeado al centavo más cercano.</summary>
    public static long Porcentaje(long centavos, decimal porcentaje) =>
        checked((long)decimal.Round(centavos * porcentaje / 100m, 0, MidpointRounding.AwayFromZero));

    /// <summary>
    /// Impuesto ya incluido en un importe. Con tasa 16% y $116.00 devuelve $16.00.
    /// Si la tasa es cero o negativa, el impuesto es cero.
    /// </summary>
    public static long ImpuestoIncluido(long importeConImpuesto, decimal tasaPorcentaje)
    {
        if (importeConImpuesto <= 0 || tasaPorcentaje <= 0)
            return 0;

        return checked((long)decimal.Round(
            importeConImpuesto * tasaPorcentaje / (100m + tasaPorcentaje),
            0, MidpointRounding.AwayFromZero));
    }

    /// <summary>Importe sin el impuesto incluido; nunca es negativo.</summary>
    public static long BaseSinImpuesto(long importeConImpuesto, decimal tasaPorcentaje)
    {
        var impuesto = ImpuestoIncluido(importeConImpuesto, tasaPorcentaje);
        return Math.Max(0, importeConImpuesto - impuesto);
    }

    /// <summary>Formatea centavos como pesos con dos decimales, sin símbolo de moneda.</summary>
    public static string Formatear(long centavos) =>
        APesos(centavos).ToString("F2", CultureInfo.InvariantCulture);
}
