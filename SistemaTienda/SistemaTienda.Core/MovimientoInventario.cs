namespace SistemaTienda.Core;

/// <summary>
/// Movimiento de inventario con cantidad firmada: positiva para entradas,
/// negativa para salidas y ajustes. El stock de un producto es la suma de sus movimientos.
/// </summary>
public class MovimientoInventario
{
    public int Id { get; set; }
    public int ProductoId { get; set; }
    public string Tipo { get; set; } = MovimientoTipo.Entrada;
    public int Cantidad { get; set; }
    public long CostoUnitario { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public string Usuario { get; set; } = string.Empty;
    public DateTime Fecha { get; set; } = DateTime.Now;
}

public static class MovimientoTipo
{
    public const string Entrada = "ENTRADA";
    public const string Salida = "SALIDA";
    public const string Ajuste = "AJUSTE";
}

public class GastoPorConcepto
{
    public string Concepto { get; set; } = string.Empty;
    public long Total { get; set; }
}


/// <summary>
/// Producto cuyo stock en caché no coincide con la suma de sus movimientos.
/// El inventario se reconcilia con este dato al abrir la base y en soporte.
/// </summary>
public sealed class DescuadreStock
{
    public int ProductoId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Stock { get; set; }
    public int Movimientos { get; set; }
}
