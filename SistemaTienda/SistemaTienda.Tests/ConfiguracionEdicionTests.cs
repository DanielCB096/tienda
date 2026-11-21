using Microsoft.Data.Sqlite;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;

namespace SistemaTienda.Tests;

public sealed class ConfiguracionEdicionTests
{
    private static ConfiguracionTienda Copiar(ConfiguracionTienda origen) => new()
    {
        Id = origen.Id,
        NombreTienda = origen.NombreTienda,
        Giro = origen.Giro,
        ImpuestoPredeterminado = origen.ImpuestoPredeterminado,
        ComisionTarjeta = origen.ComisionTarjeta,
        FondoCajaInicial = origen.FondoCajaInicial,
        FiadoActivado = origen.FiadoActivado,
        LimiCreditoCliente = origen.LimiCreditoCliente,
        RespaldoIntervaloDias = origen.RespaldoIntervaloDias,
        VersionApp = origen.VersionApp,
        FechaCreacion = origen.FechaCreacion
    };

    [Fact]
    public void ActualizarConfiguracion_ComoDuenio_GuardaTodosLosCamposYBitacora()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda(fiado: false);
        var config = Copiar(bd.Config.ObtenerConfiguracion().Value);
        config.NombreTienda = "Tienda Editada";
        config.Giro = "Ferretería";
        config.ImpuestoPredeterminado = 8m;
        config.ComisionTarjeta = 2.5m;
        config.FondoCajaInicial = 30_000;
        config.FiadoActivado = true;
        config.LimiCreditoCliente = 100_000;
        config.RespaldoIntervaloDias = 7;

        var resultado = bd.Config.ActualizarConfiguracion(config);

        Assert.True(resultado.IsSuccess, resultado.Message);
        var actual = bd.Config.ObtenerConfiguracion().Value;
        Assert.Equal("Tienda Editada", actual.NombreTienda);
        Assert.Equal("Ferretería", actual.Giro);
        Assert.Equal(8m, actual.ImpuestoPredeterminado);
        Assert.Equal(2.5m, actual.ComisionTarjeta);
        Assert.Equal(30_000, actual.FondoCajaInicial);
        Assert.True(actual.FiadoActivado);
        Assert.Equal(100_000, actual.LimiCreditoCliente);
        Assert.Equal(7, actual.RespaldoIntervaloDias);

        var detalle = bd.Db.ExecuteScalar<string>(
            "SELECT DatosDespues FROM Bitacora WHERE Accion = @accion ORDER BY Id DESC LIMIT 1",
            new SqliteParameter("@accion", AccionBitacora.Configuracion));
        Assert.Contains("Nombre=Tienda Editada", detalle);
    }

    [Fact]
    public void ActualizarConfiguracion_ActivaYDesactivaFiadoSinPerderDatos()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda(fiado: false);

        var activar = Copiar(bd.Config.ObtenerConfiguracion().Value);
        activar.FiadoActivado = true;
        activar.LimiCreditoCliente = 80_000;
        Assert.True(bd.Config.ActualizarConfiguracion(activar).IsSuccess);

        var activada = bd.Config.ObtenerConfiguracion().Value;
        Assert.True(activada.FiadoActivado);
        Assert.Equal(80_000, activada.LimiCreditoCliente);

        var desactivar = Copiar(activada);
        desactivar.FiadoActivado = false;
        desactivar.LimiCreditoCliente = 0;
        Assert.True(bd.Config.ActualizarConfiguracion(desactivar).IsSuccess);

        var desactivada = bd.Config.ObtenerConfiguracion().Value;
        Assert.False(desactivada.FiadoActivado);
        Assert.Equal(0, desactivada.LimiCreditoCliente);
        Assert.Equal("Tienda de prueba", desactivada.NombreTienda);
    }

    [Theory]
    [InlineData(RolUsuario.Encargado)]
    [InlineData(RolUsuario.Empleado)]
    public void ActualizarConfiguracion_ConRolSinPermiso_SeRechaza(RolUsuario rol)
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        bd.IniciarSesion(rol);
        var config = Copiar(bd.Config.ObtenerConfiguracion().Value);
        config.NombreTienda = "No permitido";

        var resultado = bd.Config.ActualizarConfiguracion(config);

        Assert.True(resultado.IsError);
        Assert.Equal("Tienda de prueba", bd.Db.ObtenerConfiguracion().NombreTienda);
    }

    [Fact]
    public void ActualizarConfiguracion_ConNombreVacio_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var config = Copiar(bd.Config.ObtenerConfiguracion().Value);
        config.NombreTienda = " ";

        var resultado = bd.Config.ActualizarConfiguracion(config);

        Assert.True(resultado.IsError);
        Assert.Contains("nombre", resultado.Message);
        Assert.Equal("Tienda de prueba", bd.Db.ObtenerConfiguracion().NombreTienda);
    }

    [Fact]
    public void ActualizarConfiguracion_ConValoresNegativos_SeRechazaSinCambios()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var original = bd.Config.ObtenerConfiguracion().Value;

        var negativo = Copiar(original);
        negativo.ImpuestoPredeterminado = -1m;
        Assert.True(bd.Config.ActualizarConfiguracion(negativo).IsError);

        negativo = Copiar(original);
        negativo.ComisionTarjeta = -1m;
        Assert.True(bd.Config.ActualizarConfiguracion(negativo).IsError);

        negativo = Copiar(original);
        negativo.FondoCajaInicial = -1;
        Assert.True(bd.Config.ActualizarConfiguracion(negativo).IsError);

        negativo = Copiar(original);
        negativo.LimiCreditoCliente = -1;
        Assert.True(bd.Config.ActualizarConfiguracion(negativo).IsError);

        negativo = Copiar(original);
        negativo.RespaldoIntervaloDias = -1;
        Assert.True(bd.Config.ActualizarConfiguracion(negativo).IsError);

        var actual = bd.Config.ObtenerConfiguracion().Value;
        Assert.Equal(original.NombreTienda, actual.NombreTienda);
        Assert.Equal(original.ImpuestoPredeterminado, actual.ImpuestoPredeterminado);
        Assert.Equal(original.ComisionTarjeta, actual.ComisionTarjeta);
        Assert.Equal(original.FondoCajaInicial, actual.FondoCajaInicial);
        Assert.Equal(original.LimiCreditoCliente, actual.LimiCreditoCliente);
        Assert.Equal(original.RespaldoIntervaloDias, actual.RespaldoIntervaloDias);
    }
}
