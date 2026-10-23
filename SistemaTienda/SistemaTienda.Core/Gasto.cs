namespace SistemaTienda.Core;

public class Gasto
{
    public int Id { get; set; }
    public string Concepto { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    public string MetodoPago { get; set; } = string.Empty;
    public string Origen { get; set; } = OrigenGasto.Caja;
    public string Observaciones { get; set; } = string.Empty;
}

public static class OrigenGasto
{
    public const string Caja = "Caja";
    public const string Externo = "Externo";
}
