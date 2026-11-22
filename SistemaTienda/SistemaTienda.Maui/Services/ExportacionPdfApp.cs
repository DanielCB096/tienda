using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;

namespace SistemaTienda.Maui.Services;

/// <summary>Comparte un PDF generado localmente con el diálogo del sistema.</summary>
public static class ExportacionPdfApp
{
    public static async Task CompartirAsync(string nombreArchivo, byte[] contenido)
    {
        var ruta = Path.Combine(FileSystem.CacheDirectory, nombreArchivo);
        await File.WriteAllBytesAsync(ruta, contenido);

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Exportar PDF",
            File = new ShareFile(ruta, "application/pdf")
        });
    }
}
