namespace SistemaTienda.Core;

/// <summary>
/// Corte de caja inmutable: conciliación del efectivo de un periodo contra lo registrado.
/// Todos los importes están en centavos.
/// </summary>
public class CorteCaja
{
    public int Id { get; set; }
    public DateTime Desde { get; set; }
    public DateTime Hasta { get; set; }
    public long FondoInicial { get; set; }
    public long VentasEfectivo { get; set; }
    public long VentasTarjeta { get; set; }
    public long VentasFiado { get; set; }
    public long TotalVentas { get; set; }
    public int NumeroVentas { get; set; }
    public int NumeroVentasEfectivo { get; set; }
    public int NumeroVentasTarjeta { get; set; }
    public int NumeroVentasMixto { get; set; }
    public int NumeroVentasFiado { get; set; }
    public long AbonosEfectivo { get; set; }
    public long IngresosCaja { get; set; }
    public long RetirosCaja { get; set; }
    public long GastosCaja { get; set; }
    public long EfectivoEsperado { get; set; }
    public long EfectivoContado { get; set; }
    public long Diferencia { get; set; }
    public string Usuario { get; set; } = string.Empty;
    public string Observaciones { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; }
}

public class MovimientoCaja
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    public string Tipo { get; set; } = string.Empty;
    public long Monto { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public string Usuario { get; set; } = string.Empty;
}

public static class TipoMovimientoCaja
{
    public const string Ingreso = "Ingreso";
    public const string Retiro = "Retiro";
}
