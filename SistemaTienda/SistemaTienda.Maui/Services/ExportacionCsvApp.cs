using System.Text;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;

namespace SistemaTienda.Maui.Services;

/// <summary>
/// Escribe un CSV UTF-8 con BOM en la caché de la app y abre el diálogo del
/// sistema para compartirlo o guardarlo. La app sigue funcionando sin red.
/// </summary>
public static class ExportacionCsvApp
{
    public static async Task CompartirAsync(string nombreArchivo, string contenido)
    {
        var ruta = Path.Combine(FileSystem.CacheDirectory, nombreArchivo);
        await File.WriteAllTextAsync(ruta, contenido, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Exportar CSV",
            File = new ShareFile(ruta, "text/csv")
        });
    }
}
