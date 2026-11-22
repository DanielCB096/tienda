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

    /// <summary>
    /// Cobros del periodo por método. Una venta mixta suma su parte en efectivo
    /// y su parte con tarjeta; el fiado se reporta como cuenta por cobrar.
    /// </summary>
    public List<DesgloseMetodoPago> DesglosePagos { get; set; } = new();

    public long UtilidadBruta => VentasTotal - CostoVentas - ComisionTarjeta;
    public long UtilidadNeta => UtilidadBruta - Mermas - Gastos;
}

/// <summary>Cobros de un método de pago dentro del periodo, en centavos.</summary>
public sealed class DesgloseMetodoPago
{
    public string Metodo { get; set; } = string.Empty;
    public int NumeroVentas { get; set; }
    public long Monto { get; set; }
}
