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

    /// <summary>Descuento aplicado a la venta, en centavos. No se suma al total.</summary>
    public long Descuento { get; set; }

    /// <summary>
    /// Porcentaje capturado por el cajero. Vale cero cuando el descuento se dio
    /// como monto fijo o cuando la venta no tiene descuento.
    /// </summary>
    public decimal DescuentoPorcentaje { get; set; }

    /// <summary>Importe antes del descuento, en centavos.</summary>
    public long Subtotal => Total + Descuento;

    public long CostoTotal { get; set; }
    public long PagoEfectivo { get; set; }
    public long PagoTarjeta { get; set; }
    public long PagoFiado { get; set; }
    public long ComisionTarjeta { get; set; }

    /// <summary>
    /// Efectivo entregado por el cliente en ventas en efectivo. Es nulo en ventas
    /// con tarjeta, fiado, mixto o anteriores al esquema v12.
    /// </summary>
    public long? EfectivoRecibido { get; set; }

    public bool Anulada { get; set; }
}

/// <summary>
/// Registro inmutable de la anulación de una venta completa. La venta original no se modifica.
/// </summary>
public class AnulacionVenta
{
    public int Id { get; set; }
    public int VentaId { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    public string Usuario { get; set; } = string.Empty;
    public string Motivo { get; set; } = string.Empty;
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

    /// <summary>Importe neto después del descuento, en centavos.</summary>
    public long Importe { get; set; }

    /// <summary>Parte del descuento de la venta asignada a esta línea, en centavos.</summary>
    public long Descuento { get; set; }

    /// <summary>Porcentaje capturado para esta línea; cero si el descuento fue un monto.</summary>
    public decimal DescuentoPorcentaje { get; set; }

    /// <summary>Importe bruto antes del descuento, en centavos.</summary>
    public long Subtotal => Importe + Descuento;

    /// <summary>Tasa de impuesto congelada al vender; el precio ya la incluye.</summary>
    public decimal ImpuestoPorcentaje { get; set; }

    /// <summary>Impuesto contenido en <see cref="Importe"/>; informativo, no se suma al total.</summary>
    public long ImpuestoImporte { get; set; }
}

public static class MetodoPagoVenta
{
    public const string Efectivo = "Efectivo";
    public const string Tarjeta = "Tarjeta";
    public const string Mixto = "Mixto";
    public const string Fiado = "Fiado";
}
