// Punto de entrada de la aplicación (interfaz de consola provisional)
using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Presentation;

internal static class Program
{
    private static DatabaseTools _db;
    private static ConfiguracionService _config;
    private static InventoryService _inventario;
    private static RespaldoService _respaldos;
    private static UsuarioSistema _usuarioActual;
    private static ConfiguracionTienda _configTienda;

    private static bool EsDueño => _usuarioActual?.Rol == RolUsuario.Dueño;
    private static bool EsAdministrador => _usuarioActual?.Rol is RolUsuario.Dueño or RolUsuario.Encargado;

    private static int Main(string[] args)
    {
        try
        {
            var dbPath = Environment.GetEnvironmentVariable("SISTEMATIENDA_DB");
            if (string.IsNullOrWhiteSpace(dbPath))
            {
                dbPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SistemaTienda", "tienda.db");
            }

            _db = new DatabaseTools(dbPath);
            _db.InicializarBaseDeDatos();

            _config = new ConfiguracionService(_db);
            _inventario = new InventoryService(_db);
            _respaldos = new RespaldoService(_db);

            if (_db.ContarUsuarios() == 0 && !EjecutarAsistenteInicial())
                return 1;

            if (!IniciarSesion())
                return 0;

            BuclePrincipal();
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError fatal: {ex.Message}");
            return 1;
        }
    }

    // ---------------- Primer arranque y sesión ----------------

    private static bool EjecutarAsistenteInicial()
    {
        LimpiarPantalla();
        Console.WriteLine("==============================================");
        Console.WriteLine("  ASISTENTE DE PRIMERA CONFIGURACIÓN");
        Console.WriteLine("  Sistema de Gestión para Tiendas Pequeñas");
        Console.WriteLine("  Costo recurrente $0 | 100% offline");
        Console.WriteLine("==============================================\n");

        var nombre = LeerTexto("Nombre de la tienda");
        var giro = LeerTextoConDefecto("Giro del negocio", ConfiguracionPorDefecto.GiroPredeterminado);
        var impuesto = LeerDecimal("Impuesto predeterminado (%)", 0, ConfiguracionPorDefecto.ImpuestoPredeterminado);
        var comision = LeerDecimal("Comisión de tarjeta (%)", 0, ConfiguracionPorDefecto.ComisionTarjetaPredeterminada);
        var fondo = LeerDecimal("Fondo de caja inicial ($)", 0, ConfiguracionPorDefecto.FondoCajaInicialPredeterminado);
        var fiado = LeerSiNo("¿Activar fiado a clientes?", false);
        var limite = fiado ? LeerDecimal("Límite de crédito por cliente (0 = sin límite)", 0, 0) : 0;
        var intervalo = LeerEntero("Días entre respaldos (0 = deshabilitado)", 0, 365,
            ConfiguracionPorDefecto.RespaldoIntervaloDiasPredeterminado);
        var pin = LeerPin("PIN del dueño (4 a 6 dígitos)");
        var pinConfirmacion = LeerPin("Confirme el PIN");

        if (pin != pinConfirmacion)
        {
            Console.WriteLine("\n✗ Los PIN no coinciden. Vuelva a ejecutar el asistente.");
            Pausa();
            return false;
        }

        var resultado = _config.EjecutarAsistenteInicial(nombre, giro, impuesto, comision,
            fondo, fiado, limite, intervalo, pin);

        Console.WriteLine();
        Console.WriteLine(resultado.IsSuccess ? $"✓ {resultado.Message}" : $"✗ {resultado.Message}");
        Pausa();
        return resultado.IsSuccess;
    }

    private static bool IniciarSesion()
    {
        for (var intento = 1; intento <= 3; intento++)
        {
            LimpiarPantalla();
            var config = _config.ObtenerConfiguracion();
            Console.WriteLine("==============================================");
            Console.WriteLine($"  {config.Value?.NombreTienda ?? "Mi Tienda"}");
            Console.WriteLine("  Acceso al sistema");
            Console.WriteLine("==============================================\n");
            Console.Write("  PIN: ");

            var pin = Console.ReadLine();
            if (pin == null) return false;

            var resultado = _config.VerificarAcceso(pin);
            if (resultado.IsSuccess)
            {
                _usuarioActual = resultado.Value;
                _configTienda = _config.ObtenerConfiguracion().Value;
                return true;
            }

            Console.WriteLine($"\n  ✗ {resultado.Message}. Intento {intento} de 3.");
            Pausa();
        }

        Console.WriteLine("\n  Demasiados intentos. La aplicación se cerrará.");
        Pausa();
        return false;
    }

    // ---------------- Menú principal ----------------

    private static void BuclePrincipal()
    {
        while (true)
        {
            _configTienda = _config.ObtenerConfiguracion().Value;

            LimpiarPantalla();
            Console.WriteLine("==============================================");
            Console.WriteLine($"  {_configTienda.NombreTienda}  |  {_configTienda.Giro}");
            Console.WriteLine($"  Usuario: {_usuarioActual.Nombre} ({_usuarioActual.Rol})");
            Console.WriteLine("==============================================");
            Console.WriteLine("  [1] Inventario y catálogo");
            Console.WriteLine("  [2] Ventas / punto de venta");
            Console.WriteLine("  [3] Mermas");
            Console.WriteLine("  [4] Gastos");
            Console.WriteLine("  [5] Clientes y fiado");
            Console.WriteLine("  [6] Proveedores y cuentas por pagar");
            Console.WriteLine("  [7] Reportes");
            Console.WriteLine("  [8] Configuración");
            Console.WriteLine("  [0] Salir");
            Console.WriteLine("==============================================");
            Console.Write("  Opción: ");

            switch (Console.ReadLine())
            {
                case "1": MenuInventario(); break;
                case "2": MostrarModuloPendiente("Ventas / punto de venta",
                    "Falta implementar el registro de ventas, el ticket digital y el descuento automático de stock."); break;
                case "3": MenuMermas(); break;
                case "4": MenuGastos(); break;
                case "5": MenuFiado(); break;
                case "6": MenuProveedores(); break;
                case "7": MenuReportes(); break;
                case "8": MenuConfiguracion(); break;
                case "0": return;
                case null: return;
                default: Aviso("Opción no válida."); break;
            }
        }
    }

    // ---------------- Inventario ----------------

    private static void MenuInventario()
    {
        while (true)
        {
            LimpiarPantalla();
            Console.WriteLine("=== INVENTARIO Y CATÁLOGO ===");
            Console.WriteLine($"  Productos registrados: {_inventario.ObtenerProductos().Count}\n");
            Console.WriteLine("  [1] Ver productos");
            Console.WriteLine("  [2] Buscar por código de barras");
            Console.WriteLine("  [3] Buscar por nombre");
            Console.WriteLine("  [4] Productos con stock bajo");
            Console.WriteLine("  [5] Historial de movimientos");
            Console.WriteLine("  [6] Agregar producto");
            Console.WriteLine("  [7] Editar producto");
            Console.WriteLine("  [8] Ajustar stock");
            Console.WriteLine("  [0] Regresar");
            Console.Write("\n  Opción: ");

            switch (Console.ReadLine())
            {
                case "1": ListarProductosPaginado(_inventario.ObtenerProductos()); break;
                case "2": BuscarPorCodigo(); break;
                case "3": BuscarPorNombre(); break;
                case "4": ListarProductosPaginado(_inventario.ProductosStockBajo()); break;
                case "5": VerMovimientos(); break;
                case "6": AgregarProducto(); break;
                case "7": EditarProducto(); break;
                case "8": AjustarStock(); break;
                case "0": return;
                case null: return;
                default: Aviso("Opción no válida."); break;
            }
        }
    }

    private static void ListarProductosPaginado(List<Producto> productos)
    {
        LimpiarPantalla();
        if (productos.Count == 0)
        {
            Aviso("No hay productos para mostrar.");
            return;
        }

        const int tamanoPagina = 20;
        var pagina = 0;

        while (true)
        {
            LimpiarPantalla();
            var inicio = pagina * tamanoPagina;
            var fin = Math.Min(inicio + tamanoPagina, productos.Count);
            Console.WriteLine($"=== PRODUCTOS ({inicio + 1}-{fin} de {productos.Count}) ===");
            Console.WriteLine("  ID    Código            Nombre                          Stock    Precio");
            for (var i = inicio; i < fin; i++)
            {
                var p = productos[i];
                Console.WriteLine($"  {p.Id,-5} {p.CodigoBarras,-17} {p.Nombre,-31} {p.Stock,-8} ${p.Precio,8:F2}");
            }

            Console.WriteLine("\n  [Enter] siguiente página   [q] salir");
            var entrada = Console.ReadLine();
            if (entrada == null || entrada.Trim().Equals("q", StringComparison.OrdinalIgnoreCase))
                return;

            if (fin >= productos.Count)
                return;

            pagina++;
        }
    }

    private static void BuscarPorCodigo()
    {
        LimpiarPantalla();
        Console.Write("  Código de barras: ");
        var codigo = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(codigo)) return;

        MostrarProducto(_inventario.BuscarPorCodigo(codigo.Trim()));
    }

    private static void BuscarPorNombre()
    {
        LimpiarPantalla();
        Console.Write("  Texto a buscar: ");
        var texto = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(texto)) return;

        ListarProductosPaginado(_inventario.BuscarPorNombre(texto));
    }

    private static void MostrarProducto(Producto producto)
    {
        LimpiarPantalla();
        if (producto == null)
        {
            Aviso("Producto no encontrado.");
            return;
        }

        Console.WriteLine("=== PRODUCTO ===");
        Console.WriteLine($"  ID:        {producto.Id}");
        Console.WriteLine($"  Código:    {producto.CodigoBarras}");
        Console.WriteLine($"  Nombre:    {producto.Nombre}");
        Console.WriteLine($"  Categoría: {producto.Categoria}");
        Console.WriteLine($"  Costo:     ${producto.Costo:F2}");
        Console.WriteLine($"  Precio:    ${producto.Precio:F2}");
        Console.WriteLine($"  Stock:     {producto.Stock} (mínimo {producto.StockMinimo})");
        Console.WriteLine($"  Impuesto:  {producto.Impuesto:F2}%");
        Pausa();
    }

    private static void VerMovimientos()
    {
        var producto = SeleccionarProducto();
        if (producto == null) return;

        LimpiarPantalla();
        var movimientos = _inventario.HistorialMovimientos(producto.Id);
        Console.WriteLine($"=== MOVIMIENTOS: {producto.Nombre} ===");
        if (movimientos.Count == 0)
        {
            Console.WriteLine("  Sin movimientos registrados.");
        }
        else
        {
            foreach (var m in movimientos)
                Console.WriteLine($"  {m.Fecha:g}  {m.Tipo,-8} {m.Cantidad,5}  {m.Motivo}  ({m.Usuario})");
        }
        Pausa();
    }

    private static void AgregarProducto()
    {
        if (!EsAdministrador)
        {
            Aviso("Solo dueño o encargado pueden agregar productos.");
            return;
        }

        LimpiarPantalla();
        Console.WriteLine("=== AGREGAR PRODUCTO ===");
        var producto = new Producto
        {
            CodigoBarras = LeerTexto("Código de barras"),
            Nombre = LeerTexto("Nombre"),
            Categoria = LeerTextoConDefecto("Categoría", string.Empty),
            Costo = LeerDecimal("Costo ($)", 0, 0),
            Precio = LeerDecimal("Precio de venta ($)", 0, 0),
            Stock = LeerEntero("Stock inicial", 0, 1_000_000, 0),
            StockMinimo = LeerEntero("Stock mínimo", 0, 1_000_000, 5),
            Impuesto = _configTienda.ImpuestoPredeterminado
        };

        var resultado = _inventario.AgregarProducto(producto);
        Aviso(resultado.Message, resultado.IsSuccess);
    }

    private static void EditarProducto()
    {
        if (!EsAdministrador)
        {
            Aviso("Solo dueño o encargado pueden editar productos.");
            return;
        }

        var producto = SeleccionarProducto();
        if (producto == null) return;

        LimpiarPantalla();
        Console.WriteLine($"=== EDITAR PRODUCTO #{producto.Id} ===");
        Console.WriteLine("  (El stock no se edita aquí; use Ajustar stock)\n");

        producto.Nombre = LeerTextoConDefecto("Nombre", producto.Nombre);
        producto.Categoria = LeerTextoConDefecto("Categoría", producto.Categoria);
        producto.Costo = LeerDecimal("Costo ($)", 0, producto.Costo);
        producto.Precio = LeerDecimal("Precio de venta ($)", 0, producto.Precio);
        producto.StockMinimo = LeerEntero("Stock mínimo", 0, 1_000_000, producto.StockMinimo);
        producto.Impuesto = LeerDecimal("Impuesto (%)", 0, producto.Impuesto);

        var resultado = _inventario.ActualizarProducto(producto);
        Aviso(resultado.Message, resultado.IsSuccess);
    }

    private static void AjustarStock()
    {
        if (!EsAdministrador)
        {
            Aviso("Solo dueño o encargado pueden ajustar stock.");
            return;
        }

        var producto = SeleccionarProducto();
        if (producto == null) return;

        LimpiarPantalla();
        Console.WriteLine($"=== AJUSTAR STOCK: {producto.Nombre} (actual {producto.Stock}) ===");
        var nuevoStock = LeerEntero("Stock real contado", 0, 1_000_000, producto.Stock);
        var motivo = LeerTexto("Motivo del ajuste");

        var resultado = _inventario.AjustarStock(producto.Id, nuevoStock, motivo, _usuarioActual.Nombre);
        Aviso(resultado.Message, resultado.IsSuccess);
    }

    private static Producto SeleccionarProducto()
    {
        LimpiarPantalla();
        var productos = _inventario.ObtenerProductos();
        if (productos.Count == 0)
        {
            Aviso("No hay productos en el catálogo.");
            return null;
        }

        Console.WriteLine("=== SELECCIONAR PRODUCTO ===");
        foreach (var p in productos.Take(50))
            Console.WriteLine($"  [{p.Id}] {p.Nombre} (stock {p.Stock})");

        if (productos.Count > 50)
            Console.WriteLine("  ... use la búsqueda para encontrar otros productos.");

        var id = LeerEntero("\n  ID del producto (0 = cancelar)", 0, int.MaxValue, 0);
        if (id == 0) return null;

        var producto = _inventario.BuscarPorId(id);
        if (producto == null) Aviso("Producto no encontrado.");
        return producto;
    }

    // ---------------- Mermas ----------------

    private static void MenuMermas()
    {
        if (!EsAdministrador)
        {
            Aviso("Solo dueño o encargado pueden registrar mermas.");
            return;
        }

        var producto = SeleccionarProducto();
        if (producto == null) return;

        if (producto.Stock <= 0)
        {
            Aviso("El producto no tiene stock disponible.");
            return;
        }

        LimpiarPantalla();
        Console.WriteLine($"=== REGISTRAR MERMA: {producto.Nombre} (stock {producto.Stock}) ===");
        var cantidad = LeerEntero("Cantidad perdida", 1, producto.Stock, 1);
        var motivo = LeerTexto("Motivo (caducado, roto, robo...)");

        var resultado = _inventario.RegistrarMerma(producto.Id, cantidad, motivo, _usuarioActual.Nombre);
        Aviso(resultado.Message, resultado.IsSuccess);
    }

    // ---------------- Gastos ----------------

    private static void MenuGastos()
    {
        while (true)
        {
            LimpiarPantalla();
            Console.WriteLine("=== GASTOS ===");
            Console.WriteLine("  [1] Registrar gasto");
            Console.WriteLine("  [2] Ver gastos");
            Console.WriteLine("  [3] Total y por concepto");
            Console.WriteLine("  [0] Regresar");
            Console.Write("\n  Opción: ");

            switch (Console.ReadLine())
            {
                case "1": RegistrarGasto(); break;
                case "2": VerGastos(); break;
                case "3": ReporteGastos(); break;
                case "0": return;
                case null: return;
                default: Aviso("Opción no válida."); break;
            }
        }
    }

    private static void RegistrarGasto()
    {
        LimpiarPantalla();
        Console.WriteLine("=== REGISTRAR GASTO ===");
        var gasto = new Gasto
        {
            Concepto = LeerTexto("Concepto"),
            Monto = LeerDecimal("Monto ($)", 0, 0),
            MetodoPago = LeerTextoConDefecto("Método de pago (Efectivo/Tarjeta/Transferencia)", "Efectivo"),
            Origen = LeerSiNo("¿Salió del efectivo de la caja?", true) ? OrigenGasto.Caja : OrigenGasto.Externo,
            Observaciones = LeerTextoConDefecto("Observaciones", string.Empty),
            Fecha = DateTime.Now
        };

        var resultado = _config.AgregarGasto(gasto);
        Aviso(resultado.Message, resultado.IsSuccess);
    }

    private static void VerGastos()
    {
        LimpiarPantalla();
        var gastos = _config.ObtenerTodosLosGastos().Value ?? new List<Gasto>();
        Console.WriteLine("=== GASTOS REGISTRADOS ===");
        if (gastos.Count == 0)
        {
            Console.WriteLine("  Sin gastos registrados.");
        }
        else
        {
            foreach (var g in gastos.Take(100))
                Console.WriteLine($"  {g.Fecha:d}  {g.Concepto,-25} ${g.Monto,9:F2}  {g.MetodoPago}  ({g.Origen})");
        }
        Pausa();
    }

    private static void ReporteGastos()
    {
        LimpiarPantalla();
        Console.WriteLine("=== RESUMEN DE GASTOS ===");
        Console.WriteLine($"  Total: ${_config.ObtenerTotalGastos().Value:F2}\n");
        foreach (var grupo in _config.ObtenerGastosPorConcepto().Value ?? new List<GastoPorConcepto>())
            Console.WriteLine($"  {grupo.Concepto,-30} ${grupo.Total,9:F2}");
        Pausa();
    }

    // ---------------- Fiado ----------------

    private static void MenuFiado()
    {
        if (!_configTienda.FiadoActivado)
        {
            Aviso("El módulo de fiado está desactivado. Puede activarlo en Configuración.");
            return;
        }

        while (true)
        {
            LimpiarPantalla();
            Console.WriteLine("=== CLIENTES Y FIADO ===");
            Console.WriteLine("  [1] Nuevo cliente");
            Console.WriteLine("  [2] Ver todos los clientes");
            Console.WriteLine("  [3] Clientes con saldo pendiente");
            Console.WriteLine("  [4] Registrar abono");
            Console.WriteLine("  [0] Regresar");
            Console.Write("\n  Opción: ");

            switch (Console.ReadLine())
            {
                case "1": NuevoClienteFiado(); break;
                case "2": VerClientes(false); break;
                case "3": VerClientes(true); break;
                case "4": RegistrarAbono(); break;
                case "0": return;
                case null: return;
                default: Aviso("Opción no válida."); break;
            }
        }
    }

    private static void NuevoClienteFiado()
    {
        LimpiarPantalla();
        Console.WriteLine("=== NUEVO CLIENTE (FIADO) ===");
        var cliente = new ClienteFiado
        {
            Nombre = LeerTexto("Nombre"),
            Telefono = LeerTextoConDefecto("Teléfono", string.Empty),
            SaldoActual = LeerDecimal("Saldo inicial ($)", 0, 0)
        };

        var resultado = _config.AgregarClienteFiado(cliente);
        Aviso(resultado.Message, resultado.IsSuccess);
    }

    private static void VerClientes(bool soloConSaldo)
    {
        LimpiarPantalla();
        var clientes = soloConSaldo
            ? _config.ObtenerClientesConSaldo().Value ?? new List<ClienteFiado>()
            : _config.ObtenerTodosLosClientes().Value ?? new List<ClienteFiado>();

        Console.WriteLine(soloConSaldo ? "=== CLIENTES CON SALDO ===" : "=== CLIENTES ===");
        if (clientes.Count == 0)
        {
            Console.WriteLine("  Sin clientes registrados.");
        }
        else
        {
            foreach (var c in clientes.Take(100))
                Console.WriteLine($"  [{c.Id}] {c.Nombre,-25} ${c.SaldoActual,9:F2}  {c.Telefono}");
        }
        Pausa();
    }

    private static void RegistrarAbono()
    {
        var clientes = _config.ObtenerClientesConSaldo().Value ?? new List<ClienteFiado>();
        if (clientes.Count == 0)
        {
            Aviso("No hay clientes con saldo pendiente.");
            return;
        }

        LimpiarPantalla();
        Console.WriteLine("=== REGISTRAR ABONO ===");
        foreach (var c in clientes.Take(50))
            Console.WriteLine($"  [{c.Id}] {c.Nombre,-25} ${c.SaldoActual,9:F2}");

        var id = LeerEntero("\n  ID del cliente (0 = cancelar)", 0, int.MaxValue, 0);
        if (id == 0) return;

        var cliente = clientes.FirstOrDefault(c => c.Id == id);
        if (cliente == null)
        {
            Aviso("Cliente no encontrado.");
            return;
        }

        var monto = LeerDecimal($"Monto del abono (saldo ${cliente.SaldoActual:F2})", 0.01m, 0);
        var resultado = _config.RegistrarAbonoCliente(cliente.Id, monto);
        Aviso(resultado.IsSuccess
            ? $"Abono registrado. Nuevo saldo: ${resultado.Value.SaldoRestante:F2}"
            : resultado.Message, resultado.IsSuccess);
    }

    // ---------------- Proveedores ----------------

    private static void MenuProveedores()
    {
        while (true)
        {
            LimpiarPantalla();
            Console.WriteLine("=== PROVEEDORES Y CUENTAS POR PAGAR ===");
            Console.WriteLine("  [1] Nuevo proveedor");
            Console.WriteLine("  [2] Ver proveedores");
            Console.WriteLine("  [3] Registrar pago");
            Console.WriteLine("  [0] Regresar");
            Console.Write("\n  Opción: ");

            switch (Console.ReadLine())
            {
                case "1": NuevoProveedor(); break;
                case "2": VerProveedores(); break;
                case "3": RegistrarPagoProveedor(); break;
                case "0": return;
                case null: return;
                default: Aviso("Opción no válida."); break;
            }
        }
    }

    private static void NuevoProveedor()
    {
        LimpiarPantalla();
        Console.WriteLine("=== NUEVO PROVEEDOR ===");
        var proveedor = new Proveedor
        {
            Nombre = LeerTexto("Nombre"),
            Clave = LeerTextoConDefecto("RFC o razón social", string.Empty),
            Telefono = LeerTextoConDefecto("Teléfono", string.Empty),
            Direccion = LeerTextoConDefecto("Dirección", string.Empty),
            SaldoPendiente = LeerDecimal("Saldo inicial pendiente ($)", 0, 0)
        };

        var resultado = _config.AgregarProveedor(proveedor);
        Aviso(resultado.Message, resultado.IsSuccess);
    }

    private static void VerProveedores()
    {
        LimpiarPantalla();
        var proveedores = _config.ObtenerTodosLosProveedores().Value ?? new List<Proveedor>();
        Console.WriteLine("=== PROVEEDORES ===");
        if (proveedores.Count == 0)
        {
            Console.WriteLine("  Sin proveedores registrados.");
        }
        else
        {
            foreach (var p in proveedores.Take(100))
                Console.WriteLine($"  [{p.Id}] {p.Nombre,-25} ${p.SaldoPendiente,9:F2}  {p.Telefono}");
        }
        Pausa();
    }

    private static void RegistrarPagoProveedor()
    {
        var proveedores = _config.ObtenerTodosLosProveedores().Value ?? new List<Proveedor>();
        var conSaldo = proveedores.Where(p => p.SaldoPendiente > 0).ToList();
        if (conSaldo.Count == 0)
        {
            Aviso("No hay proveedores con saldo pendiente.");
            return;
        }

        LimpiarPantalla();
        Console.WriteLine("=== REGISTRAR PAGO A PROVEEDOR ===");
        foreach (var p in conSaldo.Take(50))
            Console.WriteLine($"  [{p.Id}] {p.Nombre,-25} ${p.SaldoPendiente,9:F2}");

        var id = LeerEntero("\n  ID del proveedor (0 = cancelar)", 0, int.MaxValue, 0);
        if (id == 0) return;

        var proveedor = conSaldo.FirstOrDefault(p => p.Id == id);
        if (proveedor == null)
        {
            Aviso("Proveedor no encontrado.");
            return;
        }

        var monto = LeerDecimal($"Monto del pago (saldo ${proveedor.SaldoPendiente:F2})", 0.01m, 0);
        var resultado = _config.RegistrarPagoProveedor(proveedor.Id, monto);
        Aviso(resultado.IsSuccess
            ? $"Pago registrado. Nuevo saldo: ${resultado.Value.SaldoRestante:F2}"
            : resultado.Message, resultado.IsSuccess);
    }

    // ---------------- Reportes ----------------

    private static void MenuReportes()
    {
        while (true)
        {
            LimpiarPantalla();
            Console.WriteLine("=== REPORTES ===");
            Console.WriteLine("  [1] Valorizado de inventario");
            Console.WriteLine("  [2] Productos con stock bajo");
            Console.WriteLine("  [3] Resumen de gastos");
            Console.WriteLine("  [4] Saldo total con proveedores");
            Console.WriteLine("  [5] Ventas y utilidad (pendiente)");
            Console.WriteLine("  [0] Regresar");
            Console.Write("\n  Opción: ");

            switch (Console.ReadLine())
            {
                case "1":
                    LimpiarPantalla();
                    Console.WriteLine("=== VALORIZADO DE INVENTARIO ===");
                    Console.WriteLine($"  Valor total al costo: ${_inventario.ObtenerValorizadoInventario():F2}");
                    Pausa();
                    break;
                case "2": ListarProductosPaginado(_inventario.ProductosStockBajo()); break;
                case "3": ReporteGastos(); break;
                case "4": ReporteSaldoProveedores(); break;
                case "5":
                    MostrarModuloPendiente("Ventas y utilidad",
                        "Requiere el módulo de ventas. Aún no hay ventas registradas que reportar.");
                    break;
                case "0": return;
                case null: return;
                default: Aviso("Opción no válida."); break;
            }
        }
    }

    private static void ReporteSaldoProveedores()
    {
        LimpiarPantalla();
        var proveedores = _config.ObtenerTodosLosProveedores().Value ?? new List<Proveedor>();
        Console.WriteLine("=== SALDO CON PROVEEDORES ===");
        foreach (var p in proveedores.Where(p => p.SaldoPendiente > 0))
            Console.WriteLine($"  {p.Nombre,-30} ${p.SaldoPendiente,9:F2}");
        Console.WriteLine($"\n  Total por pagar: ${proveedores.Sum(p => p.SaldoPendiente):F2}");
        Pausa();
    }

    // ---------------- Configuración ----------------

    private static void MenuConfiguracion()
    {
        while (true)
        {
            var ultimo = _respaldos.ObtenerUltimoRespaldo();
            LimpiarPantalla();
            Console.WriteLine("=== CONFIGURACIÓN ===");
            Console.WriteLine($"  Último respaldo: {(ultimo.IsSuccess && ultimo.Value.HasValue ? ultimo.Value.Value.ToString("yyyy-MM-dd HH:mm") : "ninguno")}");
            Console.WriteLine();
            Console.WriteLine("  [1] Ver configuración");
            Console.WriteLine("  [2] Editar configuración (dueño)");
            Console.WriteLine("  [3] Usuarios (dueño)");
            Console.WriteLine("  [4] Crear respaldo ahora");
            Console.WriteLine("  [5] Ver respaldos");
            Console.WriteLine("  [6] Restaurar respaldo (dueño)");
            Console.WriteLine("  [0] Regresar");
            Console.Write("\n  Opción: ");

            switch (Console.ReadLine())
            {
                case "1": VerConfiguracion(); break;
                case "2": EditarConfiguracion(); break;
                case "3": MenuUsuarios(); break;
                case "4": CrearRespaldoManual(); break;
                case "5": VerRespaldos(); break;
                case "6": RestaurarRespaldo(); break;
                case "0": return;
                case null: return;
                default: Aviso("Opción no válida."); break;
            }
        }
    }

    private static void CrearRespaldoManual()
    {
        var resultado = _respaldos.CrearRespaldo("manual");
        Aviso(resultado.IsSuccess
            ? $"Respaldo creado y verificado:\n  {resultado.Value}"
            : resultado.Message, resultado.IsSuccess);
    }

    private static void VerRespaldos()
    {
        LimpiarPantalla();
        var resultado = _respaldos.ListarRespaldos();
        Console.WriteLine("=== RESPALDOS ===");

        if (!resultado.IsSuccess)
        {
            Aviso(resultado.Message);
            return;
        }

        if (resultado.Value.Count == 0)
        {
            Console.WriteLine("  No hay respaldos todavía.");
        }
        else
        {
            Console.WriteLine($"  Carpeta: {_respaldos.DirectorioRespaldos}\n");
            foreach (var r in resultado.Value)
                Console.WriteLine($"  {r.Nombre}  {r.Fecha:yyyy-MM-dd HH:mm}  {r.TamanoBytes / 1024.0:F1} KB");
        }

        Pausa();
    }

    private static void RestaurarRespaldo()
    {
        if (!EsDueño)
        {
            Aviso("Solo el dueño puede restaurar un respaldo.");
            return;
        }

        var respaldos = _respaldos.ListarRespaldos();
        if (!respaldos.IsSuccess || respaldos.Value.Count == 0)
        {
            Aviso("No hay respaldos disponibles para restaurar.");
            return;
        }

        LimpiarPantalla();
        Console.WriteLine("=== RESTAURAR RESPALDO ===");
        Console.WriteLine("  ⚠ Se reemplazarán TODOS los datos actuales por los del respaldo.\n");

        for (var i = 0; i < respaldos.Value.Count; i++)
        {
            var r = respaldos.Value[i];
            Console.WriteLine($"  [{i + 1}] {r.Nombre}  {r.Fecha:yyyy-MM-dd HH:mm}  {r.TamanoBytes / 1024.0:F1} KB");
        }

        var indice = LeerEntero("Número de respaldo (0 = cancelar)", 0, respaldos.Value.Count, 0);
        if (indice == 0)
            return;

        var elegido = respaldos.Value[indice - 1];

        Console.Write($"\n  Escriba RESTAURAR para confirmar la restauración de {elegido.Nombre}: ");
        if (!string.Equals(Console.ReadLine()?.Trim(), "RESTAURAR", StringComparison.Ordinal))
        {
            Aviso("Restauración cancelada.");
            return;
        }

        var resultado = _respaldos.Restaurar(elegido.Ruta, confirmado: true);
        Console.WriteLine($"\n  {(resultado.IsSuccess ? "✓" : "⚠")} {resultado.Message}");

        if (resultado.IsSuccess)
        {
            Console.WriteLine("\n  La aplicación se cerrará para que los cambios tengan efecto.");
            Console.ReadLine();
            Environment.Exit(0);
        }

        Pausa();
    }

    private static void VerConfiguracion()
    {
        LimpiarPantalla();
        var c = _config.ObtenerConfiguracion().Value;
        Console.WriteLine("=== CONFIGURACIÓN DE LA TIENDA ===");
        Console.WriteLine($"  Nombre:            {c.NombreTienda}");
        Console.WriteLine($"  Giro:              {c.Giro}");
        Console.WriteLine($"  Impuesto:          {c.ImpuestoPredeterminado:F2}%");
        Console.WriteLine($"  Comisión tarjeta:  {c.ComisionTarjeta:F2}%");
        Console.WriteLine($"  Fondo de caja:     ${c.FondoCajaInicial:F2}");
        Console.WriteLine($"  Fiado:             {(c.FiadoActivado ? "Activado" : "Desactivado")}");
        Console.WriteLine($"  Límite de crédito: ${c.LimiCreditoCliente:F2}");
        Console.WriteLine($"  Respaldo cada:     {c.RespaldoIntervaloDias} día(s)");
        Console.WriteLine($"  Versión:           {c.VersionApp}");
        Pausa();
    }

    private static void EditarConfiguracion()
    {
        if (!EsDueño)
        {
            Aviso("Solo el dueño puede cambiar la configuración global.");
            return;
        }

        var c = _config.ObtenerConfiguracion().Value;
        LimpiarPantalla();
        Console.WriteLine("=== EDITAR CONFIGURACIÓN ===");

        c.NombreTienda = LeerTextoConDefecto("Nombre de la tienda", c.NombreTienda);
        c.Giro = LeerTextoConDefecto("Giro", c.Giro);
        c.ImpuestoPredeterminado = LeerDecimal("Impuesto predeterminado (%)", 0, c.ImpuestoPredeterminado);
        c.ComisionTarjeta = LeerDecimal("Comisión de tarjeta (%)", 0, c.ComisionTarjeta);
        c.FondoCajaInicial = LeerDecimal("Fondo de caja inicial ($)", 0, c.FondoCajaInicial);
        c.FiadoActivado = LeerSiNo("¿Activar fiado?", c.FiadoActivado);
        c.LimiCreditoCliente = c.FiadoActivado
            ? LeerDecimal("Límite de crédito por cliente (0 = sin límite)", 0, c.LimiCreditoCliente)
            : 0;
        c.RespaldoIntervaloDias = LeerEntero("Días entre respaldos (0 = deshabilitado)", 0, 365, c.RespaldoIntervaloDias);

        var resultado = _config.ActualizarConfiguracion(c);
        Aviso(resultado.Message, resultado.IsSuccess);
        _configTienda = _config.ObtenerConfiguracion().Value;
    }

    private static void MenuUsuarios()
    {
        if (!EsDueño)
        {
            Aviso("Solo el dueño puede administrar usuarios.");
            return;
        }

        while (true)
        {
            LimpiarPantalla();
            Console.WriteLine("=== USUARIOS ===");
            var usuarios = _config.ObtenerUsuarios().Value ?? new List<UsuarioSistema>();
            foreach (var u in usuarios)
                Console.WriteLine($"  [{u.Id}] {u.Nombre,-20} {u.Rol}");

            Console.WriteLine("\n  [1] Agregar usuario");
            Console.WriteLine("  [0] Regresar");
            Console.Write("  Opción: ");

            switch (Console.ReadLine())
            {
                case "1": AgregarUsuario(); break;
                case "0": return;
                case null: return;
                default: Aviso("Opción no válida."); break;
            }
        }
    }

    private static void AgregarUsuario()
    {
        LimpiarPantalla();
        Console.WriteLine("=== AGREGAR USUARIO ===");
        var nombre = LeerTexto("Nombre");
        var pin = LeerPin("PIN (4 a 6 dígitos)");
        var rol = LeerEntero("Rol (0 = Dueño, 1 = Encargado, 2 = Empleado)", 0, 2, 2);

        var resultado = _config.AgregarUsuario(nombre, pin, (RolUsuario)rol);
        Aviso(resultado.Message, resultado.IsSuccess);
    }

    // ---------------- Utilidades de consola ----------------

    private static void MostrarModuloPendiente(string modulo, string detalle)
    {
        LimpiarPantalla();
        Console.WriteLine($"=== {modulo.ToUpperInvariant()} ===");
        Console.WriteLine("\n  Módulo pendiente de implementación.");
        Console.WriteLine($"  {detalle}");
        Console.WriteLine("\n  Consulte docs/roadmap.md para ver el avance del proyecto.");
        Pausa();
    }

    private static void LimpiarPantalla()
    {
        try { Console.Clear(); }
        catch (IOException) { }
    }

    private static void Pausa()
    {
        Console.WriteLine("\n  Presione Enter para continuar...");
        Console.ReadLine();
    }

    private static void Aviso(string mensaje, bool exito = false)
    {
        Console.WriteLine($"\n  {(exito ? "✓" : "⚠")} {mensaje}");
        Pausa();
    }

    private static string LeerTexto(string etiqueta)
    {
        while (true)
        {
            Console.Write($"  {etiqueta}: ");
            var valor = Console.ReadLine();
            if (valor == null) throw new OperationCanceledException("Entrada terminada");
            if (!string.IsNullOrWhiteSpace(valor)) return valor.Trim();
            Console.WriteLine("  ⚠ Este campo es obligatorio.");
        }
    }

    private static string LeerTextoConDefecto(string etiqueta, string porDefecto)
    {
        Console.Write($"  {etiqueta} [{porDefecto}]: ");
        var valor = Console.ReadLine();
        if (valor == null) throw new OperationCanceledException("Entrada terminada");
        return string.IsNullOrWhiteSpace(valor) ? porDefecto : valor.Trim();
    }

    private static decimal LeerDecimal(string etiqueta, decimal minimo, decimal porDefecto)
    {
        while (true)
        {
            Console.Write($"  {etiqueta} [{porDefecto:F2}]: ");
            var valor = Console.ReadLine();
            if (valor == null) throw new OperationCanceledException("Entrada terminada");
            if (string.IsNullOrWhiteSpace(valor)) return porDefecto;

            if (decimal.TryParse(valor, out var numero) && numero >= minimo)
                return numero;

            Console.WriteLine($"  ⚠ Escriba un número mayor o igual a {minimo:F2}.");
        }
    }

    private static int LeerEntero(string etiqueta, int minimo, int maximo, int porDefecto)
    {
        while (true)
        {
            Console.Write($"  {etiqueta} [{porDefecto}]: ");
            var valor = Console.ReadLine();
            if (valor == null) throw new OperationCanceledException("Entrada terminada");
            if (string.IsNullOrWhiteSpace(valor)) return porDefecto;

            if (int.TryParse(valor, out var numero) && numero >= minimo && numero <= maximo)
                return numero;

            Console.WriteLine($"  ⚠ Escriba un número entre {minimo} y {maximo}.");
        }
    }

    private static bool LeerSiNo(string etiqueta, bool porDefecto)
    {
        Console.Write($"  {etiqueta} (s/n) [{(porDefecto ? "s" : "n")}]: ");
        var valor = Console.ReadLine();
        if (valor == null) throw new OperationCanceledException("Entrada terminada");
        if (string.IsNullOrWhiteSpace(valor)) return porDefecto;
        return valor.Trim().StartsWith("s", StringComparison.OrdinalIgnoreCase);
    }

    private static string LeerPin(string etiqueta)
    {
        while (true)
        {
            Console.Write($"  {etiqueta}: ");
            var pin = Console.ReadLine();
            if (pin == null) throw new OperationCanceledException("Entrada terminada");
            if (ConfiguracionService.PinValido(pin)) return pin;
            Console.WriteLine("  ⚠ El PIN debe tener entre 4 y 6 dígitos.");
        }
    }
}
