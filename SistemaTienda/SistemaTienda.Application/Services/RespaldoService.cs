// Servicio de respaldo y restauración local
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Application.Services;

public class RespaldoService
{
    private readonly DatabaseTools _db;

    public RespaldoService(DatabaseTools db) => _db = db;

    public string DirectorioRespaldos => _db.DirectorioRespaldos;
    public string RutaBaseDatos => _db.RutaBaseDatos;

    public ProcessResult<string> CrearRespaldo(string etiqueta = "manual")
    {
        try
        {
            var ruta = RespaldoLocal.Crear(_db.RutaBaseDatos, _db.DirectorioRespaldos, etiqueta);
            return ProcessResult<string>.OK(ruta, "Respaldo creado y verificado");
        }
        catch (Exception ex)
        {
            return ProcessResult<string>.Error($"No se pudo crear el respaldo: {ex.Message}");
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
