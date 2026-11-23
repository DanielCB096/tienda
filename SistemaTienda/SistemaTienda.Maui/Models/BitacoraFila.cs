namespace SistemaTienda.Maui.Models;

/// <summary>Renglón de la bitácora listo para mostrar en Ajustes.</summary>
public sealed class BitacoraFila
{
    public string Fecha { get; init; } = string.Empty;
    public string Usuario { get; init; } = string.Empty;
    public string Accion { get; init; } = string.Empty;
    public string Entidad { get; init; } = string.Empty;
    public string Detalle { get; init; } = string.Empty;
    public string Motivo { get; init; } = string.Empty;
    public bool TieneMotivo { get; init; }
}
