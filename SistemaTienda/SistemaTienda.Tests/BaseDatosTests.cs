namespace SistemaTienda.Tests;

public sealed class BaseDatosTests
{
    [Fact]
    public void InicializarBaseDeDatos_EsIdempotenteYCreaConfiguracionUnica()
    {
        using var bd = new BaseDatosPrueba();

        bd.Db.InicializarBaseDeDatos();
        bd.Db.InicializarBaseDeDatos();

        Assert.Equal(1, bd.Db.ExecuteScalar<int>("SELECT COUNT(*) FROM ConfiguracionTienda"));
        Assert.Equal(1, bd.Db.ExecuteScalar<int>("PRAGMA user_version"));
    }

    [Fact]
    public void InicializarBaseDeDatos_CreaTodasLasTablasBase()
    {
        using var bd = new BaseDatosPrueba();

        foreach (var tabla in new[]
        {
            "ConfiguracionTienda", "UsuariosSistema", "Productos",
            "MovimientoInventario", "Gastos", "ClientesFiado", "Proveedores"
        })
        {
            var existe = bd.Db.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @nombre",
                new Microsoft.Data.Sqlite.SqliteParameter("@nombre", tabla));

            Assert.True(existe == 1, $"Falta la tabla {tabla}");
        }
    }

    [Fact]
    public void InicializarBaseDeDatos_NoBorraDatosExistentes()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda(pin: "1234");

        bd.Db.InicializarBaseDeDatos();

        Assert.Equal(1, bd.Db.ContarUsuarios());
        Assert.Equal("Tienda de prueba", bd.Db.ObtenerConfiguracion().NombreTienda);
    }

    [Fact]
    public void MapeoDeFilas_ConvierteBooleanosDecimalesYFechas()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda(pin: "1234", fiado: true);

        bd.Db.ExecuteCommand(@"
            UPDATE ConfiguracionTienda
            SET FechaCreacion = '2024-01-15 08:30:00',
                ImpuestoPredeterminado = 8.25
            WHERE Id = 1");

        var config = bd.Db.ObtenerConfiguracion();

        Assert.True(config.FiadoActivado);
        Assert.Equal(8.25m, config.ImpuestoPredeterminado);
        Assert.Equal(new DateTime(2024, 1, 15, 8, 30, 0), config.FechaCreacion);
    }
}
