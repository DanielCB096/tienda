namespace SistemaTienda.Maui.Models;

public sealed class ClienteFila
{
    public int Id { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string Telefono { get; init; } = string.Empty;
    public string Saldo { get; init; } = string.Empty;
    public string Estado { get; init; } = string.Empty;
    public Color EstadoColor { get; init; } = Colors.Gray;
    public bool PuedeAbonar { get; init; }
}
