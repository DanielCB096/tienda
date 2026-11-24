// Selección de una carpeta escribible para exportar respaldos fuera del almacenamiento privado.
using System.IO;

namespace SistemaTienda.Application.Services;

/// <summary>
/// Elige la primera carpeta de la lista donde realmente se puede crear un archivo.
/// En Windows, Documentos puede estar redirigido o bloqueado por OneDrive; en ese
/// caso se usa Escritorio o Descargas y se informa la ruta final al usuario.
/// </summary>
public static class CarpetaExportacion
{
    public static string Elegir(IEnumerable<string> candidatos)
    {
        var probadas = new List<string>();

        foreach (var candidato in candidatos)
        {
            if (string.IsNullOrWhiteSpace(candidato))
                continue;

            probadas.Add(candidato);

            try
            {
                Directory.CreateDirectory(candidato);

                var prueba = Path.Combine(candidato, $".escritura-{Guid.NewGuid():N}");
                using (File.Create(prueba))
                {
                }

                File.Delete(prueba);
                return candidato;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                or NotSupportedException or ArgumentException)
            {
                // Se prueba la siguiente ruta.
            }
        }

        throw new IOException(
            "No se encontró una carpeta escribible para exportar. Rutas probadas: " +
            string.Join(", ", probadas));
    }

    /// <summary>
    /// En Android el paquete de soporte ya vive en la caché y se comparte desde ahí.
    /// Copiarlo sobre sí mismo lanza una excepción de archivo en uso.
    /// </summary>
    public static bool EsMismaRuta(string origen, string destino)
    {
        if (string.IsNullOrWhiteSpace(origen) || string.IsNullOrWhiteSpace(destino))
            return false;

        var comparacion = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return string.Equals(
            Path.GetFullPath(origen),
            Path.GetFullPath(destino),
            comparacion);
    }
}
