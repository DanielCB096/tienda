using Microsoft.Data.Sqlite;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Tests;

public sealed class DesactivacionProductosTests
{
    private static Producto CrearProducto(BaseDatosPrueba bd, string codigo,
        long precio, long costo, int stock)
    {
        var producto = new Producto
        {
            CodigoBarras = codigo,
            Nombre = $"Producto {codigo}",
            Costo = costo,
            Precio = precio,
            Stock = stock
        };

        var resultado = bd.Inventario.AgregarProducto(producto);
        Assert.True(resultado.IsSuccess, resultado.Message);
        return producto;
    }

    private static ProcessResult<Venta> Vender(BaseDatosPrueba bd, Producto producto, int cantidad)
    {
        var solicitud = new[]
        {
            new LineaVentaSolicitada { ProductoId = producto.Id, Cantidad = cantidad }
        };

        return new VentaService(bd.Db).RegistrarVenta(
            solicitud,
            new PagoSolicitado { Efectivo = producto.Precio * cantidad },
            "Cajero");
    }

    // ---------- Esquema v7 ----------

    [Fact]
    public void MigracionV7_AgregaColumnaActivoYMarcaLosProductosExistentesComoActivos()
    {
        using var entorno = new EntornoMigracionV7();

        // Simula un esquema v6: la base física pierde la columna y su versión baja a 6.
        entorno.Db.ExecuteCommand(@"
            ALTER TABLE Productos DROP COLUMN Activo;
            PRAGMA user_version = 6;
            INSERT INTO Productos (CodigoBarras, Nombre, Costo, Precio, Stock, StockMinimo, Categoria, Impuesto)
                VALUES ('A1', 'Producto heredado', 1000, 2000, 5, 5, '', 16.00);
        ");

        entorno.Db.InicializarBaseDeDatos();

        Assert.Equal(Migraciones.VersionMaxima(Migraciones.ObtenerTodas()),
            entorno.Db.ExecuteScalar<int>("PRAGMA user_version"));
        Assert.Equal("INTEGER", entorno.Db.ExecuteScalar<string>(
            "SELECT type FROM pragma_table_info('Productos') WHERE name = 'Activo'"));
        Assert.Equal(1, entorno.Db.ExecuteScalar<int>(
            "SELECT Activo FROM Productos WHERE CodigoBarras = 'A1'"));
        Assert.Equal(0, entorno.Db.ExecuteScalar<int>("SELECT COUNT(*) FROM pragma_foreign_key_check"));
    }

    [Fact]
    public void AgregarProducto_BaseNueva_ElProductoQuedaActivoPorDefecto()
    {
        using var bd = new BaseDatosPrueba();
        var producto = CrearProducto(bd, "A1", precio: 2_000, costo: 1_000, stock: 7);

        Assert.True(producto.Activo);

        var guardado = bd.Inventario.BuscarPorId(producto.Id);
        Assert.True(guardado.Activo);
        Assert.Equal(7, guardado.Stock);
    }

    // ---------- Desactivar y reactivar ----------

    [Fact]
    public void DesactivarProducto_ConservaElStockYBloqueaLaVenta()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 2_000, costo: 1_000, stock: 7);

        var desactivar = bd.Inventario.DesactivarProducto(producto.Id);
        Assert.True(desactivar.IsSuccess, desactivar.Message);

        var desactivado = bd.Inventario.BuscarPorId(producto.Id);
        Assert.False(desactivado.Activo);
        Assert.Equal(7, desactivado.Stock);

        var venta = Vender(bd, desactivado, 1);
        Assert.True(venta.IsError);
        Assert.Contains("desactivado", venta.Message);

        // El stock no cambió y no se registró ninguna venta.
        Assert.Equal(7, bd.Inventario.BuscarPorId(producto.Id).Stock);
        Assert.Empty(bd.Db.ObtenerVentasDelDia(DateTime.Now));
    }

    [Fact]
    public void ReactivarProducto_DevuelveElProductoALaVentaSinCambiarSuStock()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 2_000, costo: 1_000, stock: 7);

        Assert.True(bd.Inventario.DesactivarProducto(producto.Id).IsSuccess);
        var reactivar = bd.Inventario.ReactivarProducto(producto.Id);
        Assert.True(reactivar.IsSuccess, reactivar.Message);

        var reactivado = bd.Inventario.BuscarPorId(producto.Id);
        Assert.True(reactivado.Activo);
        Assert.Equal(7, reactivado.Stock);

        var venta = Vender(bd, reactivado, 1);
        Assert.True(venta.IsSuccess, venta.Message);
        Assert.Equal(6, bd.Inventario.BuscarPorId(producto.Id).Stock);
    }

    [Fact]
    public void DesactivarProducto_SoloElDueñoPuedeCambiarElEstado()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 2_000, costo: 1_000, stock: 7);

        bd.IniciarSesion(RolUsuario.Empleado, "Empleado");
        Assert.True(bd.Inventario.DesactivarProducto(producto.Id).IsError);
        Assert.True(bd.Inventario.ReactivarProducto(producto.Id).IsError);

        bd.IniciarSesion(RolUsuario.Encargado, "Encargado");
        Assert.True(bd.Inventario.DesactivarProducto(producto.Id).IsError);

        // El dueño desactiva y el encargado tampoco puede revertirlo.
        bd.IniciarSesion(RolUsuario.Dueño, "Dueño");
        Assert.True(bd.Inventario.DesactivarProducto(producto.Id).IsSuccess);

        bd.IniciarSesion(RolUsuario.Encargado, "Encargado");
        Assert.True(bd.Inventario.ReactivarProducto(producto.Id).IsError);
        Assert.False(bd.Inventario.BuscarPorId(producto.Id).Activo);
    }

    [Fact]
    public void DesactivarProducto_QuedaRegistradoEnLaBitacora()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 2_000, costo: 1_000, stock: 7);

        Assert.True(bd.Inventario.DesactivarProducto(producto.Id).IsSuccess);

        var registros = new BitacoraService(bd.Db)
            .Consultar(accion: AccionBitacora.CambiarEstadoProducto).Value;
        var registro = Assert.Single(registros);
        Assert.Equal("Producto", registro.Entidad);
        Assert.Equal(producto.Id.ToString(), registro.EntidadId);
        Assert.Contains("Activo=1", registro.DatosAntes);
        Assert.Contains("Activo=0", registro.DatosDespues);
    }

    [Fact]
    public void DesactivarProducto_YaDesactivado_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        var producto = CrearProducto(bd, "A1", precio: 2_000, costo: 1_000, stock: 7);

        Assert.True(bd.Inventario.DesactivarProducto(producto.Id).IsSuccess);
        var repetido = bd.Inventario.DesactivarProducto(producto.Id);
        Assert.True(repetido.IsError);
        Assert.Contains("ya está desactivado", repetido.Message);
    }

    [Fact]
    public void ReactivarProducto_YaActivo_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        var producto = CrearProducto(bd, "A1", precio: 2_000, costo: 1_000, stock: 7);

        var repetido = bd.Inventario.ReactivarProducto(producto.Id);
        Assert.True(repetido.IsError);
        Assert.Contains("ya está activo", repetido.Message);
    }

    [Fact]
    public void DesactivarProducto_ProductoInexistente_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();

        var resultado = bd.Inventario.DesactivarProducto(999);
        Assert.True(resultado.IsError);
        Assert.Contains("No se encontró el producto", resultado.Message);
    }

    // ---------- Movimientos bloqueados mientras está inactivo ----------

    [Fact]
    public void RegistrarEntrada_ProductoInactivo_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        var producto = CrearProducto(bd, "A1", precio: 2_000, costo: 1_000, stock: 7);
        Assert.True(bd.Inventario.DesactivarProducto(producto.Id).IsSuccess);

        var entrada = bd.Inventario.RegistrarEntrada(producto.Id, 3, 1_000, "Dueño", "Compra");
        Assert.True(entrada.IsError);
        Assert.Contains("desactivado", entrada.Message);
        Assert.Equal(7, bd.Inventario.BuscarPorId(producto.Id).Stock);
    }

    [Fact]
    public void RegistrarMerma_ProductoInactivo_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        var producto = CrearProducto(bd, "A1", precio: 2_000, costo: 1_000, stock: 7);
        Assert.True(bd.Inventario.DesactivarProducto(producto.Id).IsSuccess);

        var merma = bd.Inventario.RegistrarMerma(producto.Id, 2, "Caducidad", "Dueño");
        Assert.True(merma.IsError);
        Assert.Contains("desactivado", merma.Message);
        Assert.Equal(7, bd.Inventario.BuscarPorId(producto.Id).Stock);
    }

    [Fact]
    public void AjustarStock_ProductoInactivo_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        var producto = CrearProducto(bd, "A1", precio: 2_000, costo: 1_000, stock: 7);
        Assert.True(bd.Inventario.DesactivarProducto(producto.Id).IsSuccess);

        var ajuste = bd.Inventario.AjustarStock(producto.Id, 10, "Conteo físico", "Dueño");
        Assert.True(ajuste.IsError);
        Assert.Contains("desactivado", ajuste.Message);
        Assert.Equal(7, bd.Inventario.BuscarPorId(producto.Id).Stock);
    }

    // ---------- Búsquedas y alertas ----------

    [Fact]
    public void BuscarPorNombre_ConFiltroDeActivos_OcultaLosInactivos()
    {
        using var bd = new BaseDatosPrueba();
        var activo = CrearProducto(bd, "A1", precio: 2_000, costo: 1_000, stock: 7);
        var inactivo = CrearProducto(bd, "B2", precio: 2_000, costo: 1_000, stock: 7);
        Assert.True(bd.Inventario.DesactivarProducto(inactivo.Id).IsSuccess);

        var todos = bd.Inventario.BuscarPorNombre("Producto");
        var soloActivos = bd.Inventario.BuscarPorNombre("Producto", soloActivos: true);

        Assert.Equal(2, todos.Count);
        var activos = Assert.Single(soloActivos);
        Assert.Equal(activo.Id, activos.Id);
    }

    [Fact]
    public void ProductosStockBajo_SoloActivos_ExcluyeLosInactivos()
    {
        using var bd = new BaseDatosPrueba();
        var activo = CrearProducto(bd, "A1", precio: 2_000, costo: 1_000, stock: 2);
        var inactivo = CrearProducto(bd, "B2", precio: 2_000, costo: 1_000, stock: 2);
        Assert.True(bd.Inventario.DesactivarProducto(inactivo.Id).IsSuccess);

        var conInactivos = bd.Inventario.ProductosStockBajo();
        var soloActivos = bd.Inventario.ProductosStockBajo(soloActivos: true);

        Assert.Equal(2, conInactivos.Count);
        var bajos = Assert.Single(soloActivos);
        Assert.Equal(activo.Id, bajos.Id);
    }

    [Fact]
    public void BuscarPorCodigo_ProductoInactivo_SigueLocalizableParaConsultarSuEstado()
    {
        using var bd = new BaseDatosPrueba();
        var producto = CrearProducto(bd, "12345678", precio: 2_000, costo: 1_000, stock: 7);
        Assert.True(bd.Inventario.DesactivarProducto(producto.Id).IsSuccess);

        var encontrado = bd.Inventario.BuscarPorCodigo("12345678");
        Assert.NotNull(encontrado);
        Assert.False(encontrado.Activo);
    }

    private sealed class EntornoMigracionV7 : IDisposable
    {
        private readonly string _directorio;

        public string RutaDb { get; }
        public DatabaseTools Db { get; }

        public EntornoMigracionV7()
        {
            _directorio = Path.Combine(Path.GetTempPath(), $"sistematienda-v7-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_directorio);
            RutaDb = Path.Combine(_directorio, "tienda.db");
            Db = new DatabaseTools(RutaDb);
            Db.InicializarBaseDeDatos();
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            try
            {
                if (Directory.Exists(_directorio))
                    Directory.Delete(_directorio, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
