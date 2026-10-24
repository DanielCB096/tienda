using SistemaTienda.Core;

namespace SistemaTienda.Tests;

public sealed class SaldosBordeTests
{
    [Fact]
    public void RegistrarAbono_IgualAlSaldo_DejaCeroYLoExcluyeDelReporte()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var alta = bd.Config.AgregarClienteFiado(new ClienteFiado { Nombre = "Cliente", SaldoActual = 5000 });

        var abono = bd.Config.RegistrarAbonoCliente(alta.Value.ClienteId, 5000);

        Assert.True(abono.IsSuccess, abono.Message);
        Assert.Equal(0L, abono.Value.SaldoRestante);
        Assert.Equal(0L, bd.Db.ObtenerClienteFiadoPorId(alta.Value.ClienteId).SaldoActual);
        Assert.Empty(bd.Config.ObtenerClientesConSaldo().Value);
    }

    [Fact]
    public void RegistrarAbono_ConSaldoEnCero_FallaYNoRegistraNada()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var alta = bd.Config.AgregarClienteFiado(new ClienteFiado { Nombre = "Cliente", SaldoActual = 0 });

        var abono = bd.Config.RegistrarAbonoCliente(alta.Value.ClienteId, 1);

        Assert.True(abono.IsError);
        Assert.Equal(0L, bd.Db.ObtenerClienteFiadoPorId(alta.Value.ClienteId).SaldoActual);
    }

    [Fact]
    public void RegistrarAbono_ConCentavos_ConservaElSaldoExacto()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var alta = bd.Config.AgregarClienteFiado(new ClienteFiado { Nombre = "Cliente", SaldoActual = 10000 });

        var abono = bd.Config.RegistrarAbonoCliente(alta.Value.ClienteId, 3333);

        Assert.True(abono.IsSuccess, abono.Message);
        Assert.Equal(6667L, abono.Value.SaldoRestante);
        Assert.Equal(6667L, bd.Db.ObtenerClienteFiadoPorId(alta.Value.ClienteId).SaldoActual);
    }

    [Fact]
    public void RegistrarPagoProveedor_IgualAlSaldo_DejaCero()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var alta = bd.Config.AgregarProveedor(new Proveedor { Nombre = "Proveedor", SaldoPendiente = 8000 });

        var pago = bd.Config.RegistrarPagoProveedor(alta.Value.ProveedorId, 8000);

        Assert.True(pago.IsSuccess, pago.Message);
        Assert.Equal(0L, pago.Value.SaldoRestante);
        Assert.Equal(0L, bd.Db.ObtenerProveedorPorId(alta.Value.ProveedorId).SaldoPendiente);
    }
}
