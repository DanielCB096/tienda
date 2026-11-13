namespace SistemaTienda.Core;

public class Proveedor
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Clave { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public long SaldoPendiente { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaRegistro { get; set; } = DateTime.Now;
    public DateTime? UltimaActualizacion { get; set; }
}

/// <summary>
/// Pago registrado en el historial inmutable <c>PagosProveedor</c>. Guarda el
/// saldo antes y después para poder reconstruir la cuenta. Si el pago fue en
/// efectivo, <c>MovimientoCajaId</c> apunta al retiro generado en la caja.
/// </summary>
public class PagoProveedor
{
    public int Id { get; set; }
    public int ProveedorId { get; set; }
    public DateTime Fecha { get; set; }
    public long Monto { get; set; }
    public long SaldoAnterior { get; set; }
    public long SaldoNuevo { get; set; }
    public string MetodoPago { get; set; } = string.Empty;
    public int? MovimientoCajaId { get; set; }
    public string Usuario { get; set; } = string.Empty;
}

/// <summary>
/// Métodos aceptados al pagar a un proveedor. Solo <c>Efectivo</c> genera un
/// retiro de caja; los demás no tocan el efectivo esperado.
/// </summary>
public static class MetodoPagoProveedor
{
    public const string Efectivo = "Efectivo";
    public const string Tarjeta = "Tarjeta";
    public const string Transferencia = "Transferencia";
    public const string Otro = "Otro";

    public static readonly IReadOnlyList<string> Todos =
        new[] { Efectivo, Tarjeta, Transferencia, Otro };

    public static bool EsValido(string metodo) =>
        !string.IsNullOrWhiteSpace(metodo) && Todos.Contains(metodo.Trim());

    public static string Normalizar(string metodo) =>
        (metodo ?? string.Empty).Trim();
}

/// <summary>
/// Resultado interno del registro atómico de un pago. <c>MovimientoCajaId</c>
/// solo tiene valor cuando el pago fue en efectivo.
/// </summary>
public sealed record ResultadoPagoProveedor(bool Exito, int? MovimientoCajaId);

/// <summary>
/// Estado de cuenta de un proveedor: sus datos, el saldo pendiente y el
/// historial de pagos. El saldo inicial del alta no aparece como movimiento,
/// así que el saldo pendiente es la fuente de verdad.
/// </summary>
public class EstadoCuentaProveedor
{
    public Proveedor Proveedor { get; set; } = new();
    public List<PagoProveedor> Pagos { get; set; } = new();
    public long TotalPagado { get; set; }
    public long SaldoPendiente { get; set; }
}
