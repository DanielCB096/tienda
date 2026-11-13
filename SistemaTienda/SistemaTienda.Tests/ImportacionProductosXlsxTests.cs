using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using SistemaTienda.Core;

namespace SistemaTienda.Tests;

public sealed class ImportacionProductosXlsxTests
{
    [Fact]
    public void ImportarProductosXlsx_ImportaFilasValidasYRegistraStockInicial()
    {
        using var bd = new BaseDatosPrueba();
        using var archivo = CrearLibro(
            Fila(1),
            Fila(2, Texto("CodigoBarras"), Texto("Nombre"), Texto("Costo"),
                Texto("Precio"), Texto("Stock"), Texto("StockMinimo"),
                Texto("Categoria"), Texto("Impuesto")),
            Fila(3, Texto("0001"), Texto("Arroz"), Numero("12.50"),
                Numero("20.00"), Numero("4"), Numero("2"),
                Texto("Abarrotes"), Numero("8")),
            Fila(4, Texto("A2"), Texto("Café"), Numero("30"),
                Numero("45")));

        var resultado = bd.Inventario.ImportarProductosXlsx(archivo, 16m);

        Assert.True(resultado.IsSuccess, resultado.Message);
        Assert.Equal(2, resultado.Value.ProductosImportados);
        Assert.Empty(resultado.Value.Errores);

        var productos = bd.Inventario.ObtenerProductos().ToDictionary(p => p.CodigoBarras);
        Assert.Equal(1_250, productos["0001"].Costo);
        Assert.Equal(2_000, productos["0001"].Precio);
        Assert.Equal(4, productos["0001"].Stock);
        Assert.Equal(2, productos["0001"].StockMinimo);
        Assert.Equal("Abarrotes", productos["0001"].Categoria);
        Assert.Equal(8m, productos["0001"].Impuesto);
        Assert.Equal(16m, productos["A2"].Impuesto);
        Assert.Equal(0, productos["A2"].Stock);

        var entrada = Assert.Single(bd.Inventario.HistorialMovimientos(productos["0001"].Id));
        Assert.Equal(MovimientoTipo.Entrada, entrada.Tipo);
        Assert.Equal(4, entrada.Cantidad);
    }

    [Fact]
    public void ImportarProductosXlsx_OmiteFilasInvalidasDuplicadasYConFormula()
    {
        using var bd = new BaseDatosPrueba();
        Assert.True(bd.Inventario.AgregarProducto(new Producto
        {
            CodigoBarras = "EXISTENTE",
            Nombre = "Producto previo",
            Costo = 100,
            Precio = 200
        }).IsSuccess);

        using var archivo = CrearLibro(
            Fila(1, Texto("CodigoBarras"), Texto("Nombre"), Texto("Costo"), Texto("Precio")),
            Fila(2, Texto("EXISTENTE"), Texto("Duplicado"), Numero("1"), Numero("2")),
            Fila(3),
            Fila(4, Texto("A2"), Texto(""), Numero("1"), Numero("2")),
            Fila(5, Texto("A3"), Texto("Con fórmula"), Formula("3"), Numero("4")),
            Fila(6, Texto("A4"), Texto("Correcto"), Numero("3.50"), Numero("5.25")));

        var resultado = bd.Inventario.ImportarProductosXlsx(archivo, 16m);

        Assert.True(resultado.IsSuccess, resultado.Message);
        Assert.Equal(1, resultado.Value.ProductosImportados);
        Assert.Equal(new[] { 2, 4, 5 },
            resultado.Value.Errores.Select(error => error.NumeroFila));
        Assert.Contains("ya existe", resultado.Value.Errores[0].Mensaje);
        Assert.Contains("fórmulas", resultado.Value.Errores[2].Mensaje);
        Assert.Single(bd.Inventario.ObtenerProductos(), p => p.CodigoBarras == "A4");
    }

    [Fact]
    public void ImportarProductosXlsx_EncabezadosIncorrectos_NoGuardaProductos()
    {
        using var bd = new BaseDatosPrueba();
        using var archivo = CrearLibro(
            Fila(1, Texto("Codigo"), Texto("Nombre"), Texto("Costo"), Texto("Precio")),
            Fila(2, Texto("A1"), Texto("Producto"), Numero("1"), Numero("2")));

        var resultado = bd.Inventario.ImportarProductosXlsx(archivo, 16m);

        Assert.True(resultado.IsError);
        Assert.Contains("no es compatible", resultado.Message);
        Assert.Empty(bd.Inventario.ObtenerProductos());
    }

    [Fact]
    public void ImportarProductosXlsx_EmpleadoSinPermiso_NoImporta()
    {
        using var bd = new BaseDatosPrueba();
        bd.IniciarSesion(RolUsuario.Empleado);
        using var archivo = CrearLibro(
            Fila(1, Texto("CodigoBarras"), Texto("Nombre"), Texto("Costo"), Texto("Precio")),
            Fila(2, Texto("A1"), Texto("Producto"), Numero("1"), Numero("2")));

        var resultado = bd.Inventario.ImportarProductosXlsx(archivo, 16m);

        Assert.True(resultado.IsError);
        Assert.Empty(bd.Inventario.ObtenerProductos());
    }

    [Fact]
    public void ImportarProductosXlsx_ArchivoInvalido_NoGuardaProductos()
    {
        using var bd = new BaseDatosPrueba();
        using var archivo = new MemoryStream(new byte[] { 1, 2, 3 });

        var resultado = bd.Inventario.ImportarProductosXlsx(archivo, 16m);

        Assert.True(resultado.IsError);
        Assert.Contains("XLSX", resultado.Message);
        Assert.Empty(bd.Inventario.ObtenerProductos());
    }

    private static MemoryStream CrearLibro(params FilaXlsx[] filas)
    {
        var archivo = new MemoryStream();
        using (var documento = SpreadsheetDocument.Create(
                   archivo, SpreadsheetDocumentType.Workbook, true))
        {
            var parteLibro = documento.AddWorkbookPart();
            parteLibro.Workbook = new Workbook();
            var parteHoja = parteLibro.AddNewPart<WorksheetPart>();
            var datos = new SheetData();
            parteHoja.Worksheet = new Worksheet(datos);
            parteLibro.Workbook.AppendChild(new Sheets(
                new Sheet
                {
                    Name = "Productos",
                    SheetId = 1U,
                    Id = parteLibro.GetIdOfPart(parteHoja)
                }));

            var parteCadenas = parteLibro.AddNewPart<SharedStringTablePart>();
            parteCadenas.SharedStringTable = new SharedStringTable();
            var indicesCadenas = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var filaOrigen in filas)
            {
                var fila = new Row { RowIndex = (uint)filaOrigen.Numero };
                for (var indice = 0; indice < filaOrigen.Celdas.Length; indice++)
                {
                    var origen = filaOrigen.Celdas[indice];
                    var celda = new Cell
                    {
                        CellReference = $"{Columna(indice + 1)}{filaOrigen.Numero}"
                    };

                    if (origen.EsNumerica)
                    {
                        celda.DataType = CellValues.Number;
                        celda.CellValue = new CellValue(origen.Valor);
                    }
                    else
                    {
                        if (!indicesCadenas.TryGetValue(origen.Valor, out var indiceCadena))
                        {
                            indiceCadena = indicesCadenas.Count;
                            indicesCadenas.Add(origen.Valor, indiceCadena);
                            parteCadenas.SharedStringTable.AppendChild(
                                new SharedStringItem(new Text(origen.Valor)));
                        }

                        celda.DataType = CellValues.SharedString;
                        celda.CellValue = new CellValue(
                            indiceCadena.ToString(CultureInfo.InvariantCulture));
                    }

                    if (origen.EsFormula)
                        celda.CellFormula = new CellFormula("1+1");

                    fila.Append(celda);
                }

                datos.Append(fila);
            }

            parteCadenas.SharedStringTable.Save();
            parteHoja.Worksheet.Save();
            parteLibro.Workbook.Save();
        }

        archivo.Position = 0;
        return archivo;
    }

    private static FilaXlsx Fila(int numero, params CeldaXlsx[] celdas) =>
        new(numero, celdas);

    private static CeldaXlsx Texto(string valor) => new(valor, false, false);

    private static CeldaXlsx Numero(string valor) => new(valor, true, false);

    private static CeldaXlsx Formula(string valor) => new(valor, true, true);

    private static string Columna(int indice)
    {
        var nombre = string.Empty;
        while (indice > 0)
        {
            var resto = (indice - 1) % 26;
            nombre = (char)('A' + resto) + nombre;
            indice = (indice - 1) / 26;
        }

        return nombre;
    }

    private sealed record FilaXlsx(int Numero, CeldaXlsx[] Celdas);

    private sealed record CeldaXlsx(string Valor, bool EsNumerica, bool EsFormula);
}
