namespace SistemaTienda.Core;

/// <summary>
/// Corte de caja inmutable: conciliación del efectivo de un periodo contra lo registrado.
/// </summary>
public class CorteCaja
{
    public int Id { get; set; }
    public DateTime Desde { get; set; }
    public DateTime Hasta { get; set; }
    public decimal FondoInicial { get; set; }
    public decimal VentasEfectivo { get; set; }
    public decimal VentasTarjeta { get; set; }
    public decimal VentasFiado { get; set; }
    public decimal TotalVentas { get; set; }
    public int NumeroVentas { get; set; }
    public int NumeroVentasEfectivo { get; set; }
    public int NumeroVentasTarjeta { get; set; }
    public int NumeroVentasMixto { get; set; }
    public int NumeroVentasFiado { get; set; }
    public decimal AbonosEfectivo { get; set; }
    public decimal IngresosCaja { get; set; }
    public decimal RetirosCaja { get; set; }
    public decimal GastosCaja { get; set; }
    public decimal EfectivoEsperado { get; set; }
    public decimal EfectivoContado { get; set; }
    public decimal Diferencia { get; set; }
    public string Usuario { get; set; } = string.Empty;
    public string Observaciones { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; }
}

public class MovimientoCaja
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    public string Tipo { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public string Usuario { get; set; } = string.Empty;
}

public static class TipoMovimientoCaja
{
    public const string Ingreso = "Ingreso";
    public const string Retiro = "Retiro";
}
