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

    public List<Producto> BuscarPorNombre(string texto, bool soloActivos = false) =>
        string.IsNullOrWhiteSpace(texto)
            ? new List<Producto>()
            : _db.BuscarProductosPorNombre(texto.Trim(), soloActivos);

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
            $"Nombre={anterior.Nombre};Costo={Dinero.Formatear(anterior.Costo)};Precio={Dinero.Formatear(anterior.Precio)}",
            $"Nombre={producto.Nombre};Costo={Dinero.Formatear(producto.Costo)};Precio={Dinero.Formatear(producto.Precio)}");
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

    public ProcessResult RegistrarEntrada(int productoId, int cantidad, long costoUnitario,
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

        if (!producto.Activo)
            return ProcessResult.Error(
                "El producto está desactivado; reactívalo para registrar entradas");

        if (!_db.RegistrarEntrada(productoId, cantidad, costoUnitario, motivo, usuario))
            return ProcessResult.Error("No se encontró el producto");

        _bitacora.Registrar(AccionBitacora.EntradaInventario, "Producto", productoId.ToString(),
            $"Stock={producto.Stock}",
            $"Stock={producto.Stock + cantidad};CostoUnitario={Dinero.Formatear(costoUnitario)}", motivo);
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

        if (!producto.Activo)
            return ProcessResult.Error(
                "El producto está desactivado; reactívalo para registrar mermas");

        if (!_db.RegistrarSalida(productoId, cantidad, $"Merma: {motivo.Trim()}", usuario))
            return ProcessResult.Error("Stock insuficiente para registrar la merma");

        var costoPerdido = Dinero.Multiplicar(producto.Costo, cantidad);
        _bitacora.Registrar(AccionBitacora.Merma, "Producto", productoId.ToString(),
            $"Stock={producto.Stock}",
            $"Stock={producto.Stock - cantidad};CostoPerdido={Dinero.Formatear(costoPerdido)}", motivo);
        return ProcessResult.Success(
            $"Merma registrada: {cantidad} unidades, costo perdido ${Dinero.Formatear(costoPerdido)}");
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
        if (producto is { Activo: false })
            return ProcessResult.Error(
                "El producto está desactivado; reactívalo para ajustar su stock");

        if (!_db.AjustarStock(productoId, nuevoStock, motivo.Trim(), usuario))
            return ProcessResult.Error("No se pudo ajustar el stock");

        _bitacora.Registrar(AccionBitacora.AjusteInventario, "Producto", productoId.ToString(),
            $"Stock={producto?.Stock ?? 0}", $"Stock={nuevoStock}", motivo);
        return ProcessResult.Success("Stock ajustado correctamente");
    }

    /// <summary>
    /// Desactiva un producto: deja de venderse y de recibir movimientos, conservando su stock.
    /// Permiso exclusivo del dueño; la acción queda en la bitácora.
    /// </summary>
    public ProcessResult DesactivarProducto(int productoId)
    {
        return CambiarEstado(productoId, activo: false,
            mensajeYaAplicado: "El producto ya está desactivado",
            mensajeExito: "Producto desactivado");
    }

    /// <summary>
    /// Reactiva un producto desactivado. Permiso exclusivo del dueño.
    /// </summary>
    public ProcessResult ReactivarProducto(int productoId)
    {
        return CambiarEstado(productoId, activo: true,
            mensajeYaAplicado: "El producto ya está activo",
            mensajeExito: "Producto reactivado");
    }

    private ProcessResult CambiarEstado(int productoId, bool activo,
        string mensajeYaAplicado, string mensajeExito)
    {
        var autorizacion = Autorizar(PermisoSistema.DesactivarProducto);
        if (autorizacion.IsError)
            return autorizacion;

        var producto = _db.ObtenerProductoPorId(productoId);
        if (producto == null)
            return ProcessResult.Error("No se encontró el producto");

        if (producto.Activo == activo)
            return ProcessResult.Error(mensajeYaAplicado);

        if (!_db.CambiarEstadoProducto(productoId, activo))
            return ProcessResult.Error("No se pudo cambiar el estado del producto");

        _bitacora.Registrar(AccionBitacora.CambiarEstadoProducto, "Producto", productoId.ToString(),
            $"Nombre={producto.Nombre};Activo={(activo ? 0 : 1)};Stock={producto.Stock}",
            $"Nombre={producto.Nombre};Activo={(activo ? 1 : 0)};Stock={producto.Stock}");
        return ProcessResult.Success(
            $"{mensajeExito}. Su stock ({producto.Stock}) se conserva" +
            (activo ? " y vuelve a estar disponible para la venta."
                     : "; no se puede vender ni recibir movimientos mientras esté inactivo."));
    }

    public List<Producto> ProductosStockBajo(bool soloActivos = false) =>
        _db.ObtenerProductosStockBajo(soloActivos);

    public List<MovimientoInventario> HistorialMovimientos(int productoId) =>
        _db.ObtenerHistorialMovimientos(productoId);

    public long ObtenerValorizadoInventario() => _db.ObtenerValorizadoInventario();
}
