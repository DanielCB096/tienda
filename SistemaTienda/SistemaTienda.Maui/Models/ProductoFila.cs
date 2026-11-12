using SistemaTienda.Core;

namespace SistemaTienda.Maui.Models;

public sealed class ProductoFila
{
    public Producto Producto { get; init; } = null!;
    public string Nombre => Producto.Nombre;
    public string Precio => Formato.Moneda(Producto.Precio);
    public string StockTexto => $"Stock {Producto.Stock}";
    public Color StockColor => Producto.Stock <= 0
        ? Color.FromArgb("#B3261E")
        : Color.FromArgb("#2E6B4F");
}
