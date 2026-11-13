using System.Globalization;
using System.Xml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace SistemaTienda.Application.Services;

internal sealed record FilaHojaProductosXlsx(
    int NumeroFila,
    string[] Campos,
    bool TieneFormula,
    bool TieneErrorExcel);

internal sealed record HojaProductosXlsx(
    string[] Encabezados,
    IReadOnlyList<FilaHojaProductosXlsx> Filas);

internal static class LectorProductosXlsx
{
    public static HojaProductosXlsx Leer(Stream archivo)
    {
        using var copia = new MemoryStream();
        archivo.CopyTo(copia);
        copia.Position = 0;

        try
        {
            using var libro = SpreadsheetDocument.Open(copia, false);
            var parteLibro = libro.WorkbookPart
                ?? throw new InvalidDataException("El libro no contiene una estructura de Excel.");
            var hoja = parteLibro.Workbook?.Sheets?.Elements<Sheet>().FirstOrDefault()
                ?? throw new InvalidDataException("El libro no contiene hojas.");

            if (string.IsNullOrWhiteSpace(hoja.Id?.Value) ||
                parteLibro.GetPartById(hoja.Id.Value) is not WorksheetPart parteHoja)
            {
                throw new InvalidDataException("No se pudo abrir la primera hoja del libro.");
            }

            var filasOrigen = parteHoja.Worksheet.GetFirstChild<SheetData>()?
                .Elements<Row>()
                .ToList() ?? new List<Row>();
            var cadenasCompartidas = parteLibro.SharedStringTablePart?.SharedStringTable?
                .Elements<SharedStringItem>()
                .Select(elemento => elemento.InnerText)
                .ToArray() ?? Array.Empty<string>();

            var filas = new List<FilaHojaProductosXlsx>();
            for (var indice = 0; indice < filasOrigen.Count; indice++)
                filas.Add(LeerFila(filasOrigen[indice], indice + 1, cadenasCompartidas));

            var posicionEncabezado = filas.FindIndex(fila =>
                fila.Campos.Any(campo => !string.IsNullOrWhiteSpace(campo)) ||
                fila.TieneFormula ||
                fila.TieneErrorExcel);

            if (posicionEncabezado < 0)
                throw new InvalidDataException("La primera hoja está vacía.");

            var filaEncabezado = filas[posicionEncabezado];
            if (filaEncabezado.TieneFormula || filaEncabezado.TieneErrorExcel)
                throw new InvalidDataException("La fila de encabezados contiene fórmulas o errores de Excel.");

            var encabezados = RecortarColumnasVaciasAlFinal(filaEncabezado.Campos);
            if (encabezados.Length == 0)
                throw new InvalidDataException("La primera hoja no contiene encabezados.");

            return new HojaProductosXlsx(
                encabezados,
                filas.Skip(posicionEncabezado + 1).ToArray());
        }
        catch (OpenXmlPackageException ex)
        {
            throw new InvalidDataException("El archivo no es un libro XLSX válido.", ex);
        }
        catch (FileFormatException ex)
        {
            throw new InvalidDataException("El archivo no es un libro XLSX válido.", ex);
        }
        catch (XmlException ex)
        {
            throw new InvalidDataException("El archivo XLSX contiene datos XML inválidos.", ex);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new InvalidDataException("El libro XLSX contiene referencias inválidas.", ex);
        }
        catch (OverflowException ex)
        {
            throw new InvalidDataException("El libro XLSX contiene una referencia de fila o columna inválida.", ex);
        }
    }

    private static FilaHojaProductosXlsx LeerFila(
        Row fila, int indiceRespaldo, IReadOnlyList<string> cadenasCompartidas)
    {
        var celdas = new List<(int Columna, string Valor, bool Formula, bool Error)>();
        var columnasVistas = new HashSet<int>();
        foreach (var celda in fila.Elements<Cell>())
        {
            var columna = ObtenerIndiceColumna(celda.CellReference?.Value);
            if (!columnasVistas.Add(columna))
                throw new InvalidDataException("Una fila contiene referencias de celda repetidas.");

            var esError = celda.DataType?.Value == CellValues.Error;
            celdas.Add((
                columna,
                esError ? string.Empty : LeerValor(celda, cadenasCompartidas),
                celda.CellFormula != null,
                esError));
        }

        var ultimaColumna = celdas
            .Where(celda => !string.IsNullOrWhiteSpace(celda.Valor) || celda.Formula || celda.Error)
            .Select(celda => celda.Columna)
            .DefaultIfEmpty(-1)
            .Max();
        var campos = ultimaColumna < 0
            ? Array.Empty<string>()
            : Enumerable.Repeat(string.Empty, ultimaColumna + 1).ToArray();

        foreach (var celda in celdas)
        {
            if (celda.Columna <= ultimaColumna)
                campos[celda.Columna] = celda.Valor;
        }

        var numeroFila = fila.RowIndex?.Value ?? (uint)indiceRespaldo;
        if (numeroFila == 0 || numeroFila > int.MaxValue)
            throw new InvalidDataException("El libro XLSX contiene un número de fila inválido.");

        return new FilaHojaProductosXlsx(
            (int)numeroFila,
            campos,
            celdas.Any(celda => celda.Formula),
            celdas.Any(celda => celda.Error));
    }

    private static string LeerValor(Cell celda, IReadOnlyList<string> cadenasCompartidas)
    {
        var valor = celda.CellValue?.Text ?? string.Empty;
        if (celda.DataType?.Value == CellValues.SharedString)
        {
            if (!int.TryParse(valor, NumberStyles.None, CultureInfo.InvariantCulture, out var indice) ||
                indice < 0 ||
                indice >= cadenasCompartidas.Count)
            {
                throw new InvalidDataException("El libro XLSX contiene una cadena compartida inválida.");
            }

            return cadenasCompartidas[indice];
        }

        if (celda.DataType?.Value == CellValues.InlineString)
            return celda.InlineString?.InnerText ?? string.Empty;
        if (celda.DataType?.Value == CellValues.Boolean)
            return valor == "1" ? "TRUE" : "FALSE";

        return valor;
    }

    private static int ObtenerIndiceColumna(string referencia)
    {
        if (string.IsNullOrWhiteSpace(referencia))
            throw new InvalidDataException("Una celda no tiene referencia de columna.");

        var columna = 0;
        var encontroLetra = false;
        foreach (var caracter in referencia)
        {
            if (caracter == '$')
                continue;

            if (!char.IsLetter(caracter))
                break;

            var letra = char.ToUpperInvariant(caracter);
            if (letra < 'A' || letra > 'Z')
                throw new InvalidDataException("Una celda tiene una referencia de columna inválida.");

            encontroLetra = true;
            columna = checked(columna * 26 + letra - 'A' + 1);
        }

        if (!encontroLetra)
            throw new InvalidDataException("Una celda tiene una referencia de columna inválida.");

        return columna - 1;
    }

    private static string[] RecortarColumnasVaciasAlFinal(string[] campos)
    {
        var longitud = campos.Length;
        while (longitud > 0 && string.IsNullOrWhiteSpace(campos[longitud - 1]))
            longitud--;

        return campos.Take(longitud).ToArray();
    }
}
