using Microsoft.Data.Sqlite;
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Tests;

/// <summary>
/// H5: manifiesto de versión y actualización con respaldo, verificación y
/// reversión. Una actualización fallida deja la versión anterior operativa.
/// </summary>
public sealed class ActualizacionTests
{
    private static int Version(BaseDatosPrueba bd) =>
        bd.Db.ExecuteScalar<int>("PRAGMA user_version");

    [Fact]
    public void Manifiesto_SeEscribeYLee()
    {
        var directorio = Path.Combine(Path.GetTempPath(), $"manifiesto-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directorio);
        var ruta = Path.Combine(directorio, "manifiesto.json");

        try
        {
            var original = new ManifiestoSistema
            {
                VersionApp = "1.2.3",
                VersionEsquema = 17,
                Generado = new DateTime(2026, 11, 25, 9, 0, 0)
            };

            original.Escribir(ruta);
            var leido = ManifiestoSistema.Leer(ruta);

            Assert.Equal("SistemaTienda", leido.Producto);
            Assert.Equal("1.2.3", leido.VersionApp);
            Assert.Equal(17, leido.VersionEsquema);
            Assert.Equal(original.Generado, leido.Generado);
        }
        finally
        {
            Directory.Delete(directorio, recursive: true);
        }
    }

    [Fact]
    public void ManifiestoActual_DeclaraElEsquemaQueConoceLaApp()
    {
        var manifiesto = ManifiestoSistema.Actual();

        Assert.Equal(Migraciones.VersionActual, manifiesto.VersionEsquema);
        Assert.False(string.IsNullOrWhiteSpace(manifiesto.VersionApp));
        Assert.Equal("SistemaTienda", manifiesto.Producto);
    }

    [Fact]
    public void BaseAlDia_NoCreaRespaldoNiCambiaNada()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var respaldosAntes = RespaldoLocal.Listar(bd.Db.DirectorioRespaldos).Count;

        var resultado = new ActualizadorEsquema(bd.Ruta).Aplicar();

        Assert.True(resultado.YaAlDia);
        Assert.False(resultado.Actualizada);
        Assert.Equal(Migraciones.VersionActual, resultado.VersionFinal);
        Assert.Equal(respaldosAntes, RespaldoLocal.Listar(bd.Db.DirectorioRespaldos).Count);
    }

    [Fact]
    public void EsquemaViejo_SeActualizaYVerifica()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        bd.Db.ExecuteCommand("DROP TABLE CompraProveedorLineas");
        bd.Db.ExecuteCommand("DROP TABLE ComprasProveedor");
        bd.Db.ExecuteCommand("PRAGMA user_version = 12");

        var resultado = new ActualizadorEsquema(bd.Ruta).Aplicar();

        Assert.True(resultado.Actualizada, resultado.Mensaje);
        Assert.False(resultado.Restaurada);
        Assert.Equal(Migraciones.VersionActual, resultado.VersionFinal);
        Assert.True(File.Exists(resultado.RutaRespaldo));
        Assert.True(ActualizadorEsquema.VerificarBaseDatos(bd.Ruta));
        Assert.Equal(1, bd.Db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ComprasProveedor'"));
    }

    [Fact]
    public void MigracionFallida_RestauraLaVersionAnterior()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = new Producto
        {
            CodigoBarras = "H5-FALLA",
            Nombre = "Producto H5",
            Costo = 1_000,
            Precio = 2_000,
            Stock = 3
        };
        Assert.True(bd.Inventario.AgregarProducto(producto).IsSuccess);
        var versionAnterior = Version(bd);

        var falla = new Migracion(versionAnterior + 1, "falla a propósito", (conn, tx) =>
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO TablaInexistente VALUES (1);";
            cmd.ExecuteNonQuery();
        });

        var resultado = new ActualizadorEsquema(bd.Ruta, null, new[] { falla }).Aplicar();

        Assert.False(resultado.Actualizada);
        Assert.True(resultado.Restaurada, resultado.Mensaje);
        Assert.Equal(versionAnterior, Version(bd));
        Assert.Equal(3, bd.Db.ObtenerStock(producto.Id));
        Assert.True(ActualizadorEsquema.VerificarBaseDatos(bd.Ruta));
    }

    [Fact]
    public void VerificacionFallida_RestauraLaVersionAnterior()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var versionAnterior = Version(bd);

        // Migración que "funciona" pero deja la base inservible: sin bitácora.
        var rompeVerificacion = new Migracion(versionAnterior + 1, "rompe la verificación", (conn, tx) =>
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "DROP TABLE Bitacora;";
            cmd.ExecuteNonQuery();
        });

        var resultado = new ActualizadorEsquema(bd.Ruta, null, new[] { rompeVerificacion }).Aplicar();

        Assert.False(resultado.Actualizada);
        Assert.True(resultado.Restaurada, resultado.Mensaje);
        Assert.Equal(versionAnterior, Version(bd));
        Assert.Equal(1, bd.Db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Bitacora'"));
        Assert.True(ActualizadorEsquema.VerificarBaseDatos(bd.Ruta));
    }

    [Fact]
    public void BaseConEsquemaMasNuevo_SeRechazaSinTocarNada()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        bd.Db.ExecuteCommand("PRAGMA user_version = 99");
        var respaldosAntes = RespaldoLocal.Listar(bd.Db.DirectorioRespaldos).Count;

        var resultado = new ActualizadorEsquema(bd.Ruta).Aplicar();

        Assert.False(resultado.Actualizada);
        Assert.False(resultado.Restaurada);
        Assert.Equal(99, Version(bd));
        Assert.Equal(respaldosAntes, RespaldoLocal.Listar(bd.Db.DirectorioRespaldos).Count);
    }

    [Fact]
    public void VerificarBaseDatos_DetectaBaseCorrupta()
    {
        var directorio = Path.Combine(Path.GetTempPath(), $"corrupta-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directorio);
        var ruta = Path.Combine(directorio, "tienda.db");

        try
        {
            using (var conn = new SqliteConnection($"Data Source={ruta}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "CREATE TABLE SoloEsto (Id INTEGER);";
                cmd.ExecuteNonQuery();
            }

            Assert.False(ActualizadorEsquema.VerificarBaseDatos(ruta));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directorio, recursive: true);
        }
    }
}
