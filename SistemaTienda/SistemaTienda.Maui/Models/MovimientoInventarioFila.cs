using SistemaTienda.Core;

namespace SistemaTienda.Maui.Models;

public sealed class MovimientoInventarioFila
{
    public MovimientoInventario Movimiento { get; init; } = null!;
    public string Tipo => Movimiento.Tipo;
    public string Fecha => Formato.FechaHora(Movimiento.Fecha);
    public string CantidadTexto => Movimiento.Cantidad > 0
        ? $"+{Movimiento.Cantidad}"
        : Movimiento.Cantidad.ToString();
    public Color CantidadColor => Movimiento.Cantidad >= 0
        ? Color.FromArgb("#2E6B4F")
        : Color.FromArgb("#B3261E");
    public string Detalle => $"{Movimiento.Motivo} · {Movimiento.Usuario}";
    public string CostoTexto => Movimiento.CostoUnitario > 0
        ? $"Costo unitario: {Formato.Moneda(Movimiento.CostoUnitario)}"
        : string.Empty;
}
