namespace SistemaTienda.Core;

/// <summary>
/// Reglas para sugerir el precio de venta cuando cambia el costo. La sugerencia
/// conserva el margen en pesos por pieza: precio nuevo = costo nuevo + margen
/// anterior. No cambia el precio por sí sola; el dueño decide si la aplica.
/// </summary>
public static class PrecioVenta
{
    public static long ConservandoMargen(long precioActual, long costoActual, long costoNuevo)
    {
        var margen = precioActual - costoActual;
        var sugerido = costoNuevo + margen;
        return sugerido > 0 ? sugerido : 1;
    }
}
