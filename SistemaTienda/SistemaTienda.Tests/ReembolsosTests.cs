using Microsoft.Data.Sqlite;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Tests;

public sealed class ReembolsosTests
{
    private static Producto CrearProducto(BaseDatosPrueba bd, long precio, long costo, int stock = 10)
    {
        var producto = new Producto
        {
            CodigoBarras = "RE1",
            Nombre = "Producto reembolso",
            Costo = costo,
            Precio = precio,
            Stock = stock
        };

        Assert.True(bd.Inventario.AgregarProducto(producto).IsSuccess);
        return producto;
    }

    private static Venta VenderYAnular(BaseDatosPrueba bd, Producto producto, PagoSolicitado pago)
    {
        var venta = new VentaService(bd.Db).RegistrarVenta(
            new[] { new LineaVentaSolicitada { ProductoId = producto.Id, Cantidad = 1 } },
            pago, "Cajero");
        Assert.True(venta.IsSuccess, venta.Message);

        var anulacion = new VentaService(bd.Db).AnularVenta(venta.Value.Id, "Prueba", "Dueño");
        Assert.True(anulacion.IsSuccess, anulacion.Message);
        return venta.Value;
    }

    [Fact]
    public void Reembolsar_Efectivo_CreaRetiroYAfectaElCorte()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, precio: 1_000, costo: 600);

        var venta = VenderYAnular(bd, producto, new PagoSolicitado
        {
            Efectivo = 1_000,
            EfectivoRecibido = 1_000
        });

        var estado = new VentaService(bd.Db).ObtenerEstadoReembolso(venta.Id);
        Assert.True(estado.IsSuccess, estado.Message);
        Assert.Equal(1_000, estado.Value.Pagado);
        Assert.Equal(1_000, estado.Value.Pendiente);

        var reembolso = new VentaService(bd.Db).RegistrarReembolso(
            venta.Id, MetodoPagoAbono.Efectivo, 1_000, "Dueño");

        Assert.True(reembolso.IsSuccess, reembolso.Message);
        Assert.NotNull(reembolso.Value.MovimientoCajaId);

        var actualizado = new VentaService(bd.Db).ObtenerEstadoReembolso(venta.Id).Value;
        Assert.Equal(1_000, actualizado.Reembolsado);
        Assert.Equal(0, actualizado.Pendiente);

        var resumen = bd.Db.ObtenerResumenCajaPendiente();
        Assert.Equal(19_000, resumen.EfectivoEsperado);
        Assert.Equal(1_000, resumen.RetirosCaja);
        Assert.Equal(1, bd.Db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Bitacora WHERE Accion = @accion",
            new SqliteParameter("@accion", AccionBitacora.ReembolsoVenta)));
    }

    [Fact]
    public void Reembolsar_Tarjeta_NoMueveLaCaja()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, precio: 1_000, costo: 600);

        var venta = VenderYAnular(bd, producto, new PagoSolicitado { Tarjeta = 1_000 });

        var reembolso = new VentaService(bd.Db).RegistrarReembolso(
            venta.Id, MetodoPagoAbono.Tarjeta, 1_000, "Dueño");

        Assert.True(reembolso.IsSuccess, reembolso.Message);
        Assert.Null(reembolso.Value.MovimientoCajaId);
        Assert.Equal(20_000, bd.Db.ObtenerResumenCajaPendiente().EfectivoEsperado);
        Assert.Equal(0, bd.Db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM MovimientosCaja WHERE Tipo = @tipo",
            new SqliteParameter("@tipo", TipoMovimientoCaja.Retiro)));
    }

    [Fact]
    public void Reembolsar_Mixta_SoloElEfectivoGeneraRetiro()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, precio: 1_000, costo: 600);

        var venta = VenderYAnular(bd, producto, new PagoSolicitado
        {
            Efectivo = 600,
            Tarjeta = 400
        });

        var servicio = new VentaService(bd.Db);
        Assert.True(servicio.RegistrarReembolso(venta.Id,
            MetodoPagoAbono.Efectivo, 600, "Dueño").IsSuccess);
        Assert.True(servicio.RegistrarReembolso(venta.Id,
            MetodoPagoAbono.Tarjeta, 400, "Dueño").IsSuccess);

        Assert.Equal(19_400, bd.Db.ObtenerResumenCajaPendiente().EfectivoEsperado);
        Assert.Equal(0, servicio.ObtenerEstadoReembolso(venta.Id).Value.Pendiente);
    }

    [Fact]
    public void Reembolsar_Parcial_SePuedeCompletarDespues()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, precio: 1_000, costo: 600);
        var venta = VenderYAnular(bd, producto, new PagoSolicitado { Efectivo = 1_000 });
        var servicio = new VentaService(bd.Db);

        Assert.True(servicio.RegistrarReembolso(venta.Id,
            MetodoPagoAbono.Efectivo, 400, "Dueño").IsSuccess);
        Assert.Equal(600, servicio.ObtenerEstadoReembolso(venta.Id).Value.Pendiente);

        Assert.True(servicio.RegistrarReembolso(venta.Id,
            MetodoPagoAbono.Transferencia, 600, "Dueño").IsSuccess);
        Assert.Equal(0, servicio.ObtenerEstadoReembolso(venta.Id).Value.Pendiente);

        var tercera = servicio.RegistrarReembolso(venta.Id,
            MetodoPagoAbono.Efectivo, 1, "Dueño");
        Assert.True(tercera.IsError);
        Assert.Contains("reembolsada por completo", tercera.Message);
    }

    [Fact]
    public void Reembolsar_ExcedeElPendiente_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, precio: 1_000, costo: 600);
        var venta = VenderYAnular(bd, producto, new PagoSolicitado { Efectivo = 1_000 });

        var reembolso = new VentaService(bd.Db).RegistrarReembolso(
            venta.Id, MetodoPagoAbono.Efectivo, 1_100, "Dueño");

        Assert.True(reembolso.IsError);
        Assert.Contains("pendiente", reembolso.Message);
        Assert.Empty(bd.Db.ObtenerReembolsosVenta(venta.Id));
    }

    [Fact]
    public void Reembolsar_VentaNoAnulada_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, precio: 1_000, costo: 600);
        var venta = new VentaService(bd.Db).RegistrarVenta(
            new[] { new LineaVentaSolicitada { ProductoId = producto.Id, Cantidad = 1 } },
            new PagoSolicitado { Efectivo = 1_000 }, "Cajero");
        Assert.True(venta.IsSuccess, venta.Message);

        var reembolso = new VentaService(bd.Db).RegistrarReembolso(
            venta.Value.Id, MetodoPagoAbono.Efectivo, 1_000, "Dueño");

        Assert.True(reembolso.IsError);
        Assert.Contains("anuladas", reembolso.Message);
    }

    [Fact]
    public void Reembolsar_VentaFiada_NoTieneEfectivoQueDevolver()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, precio: 1_000, costo: 600);
        var cliente = new ClienteFiado { Nombre = "Cliente reembolso" };
        Assert.True(bd.Config.AgregarClienteFiado(cliente).IsSuccess);
        var guardado = bd.Config.ObtenerTodosLosClientes().Value.Single();

        var venta = VenderYAnular(bd, producto, new PagoSolicitado
        {
            Fiado = 1_000,
            ClienteId = guardado.Id
        });

        var estado = new VentaService(bd.Db).ObtenerEstadoReembolso(venta.Id).Value;
        Assert.Equal(0, estado.Pagado);
        Assert.Equal(0, estado.Pendiente);

        var reembolso = new VentaService(bd.Db).RegistrarReembolso(
            venta.Id, MetodoPagoAbono.Efectivo, 1, "Dueño");

        Assert.True(reembolso.IsError);
        Assert.Contains("fiado", reembolso.Message);
        Assert.Equal(0, bd.Config.ObtenerTodosLosClientes().Value.Single().SaldoActual);
    }

    [Fact]
    public void Reembolsar_EmpleadoSinPermiso_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, precio: 1_000, costo: 600);
        var venta = VenderYAnular(bd, producto, new PagoSolicitado { Efectivo = 1_000 });
        bd.IniciarSesion(RolUsuario.Empleado, "Empleado");

        var reembolso = new VentaService(bd.Db).RegistrarReembolso(
            venta.Id, MetodoPagoAbono.Efectivo, 1_000, "Empleado");

        Assert.True(reembolso.IsError);
        Assert.Empty(bd.Db.ObtenerReembolsosVenta(venta.Id));
    }

    [Fact]
    public void Migracion_DesdeV21_CreaLosReembolsos()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        bd.Db.ExecuteCommand("DROP TABLE ReembolsosVenta");
        bd.Db.ExecuteCommand("PRAGMA user_version = 21");

        bd.Db.InicializarBaseDeDatos();

        Assert.Equal(Migraciones.VersionMaxima(Migraciones.ObtenerTodas()),
            bd.Db.ExecuteScalar<int>("PRAGMA user_version"));
        Assert.Equal(1, bd.Db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'ReembolsosVenta'"));
    }
}
