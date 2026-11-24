using Microsoft.Maui.Storage;
#if WINDOWS
using System.Security.Cryptography;
#endif

namespace SistemaTienda.Maui.Services;

/// <summary>
/// Guarda la clave de respaldo en el almacenamiento seguro del sistema: DPAPI
/// (por usuario de Windows) o SecureStorage en Android. La clave nunca se
/// escribe en la base de datos ni viaja dentro de los respaldos.
/// </summary>
public static class AlmacenClaveRespaldo
{
    private const string Nombre = "respaldo.clave";

#if WINDOWS
    private static string Archivo =>
        Path.Combine(FileSystem.AppDataDirectory, "respaldo.key");
#endif

    public static async Task<string?> LeerAsync()
    {
#if WINDOWS
        try
        {
            if (!File.Exists(Archivo))
                return null;

            var protegido = await File.ReadAllBytesAsync(Archivo);
            var datos = ProtectedData.Unprotect(
                protegido, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return System.Text.Encoding.UTF8.GetString(datos);
        }
        catch
        {
            return null;
        }
#else
        try
        {
            return await SecureStorage.Default.GetAsync(Nombre);
        }
        catch
        {
            return null;
        }
#endif
    }

    public static async Task GuardarAsync(string clave)
    {
        if (string.IsNullOrWhiteSpace(clave))
            throw new ArgumentException("La clave no puede estar vacía", nameof(clave));

#if WINDOWS
        var datos = System.Text.Encoding.UTF8.GetBytes(clave);
        var protegido = ProtectedData.Protect(
            datos, optionalEntropy: null, DataProtectionScope.CurrentUser);

        Directory.CreateDirectory(Path.GetDirectoryName(Archivo)!);
        await File.WriteAllBytesAsync(Archivo, protegido);
#else
        await SecureStorage.Default.SetAsync(Nombre, clave);
#endif
    }

    public static Task BorrarAsync()
    {
#if WINDOWS
        if (File.Exists(Archivo))
            File.Delete(Archivo);
#else
        SecureStorage.Default.Remove(Nombre);
#endif
        return Task.CompletedTask;
    }
}
