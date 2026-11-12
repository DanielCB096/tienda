namespace SistemaTienda.Maui.Models;

public sealed class ProveedorFila
{
    public int Id { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string Clave { get; init; } = string.Empty;
    public string Telefono { get; init; } = string.Empty;
    public string Direccion { get; init; } = string.Empty;
    public bool TieneDireccion { get; init; }
    public string Saldo { get; init; } = string.Empty;
    public string Estado { get; init; } = string.Empty;
    public Color EstadoColor { get; init; } = Colors.Gray;
    public bool PuedePagar { get; init; }
}
