using Microsoft.Data.Sqlite;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Tests;

/// <summary>
/// Pruebas de la tarea T1: migraciones versionadas con respaldo obligatorio
/// y servicio local de respaldo y restauración.
/// </summary>
public sealed class MigracionesRespaldoTests
{
    // DDL del esquema v1, tal como lo creaba DatabaseTools antes de T1.
    // Es la figura de una base de datos antigua que debe migrar.
    private const string DdlV1 = @"
        CREATE TABLE IF NOT EXISTS ConfiguracionTienda (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            NombreTienda TEXT NOT NULL DEFAULT '',
            Giro TEXT NOT NULL DEFAULT '',
            ImpuestoPredeterminado DECIMAL NOT NULL DEFAULT 16.00,
            ComisionTarjeta DECIMAL NOT NULL DEFAULT 3.50,
            FondoCajaInicial DECIMAL NOT NULL DEFAULT 200.00,
            FiadoActivado INTEGER NOT NULL DEFAULT 0,
            LimiCreditoCliente DECIMAL NOT NULL DEFAULT 0,
            RespaldoIntervaloDias INTEGER NOT NULL DEFAULT 1,
            UsuarioDueñoPINHash TEXT NOT NULL DEFAULT '',
            FechaCreacion DATETIME NOT NULL DEFAULT (datetime('now')),
            VersionApp TEXT NOT NULL DEFAULT ''
        );
        CREATE TABLE IF NOT EXISTS UsuariosSistema (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Nombre TEXT NOT NULL DEFAULT '',
            PINHash TEXT NOT NULL DEFAULT '',
            Rol INTEGER NOT NULL DEFAULT 2,
            PermisosEspeciales TEXT NOT NULL DEFAULT '',
            FechaCreacion DATETIME NOT NULL DEFAULT (datetime('now'))
        );
        CREATE TABLE IF NOT EXISTS Productos (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            CodigoBarras TEXT NOT NULL UNIQUE,
            Nombre TEXT NOT NULL,
            Costo DECIMAL NOT NULL DEFAULT 0,
            Precio DECIMAL NOT NULL DEFAULT 0,
            Stock INTEGER NOT NULL DEFAULT 0,
            StockMinimo INTEGER NOT NULL DEFAULT 5,
            Categoria TEXT NOT NULL DEFAULT '',
            Impuesto DECIMAL NOT NULL DEFAULT 16.00,
            FechaCreacion DATETIME NOT NULL DEFAULT (datetime('now')),
            UltimaActualizacion DATETIME
        );
        CREATE TABLE IF NOT EXISTS MovimientoInventario (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            ProductoId INTEGER NOT NULL,
            Tipo TEXT NOT NULL,
            Cantidad INTEGER NOT NULL,
            CostoUnitario DECIMAL NOT NULL DEFAULT 0,
            Motivo TEXT NOT NULL DEFAULT '',
            Usuario TEXT NOT NULL DEFAULT '',
            Fecha DATETIME NOT NULL DEFAULT (datetime('now')),
            FOREIGN KEY (ProductoId) REFERENCES Productos(Id)
        );
        CREATE TABLE IF NOT EXISTS Gastos (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Concepto TEXT NOT NULL DEFAULT '',
            Monto DECIMAL NOT NULL DEFAULT 0,
            Fecha DATETIME NOT NULL DEFAULT (datetime('now')),
            MetodoPago TEXT NOT NULL DEFAULT '',
            Origen TEXT NOT NULL DEFAULT 'Caja',
            Observaciones TEXT NOT NULL DEFAULT ''
        );
        CREATE TABLE IF NOT EXISTS ClientesFiado (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Nombre TEXT NOT NULL DEFAULT '',
            Telefono TEXT NOT NULL DEFAULT '',
            SaldoActual DECIMAL NOT NULL DEFAULT 0,
            FechaRegistro DATETIME NOT NULL DEFAULT (datetime('now')),
            UltimaActualizacion DATETIME
        );
        CREATE TABLE IF NOT EXISTS Proveedores (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Nombre TEXT NOT NULL DEFAULT '',
            Clave TEXT NOT NULL DEFAULT '',
            Telefono TEXT NOT NULL DEFAULT '',
            Direccion TEXT NOT NULL DEFAULT '',
            SaldoPendiente DECIMAL NOT NULL DEFAULT 0,
            FechaRegistro DATETIME NOT NULL DEFAULT (datetime('now')),
            UltimaActualizacion DATETIME
        );";

    /// <summary>
    /// Crea un archivo de base v1 con una tienda y un producto con stock,
    /// en una carpeta temporal propia para que sus respaldos no se mezclen
    /// con los de otras pruebas.
    /// </summary>
    private static string CrearBaseV1()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), $"sistematienda-v1-{Guid.NewGuid():N}");
        Directory.CreateDirectory(carpeta);
        var ruta = Path.Combine(carpeta, "tienda.db");
        using (var conn = new SqliteConnection($"Data Source={ruta}"))
        {
            conn.Open();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = DdlV1;
                cmd.ExecuteNonQuery();
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT INTO ConfiguracionTienda (NombreTienda, Giro, VersionApp)
                    VALUES ('Tienda vieja', 'Abarrotes', 'v0.1.0');
                    INSERT INTO Productos (CodigoBarras, Nombre, Costo, Precio, Stock, StockMinimo, Categoria, Impuesto)
                    VALUES ('7501000000010', 'Frijol', 10, 15, 5, 2, 'Granos', 16);
                    INSERT INTO MovimientoInventario (ProductoId, Tipo, Cantidad, CostoUnitario, Motivo, Usuario)
                    VALUES (1, 'Entrada', 5, 10, 'Stock inicial', 'Sistema');";
                cmd.ExecuteNonQuery();
            }

            using var version = conn.CreateCommand();
            version.CommandText = "PRAGMA user_version = 1";
            version.ExecuteNonQuery();
        }

        return ruta;
    }

    private static void BorrarBase(string ruta)
    {
        SqliteConnection.ClearAllPools();
        var carpeta = Path.GetDirectoryName(ruta) ?? ".";
        if (Directory.Exists(carpeta)) Directory.Delete(carpeta, recursive: true);
    }

    private static int ContarRespaldos(string rutaBase)
    {
        var carpeta = Path.Combine(Path.GetDirectoryName(rutaBase) ?? ".", "respaldos");
        return Directory.Exists(carpeta) ? Directory.GetFiles(carpeta, "tienda-*.db").Length : 0;
    }

    [Fact]
    public void InicializarBaseDeDatos_BaseNueva_QuedaEnVersion2()
    {
        using var bd = new BaseDatosPrueba();

        Assert.Equal(2, bd.Db.ObtenerVersionEsquema());

        var tablaHistorial = bd.Db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'MigracionesAplicadas'");
        Assert.Equal(1, tablaHistorial);

        var columnaActivo = bd.Db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM pragma_table_info('Productos') WHERE name = 'Activo'");
        Assert.Equal(1, columnaActivo);
    }

    [Fact]
    public void Migrar_BaseV1_MigraAV2YConservaDatos()
    {
        var ruta = CrearBaseV1();
        try
        {
            var db = new DatabaseTools(ruta);
            db.InicializarBaseDeDatos();

            Assert.Equal(2, db.ObtenerVersionEsquema());
            Assert.Equal("Tienda vieja", db.ObtenerConfiguracion().NombreTienda);

            var producto = Assert.Single(db.ObtenerProductos());
            Assert.Equal("Frijol", producto.Nombre);
            Assert.Equal(5, producto.Stock);
            Assert.True(producto.Activo);
            Assert.Equal(5, db.ObtenerStock(producto.Id));

            var migracion = db.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM MigracionesAplicadas WHERE Version = 2");
            Assert.Equal(1, migracion);

            // El respaldo previo a migrar existe y sigue siendo la base v1.
            Assert.Equal(1, ContarRespaldos(ruta));
            var respaldo = Directory.GetFiles(
                Path.Combine(Path.GetDirectoryName(ruta) ?? ".", "respaldos"), "tienda-*.db")[0];
            var dbRespaldo = new DatabaseTools(respaldo);
            Assert.Equal(1, dbRespaldo.ObtenerVersionEsquema());
            Assert.Equal("Frijol", Assert.Single(dbRespaldo.ObtenerProductos()).Nombre);
        }
        finally
        {
            BorrarBase(ruta);
        }
    }

    [Fact]
    public void Migrar_BaseYaEnV2_EsIdempotenteYSinRespaldoNuevo()
    {
        using var bd = new BaseDatosPrueba();
        var respaldosAntes = ContarRespaldos(bd.Ruta);

        bd.Db.InicializarBaseDeDatos();
        bd.Db.InicializarBaseDeDatos();

        Assert.Equal(2, bd.Db.ObtenerVersionEsquema());
        Assert.Equal(respaldosAntes, ContarRespaldos(bd.Ruta));
    }

    [Fact]
    public void CrearRespaldo_RestaurarRespaldo_RecuperaDatosIdenticos()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda(pin: "1234");
        Assert.True(bd.Inventario.AgregarProducto(new Producto
        {
            CodigoBarras = "7501000000020",
            Nombre = "Arroz",
            Costo = 12m,
            Precio = 18m,
            Stock = 8,
            StockMinimo = 2,
            Categoria = "Granos",
            Impuesto = 16m
        }).IsSuccess);

        var respaldo = new RespaldoService(bd.Db);
        var creado = respaldo.CrearRespaldo();

        Assert.True(creado.IsSuccess, creado.Message);
        Assert.True(File.Exists(creado.Value));
        Assert.True(respaldo.VerificarRespaldo(creado.Value).IsSuccess);

        // Se modifica la base después del respaldo y luego se restaura.
        Assert.True(bd.Inventario.AgregarProducto(new Producto
        {
            CodigoBarras = "7501000000030",
            Nombre = "Azúcar",
            Costo = 9m,
            Precio = 14m,
            Stock = 3,
            StockMinimo = 1,
            Categoria = "Granos",
            Impuesto = 16m
        }).IsSuccess);
        Assert.Equal(2, bd.Inventario.ObtenerProductos().Count);

        var restaurado = respaldo.RestaurarRespaldo(creado.Value);
        Assert.True(restaurado.IsSuccess, restaurado.Message);

        var productos = bd.Inventario.ObtenerProductos();
        var arroz = Assert.Single(productos);
        Assert.Equal("Arroz", arroz.Nombre);
        Assert.Equal(8, arroz.Stock);
        Assert.Equal("Tienda de prueba", bd.Db.ObtenerConfiguracion().NombreTienda);
    }

    [Fact]
    public void Migrar_MigracionFallida_DejaOriginalIntacto()
    {
        var ruta = CrearBaseV1();
        try
        {
            var db = new DatabaseTools(ruta);
            var migracionRota = new Migracion
            {
                DesdeVersion = 1,
                HastaVersion = 2,
                Nombre = "Migración rota de prueba",
                Aplicar = (_, _) => throw new InvalidOperationException("falla simulada")
            };

            var resultado = new MigracionRunner(db, new[] { migracionRota }).Migrar();

            Assert.True(resultado.IsError);
            Assert.Equal(1, db.ObtenerVersionEsquema());
            Assert.Equal("Tienda vieja", db.ObtenerConfiguracion().NombreTienda);
            Assert.Equal("Frijol", Assert.Single(db.ObtenerProductos()).Nombre);
            Assert.Equal(5, db.ObtenerStock(1));
            Assert.Equal(1, ContarRespaldos(ruta));
        }
        finally
        {
            BorrarBase(ruta);
        }
    }

    [Fact]
    public void RestaurarRespaldo_ArchivoCorrupto_NoModificaBaseActual()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda(pin: "1234");

        var respaldo = new RespaldoService(bd.Db);
        var creado = respaldo.CrearRespaldo();
        Assert.True(creado.IsSuccess, creado.Message);

        File.WriteAllText(creado.Value, "esto no es una base de datos");

        var restaurado = respaldo.RestaurarRespaldo(creado.Value);
        Assert.True(restaurado.IsError);
        Assert.Equal("Tienda de prueba", bd.Db.ObtenerConfiguracion().NombreTienda);
    }

    [Fact]
    public void CrearRespaldo_DosVeces_GeneraArchivosDistintosYVerificados()
    {
        using var bd = new BaseDatosPrueba();
        var respaldo = new RespaldoService(bd.Db);

        var primero = respaldo.CrearRespaldo();
        var segundo = respaldo.CrearRespaldo();

        Assert.True(primero.IsSuccess, primero.Message);
        Assert.True(segundo.IsSuccess, segundo.Message);
        Assert.NotEqual(primero.Value, segundo.Value);

        var lista = respaldo.ListarRespaldos();
        Assert.Contains(lista, r => r.Ruta == primero.Value);
        Assert.Contains(lista, r => r.Ruta == segundo.Value);
        Assert.All(lista.Where(r => r.Ruta == primero.Value || r.Ruta == segundo.Value),
            r => Assert.True(respaldo.VerificarRespaldo(r.Ruta).IsSuccess));
        Assert.Equal(2, respaldo.ObtenerUltimoRespaldo().VersionEsquema);
        Assert.NotNull(respaldo.ObtenerFechaUltimoRespaldo());
    }

    [Fact]
    public void Migrar_BaseMasNuevaQueLaAplicacion_FallaSinModificar()
    {
        var ruta = CrearBaseV1();
        try
        {
            using (var conn = new SqliteConnection($"Data Source={ruta}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA user_version = 99";
                cmd.ExecuteNonQuery();
            }

            var db = new DatabaseTools(ruta);
            var respaldosAntes = ContarRespaldos(ruta);
            var resultado = new MigracionRunner(db, MigracionesRegistradas.Lista()).Migrar();

            Assert.True(resultado.IsError);
            Assert.Equal(99, db.ObtenerVersionEsquema());
            Assert.Equal(respaldosAntes, ContarRespaldos(ruta));
        }
        finally
        {
            BorrarBase(ruta);
        }
    }
}
