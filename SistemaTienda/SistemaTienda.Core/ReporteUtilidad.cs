namespace SistemaTienda.Core;

/// <summary>
/// Utilidad de un periodo: ventas menos costo congelado, comisión, mermas y gastos.
/// Todos los importes están en centavos.
/// </summary>
public sealed class ReporteUtilidad
{
    public DateTime Desde { get; set; }
    public DateTime Hasta { get; set; }
    public int NumeroVentas { get; set; }
    public long VentasTotal { get; set; }
    public long CostoVentas { get; set; }
    public long ComisionTarjeta { get; set; }
    public long Mermas { get; set; }
    public long Gastos { get; set; }

    public long UtilidadBruta => VentasTotal - CostoVentas - ComisionTarjeta;
    public long UtilidadNeta => UtilidadBruta - Mermas - Gastos;
}
