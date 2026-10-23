using SistemaTienda.Core;

namespace SistemaTienda.Tests;

public sealed class SaldosBordeTests
{
    [Fact]
    public void RegistrarAbono_IgualAlSaldo_DejaCeroYLoExcluyeDelReporte()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var alta = bd.Config.AgregarClienteFiado(new ClienteFiado { Nombre = "Cliente", SaldoActual = 50m });

        var abono = bd.Config.RegistrarAbonoCliente(alta.Value.ClienteId, 50m);

        Assert.True(abono.IsSuccess, abono.Message);
        Assert.Equal(0m, abono.Value.SaldoRestante);
        Assert.Equal(0m, bd.Db.ObtenerClienteFiadoPorId(alta.Value.ClienteId).SaldoActual);
        Assert.Empty(bd.Config.ObtenerClientesConSaldo().Value);
    }

    [Fact]
    public void RegistrarAbono_ConSaldoEnCero_FallaYNoRegistraNada()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var alta = bd.Config.AgregarClienteFiado(new ClienteFiado { Nombre = "Cliente", SaldoActual = 0m });

        var abono = bd.Config.RegistrarAbonoCliente(alta.Value.ClienteId, 0.01m);

        Assert.True(abono.IsError);
        Assert.Equal(0m, bd.Db.ObtenerClienteFiadoPorId(alta.Value.ClienteId).SaldoActual);
    }

    [Fact]
    public void RegistrarAbono_ConCentavos_ConservaElSaldoExacto()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var alta = bd.Config.AgregarClienteFiado(new ClienteFiado { Nombre = "Cliente", SaldoActual = 100m });

        var abono = bd.Config.RegistrarAbonoCliente(alta.Value.ClienteId, 33.33m);

        Assert.True(abono.IsSuccess, abono.Message);
        Assert.Equal(66.67m, abono.Value.SaldoRestante);
        Assert.Equal(66.67m, bd.Db.ObtenerClienteFiadoPorId(alta.Value.ClienteId).SaldoActual);
    }

    [Fact]
    public void RegistrarPagoProveedor_IgualAlSaldo_DejaCero()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var alta = bd.Config.AgregarProveedor(new Proveedor { Nombre = "Proveedor", SaldoPendiente = 80m });

        var pago = bd.Config.RegistrarPagoProveedor(alta.Value.ProveedorId, 80m);

        Assert.True(pago.IsSuccess, pago.Message);
        Assert.Equal(0m, pago.Value.SaldoRestante);
        Assert.Equal(0m, bd.Db.ObtenerProveedorPorId(alta.Value.ProveedorId).SaldoPendiente);
    }
}
