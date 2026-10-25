namespace SistemaTienda.Core;

/// <summary>
/// Cliente con saldo de fiado. Datos mínimos: nombre y teléfono (LFPDPPP).
/// </summary>
public class ClienteFiado
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public long SaldoActual { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.Now;
    public DateTime? UltimaActualizacion { get; set; }
}
