namespace SistemaTienda.Core;

public class Producto
{
    public int Id { get; set; }
    public string CodigoBarras { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    /// <summary>Costo en centavos.</summary>
    public long Costo { get; set; }
    /// <summary>Precio de venta en centavos.</summary>
    public long Precio { get; set; }
    public int Stock { get; set; }
    public int StockMinimo { get; set; } = 5;
    public string Categoria { get; set; } = string.Empty;
    /// <summary>Porcentaje de impuesto; no es un importe.</summary>
    public decimal Impuesto { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
    public DateTime? UltimaActualizacion { get; set; }
}
