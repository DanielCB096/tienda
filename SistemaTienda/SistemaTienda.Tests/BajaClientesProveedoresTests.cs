using SistemaTienda.Application.Services;
using SistemaTienda.Core;

namespace SistemaTienda.Tests;

public sealed class BajaClientesProveedoresTests
{
    [Fact]
    public void MigracionV10_MarcaRegistrosExistentesComoActivosYConservaSaldos()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var cliente = bd.Config.AgregarClienteFiado(new ClienteFiado
        {
            Nombre = "Cliente heredado",
            SaldoActual = 1_250
        });
        var proveedor = bd.Config.AgregarProveedor(new Proveedor
        {
            Nombre = "Proveedor heredado",
            SaldoPendiente = 2_500
        });

        bd.Db.ExecuteCommand(@"
            ALTER TABLE ClientesFiado DROP COLUMN Activo;
            ALTER TABLE Proveedores DROP COLUMN Activo;
            PRAGMA user_version = 9;
        ");
        bd.Db.InicializarBaseDeDatos();

        var clienteMigrado = bd.Db.ObtenerClienteFiadoPorId(cliente.Value.ClienteId);
        var proveedorMigrado = bd.Db.ObtenerProveedorPorId(proveedor.Value.ProveedorId);

        Assert.Equal(10, bd.Db.ExecuteScalar<int>("PRAGMA user_version"));
        Assert.True(clienteMigrado.Activo);
        Assert.Equal(1_250, clienteMigrado.SaldoActual);
        Assert.True(proveedorMigrado.Activo);
        Assert.Equal(2_500, proveedorMigrado.SaldoPendiente);
    }

    [Fact]
    public void CambiarEstadoCliente_SoloDuenioPuedeDesactivarYReactivarConBitacora()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var alta = bd.Config.AgregarClienteFiado(new ClienteFiado
        {
            Nombre = "Ana",
            SaldoActual = 3_000
        });
        var clienteId = alta.Value.ClienteId;

        bd.IniciarSesion(RolUsuario.Encargado, "Encargada");
        Assert.True(bd.Config.DesactivarCliente(clienteId).IsError);

        bd.IniciarSesion(RolUsuario.Dueño, "Dueño");
        var baja = bd.Config.DesactivarCliente(clienteId);

        Assert.True(baja.IsSuccess, baja.Message);
        var inactivo = bd.Db.ObtenerClienteFiadoPorId(clienteId);
        Assert.False(inactivo.Activo);
        Assert.Equal(3_000, inactivo.SaldoActual);

        var registroBaja = Assert.Single(new BitacoraService(bd.Db)
            .Consultar(accion: AccionBitacora.CambiarEstadoCliente).Value);
        Assert.Equal(clienteId.ToString(), registroBaja.EntidadId);
        Assert.Contains("Activo=1", registroBaja.DatosAntes);
        Assert.Contains("Activo=0", registroBaja.DatosDespues);

        var reactivacion = bd.Config.ReactivarCliente(clienteId);
        Assert.True(reactivacion.IsSuccess, reactivacion.Message);
        Assert.True(bd.Db.ObtenerClienteFiadoPorId(clienteId).Activo);
    }

    [Fact]
    public void ClienteInactivo_PuedeRecibirAbonoPeroNoNuevaVentaAFiado()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        bd.IniciarSesion(RolUsuario.Dueño, "Dueño");
        var alta = bd.Config.AgregarClienteFiado(new ClienteFiado
        {
            Nombre = "Ana",
            SaldoActual = 5_000
        });
        var clienteId = alta.Value.ClienteId;
        var producto = new Producto
        {
            CodigoBarras = "A1",
            Nombre = "Producto",
            Costo = 1_000,
            Precio = 2_000,
            Stock = 4
        };
        Assert.True(bd.Inventario.AgregarProducto(producto).IsSuccess);
        Assert.True(bd.Config.DesactivarCliente(clienteId).IsSuccess);

        var abono = bd.Config.RegistrarAbonoCliente(clienteId, 500, "Dueño");
        var venta = new VentaService(bd.Db).RegistrarVenta(
            new[] { new LineaVentaSolicitada { ProductoId = producto.Id, Cantidad = 1 } },
            new PagoSolicitado { Fiado = producto.Precio, ClienteId = clienteId },
            "Dueño");
        var ventaDirecta = bd.Db.RegistrarVenta(
            new Venta
            {
                Fecha = DateTime.Now,
                Usuario = "Dueño",
                ClienteId = clienteId,
                MetodoPago = MetodoPagoVenta.Fiado,
                Total = producto.Precio,
                CostoTotal = producto.Costo,
                PagoFiado = producto.Precio
            },
            new[]
            {
                new VentaLinea
                {
                    ProductoId = producto.Id,
                    CodigoBarras = producto.CodigoBarras,
                    Nombre = producto.Nombre,
                    Cantidad = 1,
                    PrecioUnitario = producto.Precio,
                    CostoUnitario = producto.Costo,
                    Importe = producto.Precio
                }
            });

        Assert.True(abono.IsSuccess, abono.Message);
        Assert.Equal(4_500, bd.Db.ObtenerClienteFiadoPorId(clienteId).SaldoActual);
        Assert.True(venta.IsError);
        Assert.Contains("inactivo", venta.Message);
        Assert.True(ventaDirecta.IsError);
        Assert.Contains("inactivo", ventaDirecta.Message);
        Assert.Empty(bd.Db.ObtenerVentasDelDia(DateTime.Now));
        Assert.Equal(4, bd.Inventario.BuscarPorId(producto.Id).Stock);
    }

    [Fact]
    public void ProveedorInactivo_PuedeLiquidarSaldoYReactivarse()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        bd.IniciarSesion(RolUsuario.Dueño, "Dueño");
        var alta = bd.Config.AgregarProveedor(new Proveedor
        {
            Nombre = "Distribuidora",
            SaldoPendiente = 3_000
        });
        var proveedorId = alta.Value.ProveedorId;

        var baja = bd.Config.DesactivarProveedor(proveedorId);
        var pago = bd.Config.RegistrarPagoProveedor(
            proveedorId, 1_000, MetodoPagoProveedor.Transferencia);

        Assert.True(baja.IsSuccess, baja.Message);
        Assert.True(pago.IsSuccess, pago.Message);
        var inactivo = bd.Db.ObtenerProveedorPorId(proveedorId);
        Assert.False(inactivo.Activo);
        Assert.Equal(2_000, inactivo.SaldoPendiente);

        bd.IniciarSesion(RolUsuario.Encargado, "Encargada");
        Assert.True(bd.Config.ReactivarProveedor(proveedorId).IsError);

        bd.IniciarSesion(RolUsuario.Dueño, "Dueño");
        var reactivacion = bd.Config.ReactivarProveedor(proveedorId);
        Assert.True(reactivacion.IsSuccess, reactivacion.Message);
        Assert.True(bd.Db.ObtenerProveedorPorId(proveedorId).Activo);

        var registros = new BitacoraService(bd.Db)
            .Consultar(accion: AccionBitacora.CambiarEstadoProveedor).Value;
        Assert.Equal(2, registros.Count);
        Assert.All(registros, registro => Assert.Equal(proveedorId.ToString(), registro.EntidadId));
    }
}
