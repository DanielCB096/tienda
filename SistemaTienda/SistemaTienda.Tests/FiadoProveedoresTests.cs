using SistemaTienda.Core;

namespace SistemaTienda.Tests;

public sealed class FiadoProveedoresTests
{
    [Fact]
    public void AgregarClienteFiado_GuardaTelefonoYSaldo()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var alta = bd.Config.AgregarClienteFiado(new ClienteFiado
        {
            Nombre = "Doña Mari",
            Telefono = "555-123-4567",
            SaldoActual = 300m
        });

        Assert.True(alta.IsSuccess, alta.Message);

        var cliente = bd.Db.ObtenerClienteFiadoPorId(alta.Value.ClienteId);
        Assert.Equal("Doña Mari", cliente.Nombre);
        Assert.Equal("555-123-4567", cliente.Telefono);
        Assert.Equal(300m, cliente.SaldoActual);
    }

    [Fact]
    public void AgregarClienteFiado_SinNombre_Falla()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var alta = bd.Config.AgregarClienteFiado(new ClienteFiado { Nombre = " " });

        Assert.True(alta.IsError);
    }

    [Fact]
    public void AgregarClienteFiado_ConSaldoNegativo_Falla()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var alta = bd.Config.AgregarClienteFiado(new ClienteFiado { Nombre = "Cliente", SaldoActual = -1m });

        Assert.True(alta.IsError);
    }

    [Fact]
    public void RegistrarAbono_ReduceSaldoPendiente()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var alta = bd.Config.AgregarClienteFiado(new ClienteFiado { Nombre = "Cliente", SaldoActual = 300m });

        var abono = bd.Config.RegistrarAbonoCliente(alta.Value.ClienteId, 120m);

        Assert.True(abono.IsSuccess, abono.Message);
        Assert.Equal(180m, abono.Value.SaldoRestante);
        Assert.Equal(180m, bd.Db.ObtenerClienteFiadoPorId(alta.Value.ClienteId).SaldoActual);
    }

    [Fact]
    public void RegistrarAbono_MayorAlSaldo_FallaYNoModifica()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var alta = bd.Config.AgregarClienteFiado(new ClienteFiado { Nombre = "Cliente", SaldoActual = 100m });

        var abono = bd.Config.RegistrarAbonoCliente(alta.Value.ClienteId, 150m);

        Assert.True(abono.IsError);
        Assert.Equal(100m, bd.Db.ObtenerClienteFiadoPorId(alta.Value.ClienteId).SaldoActual);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void RegistrarAbono_ConMontoNoPositivo_Falla(decimal monto)
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var alta = bd.Config.AgregarClienteFiado(new ClienteFiado { Nombre = "Cliente", SaldoActual = 100m });

        Assert.True(bd.Config.RegistrarAbonoCliente(alta.Value.ClienteId, monto).IsError);
    }

    [Fact]
    public void RegistrarAbono_DeClienteInexistente_Falla()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        Assert.True(bd.Config.RegistrarAbonoCliente(999, 10m).IsError);
    }

    [Fact]
    public void ObtenerClientesConSaldo_ExcluyeSaldosEnCero()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        bd.Config.AgregarClienteFiado(new ClienteFiado { Nombre = "Con deuda", SaldoActual = 50m });
        bd.Config.AgregarClienteFiado(new ClienteFiado { Nombre = "Al día", SaldoActual = 0m });

        var conSaldo = bd.Config.ObtenerClientesConSaldo().Value;

        var cliente = Assert.Single(conSaldo);
        Assert.Equal("Con deuda", cliente.Nombre);
    }

    [Fact]
    public void RegistrarPagoProveedor_ReduceSaldoPendiente()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var alta = bd.Config.AgregarProveedor(new Proveedor
        {
            Nombre = "Distribuidora del Norte",
            Clave = "DNO010101ABC",
            Telefono = "818-000-1111",
            SaldoPendiente = 500m
        });

        Assert.True(alta.IsSuccess, alta.Message);

        var pago = bd.Config.RegistrarPagoProveedor(alta.Value.ProveedorId, 200m);

        Assert.True(pago.IsSuccess, pago.Message);
        Assert.Equal(300m, pago.Value.SaldoRestante);
        Assert.Equal(300m, bd.Db.ObtenerProveedorPorId(alta.Value.ProveedorId).SaldoPendiente);
    }

    [Fact]
    public void RegistrarPagoProveedor_MayorAlSaldo_FallaYNoModifica()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var alta = bd.Config.AgregarProveedor(new Proveedor { Nombre = "Proveedor", SaldoPendiente = 100m });

        var pago = bd.Config.RegistrarPagoProveedor(alta.Value.ProveedorId, 101m);

        Assert.True(pago.IsError);
        Assert.Equal(100m, bd.Db.ObtenerProveedorPorId(alta.Value.ProveedorId).SaldoPendiente);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RegistrarPagoProveedor_ConMontoNoPositivo_Falla(decimal monto)
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var alta = bd.Config.AgregarProveedor(new Proveedor { Nombre = "Proveedor", SaldoPendiente = 100m });

        Assert.True(bd.Config.RegistrarPagoProveedor(alta.Value.ProveedorId, monto).IsError);
    }

    [Fact]
    public void AgregarProveedor_SinNombre_Falla()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        Assert.True(bd.Config.AgregarProveedor(new Proveedor { Nombre = "  " }).IsError);
    }
}
