// Renderiza el QR del ticket a PNG sin dependencias de interfaz ni de red.
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Net.Codecrete.QrCodeGenerator;

namespace SistemaTienda.Application.Services;

/// <summary>PNG generado para compartir el QR del ticket.</summary>
public sealed record QrImagen(byte[] Png, int Ancho, int Alto, int Modulo, int ModulosQuietos);

/// <summary>
/// Convierte el contenido del ticket en un PNG en blanco y negro. Usa
/// <c>Net.Codecrete.QrCodeGenerator</c> solo para la matriz y escribe el PNG
/// con <c>ZLibStream</c> y CRC32 propios, así que funciona offline y sin
/// bibliotecas de dibujo.
/// </summary>
public static class QrImagenService
{
    public const int ModuloPredeterminado = 8;
    public const int ModulosQuietosPredeterminados = 4;
    public const int ModuloMaximo = 32;
    public const int ModulosQuietosMaximos = 16;

    private static readonly byte[] FirmaPng = { 137, 80, 78, 71, 13, 10, 26, 10 };
    private static readonly uint[] TablaCrc = CrearTablaCrc();

    public static QrImagen Generar(string contenido,
        int modulo = ModuloPredeterminado,
        int modulosQuietos = ModulosQuietosPredeterminados)
    {
        if (string.IsNullOrWhiteSpace(contenido))
            throw new ArgumentException("El contenido del QR no puede estar vacío", nameof(contenido));

        if (modulo < 1 || modulo > ModuloMaximo)
            throw new ArgumentOutOfRangeException(nameof(modulo),
                $"El tamaño del módulo debe estar entre 1 y {ModuloMaximo} píxeles");

        if (modulosQuietos < 0 || modulosQuietos > ModulosQuietosMaximos)
            throw new ArgumentOutOfRangeException(nameof(modulosQuietos),
                $"La zona de silencio debe estar entre 0 y {ModulosQuietosMaximos} módulos");

        var codigo = QrCode.EncodeText(contenido, QrCode.Ecc.Medium);
        var totalModulos = codigo.Size + modulosQuietos * 2;
        var lado = checked(totalModulos * modulo);
        var bytesPorFila = checked(lado + 1);
        var crudo = new byte[checked(bytesPorFila * lado)];

        // Fondo blanco; cada fila empieza con el filtro PNG "None".
        Array.Fill(crudo, (byte)255);
        for (var y = 0; y < lado; y++)
            crudo[y * bytesPorFila] = 0;

        for (var y = 0; y < codigo.Size; y++)
        {
            for (var x = 0; x < codigo.Size; x++)
            {
                if (!codigo.GetModule(x, y))
                    continue;

                var x0 = (x + modulosQuietos) * modulo;
                var y0 = (y + modulosQuietos) * modulo;
                for (var dy = 0; dy < modulo; dy++)
                {
                    var inicio = (y0 + dy) * bytesPorFila + 1 + x0;
                    for (var dx = 0; dx < modulo; dx++)
                        crudo[inicio + dx] = 0;
                }
            }
        }

        var idat = Comprimir(crudo);
        var png = ConstruirPng(lado, idat);
        return new QrImagen(png, lado, lado, modulo, modulosQuietos);
    }

    private static byte[] Comprimir(byte[] datos)
    {
        using var salida = new MemoryStream();
        using (var zlib = new ZLibStream(salida, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(datos, 0, datos.Length);

        return salida.ToArray();
    }

    private static byte[] ConstruirPng(int lado, byte[] idat)
    {
        using var png = new MemoryStream();
        png.Write(FirmaPng);

        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0, 4), lado);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4, 4), lado);
        ihdr[8] = 8;  // profundidad de bits
        ihdr[9] = 0;  // escala de grises
        ihdr[10] = 0; // compresión deflate
        ihdr[11] = 0; // filtro adaptativo
        ihdr[12] = 0; // sin entrelazado

        EscribirChunk(png, "IHDR", ihdr);
        EscribirChunk(png, "IDAT", idat);
        EscribirChunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    private static void EscribirChunk(Stream salida, string tipo, byte[] datos)
    {
        var tipoBytes = Encoding.ASCII.GetBytes(tipo);
        var largo = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(largo, datos.Length);
        salida.Write(largo);
        salida.Write(tipoBytes);
        salida.Write(datos);

        var crcBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, CalcularCrc(tipoBytes, datos));
        salida.Write(crcBytes);
    }

    private static uint CalcularCrc(byte[] tipo, byte[] datos)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var valor in tipo)
            crc = TablaCrc[(crc ^ valor) & 0xFF] ^ (crc >> 8);
        foreach (var valor in datos)
            crc = TablaCrc[(crc ^ valor) & 0xFF] ^ (crc >> 8);

        return crc ^ 0xFFFFFFFFu;
    }

    private static uint[] CrearTablaCrc()
    {
        var tabla = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            tabla[n] = c;
        }

        return tabla;
    }
}
