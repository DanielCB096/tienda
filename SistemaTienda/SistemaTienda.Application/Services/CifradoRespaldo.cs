using System.Security.Cryptography;
using System.Text;

namespace SistemaTienda.Application.Services;

/// <summary>
/// Cifra y descifra archivos de respaldo con AES-256-GCM. La clave de 32 bytes
/// se deriva con PBKDF2-SHA256 a partir de una clave legible de 24 caracteres
/// (120 bits, Base32 de Crockford) y una sal aleatoria por archivo.
///
/// Formato:
///   [0..4)   "STB1"
///   [4]      versión del formato
///   [5..9)   iteraciones (int32 little endian)
///   [9..25)  sal (16 bytes)
///   [25..37) nonce (12 bytes)
///   [37..]   texto cifrado + etiqueta GCM (16 bytes)
/// El encabezado completo se usa como dato asociado autenticado (AAD).
/// </summary>
public static class CifradoRespaldo
{
    public const string Extension = ".stb";
    public const int IteracionesPredeterminadas = 600_000;
    public const int LargoClave = 24;

    private static readonly byte[] Magia = { (byte)'S', (byte)'T', (byte)'B', (byte)'1' };
    private const byte VersionFormato = 1;
    private const int LargoSal = 16;
    private const int LargoNonce = 12;
    private const int LargoEtiqueta = 16;
    private const int LargoEncabezado = 4 + 1 + 4 + LargoSal + LargoNonce;

    private const string Alfabeto = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>Genera una clave aleatoria de 120 bits en Base32 (24 caracteres).</summary>
    public static string GenerarClave()
    {
        var bytes = RandomNumberGenerator.GetBytes(15);
        return Base32Codificar(bytes);
    }

    public static bool EsCifrado(ReadOnlySpan<byte> archivo) =>
        archivo.Length > LargoEncabezado && archivo[..4].SequenceEqual(Magia);

    public static int LeerIteraciones(ReadOnlySpan<byte> archivo)
    {
        if (!EsCifrado(archivo))
            throw new InvalidDataException("El archivo no está cifrado con este formato");

        return BitConverter.ToInt32(archivo.Slice(5, 4));
    }

    public static byte[] Cifrar(byte[] datos, string clave,
        int iteraciones = IteracionesPredeterminadas)
    {
        ArgumentNullException.ThrowIfNull(datos);
        ValidarClave(clave);

        if (iteraciones < 10_000)
            throw new ArgumentOutOfRangeException(nameof(iteraciones));

        var sal = RandomNumberGenerator.GetBytes(LargoSal);
        var nonce = RandomNumberGenerator.GetBytes(LargoNonce);
        var llave = Derivar(clave, sal, iteraciones);

        var salida = new byte[LargoEncabezado + datos.Length + LargoEtiqueta];
        Magia.CopyTo(salida, 0);
        salida[4] = VersionFormato;
        BitConverter.TryWriteBytes(salida.AsSpan(5, 4), iteraciones);
        sal.CopyTo(salida, 9);
        nonce.CopyTo(salida, 25);

        using var aes = new AesGcm(llave, LargoEtiqueta);
        aes.Encrypt(nonce, datos, salida.AsSpan(LargoEncabezado, datos.Length),
            salida.AsSpan(LargoEncabezado + datos.Length, LargoEtiqueta),
            salida.AsSpan(0, LargoEncabezado));

        return salida;
    }

    public static byte[] Descifrar(byte[] archivo, string clave)
    {
        ArgumentNullException.ThrowIfNull(archivo);
        ValidarClave(clave);

        if (!EsCifrado(archivo))
            throw new InvalidDataException("El archivo no está cifrado con este formato");

        if (archivo[4] != VersionFormato)
            throw new InvalidDataException(
                $"La versión del respaldo ({archivo[4]}) no es compatible con esta app");

        var iteraciones = BitConverter.ToInt32(archivo, 5);
        if (iteraciones < 10_000)
            throw new InvalidDataException("El respaldo tiene un número de iteraciones inválido");

        if (archivo.Length < LargoEncabezado + LargoEtiqueta)
            throw new InvalidDataException("El respaldo está incompleto");

        var sal = archivo.AsSpan(9, LargoSal).ToArray();
        var nonce = archivo.AsSpan(25, LargoNonce).ToArray();
        var largoDatos = archivo.Length - LargoEncabezado - LargoEtiqueta;
        var llave = Derivar(clave, sal, iteraciones);
        var datos = new byte[largoDatos];

        using var aes = new AesGcm(llave, LargoEtiqueta);
        aes.Decrypt(nonce, archivo.AsSpan(LargoEncabezado, largoDatos),
            archivo.AsSpan(LargoEncabezado + largoDatos, LargoEtiqueta),
            datos, archivo.AsSpan(0, LargoEncabezado));

        return datos;
    }

    public static string NormalizarClave(string clave)
    {
        if (string.IsNullOrWhiteSpace(clave))
            return string.Empty;

        var limpio = new StringBuilder(LargoClave);
        foreach (var c in clave.Trim().ToUpperInvariant())
        {
            if (c is '-' or ' ')
                continue;

            // Crockford: O se confunde con 0; I y L con 1.
            var caracter = c switch
            {
                'O' => '0',
                'I' or 'L' => '1',
                _ => c
            };

            limpio.Append(caracter);
        }

        return limpio.ToString();
    }

    public static bool ClaveValida(string clave)
    {
        var normalizada = NormalizarClave(clave);
        return normalizada.Length == LargoClave &&
               normalizada.All(c => Alfabeto.IndexOf(c) >= 0);
    }

    private static void ValidarClave(string clave)
    {
        if (!ClaveValida(clave))
            throw new ArgumentException(
                $"La clave de respaldo debe tener {LargoClave} caracteres válidos", nameof(clave));
    }

    private static byte[] Derivar(string clave, byte[] sal, int iteraciones) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(NormalizarClave(clave)), sal, iteraciones,
            HashAlgorithmName.SHA256, 32);

    private static string Base32Codificar(ReadOnlySpan<byte> datos)
    {
        var salida = new StringBuilder((datos.Length * 8 + 4) / 5);
        var buffer = 0;
        var bits = 0;

        foreach (var b in datos)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                salida.Append(Alfabeto[(buffer >> bits) & 0x1F]);
            }
        }

        if (bits > 0)
            salida.Append(Alfabeto[(buffer << (5 - bits)) & 0x1F]);

        return salida.ToString();
    }
}
