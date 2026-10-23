using System;

public class Producto
{
    public int Id { get; set; }
    public string CodigoBarras { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public decimal Costo { get; set; }
    public decimal Precio { get; set; }
    public int Stock { get; set; }
    public int StockMinimo { get; set; } = 5; // alerta cuando Stock <= StockMinimo
    public string Categoria { get; set; } = string.Empty;
    public decimal Impuesto { get; set; } // Impuesto específico por producto; si 0, usa config global
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
    public DateTime? UltimaActualizacion { get; set; }
}