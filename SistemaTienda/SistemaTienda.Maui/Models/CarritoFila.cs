using SistemaTienda.Core;

namespace SistemaTienda.Maui.Models;

public sealed class CarritoFila
{
    public Producto Producto { get; init; } = null!;
    public int Cantidad { get; set; }

    public string Nombre => Producto.Nombre;
    public string PrecioUnitario => Formato.Moneda(Producto.Precio);
    public string Importe => Formato.Moneda(Dinero.Multiplicar(Producto.Precio, Cantidad));
    public string CantidadTexto => Cantidad.ToString();
}
