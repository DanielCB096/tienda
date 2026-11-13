using System.Globalization;
using System.Text;
using SistemaTienda.Core;

namespace SistemaTienda.Tests;

public sealed class ImportacionProductosCsvTests
{
    [Fact]
    public void ImportarProductosCsv_ImportaFilasValidasYRegistraStockInicial()
    {
        using var bd = new BaseDatosPrueba();
        var culturaAnterior = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("es-MX");

        try
        {
            const string csv =
                "CodigoBarras,Nombre,Costo,Precio,Stock,StockMinimo,Categoria,Impuesto\n" +
                "A1,\"Galletas, saladas\",12.50,20.00,4,2,Abarrotes,8\n" +
                "A2,Café,30,45,,,,";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
            var resultado = bd.Inventario.ImportarProductosCsv(
                stream, 16m);

            Assert.True(resultado.IsSuccess, resultado.Message);
            Assert.Equal(2, resultado.Value.ProductosImportados);
            Assert.Empty(resultado.Value.Errores);

            var productos = bd.Inventario.ObtenerProductos().ToDictionary(p => p.CodigoBarras);
            Assert.Equal(1_250, productos["A1"].Costo);
            Assert.Equal(2_000, productos["A1"].Precio);
            Assert.Equal(4, productos["A1"].Stock);
            Assert.Equal(2, productos["A1"].StockMinimo);
            Assert.Equal("Galletas, saladas", productos["A1"].Nombre);
            Assert.Equal(8m, productos["A1"].Impuesto);
            Assert.Equal(16m, productos["A2"].Impuesto);
            Assert.Equal(0, productos["A2"].Stock);

            var entrada = Assert.Single(bd.Inventario.HistorialMovimientos(productos["A1"].Id));
            Assert.Equal(MovimientoTipo.Entrada, entrada.Tipo);
            Assert.Equal(4, entrada.Cantidad);
        }
        finally
        {
            CultureInfo.CurrentCulture = culturaAnterior;
        }
    }

    [Fact]
    public void ImportarProductosCsv_OmiteFilasInvalidasYCodigosExistentes()
    {
        using var bd = new BaseDatosPrueba();
        Assert.True(bd.Inventario.AgregarProducto(new Producto
        {
            CodigoBarras = "EXISTENTE",
            Nombre = "Producto previo",
            Costo = 100,
            Precio = 200
        }).IsSuccess);

        const string csv =
            "CodigoBarras,Nombre,Costo,Precio,Stock\n" +
            "EXISTENTE,Duplicado,1,2,1\n" +
            "A2,,1,2,1\n" +
            "A3,Precio inválido,1,abc,1\n" +
            "A4,Correcto,3.50,5.25,2";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var resultado = bd.Inventario.ImportarProductosCsv(
            stream, 16m);

        Assert.True(resultado.IsSuccess, resultado.Message);
        Assert.Equal(1, resultado.Value.ProductosImportados);
        Assert.Equal(new[] { 2, 3, 4 },
            resultado.Value.Errores.Select(error => error.NumeroFila));
        Assert.Contains("ya existe", resultado.Value.Errores[0].Mensaje);
        Assert.Single(bd.Inventario.ObtenerProductos(), p => p.CodigoBarras == "A4");
        Assert.DoesNotContain(bd.Inventario.ObtenerProductos(), p => p.CodigoBarras == "A2");
    }

    [Fact]
    public void ImportarProductosCsv_OmiteTodasLasFilasConCodigoRepetidoEnElArchivo()
    {
        using var bd = new BaseDatosPrueba();
        const string csv =
            "CodigoBarras,Nombre,Costo,Precio\n" +
            "DUP,Primero,1,2\n" +
            "DUP,Segundo,3,4\n" +
            "OK,Válido,5,6";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var resultado = bd.Inventario.ImportarProductosCsv(
            stream, 16m);

        Assert.True(resultado.IsSuccess, resultado.Message);
        Assert.Equal(1, resultado.Value.ProductosImportados);
        Assert.Equal(new[] { 2, 3 },
            resultado.Value.Errores.Select(error => error.NumeroFila));
        Assert.All(resultado.Value.Errores,
            error => Assert.Contains("se repite", error.Mensaje));
        Assert.Single(bd.Inventario.ObtenerProductos());
        Assert.Equal("OK", Assert.Single(bd.Inventario.ObtenerProductos()).CodigoBarras);
    }

    [Fact]
    public void ImportarProductosCsv_ReporteUsaLineaFisicaTrasCampoMultilinea()
    {
        using var bd = new BaseDatosPrueba();
        const string csv =
            "CodigoBarras,Nombre,Costo,Precio\n" +
            "A1,\"Nombre\ncon salto\",1,2\n" +
            "A2,,1,2";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var resultado = bd.Inventario.ImportarProductosCsv(stream, 16m);

        Assert.True(resultado.IsSuccess, resultado.Message);
        Assert.Equal(1, resultado.Value.ProductosImportados);
        Assert.Equal(4, Assert.Single(resultado.Value.Errores).NumeroFila);
    }

    [Fact]
    public void ImportarProductosCsv_EncabezadosIncorrectos_NoGuardaProductos()
    {
        using var bd = new BaseDatosPrueba();
        const string csv = "Codigo,Nombre,Costo,Precio\nA1,Producto,1,2";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var resultado = bd.Inventario.ImportarProductosCsv(
            stream, 16m);

        Assert.True(resultado.IsError);
        Assert.Contains("no es compatible", resultado.Message);
        Assert.Empty(bd.Inventario.ObtenerProductos());
    }

    [Fact]
    public void ImportarProductosCsv_EmpleadoSinPermiso_NoImporta()
    {
        using var bd = new BaseDatosPrueba();
        bd.IniciarSesion(RolUsuario.Empleado);
        const string csv = "CodigoBarras,Nombre,Costo,Precio\nA1,Producto,1,2";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var resultado = bd.Inventario.ImportarProductosCsv(
            stream, 16m);

        Assert.True(resultado.IsError);
        Assert.Empty(bd.Inventario.ObtenerProductos());
    }
}
