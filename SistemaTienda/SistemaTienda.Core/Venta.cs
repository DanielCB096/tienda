namespace SistemaTienda.Core;

/// <summary>
/// Venta registrada. El historial no se edita: las correcciones se hacen con movimientos nuevos.
/// Precio y costo de cada línea se congelan al vender. Todos los importes están en centavos.
/// </summary>
public class Venta
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    public string Usuario { get; set; } = string.Empty;
    public int? ClienteId { get; set; }
    public string MetodoPago { get; set; } = MetodoPagoVenta.Efectivo;
    public long Total { get; set; }
    public long CostoTotal { get; set; }
    public long PagoEfectivo { get; set; }
    public long PagoTarjeta { get; set; }
    public long PagoFiado { get; set; }
    public long ComisionTarjeta { get; set; }
}

public class VentaLinea
{
    public int Id { get; set; }
    public int VentaId { get; set; }
    public int ProductoId { get; set; }
    public string CodigoBarras { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public long PrecioUnitario { get; set; }
    public long CostoUnitario { get; set; }
    public long Importe { get; set; }
}

public static class MetodoPagoVenta
{
    public const string Efectivo = "Efectivo";
    public const string Tarjeta = "Tarjeta";
    public const string Mixto = "Mixto";
    public const string Fiado = "Fiado";
}
