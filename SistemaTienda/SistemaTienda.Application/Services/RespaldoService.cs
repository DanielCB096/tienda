// Servicio de respaldo y restauración local
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Application.Services;

public class RespaldoService
{
    private readonly DatabaseTools _db;
    private readonly BitacoraService _bitacora;

    public RespaldoService(DatabaseTools db)
    {
        _db = db;
        _bitacora = new BitacoraService(db);
    }

    public string DirectorioRespaldos => _db.DirectorioRespaldos;
    public string RutaBaseDatos => _db.RutaBaseDatos;

    public ProcessResult<string> CrearRespaldo(string etiqueta = "manual")
    {
        try
        {
            var ruta = RespaldoLocal.Crear(_db.RutaBaseDatos, _db.DirectorioRespaldos, etiqueta);
            _bitacora.Registrar(AccionBitacora.Respaldo, "Respaldo", Path.GetFileName(ruta),
                datosDespues: $"Ruta={ruta};Etiqueta={etiqueta}",
                motivo: $"Respaldo {etiqueta}");
            return ProcessResult<string>.OK(ruta, "Respaldo creado y verificado");
        }
        catch (Exception ex)
        {
            return ProcessResult<string>.Error($"No se pudo crear el respaldo: {ex.Message}");
        }
    }

    /// <summary>
    /// Crea un respaldo cuando se cumplió el intervalo configurado (0 = deshabilitado).
    /// Se ejecuta al iniciar y al cerrar la aplicación; cualquier respaldo previo cuenta.
    /// </summary>
    public ProcessResult<ResultadoRespaldoAutomatico> EjecutarAutomaticoSiCorresponde(
        DateTime? ahora = null)
    {
        try
        {
            var config = _db.ObtenerConfiguracion();
            if (config == null)
                return ProcessResult<ResultadoRespaldoAutomatico>.Error(
                    "No se encontró la configuración de la tienda");

            if (config.RespaldoIntervaloDias <= 0)
                return ProcessResult<ResultadoRespaldoAutomatico>.OK(new ResultadoRespaldoAutomatico
                {
                    Estado = EstadoRespaldoAutomatico.Deshabilitado,
                    UltimoRespaldo = RespaldoLocal.Ultimo(_db.DirectorioRespaldos)
                });

            var ultimo = RespaldoLocal.Ultimo(_db.DirectorioRespaldos);
            var referencia = (ahora ?? DateTime.Now).Date;
            var vencido = ultimo == null ||
                          (referencia - ultimo.Value.Date).TotalDays >= config.RespaldoIntervaloDias;

            if (!vencido)
                return ProcessResult<ResultadoRespaldoAutomatico>.OK(new ResultadoRespaldoAutomatico
                {
                    Estado = EstadoRespaldoAutomatico.Vigente,
                    UltimoRespaldo = ultimo
                });

            var creado = CrearRespaldo("automatico");
            if (creado.IsError)
                return ProcessResult<ResultadoRespaldoAutomatico>.Error(
                    $"El respaldo automático está vencido y no se pudo crear: {creado.Message}");

            return ProcessResult<ResultadoRespaldoAutomatico>.OK(new ResultadoRespaldoAutomatico
            {
                Estado = EstadoRespaldoAutomatico.Creado,
                Ruta = creado.Value,
                UltimoRespaldo = RespaldoLocal.Ultimo(_db.DirectorioRespaldos)
            }, "Respaldo automático creado y verificado");
        }
        catch (Exception ex)
        {
            return ProcessResult<ResultadoRespaldoAutomatico>.Error(
                $"No se pudo revisar el respaldo automático: {ex.Message}");
        }
    }

    public ProcessResult<List<ArchivoRespaldo>> ListarRespaldos()
    {
        try
        {
            return ProcessResult<List<ArchivoRespaldo>>.OK(
                RespaldoLocal.Listar(_db.DirectorioRespaldos).ToList());
        }
        catch (Exception ex)
        {
            return ProcessResult<List<ArchivoRespaldo>>.Error($"No se pudieron listar los respaldos: {ex.Message}");
        }
    }

    public ProcessResult<DateTime?> ObtenerUltimoRespaldo()
    {
        try
        {
            return ProcessResult<DateTime?>.OK(RespaldoLocal.Ultimo(_db.DirectorioRespaldos));
        }
        catch (Exception ex)
        {
            return ProcessResult<DateTime?>.Error($"No se pudo consultar el último respaldo: {ex.Message}");
        }
    }

    /// <summary>
    /// Reemplaza la base actual. Requiere confirmación explícita y reiniciar la aplicación.
    /// </summary>
    public ProcessResult Restaurar(string rutaRespaldo, bool confirmado)
    {
        if (!confirmado)
            return ProcessResult.Error("La restauración requiere confirmación explícita");

        if (string.IsNullOrWhiteSpace(rutaRespaldo))
            return ProcessResult.Error("Indique la ruta del respaldo a restaurar");

        try
        {
            var respaldoSeguridad = RespaldoLocal.Restaurar(
                rutaRespaldo, _db.RutaBaseDatos, _db.DirectorioRespaldos);

            return ProcessResult.Success(string.IsNullOrEmpty(respaldoSeguridad)
                ? "Respaldo restaurado. Reinicie la aplicación."
                : $"Respaldo restaurado. Respaldo previo guardado en: {respaldoSeguridad}. Reinicie la aplicación.");
        }
        catch (Exception ex)
        {
            return ProcessResult.Error($"No se pudo restaurar el respaldo: {ex.Message}");
        }
    }
}
