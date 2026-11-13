// Servicio de inventario - lógica de negocio
using System.Globalization;
using System.Text;
using Microsoft.VisualBasic.FileIO;
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

    public ProcessResult<ResultadoImportacionProductos> ImportarProductosCsv(
        Stream archivo, decimal impuestoPredeterminado)
    {
        var autorizacion = Autorizar(PermisoSistema.EditarProducto);
        if (autorizacion.IsError)
            return ProcessResult<ResultadoImportacionProductos>.Error(autorizacion.Message);

        if (archivo == null || !archivo.CanRead)
            return ProcessResult<ResultadoImportacionProductos>.Error(
                "No se puede leer el archivo CSV.");

        if (impuestoPredeterminado < 0 || impuestoPredeterminado > 100)
            return ProcessResult<ResultadoImportacionProductos>.Error(
                "El impuesto predeterminado debe estar entre 0 y 100.");

        var filas = new List<FilaImportacionCsv>();
        string[] encabezados;
        TextFieldParser parser = null;

        try
        {
            using var lector = new StreamReader(
                archivo, new UTF8Encoding(false, true), true, 1024, true);
            parser = new TextFieldParser(lector);
            using (parser)
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.HasFieldsEnclosedInQuotes = true;
                parser.TrimWhiteSpace = false;
                parser.SetDelimiters(",");

                if (parser.EndOfData)
                    return ProcessResult<ResultadoImportacionProductos>.Error(
                        "El archivo CSV está vacío.");

                encabezados = parser.ReadFields() ?? Array.Empty<string>();
                var columnas = CrearMapaColumnas(encabezados);
                if (columnas.IsError)
                    return ProcessResult<ResultadoImportacionProductos>.Error(columnas.Message);

                var indices = columnas.Value;
                var numeroFilas = 0;
                while (!parser.EndOfData)
                {
                    var numeroFila = checked((int)parser.LineNumber);
                    var campos = parser.ReadFields();
                    if (campos == null || campos.All(string.IsNullOrWhiteSpace))
                        continue;

                    numeroFilas++;
                    if (campos.Length != encabezados.Length)
                    {
                        filas.Add(new FilaImportacionCsv(numeroFila, string.Empty, null,
                            new List<string>
                            {
                                $"Se esperaban {encabezados.Length} columnas y se encontraron {campos.Length}."
                            }));
                        continue;
                    }

                    filas.Add(LeerFilaCsv(campos, indices, numeroFila, impuestoPredeterminado));
                }

                if (numeroFilas == 0)
                    return ProcessResult<ResultadoImportacionProductos>.Error(
                        "El archivo CSV no contiene filas de productos.");
            }
        }
        catch (MalformedLineException ex)
        {
            return ProcessResult<ResultadoImportacionProductos>.Error(
                $"El CSV está mal formado cerca de la línea {parser.ErrorLineNumber}: {ex.Message}");
        }
        catch (DecoderFallbackException)
        {
            return ProcessResult<ResultadoImportacionProductos>.Error(
                "El archivo no tiene una codificación UTF-8 válida.");
        }
        catch (OverflowException)
        {
            return ProcessResult<ResultadoImportacionProductos>.Error(
                "El archivo contiene un número de fila fuera del rango permitido.");
        }

        AgregarErroresPorDuplicadosCsv(filas);
        var codigosExistentes = ObtenerProductos()
            .Select(producto => producto.CodigoBarras.Trim())
            .ToHashSet(StringComparer.Ordinal);

        foreach (var fila in filas)
        {
            if (!string.IsNullOrEmpty(fila.CodigoBarras) &&
                codigosExistentes.Contains(fila.CodigoBarras))
                fila.Errores.Add("El código de barras ya existe en el catálogo.");
        }

        var importados = 0;
        var errores = new List<ErrorImportacionCsv>();
        foreach (var fila in filas)
        {
            if (fila.Errores.Count == 0 && fila.Producto != null)
            {
                var resultado = AgregarProducto(fila.Producto);
                if (resultado.IsSuccess)
                    importados++;
                else
                    fila.Errores.Add(resultado.Message);
            }

            if (fila.Errores.Count > 0)
                errores.Add(new ErrorImportacionCsv(
                    fila.NumeroFila, string.Join(" ", fila.Errores)));
        }

        var reporte = new ResultadoImportacionProductos
        {
            ProductosImportados = importados,
            Errores = errores
        };
        var resumen = $"Se importaron {importados} producto(s) y se omitieron " +
                      $"{errores.Count} fila(s).";
        return ProcessResult<ResultadoImportacionProductos>.OK(reporte, resumen);
    }

    private static ProcessResult<Dictionary<string, int>> CrearMapaColumnas(string[] encabezados)
    {
        var columnasPermitidas = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CodigoBarras", "Nombre", "Costo", "Precio", "Stock", "StockMinimo",
            "Categoria", "Impuesto"
        };
        var indices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var indice = 0; indice < encabezados.Length; indice++)
        {
            var encabezado = encabezados[indice].Trim().TrimStart('\uFEFF');
            if (encabezado.Length == 0)
                return ProcessResult<Dictionary<string, int>>.Error(
                    "El CSV contiene un encabezado vacío.");

            if (!columnasPermitidas.Contains(encabezado))
                return ProcessResult<Dictionary<string, int>>.Error(
                    $"La columna «{encabezado}» no es compatible.");

            if (!indices.TryAdd(encabezado, indice))
                return ProcessResult<Dictionary<string, int>>.Error(
                    $"La columna «{encabezado}» está repetida.");
        }

        var obligatorias = new[] { "CodigoBarras", "Nombre", "Costo", "Precio" };
        var faltantes = obligatorias.Where(nombre => !indices.ContainsKey(nombre)).ToArray();
        if (faltantes.Length > 0)
            return ProcessResult<Dictionary<string, int>>.Error(
                $"Faltan columnas obligatorias: {string.Join(", ", faltantes)}.");

        return ProcessResult<Dictionary<string, int>>.OK(indices);
    }

    private static FilaImportacionCsv LeerFilaCsv(
        string[] campos, Dictionary<string, int> indices, int numeroFila,
        decimal impuestoPredeterminado)
    {
        var errores = new List<string>();
        var codigo = LeerColumna(campos, indices, "CodigoBarras").Trim();
        var nombre = LeerColumna(campos, indices, "Nombre").Trim();
        var costoTexto = LeerColumna(campos, indices, "Costo").Trim();
        var precioTexto = LeerColumna(campos, indices, "Precio").Trim();

        if (codigo.Length == 0)
            errores.Add("El código de barras es obligatorio.");
        if (nombre.Length == 0)
            errores.Add("El nombre es obligatorio.");

        var costo = LeerDineroCsv(costoTexto);
        if (!costo.HasValue)
            errores.Add("El costo debe ser un importe no negativo con punto decimal.");

        var precio = LeerDineroCsv(precioTexto);
        if (!precio.HasValue)
            errores.Add("El precio debe ser un importe no negativo con punto decimal.");

        var stock = LeerEnteroCsv(LeerColumna(campos, indices, "Stock"), 0);
        if (!stock.HasValue || stock.Value > 1_000_000)
            errores.Add("El stock debe ser un entero entre 0 y 1,000,000.");

        var stockMinimo = LeerEnteroCsv(
            LeerColumna(campos, indices, "StockMinimo"), 5);
        if (!stockMinimo.HasValue || stockMinimo.Value > 1_000_000)
            errores.Add("El stock mínimo debe ser un entero entre 0 y 1,000,000.");

        var impuestoTexto = LeerColumna(campos, indices, "Impuesto").Trim();
        var impuesto = impuestoPredeterminado;
        if (impuestoTexto.Length > 0 &&
            (!decimal.TryParse(impuestoTexto, NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out impuesto) ||
             impuesto < 0 || impuesto > 100))
        {
            errores.Add("El impuesto debe ser un porcentaje entre 0 y 100.");
        }

        Producto producto = null;
        if (errores.Count == 0)
        {
            producto = new Producto
            {
                CodigoBarras = codigo,
                Nombre = nombre,
                Costo = costo.Value,
                Precio = precio.Value,
                Stock = stock.Value,
                StockMinimo = stockMinimo.Value,
                Categoria = LeerColumna(campos, indices, "Categoria").Trim(),
                Impuesto = impuesto
            };
        }

        return new FilaImportacionCsv(numeroFila, codigo, producto, errores);
    }

    private static string LeerColumna(
        string[] campos, Dictionary<string, int> indices, string nombre) =>
        indices.TryGetValue(nombre, out var indice) ? campos[indice] : string.Empty;

    private static long? LeerDineroCsv(string texto)
    {
        if (!decimal.TryParse(texto,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var pesos) || pesos < 0)
            return null;

        try
        {
            return Dinero.DePesos(pesos);
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    private static int? LeerEnteroCsv(string texto, int valorPredeterminado)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return valorPredeterminado;

        return int.TryParse(texto.Trim(), NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var valor) && valor >= 0
            ? valor
            : null;
    }

    private static void AgregarErroresPorDuplicadosCsv(List<FilaImportacionCsv> filas)
    {
        var duplicados = filas
            .Where(fila => !string.IsNullOrEmpty(fila.CodigoBarras))
            .GroupBy(fila => fila.CodigoBarras, StringComparer.Ordinal)
            .Where(grupo => grupo.Count() > 1);

        foreach (var grupo in duplicados)
        {
            var filasRepetidas = string.Join(", ", grupo.Select(fila => fila.NumeroFila));
            foreach (var fila in grupo)
                fila.Errores.Add(
                    $"El código de barras se repite en las filas {filasRepetidas} del archivo.");
        }
    }

    private sealed record FilaImportacionCsv(
        int NumeroFila, string CodigoBarras, Producto Producto, List<string> Errores);

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
