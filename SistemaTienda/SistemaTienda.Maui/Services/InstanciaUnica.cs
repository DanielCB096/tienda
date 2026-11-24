using System.Security.Cryptography;
using System.Text;

namespace SistemaTienda.Maui.Services;

/// <summary>
/// Evita dos instancias de la app sobre la misma base en Windows.
/// Android ya ejecuta una sola instancia de la actividad principal.
/// </summary>
public static class InstanciaUnica
{
#if WINDOWS
    private static Mutex? _mutex;
    private static bool _creada;
#endif

    public static bool Adquirir(string rutaBaseDatos)
    {
#if WINDOWS
        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(rutaBaseDatos))));

        _mutex = new Mutex(
            initiallyOwned: true,
            name: $"SistemaTienda-{hash[..16]}",
            createdNew: out _creada);

        return _creada;
#else
        _ = rutaBaseDatos;
        return true;
#endif
    }

    public static void Liberar()
    {
#if WINDOWS
        if (_mutex == null)
            return;

        if (_creada)
            _mutex.ReleaseMutex();

        _mutex.Dispose();
        _mutex = null;
        _creada = false;
#endif
    }
}
