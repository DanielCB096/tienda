namespace SistemaTienda.Maui.Models;

/// <summary>
/// Renglón del estado de cuenta: una venta a fiado (cargo) o un abono (pago).
/// </summary>
public sealed class EstadoCuentaFila
{
    public DateTime FechaOrden { get; init; }
    public string Fecha { get; init; } = string.Empty;
    public string Tipo { get; init; } = string.Empty;
    public string Detalle { get; init; } = string.Empty;
    public string Monto { get; init; } = string.Empty;
    public Color MontoColor { get; init; } = Colors.Gray;
}
