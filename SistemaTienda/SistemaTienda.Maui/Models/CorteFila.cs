namespace SistemaTienda.Maui.Models;

public sealed class CorteFila
{
    public string Folio { get; init; } = string.Empty;
    public string Periodo { get; init; } = string.Empty;
    public string Montos { get; init; } = string.Empty;
    public string Cierre { get; init; } = string.Empty;
    public string Diferencia { get; init; } = string.Empty;
    public Color DiferenciaColor { get; init; } = Colors.Gray;
    public string Observaciones { get; init; } = string.Empty;
    public bool TieneObservaciones => !string.IsNullOrWhiteSpace(Observaciones);
}
