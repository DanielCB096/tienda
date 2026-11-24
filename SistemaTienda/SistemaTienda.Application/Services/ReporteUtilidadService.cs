using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Application.Services;

/// <summary>
/// Calcula la utilidad del periodo a partir de las ventas, sus costos congelados,
/// la comisión de tarjeta, las mermas y los gastos.
/// </summary>
public class ReporteUtilidadService
{
    private readonly DatabaseTools _db;

    public ReporteUtilidadService(DatabaseTools db) => _db = db;

    public ProcessResult<ReporteUtilidad> Obtener(DateTime desde, DateTime hasta)
    {
        var autorizacion = AutorizacionService.Exigir(SesionActual.Usuario, PermisoSistema.VerCostos);
        if (autorizacion.IsError)
            return ProcessResult<ReporteUtilidad>.Error(autorizacion.Message);

        if (hasta.Date < desde.Date)
            return ProcessResult<ReporteUtilidad>.Error("La fecha final no puede ser anterior a la inicial");

        return ProcessResult<ReporteUtilidad>.OK(_db.ObtenerReporteUtilidad(desde, hasta));
    }

    /// <summary>
    /// Compara el periodo indicado contra el bloque inmediatamente anterior de la
    /// misma duración. Sirve para saber si las ventas o la utilidad subieron o bajaron.
    /// </summary>
    public ProcessResult<ComparacionReporte> ObtenerComparacion(DateTime desde, DateTime hasta)
    {
        var autorizacion = AutorizacionService.Exigir(SesionActual.Usuario, PermisoSistema.VerCostos);
        if (autorizacion.IsError)
            return ProcessResult<ComparacionReporte>.Error(autorizacion.Message);

        if (hasta.Date < desde.Date)
            return ProcessResult<ComparacionReporte>.Error("La fecha final no puede ser anterior a la inicial");

        var periodo = PeriodoReporte.Entre(desde, hasta);
        var anterior = periodo.Anterior();

        return ProcessResult<ComparacionReporte>.OK(new ComparacionReporte
        {
            Actual = _db.ObtenerReporteUtilidad(periodo.Desde, periodo.Hasta),
            Anterior = _db.ObtenerReporteUtilidad(anterior.Desde, anterior.Hasta)
        });
    }
}
