using SistemaTienda.Application.Services;
using SistemaTienda.Core;

namespace SistemaTienda.Tests;

public sealed class AbonosMetodoPagoTests
{
    private static int CrearCliente(BaseDatosPrueba bd, long saldo = 10_000)
    {
        var resultado = bd.Config.AgregarClienteFiado(new ClienteFiado
        {
            Nombre = "Cliente de prueba",
            Telefono = "5550000000",
            SaldoActual = saldo
        });

        Assert.True(resultado.IsSuccess, resultado.Message);
        return resultado.Value.ClienteId;
    }

    [Theory]
    [InlineData(MetodoPagoAbono.Efectivo)]
    [InlineData(MetodoPagoAbono.Tarjeta)]
    [InlineData(MetodoPagoAbono.Transferencia)]
    [InlineData(MetodoPagoAbono.Otro)]
    public void RegistrarAbono_ConCadaMetodo_GuardaElMetodoElegido(string metodo)
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var clienteId = CrearCliente(bd);

        var abono = bd.Config.RegistrarAbonoCliente(clienteId, 4_000, "Cajero", metodo);

        Assert.True(abono.IsSuccess, abono.Message);
        Assert.Equal(6_000, abono.Value.SaldoRestante);
        Assert.Equal(metodo, bd.Db.ExecuteScalar<string>(
            "SELECT MetodoPago FROM AbonosFiado LIMIT 1"));
        Assert.Equal(4_000, bd.Db.ExecuteScalar<long>(
            "SELECT Monto FROM AbonosFiado LIMIT 1"));
    }

    [Fact]
    public void RegistrarAbono_ConTarjeta_NoSumaAlEfectivoEsperadoDelCorte()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var clienteId = CrearCliente(bd);

        var abono = bd.Config.RegistrarAbonoCliente(
            clienteId, 4_000, "Cajero", MetodoPagoAbono.Tarjeta);

        Assert.True(abono.IsSuccess, abono.Message);

        var pendiente = new CorteCajaService(bd.Db).ObtenerResumenPendiente().Value;
        Assert.Equal(0, pendiente.AbonosEfectivo);
        Assert.Equal(20_000, pendiente.EfectivoEsperado);
    }

    [Fact]
    public void RegistrarAbono_EnEfectivo_SiSumaAlEfectivoEsperadoDelCorte()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var clienteId = CrearCliente(bd);

        var abono = bd.Config.RegistrarAbonoCliente(
            clienteId, 4_000, "Cajero", MetodoPagoAbono.Efectivo);

        Assert.True(abono.IsSuccess, abono.Message);

        var pendiente = new CorteCajaService(bd.Db).ObtenerResumenPendiente().Value;
        Assert.Equal(4_000, pendiente.AbonosEfectivo);
        Assert.Equal(24_000, pendiente.EfectivoEsperado);
    }

    [Fact]
    public void RegistrarAbono_ConMetodosMixtos_SoloElEfectivoEntraAlCorte()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var clienteId = CrearCliente(bd, saldo: 15_000);

        Assert.True(bd.Config.RegistrarAbonoCliente(
            clienteId, 10_000, "Cajero", MetodoPagoAbono.Transferencia).IsSuccess);
        Assert.True(bd.Config.RegistrarAbonoCliente(
            clienteId, 1_000, "Cajero", MetodoPagoAbono.Efectivo).IsSuccess);

        var estado = bd.Config.ObtenerEstadoCuentaCliente(clienteId);
        Assert.True(estado.IsSuccess, estado.Message);
        Assert.Equal(11_000, estado.Value.TotalAbonado);
        Assert.Equal(4_000, estado.Value.SaldoActual);

        var pendiente = new CorteCajaService(bd.Db).ObtenerResumenPendiente().Value;
        Assert.Equal(1_000, pendiente.AbonosEfectivo);
        Assert.Equal(21_000, pendiente.EfectivoEsperado);
    }

    [Fact]
    public void RegistrarAbono_ConMetodoInvalido_SeRechazaSinCambiarSaldo()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var clienteId = CrearCliente(bd);

        var abono = bd.Config.RegistrarAbonoCliente(clienteId, 4_000, "Cajero", "Cheque");

        Assert.True(abono.IsError);
        Assert.Contains("método", abono.Message);
        Assert.Equal(10_000, bd.Db.ObtenerClienteFiadoPorId(clienteId).SaldoActual);
        Assert.Equal(0, bd.Db.ExecuteScalar<int>("SELECT COUNT(*) FROM AbonosFiado"));
        Assert.Equal(0, bd.Db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Bitacora WHERE Accion = @accion",
            new Microsoft.Data.Sqlite.SqliteParameter("@accion", AccionBitacora.Abono)));
    }

    [Fact]
    public void RegistrarAbono_SinMetodo_UsaEfectivoPorDefectoYRecortaEspacios()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var primerCliente = CrearCliente(bd);
        var segundoCliente = CrearCliente(bd);

        Assert.True(bd.Config.RegistrarAbonoCliente(primerCliente, 1_000).IsSuccess);
        Assert.True(bd.Config.RegistrarAbonoCliente(
            segundoCliente, 1_000, "Cajero", "  Efectivo  ").IsSuccess);

        var metodos = bd.Db.ExecuteReportObjects<AbonoFiadoCliente>(
            "SELECT Id, Fecha, Monto, MetodoPago, Usuario FROM AbonosFiado ORDER BY Id");
        Assert.Equal(2, metodos.Count);
        Assert.All(metodos, a => Assert.Equal(MetodoPagoAbono.Efectivo, a.MetodoPago));
    }

    [Fact]
    public void RegistrarAbono_DejaElMetodoEnLaBitacoraYEnElEstadoDeCuenta()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var clienteId = CrearCliente(bd);

        Assert.True(bd.Config.RegistrarAbonoCliente(
            clienteId, 2_500, "Cajera", MetodoPagoAbono.Transferencia).IsSuccess);

        var detalle = bd.Db.ExecuteScalar<string>(
            "SELECT DatosDespues FROM Bitacora WHERE Accion = @accion ORDER BY Id DESC LIMIT 1",
            new Microsoft.Data.Sqlite.SqliteParameter("@accion", AccionBitacora.Abono));
        Assert.Contains("Metodo=Transferencia", detalle);

        var estado = bd.Config.ObtenerEstadoCuentaCliente(clienteId);
        Assert.True(estado.IsSuccess, estado.Message);
        var abono = Assert.Single(estado.Value.Abonos);
        Assert.Equal(MetodoPagoAbono.Transferencia, abono.MetodoPago);
        Assert.Equal("Cajera", abono.Usuario);
    }
}
