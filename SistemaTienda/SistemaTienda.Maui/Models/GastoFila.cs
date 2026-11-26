using SistemaTienda.Core;

namespace SistemaTienda.Maui.Models;

public sealed class GastoFila
{
    public string Concepto { get; init; } = string.Empty;
    public string Monto { get; init; } = string.Empty;
    public string Detalle { get; init; } = string.Empty;
    public string Origen { get; init; } = string.Empty;
    public Color OrigenColor { get; init; } = Colors.Gray;
    public string Observaciones { get; init; } = string.Empty;
    public bool TieneObservaciones { get; init; }

    /// <summary>Gasto original para abrir el editor sin volver a consultarlo.</summary>
    public Gasto? Fuente { get; init; }
}
