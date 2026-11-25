namespace SistemaTienda.Core;

/// <summary>
/// Carrito guardado en el mostrador. No es una venta: no mueve stock ni caja.
/// Se persiste para sobrevivir al cierre de la app o a un corte de luz.
/// </summary>
public class TicketEspera
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Nota { get; set; } = string.Empty;
    public string Usuario { get; set; } = string.Empty;
    public DateTime Fecha { get; set; } = DateTime.Now;
    public string Estado { get; set; } = EstadoTicketEspera.Disponible;
    public string RetomadoPor { get; set; } = string.Empty;
    public DateTime? RetomadoFecha { get; set; }
    public int? VentaId { get; set; }
    public DateTime? FechaCierre { get; set; }
    public List<TicketEsperaLinea> Lineas { get; set; } = new();

    public long TotalEstimado => Lineas.Sum(linea => linea.PrecioUnitario * linea.Cantidad);
    public int Piezas => Lineas.Sum(linea => linea.Cantidad);
}

public class TicketEsperaLinea
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public int ProductoId { get; set; }
    public int Cantidad { get; set; }
    /// <summary>Precio congelado al dejar el ticket, para avisar si cambió.</summary>
    public long PrecioUnitario { get; set; }

    // Datos de apoyo para la interfaz; no se guardan en la tabla.
    public string ProductoNombre { get; set; } = string.Empty;
    public long PrecioActual { get; set; }
    public int StockActual { get; set; }
    public bool ProductoActivo { get; set; } = true;

    public bool CambioPrecio => PrecioActual != PrecioUnitario;
    public bool SinStock => Cantidad > StockActual;
}

public static class EstadoTicketEspera
{
    public const string Disponible = "Disponible";
    public const string Retomado = "Retomado";
    public const string Cobrado = "Cobrado";
    public const string Descartado = "Descartado";
}
