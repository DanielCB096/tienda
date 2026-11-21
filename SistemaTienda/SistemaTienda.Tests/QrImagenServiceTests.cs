using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Net.Codecrete.QrCodeGenerator;
using SistemaTienda.Application.Services;

namespace SistemaTienda.Tests;

public sealed class QrImagenServiceTests
{
    private const string Contenido = "Tienda: Mi Tienda\nFolio: 12\nTotal: $35.50\nNo es comprobante fiscal.";

    [Fact]
    public void Generar_ProducePngConFirmaDimensionesYChunks()
    {
        const int modulo = 6;
        const int quietos = 4;
        var codigo = QrCode.EncodeText(Contenido, QrCode.Ecc.Medium);
        var esperado = (codigo.Size + quietos * 2) * modulo;

        var imagen = QrImagenService.Generar(Contenido, modulo, quietos);
        var png = LeerPng(imagen.Png);

        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, imagen.Png.Take(8));
        Assert.Equal(esperado, imagen.Ancho);
        Assert.Equal(esperado, imagen.Alto);
        Assert.Equal(esperado, png.Ancho);
        Assert.Equal(esperado, png.Alto);
        Assert.Equal(8, png.Profundidad);
        Assert.Equal(0, png.TipoColor);
        Assert.Equal(new[] { "IHDR", "IDAT", "IEND" }, png.Chunks);
        Assert.Equal(0, png.LargoUltimoChunk);
        Assert.True(imagen.Png.Length > 100);
    }

    [Fact]
    public void Generar_PixelesCoincidenConLaMatrizYLaZonaDeSilencio()
    {
        const int modulo = 5;
        const int quietos = 4;
        var codigo = QrCode.EncodeText(Contenido, QrCode.Ecc.Medium);
        var imagen = QrImagenService.Generar(Contenido, modulo, quietos);
        var png = LeerPng(imagen.Png);
        var crudo = Descomprimir(png.Datos);
        var bytesPorFila = png.Ancho + 1;

        Assert.Equal(bytesPorFila * png.Alto, crudo.Length);
        for (var fila = 0; fila < png.Alto; fila++)
            Assert.Equal(0, crudo[fila * bytesPorFila]);

        // La zona de silencio superior izquierda es blanca.
        Assert.Equal(255, crudo[1]);

        foreach (var (x, y) in new[] { (0, 0), (1, 0), (0, 1), (codigo.Size - 1, codigo.Size - 1) })
        {
            var px = (x + quietos) * modulo + modulo / 2;
            var py = (y + quietos) * modulo + modulo / 2;
            var esperado = codigo.GetModule(x, y) ? 0 : 255;
            Assert.Equal(esperado, crudo[py * bytesPorFila + 1 + px]);
        }
    }

    [Fact]
    public void Generar_ConModuloMayor_AumentaLaImagen()
    {
        var pequena = QrImagenService.Generar(Contenido, 2);
        var grande = QrImagenService.Generar(Contenido, 10);

        Assert.True(grande.Ancho > pequena.Ancho);
        Assert.Equal(5, grande.Ancho / pequena.Ancho);
    }

    [Fact]
    public void Generar_ContenidoVacio_Lanza()
    {
        Assert.Throws<ArgumentException>(() => QrImagenService.Generar(" "));
        Assert.Throws<ArgumentException>(() => QrImagenService.Generar(null));
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(33, 4)]
    [InlineData(8, -1)]
    [InlineData(8, 17)]
    public void Generar_ParametrosFueraDeRango_Lanza(int modulo, int quietos)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => QrImagenService.Generar(Contenido, modulo, quietos));
    }

    private static byte[] Descomprimir(byte[] datos)
    {
        using var entrada = new MemoryStream(datos);
        using var zlib = new ZLibStream(entrada, CompressionMode.Decompress);
        using var salida = new MemoryStream();
        zlib.CopyTo(salida);
        return salida.ToArray();
    }

    private static (int Ancho, int Alto, byte Profundidad, byte TipoColor,
        byte[] Datos, List<string> Chunks, int LargoUltimoChunk) LeerPng(byte[] png)
    {
        var ancho = 0;
        var alto = 0;
        byte profundidad = 0;
        byte tipoColor = 0;
        var datos = new MemoryStream();
        var chunks = new List<string>();
        var largoUltimo = 0;
        var posicion = 8;

        while (posicion < png.Length)
        {
            var largo = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(posicion, 4));
            var tipo = Encoding.ASCII.GetString(png, posicion + 4, 4);
            chunks.Add(tipo);
            largoUltimo = largo;

            if (tipo == "IHDR")
            {
                ancho = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(posicion + 8, 4));
                alto = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(posicion + 12, 4));
                profundidad = png[posicion + 16];
                tipoColor = png[posicion + 17];
            }
            else if (tipo == "IDAT")
            {
                datos.Write(png, posicion + 8, largo);
            }

            posicion += 12 + largo;
            if (tipo == "IEND")
                break;
        }

        return (ancho, alto, profundidad, tipoColor, datos.ToArray(), chunks, largoUltimo);
    }
}
