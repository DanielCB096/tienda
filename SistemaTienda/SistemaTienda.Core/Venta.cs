namespace SistemaTienda.Core;

/// <summary>
/// Venta registrada. El historial no se edita: las correcciones se hacen con movimientos nuevos.
/// Precio y costo de cada línea se congelan al vender.
/// </summary>
public class Venta
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    public string Usuario { get; set; } = string.Empty;
    public int? ClienteId { get; set; }
    public string MetodoPago { get; set; } = MetodoPagoVenta.Efectivo;
    public decimal Total { get; set; }
    public decimal CostoTotal { get; set; }
    public decimal PagoEfectivo { get; set; }
    public decimal PagoTarjeta { get; set; }
    public decimal PagoFiado { get; set; }
    public decimal ComisionTarjeta { get; set; }
}

public class VentaLinea
{
    public int Id { get; set; }
    public int VentaId { get; set; }
    public int ProductoId { get; set; }
    public string CodigoBarras { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal CostoUnitario { get; set; }
    public decimal Importe { get; set; }
}

public static class MetodoPagoVenta
{
    public const string Efectivo = "Efectivo";
    public const string Tarjeta = "Tarjeta";
    public const string Mixto = "Mixto";
    public const string Fiado = "Fiado";
}
