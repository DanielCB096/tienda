using Microsoft.Data.Sqlite;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Tests;

public sealed class EfectivoRecibidoTests
{
    private static Producto CrearProducto(BaseDatosPrueba bd, long precio = 2_500, int stock = 10)
    {
        var producto = new Producto
        {
            CodigoBarras = "R1",
            Nombre = "Producto con cambio",
            Costo = 1_000,
            Precio = precio,
            Stock = stock
        };

        var resultado = bd.Inventario.AgregarProducto(producto);
        Assert.True(resultado.IsSuccess, resultado.Message);
        return producto;
    }

    private static ProcessResult<Venta> Vender(BaseDatosPrueba bd, Producto producto,
        PagoSolicitado pago, int cantidad = 1)
    {
        return new VentaService(bd.Db).RegistrarVenta(
            new[] { new LineaVentaSolicitada { ProductoId = producto.Id, Cantidad = cantidad } },
            pago, "Cajero");
    }

    [Fact]
    public void VentaEnEfectivo_GuardaElRecibidoYElTicketMuestraElCambio()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd);

        var resultado = Vender(bd, producto, new PagoSolicitado
        {
            Efectivo = 7_500,
            EfectivoRecibido = 10_000
        }, cantidad: 3);

        Assert.True(resultado.IsSuccess, resultado.Message);
        Assert.Equal(10_000, resultado.Value.EfectivoRecibido);
        Assert.Equal(10_000, bd.Db.ExecuteScalar<long>(
            "SELECT EfectivoRecibido FROM Ventas WHERE Id = @id",
            new SqliteParameter("@id", resultado.Value.Id)));

        var guardada = bd.Db.ObtenerVentaPorId(resultado.Value.Id);
        Assert.Equal(10_000, guardada.EfectivoRecibido);

        var ticket = new TicketService().Generar(
            guardada, bd.Db.ObtenerLineasVenta(guardada.Id), bd.Config.ObtenerConfiguracion().Value);

        Assert.Contains("Recibido: $100.00", ticket.Texto);
        Assert.Contains("Cambio: $25.00", ticket.Texto);
    }

    [Fact]
    public void Reimpresion_ReutilizaElRecibidoGuardadoSinPasarloDeNuevo()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, precio: 5_000);

        var venta = Vender(bd, producto, new PagoSolicitado
        {
            Efectivo = 5_000,
            EfectivoRecibido = 20_000
        });

        Assert.True(venta.IsSuccess, venta.Message);

        var guardada = bd.Db.ObtenerVentaPorId(venta.Value.Id);
        var ticket = new TicketService().Generar(
            guardada, bd.Db.ObtenerLineasVenta(guardada.Id),
            bd.Config.ObtenerConfiguracion().Value, efectivoRecibido: null);

        Assert.Contains("Recibido: $200.00", ticket.Texto);
        Assert.Contains("Cambio: $150.00", ticket.Texto);
    }

    [Fact]
    public void VentaEnEfectivo_SinRecibido_GuardaNuloYNoMuestraCambio()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd);

        var venta = Vender(bd, producto, new PagoSolicitado { Efectivo = 2_500 });

        Assert.True(venta.IsSuccess, venta.Message);
        Assert.Null(venta.Value.EfectivoRecibido);

        var guardada = bd.Db.ObtenerVentaPorId(venta.Value.Id);
        var ticket = new TicketService().Generar(
            guardada, bd.Db.ObtenerLineasVenta(guardada.Id), bd.Config.ObtenerConfiguracion().Value);

        Assert.DoesNotContain("Recibido:", ticket.Texto);
        Assert.DoesNotContain("Cambio:", ticket.Texto);
    }

    [Fact]
    public void VentaConTarjeta_IgnoraElRecibidoYNoLoGuarda()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd);

        var venta = Vender(bd, producto, new PagoSolicitado
        {
            Tarjeta = 2_500,
            EfectivoRecibido = 5_000
        });

        Assert.True(venta.IsSuccess, venta.Message);
        Assert.Null(venta.Value.EfectivoRecibido);
        Assert.Null(bd.Db.ObtenerVentaPorId(venta.Value.Id).EfectivoRecibido);
    }

    [Fact]
    public void VentaMixta_NoGuardaRecibidoAunqueSeEnvie()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, precio: 5_000);

        var venta = Vender(bd, producto, new PagoSolicitado
        {
            Efectivo = 3_000,
            Tarjeta = 2_000,
            EfectivoRecibido = 3_000
        });

        Assert.True(venta.IsSuccess, venta.Message);
        Assert.Equal(MetodoPagoVenta.Mixto, venta.Value.MetodoPago);
        Assert.Null(venta.Value.EfectivoRecibido);
    }

    [Fact]
    public void VentaEnEfectivo_ConRecibidoMenorAlTotal_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd);

        var venta = Vender(bd, producto, new PagoSolicitado
        {
            Efectivo = 2_500,
            EfectivoRecibido = 2_000
        });

        Assert.True(venta.IsError);
        Assert.Contains("efectivo recibido", venta.Message);
        Assert.Equal(0, bd.Db.ExecuteScalar<int>("SELECT COUNT(*) FROM Ventas"));
    }

    [Fact]
    public void MigracionV12_AgregaColumnaConservaVentasYDejaNulo()
    {
        using var bd = new BaseDatosPrueba(versionEsquema: 11);

        bd.Db.ExecuteCommand(@"
            INSERT INTO Productos (CodigoBarras, Nombre, Costo, Precio, Stock, StockMinimo, Categoria, Impuesto, FechaCreacion, Activo)
            VALUES ('V11', 'Producto v11', 1000, 2500, 10, 5, '', 16.00, @fecha, 1)",
            new SqliteParameter("@fecha", DateTime.Now));
        var productoId = bd.Db.ExecuteScalar<int>(
            "SELECT Id FROM Productos WHERE CodigoBarras = 'V11'");

        bd.Db.ExecuteCommand(@"
            INSERT INTO Ventas (Fecha, Usuario, MetodoPago, Total, CostoTotal,
                                PagoEfectivo, PagoTarjeta, PagoFiado, ComisionTarjeta)
            VALUES (@fecha, 'Cajero', 'Efectivo', 2500, 1000, 2500, 0, 0, 0)",
            new SqliteParameter("@fecha", DateTime.Now));
        var ventaId = bd.Db.ExecuteScalar<int>("SELECT MAX(Id) FROM Ventas");

        bd.Db.ExecuteCommand(@"
            INSERT INTO VentaLineas (VentaId, ProductoId, CodigoBarras, Nombre, Cantidad,
                                     PrecioUnitario, CostoUnitario, Importe)
            VALUES (@venta, @producto, 'V11', 'Producto v11', 1, 2500, 1000, 2500)",
            new SqliteParameter("@venta", ventaId),
            new SqliteParameter("@producto", productoId));

        bd.Db.InicializarBaseDeDatos();

        Assert.Equal(Migraciones.VersionMaxima(Migraciones.ObtenerTodas()),
            bd.Db.ExecuteScalar<int>("PRAGMA user_version"));
        Assert.Equal(1, bd.Db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM pragma_table_info('Ventas') WHERE name = 'EfectivoRecibido'"));
        Assert.Equal(1, bd.Db.ExecuteScalar<int>("SELECT COUNT(*) FROM Ventas"));
        Assert.Null(bd.Db.ObtenerVentaPorId(ventaId).EfectivoRecibido);
        Assert.Single(RespaldoLocal.Listar(bd.Db.DirectorioRespaldos),
            r => r.Nombre.Contains("antes-migrar"));
    }
}
