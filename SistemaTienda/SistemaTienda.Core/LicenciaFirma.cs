using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SistemaTienda.Core;

/// <summary>
/// Firma y verifica licencias offline con RSA-PSS/SHA-256. La llave privada
/// vive solo en la máquina del proveedor; la app lleva únicamente la pública.
/// </summary>
public static class LicenciaFirma
{
    public const string Producto = "POSale";

    private static readonly JsonSerializerOptions OpcionesPayload = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private static readonly JsonSerializerOptions OpcionesArchivo = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static string Crear(Licencia licencia, string clavePrivadaPem)
    {
        ArgumentNullException.ThrowIfNull(licencia);
        Validar(licencia);

        using var rsa = RSA.Create();
        rsa.ImportFromPem(clavePrivadaPem);

        var payload = JsonSerializer.SerializeToUtf8Bytes(licencia, OpcionesPayload);
        var firma = rsa.SignData(payload, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pss);

        return JsonSerializer.Serialize(new LicenciaArchivo
        {
            Payload = Convert.ToBase64String(payload),
            Firma = Convert.ToBase64String(firma)
        }, OpcionesArchivo);
    }

    public static bool LeerYVerificar(string contenido, string clavePublicaPem,
        out Licencia licencia, out string error)
    {
        licencia = null;
        error = string.Empty;

        try
        {
            var archivo = JsonSerializer.Deserialize<LicenciaArchivo>(
                contenido, OpcionesArchivo);
            if (archivo == null ||
                string.IsNullOrWhiteSpace(archivo.Payload) ||
                string.IsNullOrWhiteSpace(archivo.Firma))
            {
                error = "El archivo de licencia está incompleto.";
                return false;
            }

            var payload = Convert.FromBase64String(archivo.Payload);
            var firma = Convert.FromBase64String(archivo.Firma);

            using var rsa = RSA.Create();
            rsa.ImportFromPem(clavePublicaPem);
            if (!rsa.VerifyData(payload, firma, HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pss))
            {
                error = "La firma de la licencia no es válida.";
                return false;
            }

            var leida = JsonSerializer.Deserialize<Licencia>(payload, OpcionesPayload);
            if (leida == null)
            {
                error = "El contenido de la licencia no se pudo leer.";
                return false;
            }

            if (!string.Equals(leida.Producto, Producto, StringComparison.Ordinal))
            {
                error = $"La licencia es para \"{leida.Producto}\", no para {Producto}.";
                return false;
            }

            if (leida.Cajas <= 0)
            {
                error = "La licencia debe permitir al menos una caja.";
                return false;
            }

            leida.Cliente = leida.Cliente.Trim();
            leida.LicenciaId = leida.LicenciaId.Trim();
            if (leida.Cliente.Length == 0 || leida.LicenciaId.Length == 0)
            {
                error = "La licencia no tiene cliente o folio.";
                return false;
            }

            licencia = leida;
            return true;
        }
        catch (JsonException)
        {
            error = "El archivo no tiene el formato de licencia de POSale.";
            return false;
        }
        catch (FormatException)
        {
            error = "El archivo de licencia está dañado.";
            return false;
        }
        catch (CryptographicException)
        {
            error = "No se pudo validar la firma de la licencia.";
            return false;
        }
    }

    private static void Validar(Licencia licencia)
    {
        if (string.IsNullOrWhiteSpace(licencia.Cliente))
            throw new ArgumentException("La licencia requiere un cliente", nameof(licencia));

        if (string.IsNullOrWhiteSpace(licencia.LicenciaId))
            throw new ArgumentException("La licencia requiere un folio", nameof(licencia));

        if (licencia.Cajas <= 0)
            throw new ArgumentException("La licencia debe permitir al menos una caja", nameof(licencia));

        licencia.Producto = Producto;
        licencia.Cliente = licencia.Cliente.Trim();
        licencia.LicenciaId = licencia.LicenciaId.Trim();
        licencia.Tipo = string.IsNullOrWhiteSpace(licencia.Tipo) ? "Perpetua" : licencia.Tipo.Trim();
    }
}
