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

/// <summary>
/// Venta a fiado de un cliente para el estado de cuenta. Las ventas anuladas
/// no se incluyen: su reverso ya devolvió el saldo.
/// </summary>
public class VentaFiadaCliente
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public long Total { get; set; }
    public long PagoFiado { get; set; }
    public string MetodoPago { get; set; } = string.Empty;
    public string Usuario { get; set; } = string.Empty;
}

/// <summary>
/// Abono registrado en el historial inmutable <c>AbonosFiado</c>.
/// </summary>
public class AbonoFiadoCliente
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public long Monto { get; set; }
    public string MetodoPago { get; set; } = string.Empty;
    public string Usuario { get; set; } = string.Empty;
}

/// <summary>
/// Estado de cuenta de un cliente: sus datos, el saldo vigente y el historial
/// de ventas a fiado y abonos. El saldo inicial del alta no aparece como
/// movimiento, así que el saldo vigente es la fuente de verdad.
/// </summary>
public class EstadoCuentaCliente
{
    public ClienteFiado Cliente { get; set; } = new();
    public List<VentaFiadaCliente> Ventas { get; set; } = new();
    public List<AbonoFiadoCliente> Abonos { get; set; } = new();
    public long TotalFiado { get; set; }
    public long TotalAbonado { get; set; }
    public long SaldoActual { get; set; }
}
