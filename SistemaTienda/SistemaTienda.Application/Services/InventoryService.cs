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

        var auditoria = new RegistroBitacora
        {
            Fecha = DateTime.Now,
            Usuario = SesionActual.Usuario?.Nombre ?? string.Empty,
            Accion = AccionBitacora.EditarProducto,
            Entidad = "Producto",
            DatosDespues = $"Alta: {producto.Nombre};Stock={producto.Stock}"
        };

        var id = _db.AgregarProducto(producto, auditoria);
        if (id <= 0)
            return ProcessResult.Error("El código de barras ya existe. Use uno diferente.");

        return ProcessResult.Success("Producto agregado correctamente");
    }

    public ProcessResult<VistaPreviaImportacion> PrevisualizarProductosCsv(
        Stream archivo, decimal impuestoPredeterminado)
    {
        var autorizacion = Autorizar(PermisoSistema.EditarProducto);
        if (autorizacion.IsError)
            return ProcessResult<VistaPreviaImportacion>.Error(autorizacion.Message);

        var filas = LeerFilasCsv(archivo, impuestoPredeterminado);
        if (filas.IsError)
            return ProcessResult<VistaPreviaImportacion>.Error(filas.Message);

        return ProcessResult<VistaPreviaImportacion>.OK(CrearVista(filas.Value));
    }

    public ProcessResult<VistaPreviaImportacion> PrevisualizarProductosXlsx(
        Stream archivo, decimal impuestoPredeterminado)
    {
        var autorizacion = Autorizar(PermisoSistema.EditarProducto);
        if (autorizacion.IsError)
            return ProcessResult<VistaPreviaImportacion>.Error(autorizacion.Message);

        var filas = LeerFilasXlsx(archivo, impuestoPredeterminado);
        if (filas.IsError)
            return ProcessResult<VistaPreviaImportacion>.Error(filas.Message);

        return ProcessResult<VistaPreviaImportacion>.OK(CrearVista(filas.Value));
    }

    public ProcessResult<ResultadoImportacionProductos> ImportarProductosCsv(
        Stream archivo, decimal impuestoPredeterminado, string nombreArchivo = null)
    {
        var autorizacion = Autorizar(PermisoSistema.EditarProducto);
        if (autorizacion.IsError)
            return ProcessResult<ResultadoImportacionProductos>.Error(autorizacion.Message);

        if (archivo == null || !archivo.CanRead)
            return ProcessResult<ResultadoImportacionProductos>.Error("No se puede leer el archivo.");

        var contenido = LeerBytes(archivo);
        var filas = LeerFilasCsv(new MemoryStream(contenido), impuestoPredeterminado);
        if (filas.IsError)
            return ProcessResult<ResultadoImportacionProductos>.Error(filas.Message);

        return AplicarImportacion(filas.Value, nombreArchivo ?? "importacion.csv",
            HashArchivo(contenido));
    }

    public ProcessResult<ResultadoImportacionProductos> ImportarProductosXlsx(
        Stream archivo, decimal impuestoPredeterminado, string nombreArchivo = null)
    {
        var autorizacion = Autorizar(PermisoSistema.EditarProducto);
        if (autorizacion.IsError)
            return ProcessResult<ResultadoImportacionProductos>.Error(autorizacion.Message);

        if (archivo == null || !archivo.CanRead)
            return ProcessResult<ResultadoImportacionProductos>.Error("No se puede leer el archivo.");

        var contenido = LeerBytes(archivo);
        var filas = LeerFilasXlsx(new MemoryStream(contenido), impuestoPredeterminado);
        if (filas.IsError)
            return ProcessResult<ResultadoImportacionProductos>.Error(filas.Message);

        return AplicarImportacion(filas.Value, nombreArchivo ?? "importacion.xlsx",
            HashArchivo(contenido));
    }

    private ProcessResult<List<FilaImportacionProducto>> LeerFilasCsv(
        Stream archivo, decimal impuestoPredeterminado)
    {
        var validacion = ValidarImportacion(archivo, impuestoPredeterminado);
        if (validacion.IsError)
            return ProcessResult<List<FilaImportacionProducto>>.Error(validacion.Message);

        var filas = new List<FilaImportacionProducto>();
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
                    return ProcessResult<List<FilaImportacionProducto>>.Error(
                        "El archivo CSV está vacío.");

                var encabezados = parser.ReadFields() ?? Array.Empty<string>();
                var columnas = CrearMapaColumnas(encabezados);
                if (columnas.IsError)
                    return ProcessResult<List<FilaImportacionProducto>>.Error(columnas.Message);

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
                        filas.Add(new FilaImportacionProducto(numeroFila, string.Empty, null,
                            new List<string>
                            {
                                $"Se esperaban {encabezados.Length} columnas y se encontraron {campos.Length}."
                            }));
                        continue;
                    }

                    filas.Add(LeerFilaProducto(campos, indices, numeroFila, impuestoPredeterminado));
                }

                if (numeroFilas == 0)
                    return ProcessResult<List<FilaImportacionProducto>>.Error(
                        "El archivo CSV no contiene filas de productos.");
            }
        }
        catch (MalformedLineException ex)
        {
            return ProcessResult<List<FilaImportacionProducto>>.Error(
                $"El CSV está mal formado cerca de la línea {parser.ErrorLineNumber}: {ex.Message}");
        }
        catch (DecoderFallbackException)
        {
            return ProcessResult<List<FilaImportacionProducto>>.Error(
                "El archivo no tiene una codificación UTF-8 válida.");
        }
        catch (OverflowException)
        {
            return ProcessResult<List<FilaImportacionProducto>>.Error(
                "El archivo contiene un número de fila fuera del rango permitido.");
        }

        return ProcessResult<List<FilaImportacionProducto>>.OK(filas);
    }

    private ProcessResult<List<FilaImportacionProducto>> LeerFilasXlsx(
        Stream archivo, decimal impuestoPredeterminado)
    {
        var validacion = ValidarImportacion(archivo, impuestoPredeterminado);
        if (validacion.IsError)
            return ProcessResult<List<FilaImportacionProducto>>.Error(validacion.Message);

        HojaProductosXlsx hoja;
        try
        {
            hoja = LectorProductosXlsx.Leer(archivo);
        }
        catch (InvalidDataException ex)
        {
            return ProcessResult<List<FilaImportacionProducto>>.Error(
                $"El archivo XLSX no es válido: {ex.Message}");
        }
        catch (IOException)
        {
            return ProcessResult<List<FilaImportacionProducto>>.Error(
                "No se pudo leer el archivo XLSX.");
        }

        var columnas = CrearMapaColumnas(hoja.Encabezados);
        if (columnas.IsError)
            return ProcessResult<List<FilaImportacionProducto>>.Error(columnas.Message);

        var filas = new List<FilaImportacionProducto>();
        foreach (var filaExcel in hoja.Filas)
        {
            if (filaExcel.Campos.All(string.IsNullOrWhiteSpace) &&
                !filaExcel.TieneFormula &&
                !filaExcel.TieneErrorExcel)
            {
                continue;
            }

            var campos = new string[hoja.Encabezados.Length];
            Array.Fill(campos, string.Empty);
            Array.Copy(filaExcel.Campos, campos, Math.Min(filaExcel.Campos.Length, campos.Length));
            var fila = LeerFilaProducto(
                campos, columnas.Value, filaExcel.NumeroFila, impuestoPredeterminado);

            if (filaExcel.Campos.Length > campos.Length &&
                filaExcel.Campos.Skip(campos.Length).Any(campo => !string.IsNullOrWhiteSpace(campo)))
            {
                fila.Errores.Add("La fila tiene datos en columnas sin encabezado.");
            }

            if (filaExcel.TieneFormula)
                fila.Errores.Add("Las fórmulas no se importan; reemplázalas por sus valores.");
            if (filaExcel.TieneErrorExcel)
                fila.Errores.Add("La fila contiene celdas con errores de Excel.");

            filas.Add(fila);
        }

        if (filas.Count == 0)
            return ProcessResult<List<FilaImportacionProducto>>.Error(
                "La primera hoja no contiene filas de productos.");

        return ProcessResult<List<FilaImportacionProducto>>.OK(filas);
    }

    private VistaPreviaImportacion CrearVista(List<FilaImportacionProducto> filas)
    {
        AgregarErroresPorDuplicados(filas);
        var codigosExistentes = ObtenerProductos()
            .Select(producto => producto.CodigoBarras.Trim())
            .ToHashSet(StringComparer.Ordinal);

        foreach (var fila in filas)
        {
            if (!string.IsNullOrEmpty(fila.CodigoBarras) &&
                codigosExistentes.Contains(fila.CodigoBarras))
                fila.Errores.Add("El código de barras ya existe en el catálogo.");
        }

        var validas = filas
            .Where(fila => fila.Errores.Count == 0 && fila.Producto != null)
            .ToList();

        var errores = filas
            .Where(fila => fila.Errores.Count > 0)
            .Select(fila => new ErrorImportacionProducto(
                fila.NumeroFila, string.Join(" ", fila.Errores)))
            .ToList();

        return new VistaPreviaImportacion
        {
            FilasLeidas = filas.Count,
            FilasValidas = validas.Count,
            FilasOmitidas = errores.Count,
            Errores = errores,
            Muestra = validas.Take(20).Select(fila => new FilaVistaPreviaImportacion
            {
                NumeroFila = fila.NumeroFila,
                CodigoBarras = fila.Producto.CodigoBarras,
                Nombre = fila.Producto.Nombre,
                Precio = fila.Producto.Precio,
                Stock = fila.Producto.Stock
            }).ToList()
        };
    }

    private ProcessResult<ResultadoImportacionProductos> AplicarImportacion(
        List<FilaImportacionProducto> filas, string nombreArchivo, string hashArchivo)
    {
        var vista = CrearVista(filas);
        if (vista.FilasValidas == 0)
        {
            var detalle = vista.Errores.Count > 0
                ? " " + string.Join(" ", vista.Errores.Take(3)
                    .Select(error => $"Fila {error.NumeroFila}: {error.Mensaje}"))
                : string.Empty;
            return ProcessResult<ResultadoImportacionProductos>.Error(
                "Ninguna fila es válida; no se importó nada." + detalle);
        }

        var productos = filas
            .Where(fila => fila.Errores.Count == 0 && fila.Producto != null)
            .Select(fila => fila.Producto)
            .ToList();

        var auditoria = new RegistroBitacora
        {
            Fecha = DateTime.Now,
            Usuario = SesionActual.Usuario?.Nombre ?? string.Empty,
            Accion = AccionBitacora.ImportacionProductos,
            Entidad = "Importacion",
            Motivo = nombreArchivo
        };

        var guardado = _db.ImportarProductos(productos, nombreArchivo, hashArchivo,
            SesionActual.Usuario?.Nombre ?? string.Empty, vista.FilasLeidas,
            vista.FilasOmitidas, auditoria);
        if (guardado.IsError)
            return ProcessResult<ResultadoImportacionProductos>.Error(guardado.Message);

        var resumen = $"Se importaron {guardado.Value.ProductosInsertados} producto(s) y " +
                      $"se omitieron {vista.FilasOmitidas} fila(s). Lote #{guardado.Value.Id}.";
        return ProcessResult<ResultadoImportacionProductos>.OK(new ResultadoImportacionProductos
        {
            ProductosImportados = guardado.Value.ProductosInsertados,
            FilasOmitidas = vista.FilasOmitidas,
            ImportacionId = guardado.Value.Id,
            Errores = vista.Errores
        }, resumen);
    }

    /// <summary>
    /// Revierte un lote completo. Es exclusivo del dueño: devuelve el stock con
    /// ajustes compensatorios y desactiva los productos sin borrar historial.
    /// </summary>
    public ProcessResult RevertirImportacion(int importacionId)
    {
        var autorizacion = Autorizar(PermisoSistema.DesactivarProducto);
        if (autorizacion.IsError)
            return autorizacion;

        var importacion = _db.ObtenerImportacion(importacionId);
        if (importacion == null)
            return ProcessResult.Error($"La importación #{importacionId} no existe");

        var auditoria = new RegistroBitacora
        {
            Fecha = DateTime.Now,
            Usuario = SesionActual.Usuario?.Nombre ?? string.Empty,
            Accion = AccionBitacora.ReversionImportacion,
            Entidad = "Importacion",
            EntidadId = importacionId.ToString()
        };

        return _db.RevertirImportacion(importacionId,
            SesionActual.Usuario?.Nombre ?? string.Empty, auditoria);
    }

    public List<Importacion> ObtenerImportaciones(int limite = 50)
    {
        var autorizacion = Autorizar(PermisoSistema.EditarProducto);
        return autorizacion.IsError ? new List<Importacion>() : _db.ObtenerImportaciones(limite);
    }

    public Importacion ObtenerImportacion(int importacionId) =>
        _db.ObtenerImportacion(importacionId);

    private static ProcessResult ValidarImportacion(Stream archivo, decimal impuestoPredeterminado)
    {
        if (archivo == null || !archivo.CanRead)
            return ProcessResult.Error("No se puede leer el archivo.");

        if (impuestoPredeterminado < 0 || impuestoPredeterminado > 100)
            return ProcessResult.Error(
                "El impuesto predeterminado debe estar entre 0 y 100.");

        return ProcessResult.Success(string.Empty);
    }

    private static byte[] LeerBytes(Stream archivo)
    {
        using var copia = new MemoryStream();
        archivo.CopyTo(copia);
        return copia.ToArray();
    }

    private static string HashArchivo(byte[] contenido) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(contenido));

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
                    "El archivo contiene un encabezado vacío.");

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

    private static FilaImportacionProducto LeerFilaProducto(
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

        var costo = LeerDineroImportado(costoTexto);
        if (!costo.HasValue)
            errores.Add("El costo debe ser un importe no negativo con punto decimal.");

        var precio = LeerDineroImportado(precioTexto);
        if (!precio.HasValue)
            errores.Add("El precio debe ser un importe no negativo con punto decimal.");

        var stock = LeerEnteroImportado(LeerColumna(campos, indices, "Stock"), 0);
        if (!stock.HasValue || stock.Value > 1_000_000)
            errores.Add("El stock debe ser un entero entre 0 y 1,000,000.");

        var stockMinimo = LeerEnteroImportado(
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

        return new FilaImportacionProducto(numeroFila, codigo, producto, errores);
    }

    private static string LeerColumna(
        string[] campos, Dictionary<string, int> indices, string nombre) =>
        indices.TryGetValue(nombre, out var indice) ? campos[indice] : string.Empty;

    private static long? LeerDineroImportado(string texto)
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

    private static int? LeerEnteroImportado(string texto, int valorPredeterminado)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return valorPredeterminado;

        return int.TryParse(texto.Trim(), NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var valor) && valor >= 0
            ? valor
            : null;
    }

    private static void AgregarErroresPorDuplicados(List<FilaImportacionProducto> filas)
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

    private sealed record FilaImportacionProducto(
        int NumeroFila, string CodigoBarras, Producto Producto, List<string> Errores);

    public List<Producto> ObtenerProductos() => _db.ObtenerProductos();

    public Producto BuscarPorCodigo(string codigoBarras) => _db.ObtenerProductoPorCodigo(codigoBarras);

    public Producto BuscarPorId(int productoId) => _db.ObtenerProductoPorId(productoId);

    public List<Producto> BuscarPorNombre(string texto, bool soloActivos = false)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return new List<Producto>();

        var filtro = Texto.SinAcentos(texto.Trim());
        return _db.ObtenerProductos()
            .Where(p => (!soloActivos || p.Activo) &&
                        Texto.SinAcentos(p.Nombre ?? string.Empty)
                            .Contains(filtro, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Sugerencias para el punto de venta mientras se escribe. Solo productos
    /// activos y ordenados por cercanía: código exacto, prefijo de código,
    /// prefijo de nombre, inicio de palabra y coincidencia interna.
    /// </summary>
    public List<Producto> SugerirProductos(string texto, int maximo = 8)
    {
        if (string.IsNullOrWhiteSpace(texto) || maximo <= 0)
            return new List<Producto>();

        var filtro = Texto.SinAcentos(texto.Trim());
        if (filtro.Length == 0)
            return new List<Producto>();

        return _db.ObtenerProductos()
            .Where(p => p.Activo)
            .Select(p => (Producto: p, Rango: RangoSugerencia(p, filtro)))
            .Where(x => x.Rango < int.MaxValue)
            .OrderBy(x => x.Rango)
            .ThenBy(x => Texto.SinAcentos(x.Producto.Nombre ?? string.Empty),
                StringComparer.OrdinalIgnoreCase)
            .Take(maximo)
            .Select(x => x.Producto)
            .ToList();
    }

    private static int RangoSugerencia(Producto producto, string filtro)
    {
        var nombre = Texto.SinAcentos(producto.Nombre ?? string.Empty);
        var codigo = Texto.SinAcentos(producto.CodigoBarras ?? string.Empty);

        if (codigo.Equals(filtro, StringComparison.OrdinalIgnoreCase))
            return 0;
        if (codigo.StartsWith(filtro, StringComparison.OrdinalIgnoreCase))
            return 1;
        if (nombre.StartsWith(filtro, StringComparison.OrdinalIgnoreCase))
            return 2;
        if (nombre.Contains(" " + filtro, StringComparison.OrdinalIgnoreCase))
            return 3;
        if (nombre.Contains(filtro, StringComparison.OrdinalIgnoreCase))
            return 4;
        if (codigo.Contains(filtro, StringComparison.OrdinalIgnoreCase))
            return 5;

        return int.MaxValue;
    }

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

        var auditoria = new RegistroBitacora
        {
            Fecha = DateTime.Now,
            Usuario = SesionActual.Usuario?.Nombre ?? string.Empty,
            Accion = cambiaPrecio ? AccionBitacora.CambioPrecio : AccionBitacora.EditarProducto,
            Entidad = "Producto",
            EntidadId = producto.Id.ToString(),
            DatosAntes = $"Nombre={anterior.Nombre};Costo={Dinero.Formatear(anterior.Costo)};Precio={Dinero.Formatear(anterior.Precio)}",
            DatosDespues = $"Nombre={producto.Nombre};Costo={Dinero.Formatear(producto.Costo)};Precio={Dinero.Formatear(producto.Precio)}"
        };

        if (!_db.ActualizarProducto(producto, auditoria))
            return ProcessResult.Error("No se encontró el producto");

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

        var auditoria = new RegistroBitacora
        {
            Fecha = DateTime.Now,
            Usuario = usuario,
            Accion = AccionBitacora.EntradaInventario,
            Entidad = "Producto",
            DatosAntes = $"Stock={producto.Stock}",
            DatosDespues = $"Stock={producto.Stock + cantidad};CostoUnitario={Dinero.Formatear(costoUnitario)}",
            Motivo = motivo
        };

        if (!_db.RegistrarEntrada(productoId, cantidad, costoUnitario, motivo, usuario, auditoria))
            return ProcessResult.Error("No se encontró el producto");
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

        var costoPerdido = Dinero.Multiplicar(producto.Costo, cantidad);
        var auditoria = new RegistroBitacora
        {
            Fecha = DateTime.Now,
            Usuario = usuario,
            Accion = AccionBitacora.Merma,
            Entidad = "Producto",
            DatosAntes = $"Stock={producto.Stock}",
            DatosDespues = $"Stock={producto.Stock - cantidad};CostoPerdido={Dinero.Formatear(costoPerdido)}",
            Motivo = motivo
        };

        if (!_db.RegistrarSalida(productoId, cantidad, $"Merma: {motivo.Trim()}", usuario, auditoria))
            return ProcessResult.Error("Stock insuficiente para registrar la merma");
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
        if (producto == null)
            return ProcessResult.Error("No se encontró el producto");

        if (!producto.Activo)
            return ProcessResult.Error(
                "El producto está desactivado; reactívalo para ajustar su stock");

        var auditoria = new RegistroBitacora
        {
            Fecha = DateTime.Now,
            Usuario = usuario,
            Accion = AccionBitacora.AjusteInventario,
            Entidad = "Producto",
            DatosAntes = $"Stock={producto?.Stock ?? 0}",
            DatosDespues = $"Stock={nuevoStock}",
            Motivo = motivo
        };

        if (!_db.AjustarStock(productoId, nuevoStock, motivo.Trim(), usuario, auditoria))
            return ProcessResult.Error("No se pudo ajustar el stock");
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

        var auditoria = new RegistroBitacora
        {
            Fecha = DateTime.Now,
            Usuario = SesionActual.Usuario?.Nombre ?? string.Empty,
            Accion = AccionBitacora.CambiarEstadoProducto,
            Entidad = "Producto",
            EntidadId = productoId.ToString(),
            DatosAntes = $"Nombre={producto.Nombre};Activo={(activo ? 0 : 1)};Stock={producto.Stock}",
            DatosDespues = $"Nombre={producto.Nombre};Activo={(activo ? 1 : 0)};Stock={producto.Stock}"
        };

        if (!_db.CambiarEstadoProducto(productoId, activo, auditoria))
            return ProcessResult.Error("No se pudo cambiar el estado del producto");
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
