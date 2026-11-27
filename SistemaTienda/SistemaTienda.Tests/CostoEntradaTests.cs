using Microsoft.Data.Sqlite;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;

namespace SistemaTienda.Tests;

public sealed class CostoEntradaTests
{
    private static Producto CrearProducto(BaseDatosPrueba bd, long costo, int stock = 5)
    {
        var producto = new Producto
        {
            CodigoBarras = "C1",
            Nombre = "Producto con costo",
            Costo = costo,
            Precio = 1_000,
            Stock = stock
        };

        Assert.True(bd.Inventario.AgregarProducto(producto).IsSuccess);
        return bd.Inventario.BuscarPorId(producto.Id);
    }

    [Fact]
    public void RegistrarEntrada_ConCostoNuevo_ActualizaElCatalogo()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, costo: 600);

        var entrada = bd.Inventario.RegistrarEntrada(producto.Id, 10, 700, "Dueño", "Compra");

        Assert.True(entrada.IsSuccess, entrada.Message);
        Assert.Contains("actualizado", entrada.Message);
        var actualizado = bd.Inventario.BuscarPorId(producto.Id);
        Assert.Equal(700, actualizado.Costo);
        Assert.Equal(15, actualizado.Stock);
        Assert.Equal(1, bd.Db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Bitacora WHERE Accion = @accion",
            new SqliteParameter("@accion", AccionBitacora.EntradaInventario)));
    }

    [Fact]
    public void RegistrarEntrada_SinActualizar_ConservaElCosto()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, costo: 600);

        var entrada = bd.Inventario.RegistrarEntrada(producto.Id, 10, 700, "Dueño", "Compra",
            actualizarCosto: false);

        Assert.True(entrada.IsSuccess, entrada.Message);
        Assert.DoesNotContain("actualizado", entrada.Message);
        Assert.Equal(600, bd.Inventario.BuscarPorId(producto.Id).Costo);
        Assert.Equal(15, bd.Inventario.BuscarPorId(producto.Id).Stock);
    }

    [Fact]
    public void RegistrarEntrada_ConCostoCero_ConservaElCostoDelCatalogo()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, costo: 600);

        var entrada = bd.Inventario.RegistrarEntrada(producto.Id, 3, 0, "Dueño", "Donación");

        Assert.True(entrada.IsSuccess, entrada.Message);
        Assert.Equal(600, bd.Inventario.BuscarPorId(producto.Id).Costo);
    }

    [Fact]
    public void RegistrarEntrada_ActualizaElCostoCongeladoDeLaSiguienteVenta()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, costo: 600);

        Assert.True(bd.Inventario.RegistrarEntrada(producto.Id, 10, 700, "Dueño").IsSuccess);

        var venta = new VentaService(bd.Db).RegistrarVenta(
            new[] { new LineaVentaSolicitada { ProductoId = producto.Id, Cantidad = 1 } },
            new PagoSolicitado { Efectivo = 1_000 }, "Cajero");

        Assert.True(venta.IsSuccess, venta.Message);
        var linea = Assert.Single(bd.Db.ObtenerLineasVenta(venta.Value.Id));
        Assert.Equal(700, linea.CostoUnitario);
        Assert.Equal(300, linea.Importe - linea.CostoUnitario);
    }

    [Fact]
    public void RegistrarEntrada_EncargadoPuedeActualizarElCosto()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, costo: 600);
        bd.IniciarSesion(RolUsuario.Encargado, "Encargada");

        var entrada = bd.Inventario.RegistrarEntrada(producto.Id, 4, 750, "Encargada");

        Assert.True(entrada.IsSuccess, entrada.Message);
        Assert.Equal(750, bd.Inventario.BuscarPorId(producto.Id).Costo);
    }

    [Fact]
    public void RegistrarEntrada_EmpleadoSigueSinPermiso()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, costo: 600);
        bd.IniciarSesion(RolUsuario.Empleado, "Empleado");

        var entrada = bd.Inventario.RegistrarEntrada(producto.Id, 4, 750, "Empleado");

        Assert.True(entrada.IsError);
        Assert.Equal(600, bd.Inventario.BuscarPorId(producto.Id).Costo);
        Assert.Equal(5, bd.Inventario.BuscarPorId(producto.Id).Stock);
    }

    [Fact]
    public void RegistrarEntrada_ConCostoIgual_NoDuplicaLaActualizacion()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, costo: 600);

        var entrada = bd.Inventario.RegistrarEntrada(producto.Id, 2, 600, "Dueño");

        Assert.True(entrada.IsSuccess, entrada.Message);
        Assert.DoesNotContain("actualizado", entrada.Message);
        Assert.Equal(600, bd.Inventario.BuscarPorId(producto.Id).Costo);
    }
}
