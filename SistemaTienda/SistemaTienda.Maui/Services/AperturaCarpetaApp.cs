namespace SistemaTienda.Maui.Services;

/// <summary>
/// Abre una carpeta en el explorador del sistema.
/// Solo Windows la expone al usuario; en el resto de plataformas la carpeta de
/// respaldos es privada de la app y no tiene sentido abrirla.
/// </summary>
public static class AperturaCarpetaApp
{
    public static Task AbrirAsync(string ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta))
            throw new ArgumentException("La ruta de la carpeta está vacía.", nameof(ruta));

        if (!Directory.Exists(ruta))
            throw new DirectoryNotFoundException($"No existe la carpeta: {ruta}");

#if WINDOWS
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{ruta}\"",
            UseShellExecute = true
        });

        return Task.CompletedTask;
#else
        throw new PlatformNotSupportedException(
            "Abrir la carpeta solo está disponible en Windows. " +
            "Usa «Exportar respaldo» para sacar el archivo del dispositivo.");
#endif
    }
}
