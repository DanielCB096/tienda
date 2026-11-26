namespace SistemaTienda.Maui.Models;

public sealed class ReporteProductoFila
{
    public string Nombre { get; init; } = string.Empty;
    public string Detalle { get; init; } = string.Empty;
    public string Utilidad { get; init; } = string.Empty;
    public Color UtilidadColor { get; init; } = Colors.Black;
}

public sealed class ReporteCategoriaFila
{
    public string Nombre { get; init; } = string.Empty;
    public string Detalle { get; init; } = string.Empty;
    public string Utilidad { get; init; } = string.Empty;
    public Color UtilidadColor { get; init; } = Colors.Black;
}

public sealed class ReporteDiaFila
{
    public string Nombre { get; init; } = string.Empty;
    public string Detalle { get; init; } = string.Empty;
    public string Total { get; init; } = string.Empty;
    public string Utilidad { get; init; } = string.Empty;
    public Color UtilidadColor { get; init; } = Colors.Black;
}
