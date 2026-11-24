using Microsoft.Maui.Storage;

namespace SistemaTienda.Maui.Services;

/// <summary>
/// Guarda los errores no controlados en un archivo dentro de los datos de la app
/// y evita que un fallo de la interfaz cierre la caja sin dejar rastro.
/// </summary>
public static class RegistroErrores
{
    private static readonly object Candado = new();

    public static string Archivo
    {
        get
        {
            var carpeta = Path.Combine(FileSystem.AppDataDirectory, "logs");
            Directory.CreateDirectory(carpeta);
            return Path.Combine(carpeta, "errores.log");
        }
    }

    public static void Registrar(Exception excepcion, string origen)
    {
        if (excepcion == null)
            return;

        try
        {
            var linea = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {origen}: " +
                        $"{excepcion.GetType().Name}: {excepcion.Message}" +
                        Environment.NewLine + excepcion.StackTrace +
                        Environment.NewLine + new string('-', 60) + Environment.NewLine;

            lock (Candado)
            {
                File.AppendAllText(Archivo, linea);
            }
        }
        catch
        {
            // Si no se puede escribir el log, no se debe provocar otro fallo.
        }
    }
}
