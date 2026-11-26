using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;

namespace SistemaTienda.Maui.Services;

/// <summary>
/// Selector de archivos de respaldo (.db / .db.stb). En Windows pide la ruta en
/// un cuadro de texto: los selectores de WinRT y MAUI fallan con 0x80004005 en
/// esta app sin empaquetar. En Android usa el selector del sistema.
/// </summary>
public static class SelectorArchivoRespaldo
{
    private static readonly FilePickerFileType TiposRespaldo = new(
        new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.WinUI] = new[] { ".db", ".stb" },
            [DevicePlatform.Android] = new[]
            {
                "application/octet-stream",
                "application/x-sqlite3",
                "application/vnd.sqlite3",
                "*/*"
            },
            [DevicePlatform.iOS] = new[] { "public.database" },
            [DevicePlatform.MacCatalyst] = new[] { "public.database" }
        });

    public static async Task<string?> ElegirAsync(Page page)
    {
#if WINDOWS
        var ruta = await page.DisplayPromptAsync("Ruta del respaldo",
            "Pega la ruta completa del archivo .db o .db.stb. Puedes copiarla desde el Explorador con Shift + clic derecho → Copiar como ruta.",
            accept: "Continuar", cancel: "Cancelar",
            placeholder: @"C:\Users\...\respaldo-posale.db", maxLength: 500);
        return string.IsNullOrWhiteSpace(ruta) ? null : ruta.Trim().Trim('"');
#else
        var archivo = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Selecciona el respaldo de POSale",
            FileTypes = TiposRespaldo
        });
        return archivo?.FullPath;
#endif
    }

}
