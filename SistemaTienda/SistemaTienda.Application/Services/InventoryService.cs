// Servicio de inventario - lógica de negocio
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Application.Services;

public class InventoryService
{
    private readonly DatabaseTools _db;

    public InventoryService(DatabaseTools db) => _db = db;

    public ProcessResult AgregarProducto(Producto producto)
    {
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
        return id > 0
            ? ProcessResult.Success("Producto agregado correctamente")
            : ProcessResult.Error("El código de barras ya existe. Use uno diferente.");
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

        return _db.ActualizarProducto(producto)
            ? ProcessResult.Success("Producto actualizado correctamente")
            : ProcessResult.Error("No se encontró el producto");
    }

    /// <summary>
    /// Reduce el stock al registrar una venta. El stock nunca se edita directamente.
    /// </summary>
    public ProcessResult ReducirStockVenta(int productoId, int cantidad, string usuarioVendedor)
    {
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
        if (cantidad <= 0)
            return ProcessResult.Error("La cantidad debe ser mayor a cero");

        if (costoUnitario < 0)
            return ProcessResult.Error("El costo unitario no puede ser negativo");

        return _db.RegistrarEntrada(productoId, cantidad, costoUnitario, motivo, usuario)
            ? ProcessResult.Success($"Entrada registrada: {cantidad} unidades añadidas")
            : ProcessResult.Error("No se encontró el producto");
    }

    /// <summary>
    /// Registra una merma: descuenta inventario y reporta el costo perdido.
    /// </summary>
    public ProcessResult RegistrarMerma(int productoId, int cantidad, string motivo, string usuario)
    {
        if (cantidad <= 0)
            return ProcessResult.Error("La cantidad debe ser mayor a cero");

        if (string.IsNullOrWhiteSpace(motivo))
            return ProcessResult.Error("El motivo de la merma es obligatorio");

        var producto = _db.ObtenerProductoPorId(productoId);
        if (producto == null)
            return ProcessResult.Error("Producto no encontrado");

        if (!_db.RegistrarSalida(productoId, cantidad, $"Merma: {motivo.Trim()}", usuario))
            return ProcessResult.Error("Stock insuficiente para registrar la merma");

        var costoPerdido = Dinero.Multiplicar(producto.Costo, cantidad);
        return ProcessResult.Success(
            $"Merma registrada: {cantidad} unidades, costo perdido ${Dinero.Formatear(costoPerdido)}");
    }

    /// <summary>
    /// Ajusta el stock a un valor absoluto; requiere motivo y queda como movimiento.
    /// </summary>
    public ProcessResult AjustarStock(int productoId, int nuevoStock, string motivo, string usuario)
    {
        if (nuevoStock < 0)
            return ProcessResult.Error("El stock no puede ser negativo");

        if (string.IsNullOrWhiteSpace(motivo))
            return ProcessResult.Error("El motivo del ajuste es obligatorio");

        return _db.AjustarStock(productoId, nuevoStock, motivo.Trim(), usuario)
            ? ProcessResult.Success("Stock ajustado correctamente")
            : ProcessResult.Error("No se pudo ajustar el stock");
    }

    public List<Producto> ProductosStockBajo() => _db.ObtenerProductosStockBajo();

    public List<MovimientoInventario> HistorialMovimientos(int productoId) =>
        _db.ObtenerHistorialMovimientos(productoId);

    public long ObtenerValorizadoInventario() => _db.ObtenerValorizadoInventario();
}
