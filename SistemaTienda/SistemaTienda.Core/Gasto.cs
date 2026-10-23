public class Gasto
{
    public int Id { get; set; }
    public string Concepto { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    public string MetodoPago { get; set; } = string.Empty;
    public string Origen { get; set; } = "Caja"; -- 'Caja' o 'Externo'
    public string Observaciones { get; set; } = string.Empty;
}