namespace SistemaTienda.Maui.Models;

/// <summary>Producto activo que se puede agregar a una compra a proveedor.</summary>
public sealed class ProductoCompraFila
{
    public int Id { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string Codigo { get; init; } = string.Empty;
    public string Stock { get; init; } = string.Empty;
    public string Costo { get; init; } = string.Empty;
}

/// <summary>Renglón capturado antes de guardar la compra.</summary>
public sealed class LineaCompraFila
{
    public int ProductoId { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string Detalle { get; init; } = string.Empty;
    public string Importe { get; init; } = string.Empty;
    public long ImporteCentavos { get; init; }
    public int Cantidad { get; init; }
    public long CostoUnitario { get; init; }
}
