namespace SistemaTienda.Core;

public class Producto
{
    public int Id { get; set; }
    public string CodigoBarras { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public decimal Costo { get; set; }
    public decimal Precio { get; set; }
    public int Stock { get; set; }
    public int StockMinimo { get; set; } = 5;
    /// <summary>False retira el producto del catálogo sin borrarlo (esquema v2).</summary>
    public bool Activo { get; set; } = true;
    public string Categoria { get; set; } = string.Empty;
    public decimal Impuesto { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
    public DateTime? UltimaActualizacion { get; set; }
}
