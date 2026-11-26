namespace SistemaTienda.Core;

/// <summary>
/// Utilidad de un periodo: ventas menos costo congelado, comisión, mermas y gastos.
/// Todos los importes están en centavos.
/// </summary>
public sealed class ReporteUtilidad
{
    public DateTime Desde { get; set; }
    public DateTime Hasta { get; set; }
    public int NumeroVentas { get; set; }
    public long VentasTotal { get; set; }
    public long CostoVentas { get; set; }
    public long ComisionTarjeta { get; set; }
    public long Mermas { get; set; }
    public long Gastos { get; set; }

    /// <summary>
    /// Impuesto contenido en las ventas del periodo. Es informativo: el precio
    /// ya lo incluye y no se resta de la utilidad.
    /// </summary>
    public long ImpuestoIncluido { get; set; }

    /// <summary>
    /// Cobros del periodo por método. Una venta mixta suma su parte en efectivo
    /// y su parte con tarjeta; el fiado se reporta como cuenta por cobrar.
    /// </summary>
    public List<DesgloseMetodoPago> DesglosePagos { get; set; } = new();

    /// <summary>Ventas del periodo agrupadas por producto (sin anuladas).</summary>
    public List<ReporteProducto> Productos { get; set; } = new();

    /// <summary>Ventas del periodo agrupadas por categoría actual del producto.</summary>
    public List<ReporteCategoria> Categorias { get; set; } = new();

    public long UtilidadBruta => VentasTotal - CostoVentas - ComisionTarjeta;
    public long UtilidadNeta => UtilidadBruta - Mermas - Gastos;
}

/// <summary>Cobros de un método de pago dentro del periodo, en centavos.</summary>
public sealed class DesgloseMetodoPago
{
    public string Metodo { get; set; } = string.Empty;
    public int NumeroVentas { get; set; }
    public long Monto { get; set; }
}


/// <summary>Ventas y utilidad de un producto en el periodo, en centavos.</summary>
public sealed class ReporteProducto
{
    public int ProductoId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public long Ventas { get; set; }
    public long Costo { get; set; }
    public long Utilidad => Ventas - Costo;
}

/// <summary>Ventas y utilidad de una categoría en el periodo, en centavos.</summary>
public sealed class ReporteCategoria
{
    public string Categoria { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public long Ventas { get; set; }
    public long Costo { get; set; }
    public long Utilidad => Ventas - Costo;
}
