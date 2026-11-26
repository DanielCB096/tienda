using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;

namespace SistemaTienda.Maui.Services;

/// <summary>
/// Selector de archivos de respaldo (.db / .db.stb). En Windows usa un explorador
/// propio porque los selectores de WinRT y MAUI fallan con 0x80004005 en esta app
/// sin empaquetar. En Android usa el selector del sistema.
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
        var selector = new Pages.SelectorRespaldoPage();
        await page.Navigation.PushModalAsync(selector);
        return await selector.Resultado;
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
