namespace SistemaTienda.Core;

/// <summary>
/// Devolución de dinero de una venta anulada. Se registra por método y puede
/// haber varias partes hasta cubrir lo pagado. Solo el efectivo mueve la caja
/// (como retiro); el fiado no se reembolsa porque la anulación revierte el saldo.
/// </summary>
public class ReembolsoVenta
{
    public int Id { get; set; }
    public int VentaId { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    public string Usuario { get; set; } = string.Empty;
    public string Metodo { get; set; } = string.Empty;
    public long Monto { get; set; }

    /// <summary>Retiro de caja generado cuando el reembolso fue en efectivo.</summary>
    public int? MovimientoCajaId { get; set; }
}

/// <summary>
/// Pagado, reembolsado y pendiente de una venta anulada. Los importes están en
/// centavos. El fiado queda fuera: la anulación ya revierte el saldo del cliente.
/// </summary>
public sealed class EstadoReembolsoVenta
{
    public int VentaId { get; set; }
    public long Pagado { get; set; }
    public long Reembolsado { get; set; }
    public long Pendiente => Math.Max(0, Pagado - Reembolsado);
    public List<ReembolsoVenta> Reembolsos { get; set; } = new();
}
