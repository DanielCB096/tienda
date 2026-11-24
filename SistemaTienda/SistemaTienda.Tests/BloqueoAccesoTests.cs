using SistemaTienda.Application.Services;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Tests;

public sealed class BloqueoAccesoTests
{
    [Fact]
    public void CincoIntentosFallidos_BloqueanElAcceso()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda(pin: "1234");

        for (var i = 1; i <= 4; i++)
        {
            var fallo = bd.Config.VerificarAcceso("0000");
            Assert.True(fallo.IsError);
            Assert.Contains($"Intento {i} de 5", fallo.Message);
        }

        var quinto = bd.Config.VerificarAcceso("0000");
        Assert.True(quinto.IsError);
        Assert.Contains("bloqueado", quinto.Message);

        var correcto = bd.Config.VerificarAcceso("1234");
        Assert.True(correcto.IsError);
        Assert.Contains("bloqueado", correcto.Message);
    }

    [Fact]
    public void PinCorrecto_ReiniciaElContadorDeIntentos()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda(pin: "1234");

        Assert.True(bd.Config.VerificarAcceso("0000").IsError);
        Assert.True(bd.Config.VerificarAcceso("0000").IsError);
        Assert.True(bd.Config.VerificarAcceso("1234").IsSuccess);

        var fallo = bd.Config.VerificarAcceso("0000");
        Assert.Contains("Intento 1 de 5", fallo.Message);
    }

    [Fact]
    public void Bloqueo_SobreviveAUnNuevoAccesoALaBase()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda(pin: "1234");

        for (var i = 0; i < 5; i++)
            Assert.True(bd.Config.VerificarAcceso("0000").IsError);

        var otroAcceso = new DatabaseTools(bd.Ruta);
        var servicio = new ConfiguracionService(otroAcceso);
        var acceso = servicio.VerificarAcceso("1234");

        Assert.True(acceso.IsError);
        Assert.Contains("bloqueado", acceso.Message);
    }

    [Fact]
    public void Configuracion_RechazaMinutosDeInactividadFueraDeRango()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var config = bd.Config.ObtenerConfiguracion().Value;

        config.MinutosInactividad = 1;
        Assert.True(bd.Config.ActualizarConfiguracion(config).IsError);

        config.MinutosInactividad = 20;
        Assert.True(bd.Config.ActualizarConfiguracion(config).IsError);

        config.MinutosInactividad = 5;
        Assert.True(bd.Config.ActualizarConfiguracion(config).IsSuccess);
    }

    [Fact]
    public void MigracionQuince_DejaInactividadYEstadoDeAccesoListos()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        Assert.Equal(15, bd.Db.ExecuteScalar<long>("PRAGMA user_version"));
        Assert.Equal(5, bd.Config.ObtenerConfiguracion().Value.MinutosInactividad);

        var estado = bd.Db.ObtenerEstadoAcceso();
        Assert.Equal(0, estado.Fallos);
        Assert.Null(estado.BloqueadoHasta);
    }
}
