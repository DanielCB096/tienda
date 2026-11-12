using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Tests;

public sealed class RespaldoAutomaticoTests
{
    [Fact]
    public void Ejecutar_SinRespaldoPrevio_CreaRespaldoAutomaticoYLoAudita()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var servicio = new RespaldoService(bd.Db);

        var resultado = servicio.EjecutarAutomaticoSiCorresponde();

        Assert.True(resultado.IsSuccess, resultado.Message);
        Assert.Equal(EstadoRespaldoAutomatico.Creado, resultado.Value.Estado);
        Assert.True(File.Exists(resultado.Value.Ruta));
        Assert.True(RespaldoLocal.Verificar(resultado.Value.Ruta));
        Assert.Contains("automatico", Path.GetFileName(resultado.Value.Ruta));
        Assert.Single(RespaldoLocal.Listar(bd.Db.DirectorioRespaldos));

        var registro = Assert.Single(bd.Db.ObtenerBitacora(accion: AccionBitacora.Respaldo));
        Assert.Equal("Respaldo", registro.Entidad);
        Assert.Contains("Ruta=", registro.DatosDespues);
        Assert.Contains("Etiqueta=automatico", registro.DatosDespues);
    }

    [Fact]
    public void Ejecutar_ConRespaldoReciente_NoDuplicaNiAudita()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var servicio = new RespaldoService(bd.Db);

        Assert.True(servicio.EjecutarAutomaticoSiCorresponde().Value.Estado ==
                    EstadoRespaldoAutomatico.Creado);
        var auditoriaInicial = bd.Db.ObtenerBitacora(accion: AccionBitacora.Respaldo).Count;

        var segunda = servicio.EjecutarAutomaticoSiCorresponde();

        Assert.True(segunda.IsSuccess, segunda.Message);
        Assert.Equal(EstadoRespaldoAutomatico.Vigente, segunda.Value.Estado);
        Assert.NotNull(segunda.Value.UltimoRespaldo);
        Assert.Single(RespaldoLocal.Listar(bd.Db.DirectorioRespaldos));
        Assert.Equal(auditoriaInicial, bd.Db.ObtenerBitacora(accion: AccionBitacora.Respaldo).Count);
    }

    [Fact]
    public void Ejecutar_ConIntervaloCumplido_CreaOtroRespaldo()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var servicio = new RespaldoService(bd.Db);

        var primero = servicio.EjecutarAutomaticoSiCorresponde();
        Assert.True(primero.IsSuccess, primero.Message);

        var futuro = DateTime.Now.AddDays(1);
        var segundo = servicio.EjecutarAutomaticoSiCorresponde(futuro);

        Assert.True(segundo.IsSuccess, segundo.Message);
        Assert.Equal(EstadoRespaldoAutomatico.Creado, segundo.Value.Estado);
        Assert.NotEqual(primero.Value.Ruta, segundo.Value.Ruta);
        Assert.Equal(2, RespaldoLocal.Listar(bd.Db.DirectorioRespaldos).Count);
        Assert.Equal(2, bd.Db.ObtenerBitacora(accion: AccionBitacora.Respaldo).Count);
    }

    [Fact]
    public void Ejecutar_ConIntervaloEnCero_NoCreaNadaNiAudita()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var config = bd.Config.ObtenerConfiguracion().Value;
        config.RespaldoIntervaloDias = 0;
        Assert.True(bd.Config.ActualizarConfiguracion(config).IsSuccess);

        var servicio = new RespaldoService(bd.Db);
        var resultado = servicio.EjecutarAutomaticoSiCorresponde();

        Assert.True(resultado.IsSuccess, resultado.Message);
        Assert.Equal(EstadoRespaldoAutomatico.Deshabilitado, resultado.Value.Estado);
        Assert.Empty(RespaldoLocal.Listar(bd.Db.DirectorioRespaldos));
        Assert.Empty(bd.Db.ObtenerBitacora(accion: AccionBitacora.Respaldo));
    }

    [Fact]
    public void CrearRespaldoManual_TambienQuedaEnLaBitacora()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var servicio = new RespaldoService(bd.Db);

        var creado = servicio.CrearRespaldo("manual");

        Assert.True(creado.IsSuccess, creado.Message);
        var registro = Assert.Single(bd.Db.ObtenerBitacora(accion: AccionBitacora.Respaldo));
        Assert.Contains("Etiqueta=manual", registro.DatosDespues);
    }
}
