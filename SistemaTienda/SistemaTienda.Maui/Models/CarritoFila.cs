using SistemaTienda.Core;

namespace SistemaTienda.Maui.Models;

public sealed class CarritoFila
{
    public Producto Producto { get; init; } = null!;
    public int Cantidad { get; set; }

    /// <summary>Descuento capturado para este renglón: porcentaje o monto, nunca ambos.</summary>
    public decimal DescuentoPorcentaje { get; set; }
    public long DescuentoMonto { get; set; }

    public string Nombre => Producto.Nombre;
    public string PrecioUnitario => Formato.Moneda(Producto.Precio);

    public long SubtotalLinea => Dinero.Multiplicar(Producto.Precio, Cantidad);

    public long DescuentoTotal => DescuentoPorcentaje > 0
        ? (DescuentoVenta.EsPorcentajeValido(DescuentoPorcentaje)
            ? Dinero.Porcentaje(SubtotalLinea, DescuentoPorcentaje)
            : 0)
        : (DescuentoMonto > 0 && DescuentoMonto < SubtotalLinea ? DescuentoMonto : 0);

    public long ImporteNeto => SubtotalLinea - DescuentoTotal;

    public string Importe => Formato.Moneda(ImporteNeto);
    public string CantidadTexto => Cantidad.ToString();

    public bool TieneDescuento => DescuentoTotal > 0;

    public string DescuentoTexto
    {
        get
        {
            if (!TieneDescuento)
                return string.Empty;

            var etiqueta = DescuentoPorcentaje > 0
                ? $"Descuento {DescuentoPorcentaje.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}%"
                : "Descuento";
            return $"{etiqueta}: -{Formato.Moneda(DescuentoTotal)}";
        }
    }
}
