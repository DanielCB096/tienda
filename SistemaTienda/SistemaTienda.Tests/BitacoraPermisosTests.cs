using Microsoft.Data.Sqlite;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;

namespace SistemaTienda.Tests;

public sealed class BitacoraPermisosTests
{
    private static Producto Producto(string codigo = "A1", int stock = 10, decimal costo = 10m,
        decimal precio = 15m) => new()
        {
            CodigoBarras = codigo,
            Nombre = "Producto",
            Costo = costo,
            Precio = precio,
            Stock = stock,
            StockMinimo = 1
        };

    [Fact]
    public void AjustarStock_ComoDueno_RegistraAntesYDespuesEnBitacora()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        bd.Inventario.AgregarProducto(Producto(stock: 10));
        var producto = Assert.Single(bd.Inventario.ObtenerProductos());
        bd.IniciarSesion(RolUsuario.Dueño, "Ana");

        var ajuste = bd.Inventario.AjustarStock(producto.Id, 7, "Conteo físico", "Ana");

        Assert.True(ajuste.IsSuccess, ajuste.Message);
        var registro = Assert.Single(
            bd.Db.ObtenerBitacora(accion: AccionBitacora.AjusteInventario));
        Assert.Equal("Ana", registro.Usuario);
        Assert.Contains("Stock=10", registro.DatosAntes);
        Assert.Contains("Stock=7", registro.DatosDespues);
        Assert.Equal("Conteo físico", registro.Motivo);
    }

    [Fact]
    public void AjustarStock_ComoEmpleado_FallaSinModificarNiRegistrar()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        bd.Inventario.AgregarProducto(Producto(stock: 10));
        var producto = Assert.Single(bd.Inventario.ObtenerProductos());
        bd.IniciarSesion(RolUsuario.Empleado, "Luis");

        var ajuste = bd.Inventario.AjustarStock(producto.Id, 7, "Conteo físico", "Luis");

        Assert.True(ajuste.IsError);
        Assert.Contains("permiso", ajuste.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(10, bd.Inventario.BuscarPorId(producto.Id).Stock);
        Assert.Empty(bd.Db.ObtenerBitacora(accion: AccionBitacora.AjusteInventario));
    }

    [Fact]
    public void CambiarPrecio_ComoEncargado_FallaPeroEditarNombreSiPermite()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        bd.Inventario.AgregarProducto(Producto(precio: 15m));
        var producto = Assert.Single(bd.Inventario.ObtenerProductos());
        bd.IniciarSesion(RolUsuario.Encargado, "Marta");

        producto.Precio = 20m;
        var cambioPrecio = bd.Inventario.ActualizarProducto(producto);

        Assert.True(cambioPrecio.IsError);
        Assert.Equal(15m, bd.Inventario.BuscarPorId(producto.Id).Precio);

        producto.Precio = 15m;
        producto.Nombre = "Producto renombrado";
        var edicion = bd.Inventario.ActualizarProducto(producto);

        Assert.True(edicion.IsSuccess, edicion.Message);
        Assert.Empty(bd.Db.ObtenerBitacora(accion: AccionBitacora.CambioPrecio));
        Assert.Equal(2, bd.Db.ObtenerBitacora(accion: AccionBitacora.EditarProducto).Count);
    }

    [Fact]
    public void RegistrarMerma_ComoEncargado_SePermiteYRegistraBitacora()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        bd.Inventario.AgregarProducto(Producto(stock: 5, costo: 10m));
        var producto = Assert.Single(bd.Inventario.ObtenerProductos());
        bd.IniciarSesion(RolUsuario.Encargado, "Marta");

        var merma = bd.Inventario.RegistrarMerma(producto.Id, 2, "Caducado", "Marta");

        Assert.True(merma.IsSuccess, merma.Message);
        Assert.Equal(3, bd.Inventario.BuscarPorId(producto.Id).Stock);
        var registro = Assert.Single(bd.Db.ObtenerBitacora(accion: AccionBitacora.Merma));
        Assert.Contains("CostoPerdido=20", registro.DatosDespues);
    }

    [Fact]
    public void AgregarGasto_ComoEmpleado_FallaYNoTotaliza()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        bd.IniciarSesion(RolUsuario.Empleado, "Luis");

        var gasto = bd.Config.AgregarGasto(new Gasto { Concepto = "Luz", Monto = 10m });

        Assert.True(gasto.IsError);
        Assert.Contains("permiso", gasto.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0m, bd.Config.ObtenerTotalGastos().Value);
        Assert.Empty(bd.Db.ObtenerBitacora(accion: AccionBitacora.Gasto));
    }

    [Fact]
    public void AgregarGasto_ComoEncargado_SePermiteYRegistraBitacora()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        bd.IniciarSesion(RolUsuario.Encargado, "Marta");

        var gasto = bd.Config.AgregarGasto(new Gasto
        {
            Concepto = "Luz", Monto = 10m, Origen = OrigenGasto.Caja
        });

        Assert.True(gasto.IsSuccess, gasto.Message);
        var registro = Assert.Single(bd.Db.ObtenerBitacora(accion: AccionBitacora.Gasto));
        Assert.Equal("Marta", registro.Usuario);
        Assert.Contains("Monto=10", registro.DatosDespues);
    }

    [Fact]
    public void MovimientoCaja_EncargadoNoPuedeRetirarPeroSiIngresar()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var cortes = new CorteCajaService(bd.Db);
        bd.IniciarSesion(RolUsuario.Encargado, "Marta");

        var retiro = cortes.RegistrarMovimiento(TipoMovimientoCaja.Retiro, 50m, "Retiro", "Marta");
        var ingreso = cortes.RegistrarMovimiento(TipoMovimientoCaja.Ingreso, 20m, "Cambio", "Marta");

        Assert.True(retiro.IsError);
        Assert.Contains("permiso", retiro.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(ingreso.IsSuccess, ingreso.Message);
        var registro = Assert.Single(bd.Db.ObtenerBitacora(accion: AccionBitacora.MovimientoCaja));
        Assert.Contains("Tipo=Ingreso", registro.DatosDespues);
    }

    [Fact]
    public void RegistrarPagoProveedor_ComoEncargado_SePermiteYRegistraBitacora()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var alta = bd.Config.AgregarProveedor(new Proveedor
        {
            Nombre = "Proveedor", SaldoPendiente = 100m
        });
        bd.IniciarSesion(RolUsuario.Encargado, "Marta");

        var pago = bd.Config.RegistrarPagoProveedor(alta.Value.ProveedorId, 40m);

        Assert.True(pago.IsSuccess, pago.Message);
        Assert.Equal(60m, bd.Db.ObtenerProveedorPorId(alta.Value.ProveedorId).SaldoPendiente);
        var registro = Assert.Single(bd.Db.ObtenerBitacora(accion: AccionBitacora.PagoProveedor));
        Assert.Equal("Marta", registro.Usuario);
        Assert.Contains("Pago=40", registro.DatosDespues);
    }

    [Fact]
    public void Bitacora_NoPermiteModificarNiBorrarRegistros()
    {
        using var bd = new BaseDatosPrueba();
        var bitacora = new BitacoraService(bd.Db);
        var alta = bitacora.Registrar(AccionBitacora.Gasto, "Gasto", "1", "", "Monto=10");
        Assert.True(alta.IsSuccess, alta.Message);

        var actualizar = Assert.Throws<SqliteException>(() =>
            bd.Db.ExecuteCommand("UPDATE Bitacora SET Accion = 'X' WHERE Id = @id",
                new SqliteParameter("@id", alta.Value.Id)));
        var borrar = Assert.Throws<SqliteException>(() =>
            bd.Db.ExecuteCommand("DELETE FROM Bitacora WHERE Id = @id",
                new SqliteParameter("@id", alta.Value.Id)));

        Assert.Contains("inmutable", actualizar.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("inmutable", borrar.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(bd.Db.ObtenerBitacora());
    }

    [Fact]
    public void Bitacora_ConsultarFiltraPorUsuarioYAccion()
    {
        using var bd = new BaseDatosPrueba();
        var bitacora = new BitacoraService(bd.Db);
        var ana = new UsuarioSistema { Nombre = "Ana", Rol = RolUsuario.Dueño };
        var luis = new UsuarioSistema { Nombre = "Luis", Rol = RolUsuario.Empleado };

        bitacora.Registrar(AccionBitacora.Gasto, "Gasto", "1", usuario: ana);
        bitacora.Registrar(AccionBitacora.Merma, "Producto", "2", usuario: ana);
        bitacora.Registrar(AccionBitacora.Gasto, "Gasto", "3", usuario: luis);

        Assert.Equal(2, bitacora.Consultar(usuario: "Ana").Value.Count);
        Assert.Equal(2, bitacora.Consultar(accion: AccionBitacora.Gasto).Value.Count);
        Assert.Single(bitacora.Consultar(usuario: "Luis", accion: AccionBitacora.Gasto).Value);
    }

    [Fact]
    public void Bitacora_RegistrarSinAccionOSinSesion_Falla()
    {
        using var bd = new BaseDatosPrueba();
        var bitacora = new BitacoraService(bd.Db);

        var sinAccion = bitacora.Registrar("  ", "Gasto", "1");

        SesionActual.Cerrar();
        var sinSesion = bitacora.Registrar(AccionBitacora.Gasto, "Gasto", "1");

        Assert.True(sinAccion.IsError);
        Assert.True(sinSesion.IsError);
        Assert.Empty(bd.Db.ObtenerBitacora());
    }
}
