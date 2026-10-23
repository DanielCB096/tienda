// Servicio de inventario - lógica de negocio
using SistemaTienda.Core;

public class InventoryService
{
    private readonly DatabaseTools _db;

    public InventoryService(DatabaseTools db) => _db = db;

    /// <summary>
    /// Agrega un nuevo producto al catálogo
    /// </summary>
    public ProcessResult AgregarProducto(Producto prod)
    {
        try
        {
            if (_db.AgregarProducto(prod))
                return ProcessResult.OK().Success("Producto agregado correctamente");
            else
                return ProcessResult.Error("El código de barras ya existe. Use uno diferente.");
        }
        catch (Exception ex)
        {
            return ProcessResult.Error($"Error al agregar producto: {ex.Message}");
        }
    }

    /// <summary>
    /// Obtiene todos los productos del catálogo
    /// </summary>
    public List<Producto> ObtenerProductos() => _db.ObtenerProductos();

    /// <summary>
    /// Busca un producto por su código de barras
    /// </summary>
    public Producto? BuscarPorCodigo(string codigoBarras) => _db.ObtenerProductoPorCodigo(codigoBarras);

    /// <summary>
    /// Busca un producto por su ID
    /// </summary>
    public Producto? BuscarPorId(int productoId) => _db.ObtenerProductoPorId(productoId);

    /// <summary>
    /// Actualiza los datos de un producto (solo dueño/encargado)
    /// </summary>
    public bool ActualizarProducto(Producto prod)
    {
        try
        {
            if (_db.ActualizarProducto(prod))
            {
                // Registrar movimiento de ajuste de stock
                _db.AjustarStock(prod.Id, prod.Stock, "Actualización de producto", "Sistema");
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Reduce el stock al registrar una venta (cálculo simplificado: precio incluye impuesto)
    /// </summary>
    /// <param name="productoId">ID del producto vendido</param>
    /// <param name="cantidad">Cantidad vendida</param>
    /// <param name="usuarioVendedor">Nombre del cajero que vende</param>
    /// <returns>ProcessResult con éxito o error</returns>
    public ProcessResult ReducirStockVenta(int productoId, int cantidad, string usuarioVendedor)
    {
        var prod = _db.ObtenerProductoPorId(productoId);
        if (prod == null) return ProcessResult.Error("Producto no encontrado");

        if (prod.Stock < cantidad)
            return ProcessResult.Error("Stock insuficiente para completar la venta");

        // Reducir stock: el precio ya incluye impuesto, así que solo restamos del stock
        bool éxito = _db.AjustarStock(productoId, prod.Stock - cantidad, "Venta", usuarioVendedor);

        if (éxito)
            return ProcessResult.OK().Success("Stock reducido y venta registrada");
        else
            return ProcessResult.Error("Error atómico al reducir stock");
    }

    /// <summary>
    /// Registra una entrada de inventario (compra a proveedor)
    /// </summary>
    public ProcessResult RegistrarEntrada(int productoId, int cantidad, decimal costoUnitario, string usuario, string motivo = "Compra")
    {
        var prod = _db.ObtenerProductoPorId(productoId);
        if (prod == null) return ProcessResult.Error("Producto no encontrado");

        // Aumentar stock
        bool actualizo = _db.AjustarStock(productoId, prod.Stock + cantidad, motivo, usuario);

        if (actualizo)
            return ProcessResult.OK().Success($"Entrada registrada: {cantidad} unidades añadidas");
        else
            return ProcessResult.Error("Error al registrar entrada de inventario");
    }

    /// <summary>
    /// Registra una merma (pérdida de inventario)
    /// </summary>
    public ProcessResult RegistrarMerca(int productoId, int cantidad, decimal costoPerdido, string motivo, string usuario)
    {
        var prod = _db.ObtenerProductoPorId(productoId);
        if (prod == null) return ProcessResult.Error("Producto no encontrado");

        if (prod.Stock < cantidad)
            return ProcessResult.Error("Stock insuficiente para registrar la merma");

        // Reducir stock y registrar movimiento de merma
        bool éxito = _db.AjustarStock(productoId, prod.Stock - cantidad, motivo, usuario);

        if (éxito)
            return ProcessResult.OK().Success($"Mermas registradas: {cantidad} unidades, costo perdido: ${costoPerdido:F2}");
        else
            return ProcessResult.Error("Error al registrar merma");
    }

    /// <summary>
    /// Obtiene productos con stock bajo (stock <= stock_minimo)
    /// </summary>
    public List<Producto> ProductosStockBajo() => _db.ObtenerProductosStockBajo();

    /// <summary>
    /// Obtiene el historial de movimientos de un producto
    /// </summary>
    public List<MovimientoInventario> HistorialMovimientos(int productoId) => _db.ObtenerHistorialMovimientos(productoId);

    /// <summary>
    /// Obtiene reporte de valorizado de inventario (stock × costo)
    /// </summary>
    public decimal ObtenerValorizadoInventario()
    {
        var productos = ObtenerProductos();
        decimal total = 0;

        foreach (var prod in productos)
        {
            total += prod.Stock * prod.Costo;
        }

        return total;
    }
}