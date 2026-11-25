// Paquete de diagnóstico para soporte: resumen, versión y base opcional.
using System.IO.Compression;
using System.Text;
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Application.Services;

/// <summary>
/// Genera un ZIP con la información mínima para diagnosticar una instalación.
/// La base de datos es opcional y contiene datos del negocio: el usuario debe
/// confirmar antes de incluirla y compartirla por un canal seguro.
/// </summary>
public sealed class PaqueteSoporteService
{
    private readonly DatabaseTools _db;

    public PaqueteSoporteService(DatabaseTools db)
    {
        _db = db;
    }

    public ProcessResult<string> Crear(string rutaSalida, bool incluirBaseDatos = false,
        DateTime? ahora = null, string claveRespaldo = null)
    {
        if (string.IsNullOrWhiteSpace(rutaSalida))
            return ProcessResult<string>.Error("Indique la ruta del paquete de soporte");

        var rutaZip = Path.GetFullPath(rutaSalida);
        var directorioSalida = Path.GetDirectoryName(rutaZip);
        if (string.IsNullOrEmpty(directorioSalida))
            return ProcessResult<string>.Error("La ruta del paquete no es válida");

        var temporal = Path.Combine(Path.GetTempPath(),
            "sistematienda-soporte-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directorioSalida);
            Directory.CreateDirectory(temporal);

            var resumen = new StringBuilder();
            resumen.AppendLine("SistemaTienda - Paquete de soporte");
            resumen.AppendLine($"Generado: {(ahora ?? DateTime.Now):yyyy-MM-dd HH:mm:ss}");
            resumen.AppendLine($"Versión de la app: {typeof(PaqueteSoporteService).Assembly.GetName().Version}");
            resumen.AppendLine($"Sistema: {Environment.OSVersion}");
            resumen.AppendLine($"Runtime: {Environment.Version}");
            resumen.AppendLine($"Ruta de la base: {_db.RutaBaseDatos}");
            resumen.AppendLine($"Versión del esquema: {_db.ObtenerVersionEsquema()}");
            resumen.AppendLine($"Tamaño de la base: {new FileInfo(_db.RutaBaseDatos).Length} bytes");
            resumen.AppendLine($"Directorio de respaldos: {_db.DirectorioRespaldos}");
            resumen.AppendLine($"Último respaldo: {RespaldoLocal.Ultimo(_db.DirectorioRespaldos)?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Sin respaldos"}");
            resumen.AppendLine($"Base incluida: {(incluirBaseDatos ? "Sí" : "No")}");
            resumen.AppendLine($"Cifrado: {(string.IsNullOrWhiteSpace(claveRespaldo) ? "No" : "Sí")}");
            resumen.AppendLine();
            resumen.AppendLine("Este paquete puede contener información del negocio. Compártelo solo por un canal seguro.");

            File.WriteAllText(Path.Combine(temporal, "resumen.txt"), resumen.ToString(),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            File.WriteAllText(Path.Combine(temporal, "LEEME.txt"),
                "Paquete de diagnóstico de SistemaTienda.\r\n" +
                "Incluye versión, esquema y, si se autorizó, una copia consistente de la base.\r\n" +
                "No lo envíes por canales públicos. Contiene datos del negocio.\r\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            if (incluirBaseDatos)
                IncluirBaseConsistente(temporal);

            if (File.Exists(rutaZip))
                File.Delete(rutaZip);

            ZipFile.CreateFromDirectory(temporal, rutaZip, CompressionLevel.Optimal, false);

            if (!string.IsNullOrWhiteSpace(claveRespaldo))
            {
                var datos = File.ReadAllBytes(rutaZip);
                var archivoCifrado = CifradoRespaldo.Cifrar(datos, claveRespaldo);
                var destino = rutaZip + CifradoRespaldo.Extension;
                File.WriteAllBytes(destino, archivoCifrado);

                var comprobacion = CifradoRespaldo.Descifrar(
                    File.ReadAllBytes(destino), claveRespaldo);
                if (!comprobacion.SequenceEqual(datos))
                    throw new InvalidDataException("El paquete cifrado no coincide con el original");

                File.Delete(rutaZip);
                rutaZip = destino;
            }

            return ProcessResult<string>.OK(rutaZip, string.IsNullOrWhiteSpace(claveRespaldo)
                ? "Paquete de soporte creado"
                : "Paquete de soporte cifrado creado");
        }
        catch (Exception ex)
        {
            return ProcessResult<string>.Error($"No se pudo crear el paquete de soporte: {ex.Message}");
        }
        finally
        {
            try
            {
                if (Directory.Exists(temporal))
                    Directory.Delete(temporal, recursive: true);
            }
            catch (IOException)
            {
                // Un archivo temporal bloqueado no debe ocultar el resultado principal.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private void IncluirBaseConsistente(string temporal)
    {
        var respaldosTemp = Path.Combine(temporal, "respaldos");
        var respaldo = RespaldoLocal.Crear(_db.RutaBaseDatos, respaldosTemp, "soporte");
        var destino = Path.Combine(temporal, "datos.db");

        File.Move(respaldo, destino, overwrite: true);
        Directory.Delete(respaldosTemp, recursive: true);
    }
}
