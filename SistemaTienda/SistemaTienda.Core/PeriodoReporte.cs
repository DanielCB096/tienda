namespace SistemaTienda.Core;

/// <summary>
/// Periodo de un reporte con fechas sin hora. La validación del rango la hace
/// el servicio; aquí solo se calculan los límites que usa la interfaz.
/// </summary>
public sealed record PeriodoReporte(DateTime Desde, DateTime Hasta)
{
    public static PeriodoReporte Hoy(DateTime referencia) =>
        new(referencia.Date, referencia.Date);

    public static PeriodoReporte MesActual(DateTime referencia) =>
        new(new DateTime(referencia.Year, referencia.Month, 1), referencia.Date);

    public static PeriodoReporte Entre(DateTime desde, DateTime hasta) =>
        new(desde.Date, hasta.Date);

    /// <summary>
    /// Bloque inmediatamente anterior de la misma duración. Por ejemplo, del 1 al 7
    /// de noviembre (7 días) devuelve del 25 al 31 de octubre.
    /// </summary>
    public PeriodoReporte Anterior()
    {
        var inicio = Desde.Date;
        var fin = Hasta.Date;
        var dias = (fin - inicio).Days + 1;
        if (dias < 1)
            dias = 1;

        return new PeriodoReporte(inicio.AddDays(-dias), inicio.AddDays(-1));
    }
}
