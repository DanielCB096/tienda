using Microsoft.Data.Sqlite;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;

namespace SistemaTienda.Tests;

public sealed class UsuariosPinTests
{
    private static int Agregar(BaseDatosPrueba bd, string nombre, string pin,
        RolUsuario rol = RolUsuario.Encargado)
    {
        var resultado = bd.Config.AgregarUsuario(nombre, pin, rol);
        Assert.True(resultado.IsSuccess, resultado.Message);
        return resultado.Value.UsuarioId;
    }

    [Fact]
    public void AgregarUsuario_ConDatosNuevos_GuardaYRegistraBitacora()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var id = Agregar(bd, "Cajera", "5678");

        var usuario = bd.Db.ObtenerUsuarioPorId(id);
        Assert.Equal("Cajera", usuario.Nombre);
        Assert.Equal(RolUsuario.Encargado, usuario.Rol);
        Assert.True(ConfiguracionService.VerifyPin("5678", usuario.PINHash));
        Assert.False(ConfiguracionService.VerifyPin("1234", usuario.PINHash));

        var detalle = bd.Db.ExecuteScalar<string>(
            "SELECT DatosDespues FROM Bitacora WHERE Accion = @accion ORDER BY Id DESC LIMIT 1",
            new SqliteParameter("@accion", AccionBitacora.Usuario));
        Assert.Contains("Alta: Cajera", detalle);
    }

    [Fact]
    public void AgregarUsuario_ConNombreRepetido_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        Agregar(bd, "Cajera", "5678");

        var resultado = bd.Config.AgregarUsuario("  cajera  ", "2468", RolUsuario.Empleado);

        Assert.True(resultado.IsError);
        Assert.Contains("nombre", resultado.Message);
        Assert.Equal(2, bd.Db.ContarUsuarios());
    }

    [Fact]
    public void AgregarUsuario_ConPinRepetido_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        Agregar(bd, "Cajera", "5678");

        var resultado = bd.Config.AgregarUsuario("Cajero", "5678", RolUsuario.Empleado);

        Assert.True(resultado.IsError);
        Assert.Contains("PIN", resultado.Message);
        Assert.Equal(2, bd.Db.ContarUsuarios());
    }

    [Theory]
    [InlineData(RolUsuario.Encargado)]
    [InlineData(RolUsuario.Empleado)]
    public void AgregarUsuario_ConRolSinPermiso_SeRechaza(RolUsuario rol)
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        bd.IniciarSesion(rol);

        var resultado = bd.Config.AgregarUsuario("Intruso", "5678", RolUsuario.Empleado);

        Assert.True(resultado.IsError);
        Assert.Equal(1, bd.Db.ContarUsuarios());
    }

    [Fact]
    public void ActualizarUsuario_CambiaNombreYRolYRegistraBitacora()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var id = Agregar(bd, "Cajera", "5678");

        var resultado = bd.Config.ActualizarUsuario(id, "Supervisora", RolUsuario.Dueño);

        Assert.True(resultado.IsSuccess, resultado.Message);
        var usuario = bd.Db.ObtenerUsuarioPorId(id);
        Assert.Equal("Supervisora", usuario.Nombre);
        Assert.Equal(RolUsuario.Dueño, usuario.Rol);

        var detalle = bd.Db.ExecuteScalar<string>(
            "SELECT DatosDespues FROM Bitacora WHERE Accion = @accion ORDER BY Id DESC LIMIT 1",
            new SqliteParameter("@accion", AccionBitacora.EditarUsuario));
        Assert.Contains("Nombre=Supervisora;Rol=Dueño", detalle);
    }

    [Fact]
    public void ActualizarUsuario_NoPermiteDejarSinDuenio()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var duenio = bd.Config.ObtenerUsuarios().Value.Single(u => u.Rol == RolUsuario.Dueño);

        var resultado = bd.Config.ActualizarUsuario(duenio.Id, duenio.Nombre, RolUsuario.Encargado);

        Assert.True(resultado.IsError);
        Assert.Contains("Dueño", resultado.Message);
        Assert.Equal(RolUsuario.Dueño, bd.Db.ObtenerUsuarioPorId(duenio.Id).Rol);
    }

    [Fact]
    public void ActualizarUsuario_ConDosDuenios_PermiteDegradarAUno()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var segundo = Agregar(bd, "Socia", "5678", RolUsuario.Dueño);

        var resultado = bd.Config.ActualizarUsuario(segundo, "Socia", RolUsuario.Encargado);

        Assert.True(resultado.IsSuccess, resultado.Message);
        Assert.Equal(RolUsuario.Encargado, bd.Db.ObtenerUsuarioPorId(segundo).Rol);
    }

    [Fact]
    public void ActualizarUsuario_ConNombreDeOtroUsuario_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var id = Agregar(bd, "Cajera", "5678");

        var resultado = bd.Config.ActualizarUsuario(id, "Dueño", RolUsuario.Encargado);

        Assert.True(resultado.IsError);
        Assert.Contains("nombre", resultado.Message);
    }

    [Fact]
    public void CambiarPin_ActualizaElAccesoYRegistraBitacora()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var id = Agregar(bd, "Cajera", "5678");

        var resultado = bd.Config.CambiarPin(id, "2468");

        Assert.True(resultado.IsSuccess, resultado.Message);
        Assert.True(ConfiguracionService.VerifyPin("2468", bd.Db.ObtenerUsuarioPorId(id).PINHash));
        Assert.True(bd.Config.VerificarAcceso("2468").IsSuccess);
        Assert.Equal(id, bd.Config.VerificarAcceso("2468").Value.Id);

        var detalle = bd.Db.ExecuteScalar<string>(
            "SELECT DatosDespues FROM Bitacora WHERE Accion = @accion ORDER BY Id DESC LIMIT 1",
            new SqliteParameter("@accion", AccionBitacora.CambiarPin));
        Assert.Contains("PIN actualizado", detalle);
        Assert.DoesNotContain("2468", detalle);
    }

    [Fact]
    public void CambiarPin_ConPinDeOtroUsuarioOFormatoInvalido_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var id = Agregar(bd, "Cajera", "5678");

        Assert.True(bd.Config.CambiarPin(id, "1234").IsError);
        Assert.True(bd.Config.CambiarPin(id, "12").IsError);
        Assert.True(ConfiguracionService.VerifyPin("5678", bd.Db.ObtenerUsuarioPorId(id).PINHash));
    }

    [Fact]
    public void CambiarPin_ConRolSinPermiso_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var id = Agregar(bd, "Cajera", "5678");
        bd.IniciarSesion(RolUsuario.Encargado);

        var resultado = bd.Config.CambiarPin(id, "2468");

        Assert.True(resultado.IsError);
        Assert.True(ConfiguracionService.VerifyPin("5678", bd.Db.ObtenerUsuarioPorId(id).PINHash));
    }
}
