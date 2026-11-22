namespace SistemaTienda.Core;

/// <summary>
/// Comparación del reporte de utilidad actual contra el bloque inmediatamente
/// anterior de la misma duración. Todos los importes están en centavos.
/// </summary>
public sealed class ComparacionReporte
{
    public ReporteUtilidad Actual { get; set; } = new();
    public ReporteUtilidad Anterior { get; set; } = new();

    public int DiferenciaVentas => Actual.NumeroVentas - Anterior.NumeroVentas;
    public long DiferenciaMontoVentas => Actual.VentasTotal - Anterior.VentasTotal;
    public long DiferenciaUtilidadBruta => Actual.UtilidadBruta - Anterior.UtilidadBruta;
    public long DiferenciaUtilidadNeta => Actual.UtilidadNeta - Anterior.UtilidadNeta;
    public long DiferenciaGastos => Actual.Gastos - Anterior.Gastos;

    /// <summary>
    /// Variación porcentual contra el periodo anterior; es nula cuando el anterior
    /// vale cero porque el porcentaje no está definido.
    /// </summary>
    public decimal? VariacionVentas => Variacion(Actual.VentasTotal, Anterior.VentasTotal);
    public decimal? VariacionUtilidadNeta => Variacion(Actual.UtilidadNeta, Anterior.UtilidadNeta);
    public decimal? VariacionGastos => Variacion(Actual.Gastos, Anterior.Gastos);

    public static decimal? Variacion(long actual, long anterior) =>
        anterior == 0 ? null : (actual - anterior) * 100m / anterior;
}
