using System.Diagnostics;
using System.Text;
using Microsoft.Data.Sqlite;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Tests;

/// <summary>
/// H6.1: vista previa, lote de importación y reversión compensatoria.
/// La reversión no borra historial: devuelve el stock y desactiva productos.
/// </summary>
public sealed class ImportacionLotesTests
{
    private const string Encabezados =
        "CodigoBarras,Nombre,Costo,Precio,Stock,StockMinimo,Categoria,Impuesto";

    private static Stream Csv(string filas)
    {
        var texto = Encabezados + "\n" + filas;
        return new MemoryStream(new UTF8Encoding(false).GetBytes(texto));
    }

    private static string Fila(string codigo, string nombre, string costo = "10.00",
        string precio = "20.00", int stock = 5) =>
        $"{codigo},{nombre},{costo},{precio},{stock},2,Abarrotes,16";

    private static int Contar(BaseDatosPrueba bd, string sql) =>
        bd.Db.ExecuteScalar<int>(sql);

    [Fact]
    public void Plantilla_Oficial_SePrevisualizaSinErrores()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var vista = bd.Inventario.PrevisualizarProductosCsv(
            new MemoryStream(PlantillaProductos.ComoBytes()), 16m);

        Assert.True(vista.IsSuccess, vista.Message);
        Assert.Equal(2, vista.Value.FilasValidas);
        Assert.Empty(vista.Value.Errores);
        Assert.StartsWith(PlantillaProductos.Encabezados, PlantillaProductos.Csv);
    }

    [Fact]
    public void VistaPrevia_NoGuardaYCuentaValidasYOmitidas()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var vista = bd.Inventario.PrevisualizarProductosCsv(Csv(
            Fila("H61-1", "Producto uno") + "\n" +
            Fila("H61-2", "Producto dos") + "\n" +
            Fila("H61-3", "Precio malo", precio: "abc")), 16m);

        Assert.True(vista.IsSuccess, vista.Message);
        Assert.Equal(3, vista.Value.FilasLeidas);
        Assert.Equal(2, vista.Value.FilasValidas);
        Assert.Equal(1, vista.Value.FilasOmitidas);
        Assert.Single(vista.Value.Errores);
        // TextFieldParser reporta la línea siguiente a la fila leída.
        Assert.Equal(4, vista.Value.Errores[0].NumeroFila);
        Assert.Equal(2, vista.Value.Muestra.Count);
        Assert.Empty(bd.Inventario.ObtenerProductos());
        Assert.Empty(bd.Inventario.ObtenerImportaciones());
    }

    [Fact]
    public void Importar_RegistraLoteYVinculaProductosYMovimientos()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var resultado = bd.Inventario.ImportarProductosCsv(Csv(
            Fila("H61-10", "Producto diez", stock: 5) + "\n" +
            Fila("H61-11", "Producto once", stock: 7)), 16m, "catalogo.csv");

        Assert.True(resultado.IsSuccess, resultado.Message);
        Assert.Equal(2, resultado.Value.ProductosImportados);
        Assert.True(resultado.Value.ImportacionId > 0);

        var lote = bd.Inventario.ObtenerImportacion(resultado.Value.ImportacionId);
        Assert.NotNull(lote);
        Assert.Equal("catalogo.csv", lote.NombreArchivo);
        Assert.Equal(EstadoImportacion.Aplicada, lote.Estado);
        Assert.Equal(2, lote.ProductosInsertados);
        Assert.False(string.IsNullOrWhiteSpace(lote.HashArchivo));

        var productos = bd.Inventario.ObtenerProductos();
        Assert.All(productos, p => Assert.Equal(lote.Id, p.ImportacionId));
        Assert.Equal(2, Contar(bd, "SELECT COUNT(*) FROM MovimientoInventario " +
            $"WHERE ImportacionId = {lote.Id}"));
        Assert.Equal(12, bd.Db.ObtenerStock(productos[0].Id) +
                         bd.Db.ObtenerStock(productos[1].Id));
        Assert.Empty(bd.Db.ProductosDescuadrados());
        Assert.Equal(1, Contar(bd, "SELECT COUNT(*) FROM Bitacora " +
            $"WHERE Accion = '{AccionBitacora.ImportacionProductos}'"));
    }

    [Fact]
    public void Importar_3000Filas_EnUnSoloLote()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var texto = new StringBuilder(Encabezados).Append('\n');
        for (var i = 0; i < 3_000; i++)
            texto.Append(Fila($"H61-{i:D5}", $"Producto {i}", stock: 1)).Append('\n');

        var reloj = Stopwatch.StartNew();
        var resultado = bd.Inventario.ImportarProductosCsv(
            new MemoryStream(Encoding.UTF8.GetBytes(texto.ToString())), 16m);
        reloj.Stop();

        Assert.True(resultado.IsSuccess, resultado.Message);
        Assert.Equal(3_000, resultado.Value.ProductosImportados);
        Assert.Equal(3_000, Contar(bd, "SELECT COUNT(*) FROM Productos"));
        Assert.Equal(3_000, Contar(bd, "SELECT COUNT(*) FROM MovimientoInventario"));
        Assert.Equal(1, Contar(bd, "SELECT COUNT(*) FROM Importaciones"));
        Assert.True(reloj.Elapsed < TimeSpan.FromSeconds(30),
            $"La importación de 3000 filas tardó {reloj.Elapsed}");
    }

    [Fact]
    public void Revertir_LoteSinUso_DejaStockCeroYDesactivaSinBorrar()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var importado = bd.Inventario.ImportarProductosCsv(Csv(
            Fila("H61-20", "Producto veinte", stock: 5) + "\n" +
            Fila("H61-21", "Producto veintiuno", stock: 9)), 16m);
        var loteId = importado.Value.ImportacionId;
        var productos = bd.Inventario.ObtenerProductos();

        var reversion = bd.Inventario.RevertirImportacion(loteId);

        Assert.True(reversion.IsSuccess, reversion.Message);
        Assert.All(productos, p =>
        {
            Assert.Equal(0, bd.Db.ObtenerStock(p.Id));
            Assert.False(bd.Inventario.BuscarPorId(p.Id).Activo);
        });
        Assert.Equal(2, bd.Inventario.ObtenerProductos().Count);
        Assert.Equal(2, Contar(bd, "SELECT COUNT(*) FROM MovimientoInventario " +
            $"WHERE ImportacionId = {loteId} AND Tipo = '{MovimientoTipo.Ajuste}'"));
        Assert.Empty(bd.Db.ProductosDescuadrados());
        Assert.True(bd.Inventario.ObtenerImportacion(loteId).Revertida);
        Assert.Equal(1, Contar(bd, "SELECT COUNT(*) FROM Bitacora " +
            $"WHERE Accion = '{AccionBitacora.ReversionImportacion}'"));

        var segunda = bd.Inventario.RevertirImportacion(loteId);
        Assert.True(segunda.IsError);
    }

    [Fact]
    public void Revertir_ConVentaPosterior_SeBloquea()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var importado = bd.Inventario.ImportarProductosCsv(
            Csv(Fila("H61-30", "Producto treinta", stock: 10)), 16m);
        var loteId = importado.Value.ImportacionId;
        var producto = bd.Inventario.ObtenerProductos().Single();

        var venta = new VentaService(bd.Db).RegistrarVenta(
            new List<LineaVentaSolicitada>
            {
                new() { ProductoId = producto.Id, Cantidad = 1 }
            },
            new PagoSolicitado { Efectivo = producto.Precio },
            "Dueño");
        Assert.True(venta.IsSuccess, venta.Message);

        var reversion = bd.Inventario.RevertirImportacion(loteId);

        Assert.True(reversion.IsError);
        Assert.Contains("no se puede", reversion.Message);
        Assert.Equal(9, bd.Db.ObtenerStock(producto.Id));
        Assert.False(bd.Inventario.ObtenerImportacion(loteId).Revertida);
        Assert.True(bd.Inventario.BuscarPorId(producto.Id).Activo);
    }

    [Fact]
    public void Importar_SiFallaLaInsercion_NoDejaNada()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var repetido = new[]
        {
            new Producto { CodigoBarras = "H61-40", Nombre = "Uno", Costo = 100, Precio = 200, Stock = 1 },
            new Producto { CodigoBarras = "H61-40", Nombre = "Dos", Costo = 100, Precio = 200, Stock = 1 }
        };

        var resultado = bd.Db.ImportarProductos(repetido, "repetido.csv", "hash", "Dueño", 2, 0);

        Assert.True(resultado.IsError);
        Assert.Empty(bd.Inventario.ObtenerProductos());
        Assert.Empty(bd.Inventario.ObtenerImportaciones());
    }

    [Fact]
    public void Revertir_EmpleadoSinPermiso_NoCambiaNada()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var importado = bd.Inventario.ImportarProductosCsv(
            Csv(Fila("H61-50", "Producto cincuenta", stock: 4)), 16m);
        bd.IniciarSesion(RolUsuario.Empleado, "Empleado");

        var reversion = bd.Inventario.RevertirImportacion(importado.Value.ImportacionId);

        Assert.True(reversion.IsError);
        Assert.False(bd.Inventario.ObtenerImportacion(importado.Value.ImportacionId).Revertida);
        Assert.Equal(4, bd.Db.ObtenerStock(bd.Inventario.ObtenerProductos().Single().Id));
    }

    [Fact]
    public void Migracion_DesdeV17_CreaTablasYColumnas()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        bd.Db.ExecuteCommand("DROP TABLE Importaciones");
        bd.Db.ExecuteCommand("PRAGMA user_version = 17");

        bd.Db.InicializarBaseDeDatos();

        Assert.Equal(Migraciones.VersionActual, bd.Db.ExecuteScalar<int>("PRAGMA user_version"));
        Assert.Equal(1, Contar(bd, "SELECT COUNT(*) FROM sqlite_master " +
            "WHERE type='table' AND name='Importaciones'"));
        Assert.Equal(1, Contar(bd, "SELECT COUNT(*) FROM pragma_table_info('Productos') " +
            "WHERE name='ImportacionId'"));
        Assert.Equal(1, Contar(bd, "SELECT COUNT(*) FROM pragma_table_info('MovimientoInventario') " +
            "WHERE name='ImportacionId'"));
    }
}
