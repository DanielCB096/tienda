using SistemaTienda.Application.Services;
using SistemaTienda.Core;

namespace SistemaTienda.Tests;

public sealed class ConfiguracionTests
{
    [Fact]
    public void AsistenteInicial_CreaConfiguracionYUsuarioDueno()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda(pin: "4321");

        var config = bd.Config.ObtenerConfiguracion();
        Assert.True(config.IsSuccess, config.Message);
        Assert.Equal("Tienda de prueba", config.Value.NombreTienda);
        Assert.Equal("Abarrotes", config.Value.Giro);
        Assert.Equal(16m, config.Value.ImpuestoPredeterminado);
        Assert.True(config.Value.FiadoActivado);

        var usuarios = bd.Config.ObtenerUsuarios().Value;
        var dueno = Assert.Single(usuarios);
        Assert.Equal(RolUsuario.Dueño, dueno.Rol);
    }

    [Fact]
    public void AsistenteInicial_NoSePuedeEjecutarDosVeces()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var segundoIntento = bd.Config.EjecutarAsistenteInicial(
            "Otra tienda", "Abarrotes", 16m, 3.5m, 20_000, false, 0, 1, "9999");

        Assert.True(segundoIntento.IsError);
    }

    [Fact]
    public void Pin_SeGuardaHasheadoYNoEnClaro()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda(pin: "1234");

        var dueno = Assert.Single(bd.Config.ObtenerUsuarios().Value);

        Assert.NotEqual("1234", dueno.PINHash);
        Assert.StartsWith("pbkdf2-sha256$", dueno.PINHash);

        var partes = dueno.PINHash.Split('$');
        Assert.Equal(4, partes.Length);
        Assert.Equal(100_000, int.Parse(partes[1]));
        Assert.Equal(16, Convert.FromBase64String(partes[2]).Length);
        Assert.Equal(32, Convert.FromBase64String(partes[3]).Length);
        Assert.True(ConfiguracionService.VerifyPin("1234", dueno.PINHash));
    }

    [Fact]
    public void VerificarAcceso_ConPinCorrecto_DevuelveUsuario()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda(pin: "2468");

        var acceso = bd.Config.VerificarAcceso("2468");

        Assert.True(acceso.IsSuccess, acceso.Message);
        Assert.Equal(RolUsuario.Dueño, acceso.Value.Rol);
    }

    [Fact]
    public void VerificarAcceso_ConPinIncorrecto_Falla()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda(pin: "2468");

        var acceso = bd.Config.VerificarAcceso("0000");

        Assert.True(acceso.IsError);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("1234567")]
    [InlineData("12a4")]
    [InlineData(" 1234")]
    public void PinValido_RechazaFormatosInvalidos(string pin)
    {
        Assert.False(ConfiguracionService.PinValido(pin));
    }

    [Theory]
    [InlineData("1234")]
    [InlineData("000000")]
    public void PinValido_AceptaCuatroASeisDigitos(string pin)
    {
        Assert.True(ConfiguracionService.PinValido(pin));
    }

    [Fact]
    public void ValidarPin_UnHashConFormatoDesconocido_LanzaFormatException()
    {
        Assert.Throws<FormatException>(() => ConfiguracionService.VerifyPin("1234", "texto-plano"));
    }

    [Fact]
    public void AgregarUsuario_ConPinInvalido_Falla()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var resultado = bd.Config.AgregarUsuario("Cajero", "12", RolUsuario.Empleado);

        Assert.True(resultado.IsError);
    }

    [Fact]
    public void AgregarUsuario_ConDatosValidos_PermiteAccesoYValidaRol()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda(pin: "1234");

        var alta = bd.Config.AgregarUsuario("Cajero", "567890", RolUsuario.Empleado);
        Assert.True(alta.IsSuccess, alta.Message);

        var acceso = bd.Config.VerificarAcceso("567890");
        Assert.True(acceso.IsSuccess);
        Assert.Equal(RolUsuario.Empleado, acceso.Value.Rol);

        Assert.True(bd.Config.UsuarioTieneRol(alta.Value.UsuarioId, RolUsuario.Empleado));
        Assert.False(bd.Config.UsuarioTieneRol(alta.Value.UsuarioId, RolUsuario.Dueño));
    }

    [Fact]
    public void Gastos_SeTotalizanYSeAgrupanPorConcepto()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        Assert.True(bd.Config.AgregarGasto(new Gasto
        {
            Concepto = "Renta", Monto = 10_000, MetodoPago = "Efectivo", Origen = OrigenGasto.Caja
        }).IsSuccess);

        Assert.True(bd.Config.AgregarGasto(new Gasto
        {
            Concepto = "Renta", Monto = 5_050, MetodoPago = "Tarjeta", Origen = OrigenGasto.Externo
        }).IsSuccess);

        Assert.True(bd.Config.AgregarGasto(new Gasto
        {
            Concepto = "Luz", Monto = 8_000, MetodoPago = "Transferencia", Origen = OrigenGasto.Externo
        }).IsSuccess);

        Assert.Equal(23_050, bd.Config.ObtenerTotalGastos().Value);

        var porConcepto = bd.Config.ObtenerGastosPorConcepto().Value;
        Assert.Equal(2, porConcepto.Count);
        Assert.Equal("Renta", porConcepto[0].Concepto);
        Assert.Equal(15_050, porConcepto[0].Total);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void AgregarGasto_ConMontoNoPositivo_Falla(long monto)
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var resultado = bd.Config.AgregarGasto(new Gasto { Concepto = "Prueba", Monto = monto });


        Assert.True(resultado.IsError);
    }

    [Fact]
    public void AgregarGasto_SinConcepto_Falla()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var resultado = bd.Config.AgregarGasto(new Gasto { Concepto = "  ", Monto = 1_000 });

        Assert.True(resultado.IsError);
    }
}
