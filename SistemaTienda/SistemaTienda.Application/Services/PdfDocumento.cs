// Generador PDF mínimo para reportes y estados de cuenta, sin dependencias externas.
using System.Globalization;
using System.Text;

namespace SistemaTienda.Application.Services;

/// <summary>
/// Documento PDF sencillo en tamaño carta con texto, líneas y saltos de página.
/// Usa las fuentes base Helvetica y Helvetica-Bold de PDF, sin incrustar archivos.
/// </summary>
public sealed class PdfDocumento
{
    public const float AnchoPagina = 612f;
    public const float AltoPagina = 792f;
    public const float Margen = 48f;

    private static readonly byte[] Firma =
    {
        0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0x0A,
        0xE2, 0xE3, 0xCF, 0xD3, 0x0A
    };

    private readonly List<string> _paginas = new();
    private StringBuilder _contenido = new();
    private float _y = AltoPagina - Margen;
    private int _numeroPagina;

    public PdfDocumento() => IniciarPagina();

    public float Y => _y;
    public int Paginas => _paginas.Count + (_contenido.Length > 0 ? 1 : 0);

    public void NuevaPagina()
    {
        if (_contenido.Length > 0)
            _paginas.Add(_contenido.ToString());

        _contenido = new StringBuilder();
        IniciarPagina();
    }

    public void AsegurarEspacio(float alto)
    {
        if (_y - alto < Margen)
            NuevaPagina();
    }

    public void Espacio(float alto) => _y -= alto;

    public void Texto(string texto, float tamano = 10f, bool negrita = false, float x = Margen)
    {
        foreach (var linea in Partir(texto, tamano, AnchoPagina - Margen * 2))
        {
            AsegurarEspacio(tamano + 4);
            EscribirLinea(linea, x, _y, tamano, negrita);
            _y -= tamano + 4;
        }
    }

    /// <summary>Fila con un texto a la izquierda y otro alineado a la derecha.</summary>
    public void Fila(string izquierda, string derecha, float tamano = 10f, bool negrita = false)
    {
        AsegurarEspacio(tamano + 4);
        EscribirLinea(izquierda, Margen, _y, tamano, negrita);
        var anchoDerecha = AnchoTexto(derecha, tamano);
        EscribirLinea(derecha, AnchoPagina - Margen - anchoDerecha, _y, tamano, negrita);
        _y -= tamano + 4;
    }

    public void LineaHorizontal(float grosor = 0.5f)
    {
        AsegurarEspacio(8);
        _contenido.Append(string.Format(CultureInfo.InvariantCulture,
            "{0:0.##} w {1:0.##} {2:0.##} m {3:0.##} {2:0.##} l S\n",
            grosor, Margen, _y, AnchoPagina - Margen));
        _y -= 8;
    }

    public byte[] Generar()
    {
        if (_contenido.Length > 0)
        {
            _paginas.Add(_contenido.ToString());
            _contenido = new StringBuilder();
        }

        if (_paginas.Count == 0)
            _paginas.Add(string.Empty);

        var objetos = new List<byte[]>
        {
            Ascii("<< /Type /Catalog /Pages 2 0 R >>"),
            Ascii($"<< /Type /Pages /Kids [{string.Join(" ", Enumerable.Range(0, _paginas.Count).Select(i => $"{5 + i * 2} 0 R"))}] /Count {_paginas.Count} >>"),
            Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"),
            Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>")
        };

        for (var i = 0; i < _paginas.Count; i++)
        {
            var contenidoId = 6 + i * 2;
            var contenido = Encoding.Latin1.GetBytes(_paginas[i]);

            objetos.Add(Ascii(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {AnchoPagina:0.##} {AltoPagina:0.##}] " +
                $"/Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {contenidoId} 0 R >>"));
            objetos.Add(Unir(
                Ascii($"<< /Length {contenido.Length} >>\nstream\n"),
                contenido,
                Ascii("\nendstream")));
        }

        using var salida = new MemoryStream();
        salida.Write(Firma, 0, Firma.Length);

        var posiciones = new List<long>(objetos.Count);
        for (var i = 0; i < objetos.Count; i++)
        {
            posiciones.Add(salida.Position);
            Escribir(salida, Ascii($"{i + 1} 0 obj\n"));
            Escribir(salida, objetos[i]);
            Escribir(salida, Ascii("\nendobj\n"));
        }

        var inicioXref = salida.Position;
        Escribir(salida, Ascii($"xref\n0 {objetos.Count + 1}\n"));
        Escribir(salida, Ascii("0000000000 65535 f \n"));
        foreach (var posicion in posiciones)
            Escribir(salida, Ascii($"{posicion:0000000000} 00000 n \n"));

        Escribir(salida, Ascii(
            $"trailer\n<< /Size {objetos.Count + 1} /Root 1 0 R >>\n" +
            $"startxref\n{inicioXref}\n%%EOF\n"));
        return salida.ToArray();
    }

    private void IniciarPagina()
    {
        _numeroPagina++;
        _y = AltoPagina - Margen;
        _contenido.Append(string.Format(CultureInfo.InvariantCulture,
            "BT /F1 8 Tf {0:0.##} 24 Td (Pagina {1}) Tj ET\n",
            AnchoPagina / 2 - 18, _numeroPagina));
    }

    private void EscribirLinea(string texto, float x, float y, float tamano, bool negrita)
    {
        var fuente = negrita ? "F2" : "F1";
        _contenido.Append(string.Format(CultureInfo.InvariantCulture,
            "BT /{0} {1:0.##} Tf {2:0.##} {3:0.##} Td ({4}) Tj ET\n",
            fuente, tamano, x, y, Escapar(texto)));
    }

    private static float AnchoTexto(string texto, float tamano) =>
        Math.Max(1f, (texto?.Length ?? 0) * tamano * 0.5f);

    private static IEnumerable<string> Partir(string texto, float tamano, float ancho)
    {
        var limpio = Sanear(texto);
        var maximo = Math.Max(10, (int)(ancho / (tamano * 0.5f)));
        if (limpio.Length == 0)
        {
            yield return string.Empty;
            yield break;
        }

        var palabras = limpio.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var linea = new StringBuilder();
        foreach (var palabra in palabras)
        {
            if (linea.Length > 0 && linea.Length + 1 + palabra.Length > maximo)
            {
                yield return linea.ToString();
                linea.Clear();
            }

            if (palabra.Length > maximo)
            {
                foreach (var parte in PartirPalabra(palabra, maximo))
                    yield return parte;
                continue;
            }

            if (linea.Length > 0)
                linea.Append(' ');
            linea.Append(palabra);
        }

        if (linea.Length > 0)
            yield return linea.ToString();
    }

    private static IEnumerable<string> PartirPalabra(string palabra, int maximo)
    {
        for (var i = 0; i < palabra.Length; i += maximo)
            yield return palabra.Substring(i, Math.Min(maximo, palabra.Length - i));
    }

    private static string Sanear(string texto)
    {
        if (string.IsNullOrEmpty(texto))
            return string.Empty;

        return texto
            .Replace('\u2014', '-')
            .Replace('\u2013', '-')
            .Replace('\u2018', '\'')
            .Replace('\u2019', '\'')
            .Replace('\u201c', '"')
            .Replace('\u201d', '"')
            .Replace("\u2026", "...");
    }

    private static string Escapar(string texto) =>
        (texto ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace("(", "\\(")
            .Replace(")", "\\)");

    private static byte[] Ascii(string texto) => Encoding.ASCII.GetBytes(texto);

    private static byte[] Unir(params byte[][] partes)
    {
        var total = partes.Sum(p => p.Length);
        var resultado = new byte[total];
        var offset = 0;
        foreach (var parte in partes)
        {
            Buffer.BlockCopy(parte, 0, resultado, offset, parte.Length);
            offset += parte.Length;
        }

        return resultado;
    }

    private static void Escribir(Stream stream, byte[] datos) =>
        stream.Write(datos, 0, datos.Length);
}
