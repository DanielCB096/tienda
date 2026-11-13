namespace SistemaTienda.Maui.Models;

public sealed class ClienteFila
{
    public int Id { get; init; }
    public string Nombre { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string TelefonoReal { get; set; } = string.Empty;
    public string Saldo { get; init; } = string.Empty;
    public string Estado { get; init; } = string.Empty;
    public Color EstadoColor { get; init; } = Colors.Gray;
    public bool Activo { get; init; }
    public bool PuedeAbonar { get; init; }
    public bool PuedeEditar { get; init; }
    public bool PuedeCambiarEstado { get; init; }
    public string AccionEstado => Activo ? "Dar de baja" : "Reactivar";
}
