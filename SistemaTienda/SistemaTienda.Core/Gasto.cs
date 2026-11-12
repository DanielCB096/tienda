namespace SistemaTienda.Core;

public class Gasto
{
    public int Id { get; set; }
    public string Concepto { get; set; } = string.Empty;
    public long Monto { get; set; }
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

/// <summary>
/// Totales de gastos para la pantalla de la app: el total, lo pagado del
/// efectivo de la caja (afecta el corte) y lo pagado con otra fuente.
/// </summary>
public class ResumenGastos
{
    public int Cantidad { get; set; }
    public long Total { get; set; }
    public long DeCaja { get; set; }
    public long Externos { get; set; }
}
