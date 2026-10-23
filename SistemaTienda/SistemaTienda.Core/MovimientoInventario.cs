public class MovimientoInventario
{
    public int Id { get; set; }
    public int ProductoId { get; set; }
    public string Tipo { get; set; } = string.Empty; // "ENTRADA", "SALIDA", "AJUSTE"
    public int Cantidad { get; set; }
    public decimal CostoUnitario { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public string Usuario { get; set; } = string.Empty;
    public DateTime Fecha { get; set; } = DateTime.Now;
}