using SistemaTienda.Application.Services;
using SistemaTienda.Core;

namespace SistemaTienda.Tests;

public sealed class UsuariosPermisosTests
{
    [Fact]
    public void AgregarUsuario_ConNombreConEspacios_LoGuardaRecortado()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda(pin: "1234");

        var alta = bd.Config.AgregarUsuario("  Cajero  ", "5678", RolUsuario.Empleado);

        Assert.True(alta.IsSuccess, alta.Message);

        var usuarios = bd.Config.ObtenerUsuarios().Value;
        Assert.Contains(usuarios, u => u.Nombre == "Cajero");
        Assert.DoesNotContain(usuarios, u => u.Nombre != u.Nombre.Trim());
    }

    [Fact]
    public void AgregarUsuario_ConPinDeCuatroDigitos_PermiteAcceso()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda(pin: "1234");

        var alta = bd.Config.AgregarUsuario("Cajero", "4321", RolUsuario.Empleado);
        Assert.True(alta.IsSuccess, alta.Message);

        var acceso = bd.Config.VerificarAcceso("4321");

        Assert.True(acceso.IsSuccess, acceso.Message);
        Assert.Equal(RolUsuario.Empleado, acceso.Value.Rol);
    }

    [Fact]
    public void AgregarUsuario_ConNombreEnBlanco_FallaYNoCreaUsuario()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda(pin: "1234");

        var alta = bd.Config.AgregarUsuario("   ", "5678", RolUsuario.Empleado);

        Assert.True(alta.IsError);
        Assert.Equal(1, bd.Db.ContarUsuarios());
    }

    [Fact]
    public void UsuarioTieneRol_ConUsuarioInexistente_DevuelveFalse()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        Assert.False(bd.Config.UsuarioTieneRol(999, RolUsuario.Dueño));
    }

    [Fact]
    public void HashPin_ConMismoPin_GeneraSalDistintaYAmbosVerifican()
    {
        var hash1 = ConfiguracionService.HashPin("1234");
        var hash2 = ConfiguracionService.HashPin("1234");

        Assert.NotEqual(hash1, hash2);
        Assert.True(ConfiguracionService.VerifyPin("1234", hash1));
        Assert.True(ConfiguracionService.VerifyPin("1234", hash2));
        Assert.False(ConfiguracionService.VerifyPin("4321", hash1));
    }

    [Theory]
    [InlineData("pbkdf2-sha256$100000$c2FsdA==")]
    [InlineData("pbkdf2-sha256$no-numero$c2FsdA==$aGFzaA==")]
    public void VerifyPin_ConHashMalFormado_LanzaFormatException(string hash)
    {
        Assert.Throws<FormatException>(() => ConfiguracionService.VerifyPin("1234", hash));
    }
}
