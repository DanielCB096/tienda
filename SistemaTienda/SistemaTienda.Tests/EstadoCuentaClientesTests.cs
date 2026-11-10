using SistemaTienda.Application.Services;
using SistemaTienda.Core;

namespace SistemaTienda.Tests;

public sealed class EstadoCuentaClientesTests
{
    private static int CrearCliente(BaseDatosPrueba bd, string nombre = "Ana López",
        string telefono = "5550000000", long saldoInicial = 0)
    {
        var resultado = bd.Config.AgregarClienteFiado(new ClienteFiado
        {
            Nombre = nombre,
            Telefono = telefono,
            SaldoActual = saldoInicial
        });

        Assert.True(resultado.IsSuccess, resultado.Message);
        return resultado.Value.ClienteId;
    }

    private static Producto CrearProducto(BaseDatosPrueba bd, string codigo,
        long precio, long costo, int stock)
    {
        var producto = new Producto
        {
            CodigoBarras = codigo,
            Nombre = $"Producto {codigo}",
            Costo = costo,
            Precio = precio,
            Stock = stock
        };

        var resultado = bd.Inventario.AgregarProducto(producto);
        Assert.True(resultado.IsSuccess, resultado.Message);
        return producto;
    }

    private static Venta VenderFiado(BaseDatosPrueba bd, int clienteId, Producto producto, int cantidad)
    {
        var resultado = new VentaService(bd.Db).RegistrarVenta(
            new List<LineaVentaSolicitada> { new() { ProductoId = producto.Id, Cantidad = cantidad } },
            new PagoSolicitado { Fiado = producto.Precio * cantidad, ClienteId = clienteId },
            "Cajero");

        Assert.True(resultado.IsSuccess, resultado.Message);
        return resultado.Value;
    }

    [Fact]
    public void EditarClienteFiado_ComoEncargado_ActualizaDatosYRegistraBitacora()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var clienteId = CrearCliente(bd, "Ana", "5551111111");
        bd.IniciarSesion(RolUsuario.Encargado, "Encargada");

        var resultado = bd.Config.EditarClienteFiado(clienteId, "  Ana López  ", " 5552222222 ");

        Assert.True(resultado.IsSuccess, resultado.Message);
        var cliente = bd.Db.ObtenerClienteFiadoPorId(clienteId);
        Assert.Equal("Ana López", cliente.Nombre);
        Assert.Equal("5552222222", cliente.Telefono);

        var registro = Assert.Single(bd.Db.ObtenerBitacora(accion: AccionBitacora.EditarCliente));
        Assert.Contains("Ana López", registro.DatosDespues);
        Assert.Contains("5552222222", registro.DatosDespues);
        Assert.Contains("5551111111", registro.DatosAntes);
        Assert.Equal("Encargada", registro.Usuario);
    }

    [Fact]
    public void EditarClienteFiado_ComoEmpleado_NoCambiaNada()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var clienteId = CrearCliente(bd, "Ana", "5551111111");
        bd.IniciarSesion(RolUsuario.Empleado, "Empleado");

        var resultado = bd.Config.EditarClienteFiado(clienteId, "Otra persona", "5559999999");

        Assert.True(resultado.IsError);
        var cliente = bd.Db.ObtenerClienteFiadoPorId(clienteId);
        Assert.Equal("Ana", cliente.Nombre);
        Assert.Equal("5551111111", cliente.Telefono);
        Assert.Empty(bd.Db.ObtenerBitacora(accion: AccionBitacora.EditarCliente));
    }

    [Fact]
    public void EditarClienteFiado_NoModificaElSaldo()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var clienteId = CrearCliente(bd, "Ana", saldoInicial: 15_000);
        var producto = CrearProducto(bd, "A1", precio: 5_000, costo: 2_000, stock: 5);
        VenderFiado(bd, clienteId, producto, 2);

        var resultado = bd.Config.EditarClienteFiado(clienteId, "Ana López", "5552222222");

        Assert.True(resultado.IsSuccess, resultado.Message);
        Assert.Equal(25_000, bd.Db.ObtenerClienteFiadoPorId(clienteId).SaldoActual);
    }

    [Fact]
    public void EditarClienteFiado_SinNombreOFueraDeRango_NoCambiaNada()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var clienteId = CrearCliente(bd, "Ana", "5551111111");

        var sinNombre = bd.Config.EditarClienteFiado(clienteId, "   ", "5552222222");
        var inexistente = bd.Config.EditarClienteFiado(9_999, "Fantasma", "");

        Assert.True(sinNombre.IsError);
        Assert.True(inexistente.IsError);
        var cliente = bd.Db.ObtenerClienteFiadoPorId(clienteId);
        Assert.Equal("Ana", cliente.Nombre);
        Assert.Equal("5551111111", cliente.Telefono);
    }

    [Fact]
    public void ObtenerEstadoCuentaCliente_SumaVentasYAbonosYDejaElSaldoVigente()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var clienteId = CrearCliente(bd);
        var producto = CrearProducto(bd, "A1", precio: 5_000, costo: 2_000, stock: 10);

        var primera = VenderFiado(bd, clienteId, producto, 2);
        VenderFiado(bd, clienteId, producto, 1);
        Assert.True(bd.Config.RegistrarAbonoCliente(clienteId, 4_000, "Encargada").IsSuccess);

        var estado = bd.Config.ObtenerEstadoCuentaCliente(clienteId);

        Assert.True(estado.IsSuccess, estado.Message);
        Assert.Equal(clienteId, estado.Value.Cliente.Id);
        Assert.Equal(2, estado.Value.Ventas.Count);
        Assert.Equal(15_000, estado.Value.TotalFiado);
        Assert.Single(estado.Value.Abonos);
        Assert.Equal(4_000, estado.Value.TotalAbonado);
        Assert.Equal(11_000, estado.Value.SaldoActual);
        Assert.Contains(estado.Value.Ventas, v => v.Id == primera.Id && v.PagoFiado == 10_000);
    }

    [Fact]
    public void ObtenerEstadoCuentaCliente_ExcluyeVentasAnuladas()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var clienteId = CrearCliente(bd);
        var producto = CrearProducto(bd, "A1", precio: 5_000, costo: 2_000, stock: 10);

        var vigente = VenderFiado(bd, clienteId, producto, 1);
        var anulada = VenderFiado(bd, clienteId, producto, 2);
        Assert.True(new VentaService(bd.Db).AnularVenta(anulada.Id, "Devolución", "Encargada").IsSuccess);

        var estado = bd.Config.ObtenerEstadoCuentaCliente(clienteId);

        Assert.True(estado.IsSuccess, estado.Message);
        var venta = Assert.Single(estado.Value.Ventas);
        Assert.Equal(vigente.Id, venta.Id);
        Assert.Equal(5_000, estado.Value.TotalFiado);
        Assert.Equal(5_000, estado.Value.SaldoActual);
    }

    [Fact]
    public void ObtenerEstadoCuentaCliente_SinMovimientos_DevuelveCeros()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var clienteId = CrearCliente(bd, saldoInicial: 2_500);

        var estado = bd.Config.ObtenerEstadoCuentaCliente(clienteId);

        Assert.True(estado.IsSuccess, estado.Message);
        Assert.Empty(estado.Value.Ventas);
        Assert.Empty(estado.Value.Abonos);
        Assert.Equal(0, estado.Value.TotalFiado);
        Assert.Equal(0, estado.Value.TotalAbonado);
        Assert.Equal(2_500, estado.Value.SaldoActual);
    }

    [Fact]
    public void ObtenerEstadoCuentaCliente_Inexistente_DevuelveError()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var estado = bd.Config.ObtenerEstadoCuentaCliente(9_999);

        Assert.True(estado.IsError);
    }
}
