using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Tests;

/// <summary>
/// H6.2: tickets en espera persistidos. No reservan stock, recalculan precios al
/// retomar y la venta cierra el ticket en la misma transacción.
/// </summary>
public sealed class TicketsEsperaTests
{
    private static Producto CrearProducto(BaseDatosPrueba bd, string codigo,
        int stock = 10, long precio = 2_000)
    {
        var producto = new Producto
        {
            CodigoBarras = codigo,
            Nombre = $"Producto {codigo}",
            Costo = 1_000,
            Precio = precio,
            Stock = stock
        };
        Assert.True(bd.Inventario.AgregarProducto(producto).IsSuccess);
        return producto;
    }

    private static int Contar(BaseDatosPrueba bd, string sql) =>
        bd.Db.ExecuteScalar<int>(sql);

    [Fact]
    public void Dejar_GuardaTicketSinMoverStockNiCaja()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "H62-1", stock: 10);
        var servicio = new TicketEsperaService(bd.Db);

        var ticket = servicio.Dejar(
            new[] { new LineaTicketEsperaSolicitada(producto.Id, 2) }, "Señora blusa roja");

        Assert.True(ticket.IsSuccess, ticket.Message);
        Assert.True(ticket.Value.Id > 0);
        Assert.Equal(4_000, ticket.Value.TotalEstimado);
        Assert.Equal("Señora blusa roja", ticket.Value.Nombre);
        Assert.Equal(10, bd.Db.ObtenerStock(producto.Id));
        Assert.Empty(bd.Db.ObtenerVentasDelDia(DateTime.Today));
        Assert.Equal(1, Contar(bd, "SELECT COUNT(*) FROM MovimientoInventario"));
        Assert.Equal(1, Contar(bd, "SELECT COUNT(*) FROM Bitacora " +
            $"WHERE Accion = '{AccionBitacora.DejarTicketEspera}'"));

        var guardado = bd.Db.ObtenerTicketEspera(ticket.Value.Id);
        Assert.Equal(EstadoTicketEspera.Disponible, guardado.Estado);
        Assert.Single(guardado.Lineas);
        Assert.Equal(2_000, guardado.Lineas[0].PrecioUnitario);
    }

    [Fact]
    public void Dejar_MasDeDiez_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "H62-2");
        var servicio = new TicketEsperaService(bd.Db);

        for (var i = 0; i < TicketEsperaService.MaximoTickets; i++)
            Assert.True(servicio.Dejar(
                new[] { new LineaTicketEsperaSolicitada(producto.Id, 1) }).IsSuccess);

        var extra = servicio.Dejar(new[] { new LineaTicketEsperaSolicitada(producto.Id, 1) });

        Assert.True(extra.IsError);
        Assert.Contains("10 tickets", extra.Message);
        Assert.Equal(10, servicio.Listar().Value.Count);
    }

    [Fact]
    public void Listar_MuestraElPrecioActualYElCambio()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "H62-3", precio: 2_000);
        var servicio = new TicketEsperaService(bd.Db);
        var ticket = servicio.Dejar(
            new[] { new LineaTicketEsperaSolicitada(producto.Id, 1) });
        Assert.True(ticket.IsSuccess, ticket.Message);

        producto.Precio = 2_500;
        Assert.True(bd.Inventario.ActualizarProducto(producto).IsSuccess);

        var linea = Assert.Single(servicio.Listar().Value.Single().Lineas);

        Assert.Equal(2_000, linea.PrecioUnitario);
        Assert.Equal(2_500, linea.PrecioActual);
        Assert.True(linea.CambioPrecio);
        Assert.Equal(producto.Nombre, linea.ProductoNombre);
    }

    [Fact]
    public void Retomar_MarcaElTicketYRegistraBitacora()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "H62-4");
        var servicio = new TicketEsperaService(bd.Db);
        var ticket = servicio.Dejar(new[] { new LineaTicketEsperaSolicitada(producto.Id, 2) });

        var retomado = servicio.Retomar(ticket.Value.Id);

        Assert.True(retomado.IsSuccess, retomado.Message);
        var guardado = bd.Db.ObtenerTicketEspera(ticket.Value.Id);
        Assert.Equal(EstadoTicketEspera.Retomado, guardado.Estado);
        Assert.Equal("Dueño", guardado.RetomadoPor);
        Assert.Equal(1, Contar(bd, "SELECT COUNT(*) FROM Bitacora " +
            $"WHERE Accion = '{AccionBitacora.RetomarTicketEspera}'"));
    }

    [Fact]
    public void Retomar_OtroCajero_SeBloquea()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "H62-5");
        var servicio = new TicketEsperaService(bd.Db);
        var ticket = servicio.Dejar(new[] { new LineaTicketEsperaSolicitada(producto.Id, 1) });
        Assert.True(servicio.Retomar(ticket.Value.Id).IsSuccess);

        bd.IniciarSesion(RolUsuario.Encargado, "Cajera dos");
        var intento = new TicketEsperaService(bd.Db).Retomar(ticket.Value.Id);

        Assert.True(intento.IsError);
        Assert.Contains("otro cajero", intento.Message);
    }

    [Fact]
    public void Descartar_DejaBitacoraYNoSePuedeCobrar()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "H62-6");
        var servicio = new TicketEsperaService(bd.Db);
        var ticket = servicio.Dejar(new[] { new LineaTicketEsperaSolicitada(producto.Id, 3) });
        Assert.True(servicio.Retomar(ticket.Value.Id).IsSuccess);

        var descarte = servicio.Descartar(ticket.Value.Id);

        Assert.True(descarte.IsSuccess, descarte.Message);
        Assert.Equal(EstadoTicketEspera.Descartado,
            bd.Db.ObtenerTicketEspera(ticket.Value.Id).Estado);
        Assert.Equal(1, Contar(bd, "SELECT COUNT(*) FROM Bitacora " +
            $"WHERE Accion = '{AccionBitacora.DescartarTicketEspera}'"));

        var venta = new VentaService(bd.Db).RegistrarVenta(
            new List<LineaVentaSolicitada> { new() { ProductoId = producto.Id, Cantidad = 3 } },
            new PagoSolicitado { Efectivo = 6_000 }, "Dueño", ticket.Value.Id);

        Assert.True(venta.IsError);
        Assert.Empty(bd.Db.ObtenerVentasDelDia(DateTime.Today));
    }

    [Fact]
    public void Venta_CierraElTicketEnLaMismaTransaccion()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "H62-7", stock: 10);
        var servicio = new TicketEsperaService(bd.Db);
        var ticket = servicio.Dejar(new[] { new LineaTicketEsperaSolicitada(producto.Id, 2) });
        Assert.True(servicio.Retomar(ticket.Value.Id).IsSuccess);

        var venta = new VentaService(bd.Db).RegistrarVenta(
            new List<LineaVentaSolicitada> { new() { ProductoId = producto.Id, Cantidad = 2 } },
            new PagoSolicitado { Efectivo = 4_000 }, "Dueño", ticket.Value.Id);

        Assert.True(venta.IsSuccess, venta.Message);
        var guardado = bd.Db.ObtenerTicketEspera(ticket.Value.Id);
        Assert.Equal(EstadoTicketEspera.Cobrado, guardado.Estado);
        Assert.Equal(venta.Value.Id, guardado.VentaId);
        Assert.Equal(8, bd.Db.ObtenerStock(producto.Id));
        Assert.Single(bd.Db.ObtenerVentasDelDia(DateTime.Today));

        var segunda = new VentaService(bd.Db).RegistrarVenta(
            new List<LineaVentaSolicitada> { new() { ProductoId = producto.Id, Cantidad = 2 } },
            new PagoSolicitado { Efectivo = 4_000 }, "Dueño", ticket.Value.Id);

        Assert.True(segunda.IsError);
        Assert.Single(bd.Db.ObtenerVentasDelDia(DateTime.Today));
        Assert.Equal(8, bd.Db.ObtenerStock(producto.Id));
    }

    [Fact]
    public void Venta_ConTicketDeOtroCajero_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "H62-8");
        var servicio = new TicketEsperaService(bd.Db);
        var ticket = servicio.Dejar(new[] { new LineaTicketEsperaSolicitada(producto.Id, 1) });
        Assert.True(servicio.Retomar(ticket.Value.Id).IsSuccess);

        var venta = new VentaService(bd.Db).RegistrarVenta(
            new List<LineaVentaSolicitada> { new() { ProductoId = producto.Id, Cantidad = 1 } },
            new PagoSolicitado { Efectivo = 2_000 }, "Otro cajero", ticket.Value.Id);

        Assert.True(venta.IsError);
        Assert.Empty(bd.Db.ObtenerVentasDelDia(DateTime.Today));
        Assert.Equal(10, bd.Db.ObtenerStock(producto.Id));
    }

    [Fact]
    public void Listar_LiberaTicketsRetomadosVencidos()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "H62-9");
        var servicio = new TicketEsperaService(bd.Db);
        var ticket = servicio.Dejar(new[] { new LineaTicketEsperaSolicitada(producto.Id, 1) });
        Assert.True(servicio.Retomar(ticket.Value.Id).IsSuccess);
        bd.Db.ExecuteCommand("UPDATE TicketsEspera SET RetomadoFecha = @fecha",
            new Microsoft.Data.Sqlite.SqliteParameter("@fecha", DateTime.Now.AddHours(-2)));

        var lista = servicio.Listar();

        Assert.True(lista.IsSuccess, lista.Message);
        var libre = Assert.Single(lista.Value);
        Assert.Equal(EstadoTicketEspera.Disponible, libre.Estado);
        Assert.True(servicio.Retomar(ticket.Value.Id).IsSuccess);
    }

    [Fact]
    public void Migracion_DesdeV18_CreaLasTablasDeTickets()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        bd.Db.ExecuteCommand("DROP TABLE TicketEsperaLineas");
        bd.Db.ExecuteCommand("DROP TABLE TicketsEspera");
        bd.Db.ExecuteCommand("PRAGMA user_version = 18");

        bd.Db.InicializarBaseDeDatos();

        Assert.Equal(Migraciones.VersionActual, bd.Db.ExecuteScalar<int>("PRAGMA user_version"));
        Assert.Equal(1, Contar(bd, "SELECT COUNT(*) FROM sqlite_master " +
            "WHERE type='table' AND name='TicketsEspera'"));
        Assert.Equal(1, Contar(bd, "SELECT COUNT(*) FROM sqlite_master " +
            "WHERE type='table' AND name='TicketEsperaLineas'"));
    }
}
