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

    [Fact]
    public void BuscarProveedores_SinTexto_DevuelveTodosOrdenadosPorNombre()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        bd.Config.AgregarProveedor(new Proveedor { Nombre = "Zeta Distribuciones" });
        bd.Config.AgregarProveedor(new Proveedor { Nombre = "Abarrotes del Valle" });

        var proveedores = bd.Config.BuscarProveedores().Value;

        Assert.Equal(new[] { "Abarrotes del Valle", "Zeta Distribuciones" },
            proveedores.Select(p => p.Nombre));
    }

    [Fact]
    public void BuscarProveedores_FiltraNombreClaveYTelefono_IgnorandoMayusculasYAcentos()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        bd.Config.AgregarProveedor(new Proveedor
        {
            Nombre = "Distribuidora Núñez", Clave = "DNU010101ABC", Telefono = "818-000-1111"
        });
        bd.Config.AgregarProveedor(new Proveedor
        {
            Nombre = "Abarrotes del Valle", Clave = "AVL020202XYZ", Telefono = "555-123-4567"
        });

        var porNombre = bd.Config.BuscarProveedores("NUNEZ").Value;
        var porClave = bd.Config.BuscarProveedores("dnu010101").Value;
        var porTelefono = bd.Config.BuscarProveedores("000-1111").Value;

        Assert.Equal("Distribuidora Núñez", Assert.Single(porNombre).Nombre);
        Assert.Equal("Distribuidora Núñez", Assert.Single(porClave).Nombre);
        Assert.Equal("Distribuidora Núñez", Assert.Single(porTelefono).Nombre);
    }

    [Fact]
    public void BuscarProveedores_SoloConSaldo_ExcluyeProveedoresAlDia()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        bd.Config.AgregarProveedor(new Proveedor { Nombre = "Con saldo", SaldoPendiente = 5_000 });
        bd.Config.AgregarProveedor(new Proveedor { Nombre = "Al día", SaldoPendiente = 0 });

        var proveedores = bd.Config.BuscarProveedores(soloConSaldo: true).Value;

        Assert.Equal("Con saldo", Assert.Single(proveedores).Nombre);
    }

    [Fact]
    public void AgregarProveedor_RecortaDatosYRegistraBitacora()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var alta = bd.Config.AgregarProveedor(new Proveedor
        {
            Nombre = "  Distribuidora del Norte  ",
            Clave = "  DNO010101ABC  ",
            Telefono = "  818-000-1111  ",
            Direccion = "  Av. Constitución 123  ",
            SaldoPendiente = 25_000
        });

        Assert.True(alta.IsSuccess, alta.Message);
        var proveedor = bd.Db.ObtenerProveedorPorId(alta.Value.ProveedorId);
        Assert.Equal("Distribuidora del Norte", proveedor.Nombre);
        Assert.Equal("DNO010101ABC", proveedor.Clave);
        Assert.Equal("818-000-1111", proveedor.Telefono);
        Assert.Equal("Av. Constitución 123", proveedor.Direccion);

        var registro = Assert.Single(bd.Db.ObtenerBitacora(accion: AccionBitacora.Proveedor));
        Assert.Contains("Distribuidora del Norte", registro.DatosDespues);
        Assert.Equal("Dueño", registro.Usuario);
    }

    [Fact]
    public void BuscarClientes_SinTexto_DevuelveTodosOrdenadosPorNombre()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        bd.Config.AgregarClienteFiado(new ClienteFiado { Nombre = "Zulema", SaldoActual = 0 });
        bd.Config.AgregarClienteFiado(new ClienteFiado { Nombre = "Ana", SaldoActual = 5_000 });

        var clientes = bd.Config.BuscarClientes().Value;

        Assert.Equal(new[] { "Ana", "Zulema" }, clientes.Select(c => c.Nombre));
    }

    [Fact]
    public void BuscarClientes_FiltraNombreYTelefono_IgnorandoMayusculasYAcentos()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        bd.Config.AgregarClienteFiado(new ClienteFiado
        {
            Nombre = "María López", Telefono = "555-123-4567", SaldoActual = 0
        });
        bd.Config.AgregarClienteFiado(new ClienteFiado
        {
            Nombre = "Juan Pérez", Telefono = "818-000-1111", SaldoActual = 0
        });

        var porNombre = bd.Config.BuscarClientes("MARIA").Value;
        var porTelefono = bd.Config.BuscarClientes("000-1111").Value;

        Assert.Equal("María López", Assert.Single(porNombre).Nombre);
        Assert.Equal("Juan Pérez", Assert.Single(porTelefono).Nombre);
    }

    [Fact]
    public void BuscarClientes_SoloConSaldo_ExcluyeClientesAlDia()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        bd.Config.AgregarClienteFiado(new ClienteFiado { Nombre = "Con deuda", SaldoActual = 1_000 });
        bd.Config.AgregarClienteFiado(new ClienteFiado { Nombre = "Al día", SaldoActual = 0 });

        var clientes = bd.Config.BuscarClientes(soloConSaldo: true).Value;

        Assert.Equal("Con deuda", Assert.Single(clientes).Nombre);
    }

    [Fact]
    public void AgregarClienteFiado_RecortaNombreYTelefonoYRegistraBitacora()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var alta = bd.Config.AgregarClienteFiado(new ClienteFiado
        {
            Nombre = "  Doña Mari  ",
            Telefono = "  555-123-4567  "
        });

        Assert.True(alta.IsSuccess, alta.Message);
        var cliente = bd.Db.ObtenerClienteFiadoPorId(alta.Value.ClienteId);
        Assert.Equal("Doña Mari", cliente.Nombre);
        Assert.Equal("555-123-4567", cliente.Telefono);

        var registro = Assert.Single(bd.Db.ObtenerBitacora(accion: AccionBitacora.Cliente));
        Assert.Contains("Doña Mari", registro.DatosDespues);
        Assert.Equal("Dueño", registro.Usuario);
    }
}
