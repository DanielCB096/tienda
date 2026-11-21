using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;
using SistemaTienda.Application.Services;

namespace SistemaTienda.Maui.Services;

/// <summary>
/// Genera el PNG del QR y lo comparte con el diálogo del sistema.
/// El archivo vive en la caché de la app y no requiere red.
/// </summary>
public static class QrImagenApp
{
    public static async Task CompartirAsync(string contenidoQr)
    {
        var imagen = QrImagenService.Generar(contenidoQr);
        var nombre = $"qr-ticket-{DateTime.Now:yyyyMMdd-HHmmss}.png";
        var ruta = Path.Combine(FileSystem.CacheDirectory, nombre);
        await File.WriteAllBytesAsync(ruta, imagen.Png);

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Compartir QR del ticket",
            File = new ShareFile(ruta, "image/png")
        });
    }
}
