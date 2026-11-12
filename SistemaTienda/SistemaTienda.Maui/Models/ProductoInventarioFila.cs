using SistemaTienda.Core;

namespace SistemaTienda.Maui.Models;

public sealed class ProductoInventarioFila
{
    public Producto Producto { get; init; } = null!;
    public string Nombre => Producto.Nombre;
    public string Detalle => string.IsNullOrWhiteSpace(Producto.Categoria)
        ? $"Código {Producto.CodigoBarras}"
        : $"{Producto.CodigoBarras} · {Producto.Categoria}";
    public string Precio => Formato.Moneda(Producto.Precio);
    public bool StockBajo => Producto.Stock <= Producto.StockMinimo;
    public string StockTexto => $"Stock {Producto.Stock} · mínimo {Producto.StockMinimo}";
    public Color StockColor => StockBajo
        ? Color.FromArgb("#B3261E")
        : Color.FromArgb("#2E6B4F");
}
