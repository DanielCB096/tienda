// Servicio de inventario - lógica de negocio
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Application.Services;

public class InventoryService
{
    private readonly DatabaseTools _db;
    private readonly BitacoraService _bitacora;

    public InventoryService(DatabaseTools db)
    {
        _db = db;
        _bitacora = new BitacoraService(db);
    }

    private static ProcessResult Autorizar(PermisoSistema permiso) =>
        AutorizacionService.Exigir(SesionActual.Usuario, permiso);

    public ProcessResult AgregarProducto(Producto producto)
    {
        var autorizacion = Autorizar(PermisoSistema.EditarProducto);
        if (autorizacion.IsError)
            return autorizacion;

        if (string.IsNullOrWhiteSpace(producto.CodigoBarras))
            return ProcessResult.Error("El código de barras es obligatorio");

        if (string.IsNullOrWhiteSpace(producto.Nombre))
            return ProcessResult.Error("El nombre del producto es obligatorio");

        if (producto.Costo < 0 || producto.Precio < 0)
            return ProcessResult.Error("El costo y el precio no pueden ser negativos");

        if (producto.Stock < 0)
            return ProcessResult.Error("El stock inicial no puede ser negativo");

        if (producto.StockMinimo < 0)
            return ProcessResult.Error("El stock mínimo no puede ser negativo");

        producto.CodigoBarras = producto.CodigoBarras.Trim();
        producto.Nombre = producto.Nombre.Trim();

        var id = _db.AgregarProducto(producto);
        if (id <= 0)
            return ProcessResult.Error("El código de barras ya existe. Use uno diferente.");

        _bitacora.Registrar(AccionBitacora.EditarProducto, "Producto", id.ToString(),
            string.Empty, $"Alta: {producto.Nombre};Stock={producto.Stock}");
        return ProcessResult.Success("Producto agregado correctamente");
    }

    public List<Producto> ObtenerProductos() => _db.ObtenerProductos();

    public Producto BuscarPorCodigo(string codigoBarras) => _db.ObtenerProductoPorCodigo(codigoBarras);

    public Producto BuscarPorId(int productoId) => _db.ObtenerProductoPorId(productoId);

    public List<Producto> BuscarPorNombre(string texto) =>
        string.IsNullOrWhiteSpace(texto) ? new List<Producto>() : _db.BuscarProductosPorNombre(texto.Trim());

    public ProcessResult ActualizarProducto(Producto producto)
    {
        if (producto == null || producto.Id <= 0)
            return ProcessResult.Error("Producto no válido");

        if (string.IsNullOrWhiteSpace(producto.Nombre))
            return ProcessResult.Error("El nombre del producto es obligatorio");

        if (producto.Costo < 0 || producto.Precio < 0 || producto.StockMinimo < 0)
            return ProcessResult.Error("El costo, el precio y el stock mínimo no pueden ser negativos");

        var anterior = _db.ObtenerProductoPorId(producto.Id);
        if (anterior == null)
            return ProcessResult.Error("No se encontró el producto");

        var cambiaPrecio = anterior.Costo != producto.Costo || anterior.Precio != producto.Precio;
        var autorizacion = Autorizar(cambiaPrecio
            ? PermisoSistema.CambiarPrecios
            : PermisoSistema.EditarProducto);
        if (autorizacion.IsError)
            return autorizacion;

        if (!_db.ActualizarProducto(producto))
            return ProcessResult.Error("No se encontró el producto");

        var accion = cambiaPrecio ? AccionBitacora.CambioPrecio : AccionBitacora.EditarProducto;
        _bitacora.Registrar(accion, "Producto", producto.Id.ToString(),
            $"Nombre={anterior.Nombre};Costo={anterior.Costo:F2};Precio={anterior.Precio:F2}",
            $"Nombre={producto.Nombre};Costo={producto.Costo:F2};Precio={producto.Precio:F2}");
        return ProcessResult.Success("Producto actualizado correctamente");
    }

    /// <summary>
    /// Reduce el stock al registrar una venta. El stock nunca se edita directamente.
    /// </summary>
    public ProcessResult ReducirStockVenta(int productoId, int cantidad, string usuarioVendedor)
    {
        var autorizacion = Autorizar(PermisoSistema.Vender);
        if (autorizacion.IsError)
            return autorizacion;

        if (cantidad <= 0)
            return ProcessResult.Error("La cantidad debe ser mayor a cero");

        var producto = _db.ObtenerProductoPorId(productoId);
        if (producto == null)
            return ProcessResult.Error("Producto no encontrado");

        return _db.RegistrarSalida(productoId, cantidad, "Venta", usuarioVendedor)
            ? ProcessResult.Success("Stock reducido y venta registrada")
            : ProcessResult.Error("Stock insuficiente para completar la venta");
    }

    public ProcessResult RegistrarEntrada(int productoId, int cantidad, decimal costoUnitario,
        string usuario, string motivo = "Compra")
    {
        var autorizacion = Autorizar(PermisoSistema.RegistrarEntrada);
        if (autorizacion.IsError)
            return autorizacion;

        if (cantidad <= 0)
            return ProcessResult.Error("La cantidad debe ser mayor a cero");

        if (costoUnitario < 0)
            return ProcessResult.Error("El costo unitario no puede ser negativo");

        var producto = _db.ObtenerProductoPorId(productoId);
        if (producto == null)
            return ProcessResult.Error("No se encontró el producto");

        if (!_db.RegistrarEntrada(productoId, cantidad, costoUnitario, motivo, usuario))
            return ProcessResult.Error("No se encontró el producto");

        _bitacora.Registrar(AccionBitacora.EntradaInventario, "Producto", productoId.ToString(),
            $"Stock={producto.Stock}",
            $"Stock={producto.Stock + cantidad};CostoUnitario={costoUnitario:F2}", motivo);
        return ProcessResult.Success($"Entrada registrada: {cantidad} unidades añadidas");
    }

    /// <summary>
    /// Registra una merma: descuenta inventario y reporta el costo perdido.
    /// </summary>
    public ProcessResult RegistrarMerma(int productoId, int cantidad, string motivo, string usuario)
    {
        var autorizacion = Autorizar(PermisoSistema.RegistrarMerma);
        if (autorizacion.IsError)
            return autorizacion;

        if (cantidad <= 0)
            return ProcessResult.Error("La cantidad debe ser mayor a cero");

        if (string.IsNullOrWhiteSpace(motivo))
            return ProcessResult.Error("El motivo de la merma es obligatorio");

        var producto = _db.ObtenerProductoPorId(productoId);
        if (producto == null)
            return ProcessResult.Error("Producto no encontrado");

        if (!_db.RegistrarSalida(productoId, cantidad, $"Merma: {motivo.Trim()}", usuario))
            return ProcessResult.Error("Stock insuficiente para registrar la merma");

        var costoPerdido = producto.Costo * cantidad;
        _bitacora.Registrar(AccionBitacora.Merma, "Producto", productoId.ToString(),
            $"Stock={producto.Stock}",
            $"Stock={producto.Stock - cantidad};CostoPerdido={costoPerdido:F2}", motivo);
        return ProcessResult.Success(
            $"Merma registrada: {cantidad} unidades, costo perdido ${costoPerdido:F2}");
    }

    /// <summary>
    /// Ajusta el stock a un valor absoluto; requiere motivo y queda como movimiento.
    /// </summary>
    public ProcessResult AjustarStock(int productoId, int nuevoStock, string motivo, string usuario)
    {
        var autorizacion = Autorizar(PermisoSistema.AjustarInventario);
        if (autorizacion.IsError)
            return autorizacion;

        if (nuevoStock < 0)
            return ProcessResult.Error("El stock no puede ser negativo");

        if (string.IsNullOrWhiteSpace(motivo))
            return ProcessResult.Error("El motivo del ajuste es obligatorio");

        var producto = _db.ObtenerProductoPorId(productoId);
        if (!_db.AjustarStock(productoId, nuevoStock, motivo.Trim(), usuario))
            return ProcessResult.Error("No se pudo ajustar el stock");

        _bitacora.Registrar(AccionBitacora.AjusteInventario, "Producto", productoId.ToString(),
            $"Stock={producto?.Stock ?? 0}", $"Stock={nuevoStock}", motivo);
        return ProcessResult.Success("Stock ajustado correctamente");
    }

    public List<Producto> ProductosStockBajo() => _db.ObtenerProductosStockBajo();

    public List<MovimientoInventario> HistorialMovimientos(int productoId) =>
        _db.ObtenerHistorialMovimientos(productoId);

    public decimal ObtenerValorizadoInventario() => _db.ObtenerValorizadoInventario();
}
