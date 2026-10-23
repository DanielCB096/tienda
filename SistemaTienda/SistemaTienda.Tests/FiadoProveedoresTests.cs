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
            SaldoActual = 30_000
        });

        Assert.True(alta.IsSuccess, alta.Message);

        var cliente = bd.Db.ObtenerClienteFiadoPorId(alta.Value.ClienteId);
        Assert.Equal("Doña Mari", cliente.Nombre);
        Assert.Equal("555-123-4567", cliente.Telefono);
        Assert.Equal(30_000, cliente.SaldoActual);
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

        var alta = bd.Config.AgregarClienteFiado(new ClienteFiado { Nombre = "Cliente", SaldoActual = -1 });

        Assert.True(alta.IsError);
    }

    [Fact]
    public void RegistrarAbono_ReduceSaldoPendiente()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var alta = bd.Config.AgregarClienteFiado(new ClienteFiado { Nombre = "Cliente", SaldoActual = 30_000 });

        var abono = bd.Config.RegistrarAbonoCliente(alta.Value.ClienteId, 12_000);

        Assert.True(abono.IsSuccess, abono.Message);
        Assert.Equal(18_000, abono.Value.SaldoRestante);
        Assert.Equal(18_000, bd.Db.ObtenerClienteFiadoPorId(alta.Value.ClienteId).SaldoActual);
    }

    [Fact]
    public void RegistrarAbono_MayorAlSaldo_FallaYNoModifica()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var alta = bd.Config.AgregarClienteFiado(new ClienteFiado { Nombre = "Cliente", SaldoActual = 10_000 });

        var abono = bd.Config.RegistrarAbonoCliente(alta.Value.ClienteId, 15_000);

        Assert.True(abono.IsError);
        Assert.Equal(10_000, bd.Db.ObtenerClienteFiadoPorId(alta.Value.ClienteId).SaldoActual);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-500L)]
    public void RegistrarAbono_ConMontoNoPositivo_Falla(long monto)
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var alta = bd.Config.AgregarClienteFiado(new ClienteFiado { Nombre = "Cliente", SaldoActual = 10_000 });

        Assert.True(bd.Config.RegistrarAbonoCliente(alta.Value.ClienteId, monto).IsError);
    }

    [Fact]
    public void RegistrarAbono_DeClienteInexistente_Falla()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        Assert.True(bd.Config.RegistrarAbonoCliente(999, 1_000).IsError);
    }

    [Fact]
    public void ObtenerClientesConSaldo_ExcluyeSaldosEnCero()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        bd.Config.AgregarClienteFiado(new ClienteFiado { Nombre = "Con deuda", SaldoActual = 5_000 });
        bd.Config.AgregarClienteFiado(new ClienteFiado { Nombre = "Al día", SaldoActual = 0 });

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
            SaldoPendiente = 50_000
        });

        Assert.True(alta.IsSuccess, alta.Message);

        var pago = bd.Config.RegistrarPagoProveedor(alta.Value.ProveedorId, 20_000);

        Assert.True(pago.IsSuccess, pago.Message);
        Assert.Equal(30_000, pago.Value.SaldoRestante);
        Assert.Equal(30_000, bd.Db.ObtenerProveedorPorId(alta.Value.ProveedorId).SaldoPendiente);
    }

    [Fact]
    public void RegistrarPagoProveedor_MayorAlSaldo_FallaYNoModifica()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var alta = bd.Config.AgregarProveedor(new Proveedor { Nombre = "Proveedor", SaldoPendiente = 10_000 });

        var pago = bd.Config.RegistrarPagoProveedor(alta.Value.ProveedorId, 10_100);

        Assert.True(pago.IsError);
        Assert.Equal(10_000, bd.Db.ObtenerProveedorPorId(alta.Value.ProveedorId).SaldoPendiente);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-100L)]
    public void RegistrarPagoProveedor_ConMontoNoPositivo_Falla(long monto)
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var alta = bd.Config.AgregarProveedor(new Proveedor { Nombre = "Proveedor", SaldoPendiente = 10_000 });

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
