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
        var autorizacion = AutorizacionService.Exigir(SesionActual.Usuario, PermisoSistema.Consultar);
        if (autorizacion.IsError)
            return ProcessResult<ReporteUtilidad>.Error(autorizacion.Message);

        if (hasta.Date < desde.Date)
            return ProcessResult<ReporteUtilidad>.Error("La fecha final no puede ser anterior a la inicial");

        return ProcessResult<ReporteUtilidad>.OK(_db.ObtenerReporteUtilidad(desde, hasta));
    }
}
