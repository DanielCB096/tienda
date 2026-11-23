namespace SistemaTienda.Core;

/// <summary>
/// Compra registrada a un proveedor. Es inmutable: aumenta el stock de los
/// productos y el saldo pendiente del proveedor en una sola transacción.
/// El pago se hace después con el flujo de pagos a proveedores.
/// </summary>
public class CompraProveedor
{
    public int Id { get; set; }
    public int ProveedorId { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    public string Usuario { get; set; } = string.Empty;
    public long Total { get; set; }
    public string Observaciones { get; set; } = string.Empty;
    public long SaldoAnterior { get; set; }
    public long SaldoNuevo { get; set; }

    public List<CompraProveedorLinea> Lineas { get; set; } = new();
}

/// <summary>Renglón de una compra con costo y cantidad congelados.</summary>
public class CompraProveedorLinea
{
    public int Id { get; set; }
    public int CompraId { get; set; }
    public int ProductoId { get; set; }
    public string CodigoBarras { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public long CostoUnitario { get; set; }
    public long Importe { get; set; }
}

/// <summary>Solicitud de compra que valida la capa de aplicación.</summary>
public sealed class LineaCompraSolicitada
{
    public int ProductoId { get; set; }
    public int Cantidad { get; set; }
    public long CostoUnitario { get; set; }
}
