namespace SistemaTienda.Core;

/// <summary>
/// Reglas del descuento de una venta. El descuento se captura una sola vez
/// (porcentaje o monto) y se reparte entre las líneas en centavos enteros,
/// con el método del mayor residuo, para que el total de las líneas coincida
/// exactamente con el descuento cobrado.
/// </summary>
public static class DescuentoVenta
{
    /// <summary>Descuento máximo permitido: siempre queda un total mayor a cero.</summary>
    public const decimal PorcentajeMaximo = 100m;

    public static bool EsPorcentajeValido(decimal porcentaje) =>
        porcentaje > 0m && porcentaje < PorcentajeMaximo;

    public static bool EsMontoValido(long monto, long subtotal) =>
        monto > 0 && monto < subtotal;

    public static long DesdePorcentaje(long subtotal, decimal porcentaje)
    {
        if (!EsPorcentajeValido(porcentaje))
            throw new ArgumentOutOfRangeException(nameof(porcentaje),
                "El porcentaje debe ser mayor a cero y menor a 100");

        return Dinero.Porcentaje(subtotal, porcentaje);
    }

    /// <summary>
    /// Reparte <paramref name="descuento"/> entre las líneas de forma proporcional
    /// a su importe bruto. La suma del resultado es exactamente el descuento.
    /// </summary>
    public static IReadOnlyList<long> Repartir(long descuento, IReadOnlyList<long> importes)
    {
        if (importes == null || importes.Count == 0)
            throw new ArgumentException("La venta requiere al menos una línea", nameof(importes));

        if (descuento < 0)
            throw new ArgumentOutOfRangeException(nameof(descuento),
                "El descuento no puede ser negativo");

        var resultado = new long[importes.Count];
        if (descuento == 0)
            return resultado;

        long subtotal = 0;
        foreach (var importe in importes)
        {
            if (importe < 0)
                throw new ArgumentOutOfRangeException(nameof(importes),
                    "Los importes no pueden ser negativos");

            subtotal += importe;
        }

        if (subtotal <= 0)
            throw new ArgumentException("El subtotal debe ser mayor a cero", nameof(importes));

        if (descuento >= subtotal)
            throw new ArgumentOutOfRangeException(nameof(descuento),
                "El descuento debe ser menor al subtotal");

        var restos = new List<(int Indice, decimal Resto)>(importes.Count);
        long asignado = 0;

        for (var i = 0; i < importes.Count; i++)
        {
            var exacto = descuento * (decimal)importes[i] / subtotal;
            var baseLinea = (long)Math.Floor(exacto);
            resultado[i] = baseLinea;
            asignado += baseLinea;
            restos.Add((i, exacto - baseLinea));
        }

        var sobrante = descuento - asignado;
        foreach (var (indice, _) in restos
                     .OrderByDescending(r => r.Resto)
                     .ThenBy(r => r.Indice))
        {
            if (sobrante == 0)
                break;

            if (resultado[indice] < importes[indice])
            {
                resultado[indice]++;
                sobrante--;
            }
        }

        // En casos extremos (líneas de un centavo) se completa en orden.
        for (var i = 0; i < resultado.Length && sobrante > 0; i++)
        {
            while (sobrante > 0 && resultado[i] < importes[i])
            {
                resultado[i]++;
                sobrante--;
            }
        }

        return resultado;
    }
}
