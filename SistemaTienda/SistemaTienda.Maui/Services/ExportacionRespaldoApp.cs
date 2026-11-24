using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;
using SistemaTienda.Application.Services;

namespace SistemaTienda.Maui.Services;

/// <summary>
/// Exporta un respaldo o paquete de soporte fuera del almacenamiento privado.
/// En Windows copia a Documentos\SistemaTienda y abre la carpeta; en Android
/// usa el diálogo de compartir del sistema.
/// </summary>
public static class ExportacionRespaldoApp
{
    public static async Task<string> ExportarAsync(string ruta, string titulo, string mime)
    {
        if (!File.Exists(ruta))
            throw new FileNotFoundException("No existe el archivo a exportar.", ruta);

        var nombre = Path.GetFileName(ruta);

#if WINDOWS
        var perfil = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var carpeta = CarpetaExportacion.Elegir(new[]
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "SistemaTienda"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "SistemaTienda"),
            Path.Combine(perfil, "Downloads", "SistemaTienda")
        });

        var destino = Path.Combine(carpeta, nombre);
        File.Copy(ruta, destino, overwrite: true);

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{carpeta}\"",
                UseShellExecute = true
            });
        }
        catch (Exception)
        {
            // Si no se puede abrir el explorador, la ruta sigue siendo válida.
        }

        return await Task.FromResult(destino);
#else
        var destino = Path.Combine(FileSystem.CacheDirectory, nombre);
        if (!CarpetaExportacion.EsMismaRuta(ruta, destino))
            File.Copy(ruta, destino, overwrite: true);

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = titulo,
            File = new ShareFile(destino, mime)
        });

        return destino;
#endif
    }
}
