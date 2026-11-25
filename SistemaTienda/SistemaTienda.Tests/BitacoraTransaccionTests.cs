using Microsoft.Data.Sqlite;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;

namespace SistemaTienda.Tests;

/// <summary>
/// H4.2b: la bitácora de inventario, altas/ediciones, usuarios y configuración
/// se escribe dentro de la misma transacción que la operación. Si la bitácora
/// falla, la operación completa se revierte.
/// </summary>
public sealed class BitacoraTransaccionTests
{
    private static int Contar(BaseDatosPrueba bd, string accion) =>
        bd.Db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Bitacora WHERE Accion = @accion",
            new SqliteParameter("@accion", accion));

    private static string UltimoDetalle(BaseDatosPrueba bd, string accion) =>
        bd.Db.ExecuteScalar<string>(
            "SELECT DatosDespues FROM Bitacora WHERE Accion = @accion ORDER BY Id DESC LIMIT 1",
            new SqliteParameter("@accion", accion));

    /// <summary>
    /// Rompe la inserción de bitácora para una acción concreta. La tabla es
    /// inmutable, pero un disparador temporal permite simular la falla.
    /// </summary>
    private static void FallarBitacora(BaseDatosPrueba bd, string accion) =>
        bd.Db.ExecuteCommand($@"
            CREATE TRIGGER falla_bitacora_{accion}
            BEFORE INSERT ON Bitacora
            WHEN NEW.Accion = '{accion}'
            BEGIN
                SELECT RAISE(ABORT, 'bitacora no disponible');
            END;");

    private static Producto AltaProducto(BaseDatosPrueba bd, string codigo,
        int stock = 10, long costo = 1_000, long precio = 2_000)
    {
        var producto = new Producto
        {
            CodigoBarras = codigo,
            Nombre = $"Producto {codigo}",
            Costo = costo,
            Precio = precio,
            Stock = stock
        };

        var alta = bd.Inventario.AgregarProducto(producto);
        Assert.True(alta.IsSuccess, alta.Message);
        return producto;
    }

    [Fact]
    public void EntradaMermaYAjuste_DejanUnaFilaCadaUno()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = AltaProducto(bd, "H421");

        Assert.True(bd.Inventario.RegistrarEntrada(producto.Id, 5, 1_200, "Dueño").IsSuccess);
        Assert.True(bd.Inventario.RegistrarMerma(producto.Id, 2, "Producto dañado", "Dueño").IsSuccess);
        Assert.True(bd.Inventario.AjustarStock(producto.Id, 20, "Conteo físico", "Dueño").IsSuccess);

        Assert.Equal(1, Contar(bd, AccionBitacora.EntradaInventario));
        Assert.Equal(1, Contar(bd, AccionBitacora.Merma));
        Assert.Equal(1, Contar(bd, AccionBitacora.AjusteInventario));
        Assert.Equal(20, bd.Db.ObtenerStock(producto.Id));
        Assert.Contains("Stock=15", UltimoDetalle(bd, AccionBitacora.EntradaInventario));
        Assert.Contains("Stock=20", UltimoDetalle(bd, AccionBitacora.AjusteInventario));
    }

    [Fact]
    public void Entrada_SiFallaLaBitacora_NoCambiaElStock()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = AltaProducto(bd, "H422", stock: 10);
        FallarBitacora(bd, AccionBitacora.EntradaInventario);

        var entrada = bd.Inventario.RegistrarEntrada(producto.Id, 5, 1_200, "Dueño");

        Assert.True(entrada.IsError);
        Assert.Equal(10, bd.Db.ObtenerStock(producto.Id));
        Assert.Equal(0, Contar(bd, AccionBitacora.EntradaInventario));
        Assert.Single(bd.Db.ObtenerHistorialMovimientos(producto.Id));
    }

    [Fact]
    public void Productos_AltaEdicionYPrecio_DejanBitacora()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = AltaProducto(bd, "H423");

        Assert.Equal(1, Contar(bd, AccionBitacora.EditarProducto));

        producto.Precio = 2_500;
        Assert.True(bd.Inventario.ActualizarProducto(producto).IsSuccess);
        Assert.Equal(1, Contar(bd, AccionBitacora.CambioPrecio));

        Assert.True(bd.Inventario.DesactivarProducto(producto.Id).IsSuccess);
        Assert.Equal(1, Contar(bd, AccionBitacora.CambiarEstadoProducto));
    }

    [Fact]
    public void ClienteYProveedor_AltasYEdiciones_DejanBitacora()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var cliente = bd.Config.AgregarClienteFiado(new ClienteFiado
        {
            Nombre = "Cliente Prueba",
            Telefono = "5551234567"
        });
        Assert.True(cliente.IsSuccess, cliente.Message);
        Assert.Equal(1, Contar(bd, AccionBitacora.Cliente));

        var proveedor = bd.Config.AgregarProveedor(new Proveedor
        {
            Nombre = "Proveedor Prueba",
            Clave = "PRV1"
        });
        Assert.True(proveedor.IsSuccess, proveedor.Message);
        Assert.Equal(1, Contar(bd, AccionBitacora.Proveedor));
    }

    [Fact]
    public void UsuariosYConfiguracion_DejanBitacoraDentroDeLaTransaccion()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var alta = bd.Config.AgregarUsuario("Cajera H42", "5678", RolUsuario.Encargado);
        Assert.True(alta.IsSuccess, alta.Message);
        Assert.Equal(1, Contar(bd, AccionBitacora.Usuario));

        var pin = bd.Config.CambiarPin(alta.Value.UsuarioId, "8765");
        Assert.True(pin.IsSuccess, pin.Message);
        Assert.Equal(1, Contar(bd, AccionBitacora.CambiarPin));

        var config = bd.Db.ObtenerConfiguracion();
        config.NombreTienda = "Tienda H42";
        Assert.True(bd.Config.ActualizarConfiguracion(config).IsSuccess);
        Assert.Equal(1, Contar(bd, AccionBitacora.Configuracion));
        Assert.Contains("Nombre=Tienda H42", UltimoDetalle(bd, AccionBitacora.Configuracion));
    }

    [Fact]
    public void Gasto_SiFallaLaBitacora_NoSeGuarda()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        FallarBitacora(bd, AccionBitacora.Gasto);

        var gasto = bd.Config.AgregarGasto(new Gasto
        {
            Concepto = "Prueba H42",
            Monto = 5_000,
            MetodoPago = MetodoPagoAbono.Efectivo,
            Origen = OrigenGasto.Caja
        });

        Assert.True(gasto.IsError);
        Assert.Empty(bd.Db.ObtenerGastos());
        Assert.Equal(0, Contar(bd, AccionBitacora.Gasto));
    }

    [Fact]
    public void Venta_SiFallaLaBitacora_NoSeGuardaNiMueveStock()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = AltaProducto(bd, "H424", stock: 10);
        FallarBitacora(bd, AccionBitacora.Venta);

        var venta = new VentaService(bd.Db).RegistrarVenta(
            new List<LineaVentaSolicitada>
            {
                new() { ProductoId = producto.Id, Cantidad = 2 }
            },
            new PagoSolicitado { Efectivo = producto.Precio * 2 },
            "Dueño");

        Assert.True(venta.IsError);
        Assert.Empty(bd.Db.ObtenerVentasDelDia(DateTime.Today));
        Assert.Equal(10, bd.Db.ObtenerStock(producto.Id));
        Assert.Equal(0, Contar(bd, AccionBitacora.Venta));
    }
}
