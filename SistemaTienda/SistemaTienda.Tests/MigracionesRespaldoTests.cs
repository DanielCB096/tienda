using Microsoft.Data.Sqlite;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Tests;

public sealed class MigracionesRespaldoTests
{
    private static Migracion MigracionCreaTabla(int version = 4) =>
        new(version, "crea TablaPrueba", (conn, tx) =>
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "CREATE TABLE TablaPrueba (Id INTEGER PRIMARY KEY, Nota TEXT NOT NULL DEFAULT '')";
            cmd.ExecuteNonQuery();
        });

    [Fact]
    public void Migrar_BaseV1_AplicaPendientesYRespaldaAntes()
    {
        using var entorno = new EntornoMigracion();
        var migrador = new MigradorEsquema(entorno.RutaDb, new[] { MigracionCreaTabla() }, entorno.DirectorioRespaldos);

        var resultado = migrador.Migrar();

        Assert.True(resultado.IsSuccess, resultado.Message);
        Assert.Equal(3, resultado.Value.VersionInicial);
        Assert.Equal(4, resultado.Value.VersionFinal);
        Assert.Equal(new[] { 4 }, resultado.Value.VersionesAplicadas);
        Assert.True(resultado.Value.HuboCambios);

        Assert.True(File.Exists(resultado.Value.RutaRespaldo));
        Assert.True(RespaldoLocal.Verificar(resultado.Value.RutaRespaldo));
        Assert.Equal(4, entorno.Db.ExecuteScalar<int>("PRAGMA user_version"));
        Assert.Equal(1, entorno.Db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'TablaPrueba'"));
    }

    [Fact]
    public void Migrar_SinPendientes_EsIdempotenteYNoCreaOtroRespaldo()
    {
        using var entorno = new EntornoMigracion();
        var migrador = new MigradorEsquema(entorno.RutaDb, new[] { MigracionCreaTabla() }, entorno.DirectorioRespaldos);

        Assert.True(migrador.Migrar().IsSuccess);
        var respaldosTrasPrimera = RespaldoLocal.Listar(entorno.DirectorioRespaldos).Count;

        var segunda = migrador.Migrar();

        Assert.True(segunda.IsSuccess, segunda.Message);
        Assert.False(segunda.Value.HuboCambios);
        Assert.Empty(segunda.Value.VersionesAplicadas);
        Assert.Equal(respaldosTrasPrimera, RespaldoLocal.Listar(entorno.DirectorioRespaldos).Count);
        Assert.Equal(4, entorno.Db.ExecuteScalar<int>("PRAGMA user_version"));
    }

    [Fact]
    public void Migrar_SiUnaMigracionFalla_DejaLaVersionYLosDatosIntactos()
    {
        using var entorno = new EntornoMigracion();

        var migracionQueFalla = new Migracion(4, "falla a propósito", (conn, tx) =>
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                CREATE TABLE TablaParcial (Id INTEGER PRIMARY KEY);
                INSERT INTO TablaInexistente VALUES (1);";
            cmd.ExecuteNonQuery();
        });

        var migrador = new MigradorEsquema(entorno.RutaDb, new[] { migracionQueFalla }, entorno.DirectorioRespaldos);
        var resultado = migrador.Migrar();

        Assert.True(resultado.IsError);
        Assert.Equal(3, entorno.Db.ExecuteScalar<int>("PRAGMA user_version"));
        Assert.Equal(0, entorno.Db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'TablaParcial'"));
        Assert.Equal(1, entorno.Db.ExecuteScalar<int>("SELECT COUNT(*) FROM ConfiguracionTienda"));

        var respaldos = RespaldoLocal.Listar(entorno.DirectorioRespaldos);
        var respaldo = Assert.Single(respaldos);
        Assert.True(RespaldoLocal.Verificar(respaldo.Ruta));
    }

    [Fact]
    public void Migrar_BaseMasNuevaQueLaApp_SeRechazaSinTocarNada()
    {
        using var entorno = new EntornoMigracion();
        entorno.Db.ExecuteCommand("PRAGMA user_version = 99");

        var migrador = new MigradorEsquema(entorno.RutaDb, new[] { MigracionCreaTabla() }, entorno.DirectorioRespaldos);
        var resultado = migrador.Migrar();

        Assert.True(resultado.IsError);
        Assert.Equal(99, entorno.Db.ExecuteScalar<int>("PRAGMA user_version"));
        Assert.Empty(RespaldoLocal.Listar(entorno.DirectorioRespaldos));
    }

    [Fact]
    public void Migrador_RechazaVersionesInvalidasODuplicadas()
    {
        var ruta = Path.Combine(Path.GetTempPath(), $"no-se-usa-{Guid.NewGuid():N}.db");

        Assert.Throws<ArgumentException>(() =>
            new MigradorEsquema(ruta, new[] { MigracionCreaTabla(version: 1) }));

        Assert.Throws<ArgumentException>(() =>
            new MigradorEsquema(ruta, new[] { MigracionCreaTabla(4), MigracionCreaTabla(4) }));
    }

    [Fact]
    public void Respaldo_SeCreaVerificaYLista()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var servicio = new RespaldoService(bd.Db);

        var creado = servicio.CrearRespaldo("manual");

        Assert.True(creado.IsSuccess, creado.Message);
        Assert.True(File.Exists(creado.Value));
        Assert.True(RespaldoLocal.Verificar(creado.Value));
        Assert.Contains(RespaldoLocal.Listar(bd.Db.DirectorioRespaldos), r => r.Ruta == creado.Value);
        Assert.True(servicio.ObtenerUltimoRespaldo().Value.HasValue);
    }

    [Fact]
    public void Restaurar_DevuelveLaBaseAlEstadoDelRespaldo()
    {
        using var bd = new BaseDatosPrueba();
        bd.Inventario.AgregarProducto(new Producto
        {
            CodigoBarras = "A1", Nombre = "Antes", Costo = 1m, Precio = 2m, Stock = 5
        });

        var servicio = new RespaldoService(bd.Db);
        var respaldo = servicio.CrearRespaldo("prueba");
        Assert.True(respaldo.IsSuccess, respaldo.Message);

        bd.Inventario.AgregarProducto(new Producto
        {
            CodigoBarras = "A2", Nombre = "Después", Costo = 1m, Precio = 2m, Stock = 3
        });
        Assert.Equal(2, bd.Db.ObtenerProductos().Count);

        var restauracion = servicio.Restaurar(respaldo.Value, confirmado: true);

        Assert.True(restauracion.IsSuccess, restauracion.Message);
        var producto = Assert.Single(bd.Db.ObtenerProductos());
        Assert.Equal("A1", producto.CodigoBarras);
        Assert.Equal(5, producto.Stock);

        // Además del respaldo original, queda el de seguridad previo a restaurar.
        Assert.True(RespaldoLocal.Listar(bd.Db.DirectorioRespaldos).Count >= 2);
    }

    [Fact]
    public void Restaurar_SinConfirmacionOFueraDeRuta_Falla()
    {
        using var bd = new BaseDatosPrueba();
        var servicio = new RespaldoService(bd.Db);
        var respaldo = servicio.CrearRespaldo("prueba");

        Assert.True(servicio.Restaurar(respaldo.Value, confirmado: false).IsError);
        Assert.True(servicio.Restaurar(Path.Combine(bd.Db.DirectorioRespaldos, "no-existe.db"), true).IsError);
    }

    [Fact]
    public void Inicializar_BaseNueva_QuedaEnLaVersionObjetivoSinRespaldo()
    {
        using var entorno = new EntornoMigracion();

        Assert.Equal(Migraciones.VersionMaxima(Migraciones.ObtenerTodas()),
            entorno.Db.ExecuteScalar<int>("PRAGMA user_version"));
        Assert.Empty(RespaldoLocal.Listar(entorno.DirectorioRespaldos));
        Assert.Empty(entorno.Db.UltimoRespaldoCreado);
    }

    private sealed class EntornoMigracion : IDisposable
    {
        public string Directorio { get; }
        public string RutaDb { get; }
        public DatabaseTools Db { get; }
        public string DirectorioRespaldos => Db.DirectorioRespaldos;

        public EntornoMigracion()
        {
            Directorio = Path.Combine(Path.GetTempPath(), $"sistematienda-mig-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Directorio);
            RutaDb = Path.Combine(Directorio, "tienda.db");
            Db = new DatabaseTools(RutaDb);
            Db.InicializarBaseDeDatos();
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            try
            {
                if (Directory.Exists(Directorio))
                    Directory.Delete(Directorio, recursive: true);
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
