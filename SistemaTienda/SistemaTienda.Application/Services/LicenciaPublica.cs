using System.Reflection;

namespace SistemaTienda.Application.Services;

/// <summary>
/// Llave pública que valida las licencias emitidas. Es pública a propósito:
/// solo sirve para verificar, nunca para firmar. La llave privada se guarda
/// fuera del repositorio, en la máquina del proveedor.
/// </summary>
internal static class LicenciaPublica
{
    public static string Pem { get; } = Leer();

    private static string Leer()
    {
        var assembly = typeof(LicenciaPublica).Assembly;
        using var stream = assembly.GetManifestResourceStream(
            "SistemaTienda.Application.licencia-publica.pem");

        if (stream == null)
            return string.Empty;

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
