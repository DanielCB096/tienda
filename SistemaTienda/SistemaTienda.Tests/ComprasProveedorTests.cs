using System.Text;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Tests;

public sealed class ComprasProveedorTests
{
    private static Producto CrearProducto(BaseDatosPrueba bd, string codigo,
        long costo, int stock = 0)
    {
        var producto = new Producto
        {
            CodigoBarras = codigo,
            Nombre = $"Producto {codigo}",
            Costo = costo,
            Precio = costo * 2,
            Stock = stock
        };

        Assert.True(bd.Inventario.AgregarProducto(producto).IsSuccess);
        return producto;
    }

    private static int CrearProveedor(BaseDatosPrueba bd, string nombre = "Distribuidora")
    {
        var resultado = bd.Config.AgregarProveedor(new Proveedor { Nombre = nombre });
        Assert.True(resultado.IsSuccess, resultado.Message);
        return resultado.Value.ProveedorId;
    }

    private static ProcessResult<CompraProveedor> Comprar(BaseDatosPrueba bd, int proveedorId,
        params (Producto Producto, int Cantidad, long Costo)[] lineas)
    {
        var solicitadas = lineas.Select(l => new LineaCompraSolicitada
        {
            ProductoId = l.Producto.Id,
            Cantidad = l.Cantidad,
            CostoUnitario = l.Costo
        }).ToList();

        return bd.Config.RegistrarCompraProveedor(proveedorId, solicitadas, "Compra de prueba");
    }

    [Fact]
    public void RegistrarCompra_SumaStockSaldoYActualizaElCosto()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var proveedorId = CrearProveedor(bd);
        var producto = CrearProducto(bd, "A1", costo: 1_000, stock: 5);

        var resultado = Comprar(bd, proveedorId, (producto, 10, 1_500));

        Assert.True(resultado.IsSuccess, resultado.Message);
        var compra = resultado.Value;
        Assert.Equal(15_000, compra.Total);
        Assert.Equal(0, compra.SaldoAnterior);
        Assert.Equal(15_000, compra.SaldoNuevo);
        Assert.Single(compra.Lineas);
        Assert.Equal(10, compra.Lineas[0].Cantidad);
        Assert.Equal(1_500, compra.Lineas[0].CostoUnitario);
        Assert.Equal(15_000, compra.Lineas[0].Importe);

        Assert.Equal(15, bd.Db.ObtenerStock(producto.Id));
        Assert.Equal(1_500, bd.Inventario.BuscarPorId(producto.Id).Costo);
        Assert.Equal(15_000, bd.Db.ObtenerProveedorPorId(proveedorId).SaldoPendiente);

        var entrada = Assert.Single(bd.Inventario.HistorialMovimientos(producto.Id),
            m => m.Tipo == MovimientoTipo.Entrada && m.Motivo.Contains("Compra proveedor"));
        Assert.Equal(10, entrada.Cantidad);
        Assert.Equal(1_500, entrada.CostoUnitario);
        Assert.Contains($"Compra proveedor #{compra.Id}", entrada.Motivo);
    }

    [Fact]
    public void RegistrarCompra_ConDosProductos_GuardaAmbosRenglones()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var proveedorId = CrearProveedor(bd);
        var a = CrearProducto(bd, "A1", costo: 1_000);
        var b = CrearProducto(bd, "B1", costo: 2_000);

        var resultado = Comprar(bd, proveedorId, (a, 2, 1_200), (b, 3, 2_100));

        Assert.True(resultado.IsSuccess, resultado.Message);
        Assert.Equal(2_400 + 6_300, resultado.Value.Total);
        Assert.Equal(2, resultado.Value.Lineas.Count);
        Assert.Equal(2, bd.Db.ObtenerStock(a.Id));
        Assert.Equal(3, bd.Db.ObtenerStock(b.Id));
        Assert.Equal(8_700, bd.Db.ObtenerProveedorPorId(proveedorId).SaldoPendiente);
    }

    [Fact]
    public void RegistrarCompra_ConProductoInactivo_FallaSinCambios()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var proveedorId = CrearProveedor(bd);
        var producto = CrearProducto(bd, "A1", costo: 1_000, stock: 5);
        Assert.True(bd.Inventario.DesactivarProducto(producto.Id).IsSuccess);

        var resultado = Comprar(bd, proveedorId, (producto, 10, 1_500));

        Assert.True(resultado.IsError);
        Assert.Equal(5, bd.Db.ObtenerStock(producto.Id));
        Assert.Equal(0, bd.Db.ObtenerProveedorPorId(proveedorId).SaldoPendiente);
    }

    [Fact]
    public void RegistrarCompra_ConProveedorInactivo_Falla()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var proveedorId = CrearProveedor(bd);
        var producto = CrearProducto(bd, "A1", costo: 1_000);
        Assert.True(bd.Config.DesactivarProveedor(proveedorId).IsSuccess);

        var resultado = Comprar(bd, proveedorId, (producto, 1, 1_000));

        Assert.True(resultado.IsError);
        Assert.Equal(0, bd.Db.ObtenerStock(producto.Id));
    }

    [Fact]
    public void RegistrarCompra_ConCantidadInvalida_Falla()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var proveedorId = CrearProveedor(bd);
        var producto = CrearProducto(bd, "A1", costo: 1_000);

        var resultado = Comprar(bd, proveedorId, (producto, 0, 1_000));

        Assert.True(resultado.IsError);
        Assert.Equal(0, bd.Db.ObtenerStock(producto.Id));
    }

    [Fact]
    public void RegistrarCompra_ConProductoRepetido_Falla()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var proveedorId = CrearProveedor(bd);
        var producto = CrearProducto(bd, "A1", costo: 1_000);

        var resultado = Comprar(bd, proveedorId, (producto, 1, 1_000), (producto, 2, 900));

        Assert.True(resultado.IsError);
        Assert.Equal(0, bd.Db.ObtenerStock(producto.Id));
    }

    [Fact]
    public void RegistrarCompra_ElEncargadoPuedeYElEmpleadoNo()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var proveedorId = CrearProveedor(bd);
        var producto = CrearProducto(bd, "A1", costo: 1_000);

        bd.IniciarSesion(RolUsuario.Empleado);
        Assert.True(Comprar(bd, proveedorId, (producto, 1, 1_000)).IsError);

        bd.IniciarSesion(RolUsuario.Encargado);
        Assert.True(Comprar(bd, proveedorId, (producto, 1, 1_000)).IsSuccess);
    }

    [Fact]
    public void EstadoCuentaProveedor_IncluyeComprasYTotalComprado()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var proveedorId = CrearProveedor(bd);
        var producto = CrearProducto(bd, "A1", costo: 1_000);
        Assert.True(Comprar(bd, proveedorId, (producto, 4, 1_250)).IsSuccess);

        var estado = bd.Config.ObtenerEstadoCuentaProveedor(proveedorId).Value;

        Assert.Single(estado.Compras);
        Assert.Equal(5_000, estado.TotalComprado);
        Assert.Equal(5_000, estado.SaldoPendiente);
        Assert.Single(estado.Compras[0].Lineas);
        Assert.Equal("Producto A1", estado.Compras[0].Lineas[0].Nombre);

        var csv = ExportadorCsv.EstadoCuentaProveedor(estado);
        Assert.Contains("Total comprado,50.00", csv);
        Assert.Contains("Compra #", csv);

        var pdf = Encoding.Latin1.GetString(ExportadorPdf.EstadoCuentaProveedor(estado));
        Assert.Contains("Total comprado", pdf);
        Assert.Contains("Compras", pdf);
    }

    [Fact]
    public void RegistrarCompra_QuedaEnLaBitacora()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var proveedorId = CrearProveedor(bd);
        var producto = CrearProducto(bd, "A1", costo: 1_000);
        Assert.True(Comprar(bd, proveedorId, (producto, 2, 1_100)).IsSuccess);

        var registro = bd.Db.ObtenerBitacora(200)
            .FirstOrDefault(r => r.Accion == AccionBitacora.CompraProveedor);

        Assert.NotNull(registro);
        Assert.Equal("Proveedor", registro.Entidad);
        Assert.Equal(proveedorId.ToString(), registro.EntidadId);
        Assert.Contains("Total=22.00", registro.DatosDespues);
    }

    [Fact]
    public void Migracion_DesdeV12_CreaLasTablasDeCompras()
    {
        using var bd = new BaseDatosPrueba(versionEsquema: 12);

        bd.Db.InicializarBaseDeDatos();

        Assert.Equal(Migraciones.VersionMaxima(Migraciones.ObtenerTodas()),
            bd.Db.ExecuteScalar<int>("PRAGMA user_version"));
        Assert.Equal(1, bd.Db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ComprasProveedor'"));
        Assert.Equal(1, bd.Db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='CompraProveedorLineas'"));
        Assert.Contains(RespaldoLocal.Listar(bd.Db.DirectorioRespaldos),
            r => r.Nombre.Contains("antes-migrar"));
    }
}
