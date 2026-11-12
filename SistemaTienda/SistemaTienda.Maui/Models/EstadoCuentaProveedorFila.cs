namespace SistemaTienda.Maui.Models;

/// <summary>
/// Renglón del estado de cuenta de un proveedor: un pago registrado en
/// <c>PagosProveedor</c> con el saldo antes y después.
/// </summary>
public sealed class EstadoCuentaProveedorFila
{
    public DateTime FechaOrden { get; init; }
    public string Fecha { get; init; } = string.Empty;
    public string Tipo { get; init; } = string.Empty;
    public string Detalle { get; init; } = string.Empty;
    public string Monto { get; init; } = string.Empty;
    public Color MontoColor { get; init; } = Colors.Gray;
}
