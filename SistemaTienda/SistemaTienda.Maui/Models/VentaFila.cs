namespace SistemaTienda.Maui.Models;

public sealed class VentaFila
{
    public string Folio { get; init; } = string.Empty;
    public string Total { get; init; } = string.Empty;
    public string Detalle { get; init; } = string.Empty;
    public string Estado { get; init; } = string.Empty;
    public Color EstadoColor { get; init; } = Colors.Gray;
}
