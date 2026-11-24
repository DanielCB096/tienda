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

    /// <summary>
    /// Respaldo manual con permiso de configuración. El respaldo automático
    /// usa <see cref="CrearRespaldo"/> porque se ejecuta antes de iniciar sesión.
    /// </summary>
    public ProcessResult<string> CrearRespaldoManual(string etiqueta = "manual",
        string claveRespaldo = null)
    {
        var autorizacion = AutorizacionService.Exigir(SesionActual.Usuario, PermisoSistema.Configurar);
        if (autorizacion.IsError)
            return ProcessResult<string>.Error(autorizacion.Message);

        return CrearRespaldo(etiqueta, claveRespaldo);
    }

    public ProcessResult<string> CrearRespaldo(string etiqueta = "manual",
        string claveRespaldo = null)
    {
        try
        {
            var ruta = RespaldoLocal.Crear(_db.RutaBaseDatos, _db.DirectorioRespaldos, etiqueta);
            var cifrado = false;

            if (!string.IsNullOrWhiteSpace(claveRespaldo))
            {
                var datos = File.ReadAllBytes(ruta);
                var archivoCifrado = CifradoRespaldo.Cifrar(datos, claveRespaldo);
                var destino = ruta + CifradoRespaldo.Extension;
                File.WriteAllBytes(destino, archivoCifrado);

                // Verificar el archivo cifrado antes de borrar la copia en claro.
                var comprobacion = CifradoRespaldo.Descifrar(
                    File.ReadAllBytes(destino), claveRespaldo);
                if (!comprobacion.SequenceEqual(datos))
                    throw new InvalidDataException("El respaldo cifrado no coincide con el original");

                File.Delete(ruta);
                ruta = destino;
                cifrado = true;
            }

            _bitacora.Registrar(AccionBitacora.Respaldo, "Respaldo", Path.GetFileName(ruta),
                datosDespues: $"Ruta={ruta};Etiqueta={etiqueta};Cifrado={cifrado}",
                motivo: $"Respaldo {etiqueta}");
            return ProcessResult<string>.OK(ruta, cifrado
                ? "Respaldo cifrado creado y verificado"
                : "Respaldo creado y verificado");
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
        DateTime? ahora = null, string claveRespaldo = null)
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

            var creado = CrearRespaldo("automatico", claveRespaldo);
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
    public ProcessResult Restaurar(string rutaRespaldo, bool confirmado,
        string claveRespaldo = null)
    {
        if (!confirmado)
            return ProcessResult.Error("La restauración requiere confirmación explícita");

        if (string.IsNullOrWhiteSpace(rutaRespaldo))
            return ProcessResult.Error("Indique la ruta del respaldo a restaurar");

        var autorizacion = AutorizacionService.Exigir(SesionActual.Usuario, PermisoSistema.Configurar);
        if (autorizacion.IsError)
            return ProcessResult.Error(autorizacion.Message);

        string temporal = null;
        try
        {
            var ruta = rutaRespaldo;

            if (rutaRespaldo.EndsWith(CifradoRespaldo.Extension, StringComparison.OrdinalIgnoreCase))
            {
                if (!CifradoRespaldo.ClaveValida(claveRespaldo))
                    return ProcessResult.Error(
                        "Este respaldo está cifrado. Escribe la clave de respaldo de 24 caracteres.");

                var datos = CifradoRespaldo.Descifrar(
                    File.ReadAllBytes(rutaRespaldo), claveRespaldo);

                temporal = Path.Combine(Path.GetTempPath(),
                    $"restaurar-{Guid.NewGuid():N}.db");
                File.WriteAllBytes(temporal, datos);
                ruta = temporal;
            }

            var respaldoSeguridad = RespaldoLocal.Restaurar(
                ruta, _db.RutaBaseDatos, _db.DirectorioRespaldos);

            return ProcessResult.Success(string.IsNullOrEmpty(respaldoSeguridad)
                ? "Respaldo restaurado. Reinicie la aplicación."
                : $"Respaldo restaurado. Respaldo previo guardado en: {respaldoSeguridad}. Reinicie la aplicación.");
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return ProcessResult.Error(
                "La clave no coincide con el respaldo o el archivo fue alterado");
        }
        catch (Exception ex)
        {
            return ProcessResult.Error($"No se pudo restaurar el respaldo: {ex.Message}");
        }
        finally
        {
            if (temporal != null)
            {
                try { File.Delete(temporal); } catch { }
            }
        }
    }
}
