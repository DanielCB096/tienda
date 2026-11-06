// Servicio local de respaldo y restauración (reglas de negocio #11 y #12).
// 100% offline: el respaldo es una copia del archivo .db en la carpeta
// "respaldos" junto a la base de datos. Sin nube ni servicios externos.
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Application.Services;

/// <summary>Datos de un archivo de respaldo.</summary>
public sealed class InfoRespaldo
{
    public string Ruta { get; init; } = string.Empty;
    public string NombreArchivo { get; init; } = string.Empty;
    public DateTime FechaCreacion { get; init; }
    public long TamanoBytes { get; init; }
    public int VersionEsquema { get; init; }
}

public class RespaldoService
{
    private readonly DatabaseTools _db;

    public RespaldoService(DatabaseTools db) => _db = db;

    /// <summary>Carpeta donde se guardan los respaldos.</summary>
    public string CarpetaRespaldos => _db.CarpetaRespaldos;

    /// <summary>
    /// Crea un respaldo manual de la base de datos y verifica su integridad.
    /// Devuelve la ruta del archivo creado.
    /// </summary>
    public ProcessResult<string> CrearRespaldo()
    {
        var resultado = _db.CrearRespaldoSeguro();
        return resultado.IsError
            ? ProcessResult<string>.Error(resultado.Message)
            : ProcessResult<string>.OK(
                resultado.Value, $"Respaldo creado: {Path.GetFileName(resultado.Value)}");
    }

    /// <summary>Verifica que un respaldo sea íntegro y tenga las tablas base.</summary>
    public ProcessResult<bool> VerificarRespaldo(string rutaRespaldo)
    {
        var resultado = DatabaseTools.VerificarArchivoRespaldo(rutaRespaldo);
        return resultado.IsError
            ? ProcessResult<bool>.Error(resultado.Message)
            : ProcessResult<bool>.OK(true, "El respaldo está íntegro");
    }

    /// <summary>
    /// Restaura la base de datos desde un respaldo. El respaldo se verifica
    /// antes de reemplazar nada: si no es válido, la base actual queda intacta.
    /// </summary>
    public ProcessResult<string> RestaurarRespaldo(string rutaRespaldo)
    {
        var resultado = _db.RestaurarDesdeRespaldo(rutaRespaldo);
        return resultado.IsError
            ? ProcessResult<string>.Error(resultado.Message)
            : ProcessResult<string>.OK(
                resultado.Value, "Base de datos restaurada desde el respaldo");
    }

    /// <summary>Respaldos disponibles, del más reciente al más antiguo.</summary>
    public List<InfoRespaldo> ListarRespaldos()
    {
        var lista = new List<InfoRespaldo>();
        if (!Directory.Exists(CarpetaRespaldos))
            return lista;

        foreach (var archivo in Directory.GetFiles(CarpetaRespaldos, "tienda-*.db"))
        {
            var info = new FileInfo(archivo);
            lista.Add(new InfoRespaldo
            {
                Ruta = archivo,
                NombreArchivo = info.Name,
                FechaCreacion = info.CreationTime,
                TamanoBytes = info.Length,
                VersionEsquema = LeerVersionEsquema(archivo)
            });
        }

        return lista.OrderByDescending(r => r.FechaCreacion).ToList();
    }

    /// <summary>El respaldo más reciente, o null si no hay ninguno.</summary>
    public InfoRespaldo ObtenerUltimoRespaldo() => ListarRespaldos().FirstOrDefault();

    /// <summary>
    /// Fecha del último respaldo, o null si no hay ninguno. Sirve para avisar
    /// en la pantalla principal cuando pasaron más días de lo configurado.
    /// </summary>
    public DateTime? ObtenerFechaUltimoRespaldo() => ObtenerUltimoRespaldo()?.FechaCreacion;

    private static int LeerVersionEsquema(string rutaRespaldo)
    {
        try
        {
            var db = new DatabaseTools(rutaRespaldo);
            return db.ObtenerVersionEsquema();
        }
        catch
        {
            return 0;
        }
    }
}
