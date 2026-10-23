// Punto de entrada de la aplicación (interfaz de consola provisional)
using System.Globalization;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Presentation;

internal static class Program
{
    private static DatabaseTools _db;
    private static ConfiguracionService _config;
    private static InventoryService _inventario;
    private static VentaService _ventas;
    private static CorteCajaService _cortes;
    private static ReporteUtilidadService _reportes;
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
            _ventas = new VentaService(_db);
            _cortes = new CorteCajaService(_db);
            _reportes = new ReporteUtilidadService(_db);
            _respaldos = new RespaldoService(_db);

            if (_db.ContarUsuarios() == 0 && !EjecutarAsistenteInicial())
                return 1;

            if (!IniciarSesion())
                return 0;

            RevisarRespaldoAutomatico(alCerrar: false);
            BuclePrincipal();
            RevisarRespaldoAutomatico(alCerrar: true);
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
        var fondo = LeerDinero("Fondo de caja inicial ($)", 0, ConfiguracionPorDefecto.FondoCajaInicialPredeterminado);
        var fiado = LeerSiNo("¿Activar fiado a clientes?", false);
        var limite = fiado ? LeerDinero("Límite de crédito por cliente (0 = sin límite)", 0, 0) : 0;
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
                SesionActual.Iniciar(_usuarioActual);
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
            Console.WriteLine("  [8] Corte de caja");
            Console.WriteLine("  [9] Configuración");
            Console.WriteLine("  [0] Salir");
            Console.WriteLine("==============================================");
            Console.Write("  Opción: ");

            switch (Console.ReadLine())
            {
                case "1": MenuInventario(); break;
                case "2": MenuVentas(); break;
                case "3": MenuMermas(); break;
                case "4": MenuGastos(); break;
                case "5": MenuFiado(); break;
                case "6": MenuProveedores(); break;
                case "7": MenuReportes(); break;
                case "8": MenuCorteCaja(); break;
                case "9": MenuConfiguracion(); break;
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
                Console.WriteLine($"  {p.Id,-5} {p.CodigoBarras,-17} {p.Nombre,-31} {p.Stock,-8} ${Dinero.Formatear(p.Precio),8}");
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
        Console.WriteLine($"  Costo:     ${Dinero.Formatear(producto.Costo)}");
        Console.WriteLine($"  Precio:    ${Dinero.Formatear(producto.Precio)}");
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
            Costo = LeerDinero("Costo ($)", 0, 0),
            Precio = LeerDinero("Precio de venta ($)", 0, 0),
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
        producto.Costo = LeerDinero("Costo ($)", 0, producto.Costo);
        producto.Precio = LeerDinero("Precio de venta ($)", 0, producto.Precio);
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

    // ---------------- Ventas ----------------

    private static void MenuVentas()
    {
        while (true)
        {
            LimpiarPantalla();
            Console.WriteLine("=== VENTAS ===");
            Console.WriteLine("  [1] Nueva venta");
            Console.WriteLine("  [2] Ventas del día");
            Console.WriteLine("  [0] Regresar");
            Console.Write("\n  Opción: ");

            switch (Console.ReadLine())
            {
                case "1": NuevaVenta(); break;
                case "2": VentasDelDia(); break;
                case "0": return;
                case null: return;
                default: Aviso("Opción no válida."); break;
            }
        }
    }

    private static void NuevaVenta()
    {
        var carrito = new List<(Producto Producto, int Cantidad)>();

        while (true)
        {
            LimpiarPantalla();
            Console.WriteLine("=== NUEVA VENTA ===");
            MostrarCarrito(carrito);
            Console.Write("\n  Código de barras o nombre (Enter para cobrar, 0 para cancelar): ");
            var entrada = Console.ReadLine();
            if (entrada == null) throw new OperationCanceledException("Entrada terminada");
            entrada = entrada.Trim();
            if (entrada.Length == 0) break;
            if (entrada == "0") return;

            var producto = _inventario.BuscarPorCodigo(entrada);
            if (producto == null)
            {
                var coincidencias = _inventario.BuscarPorNombre(entrada);
                if (coincidencias.Count == 0)
                {
                    Aviso($"No se encontró \"{entrada}\".");
                    continue;
                }

                producto = ElegirProducto(coincidencias);
                if (producto == null) continue;
            }

            var enCarrito = carrito.Where(c => c.Producto.Id == producto.Id).Sum(c => c.Cantidad);
            var disponible = producto.Stock - enCarrito;
            if (disponible <= 0)
            {
                Aviso($"No hay stock disponible de {producto.Nombre} (stock {producto.Stock}).");
                continue;
            }

            var cantidad = LeerEntero($"Cantidad de {producto.Nombre} (disponible {disponible})", 1, disponible, 1);
            var indice = carrito.FindIndex(c => c.Producto.Id == producto.Id);
            if (indice >= 0)
                carrito[indice] = (producto, carrito[indice].Cantidad + cantidad);
            else
                carrito.Add((producto, cantidad));
        }

        if (carrito.Count == 0) return;

        var total = carrito.Sum(c => Dinero.Multiplicar(c.Producto.Precio, c.Cantidad));
        var pago = CobrarVenta(total);
        if (pago == null) return;

        var lineas = carrito
            .Select(c => new LineaVentaSolicitada { ProductoId = c.Producto.Id, Cantidad = c.Cantidad })
            .ToList();

        var resultado = _ventas.RegistrarVenta(lineas, pago, _usuarioActual.Nombre);
        if (resultado.IsError)
        {
            Aviso(resultado.Message);
            return;
        }

        LimpiarPantalla();
        ImprimirTicket(resultado.Value, carrito);
        Pausa();
    }

    private static void MostrarCarrito(List<(Producto Producto, int Cantidad)> carrito)
    {
        if (carrito.Count == 0)
        {
            Console.WriteLine("\n  Carrito vacío.");
            return;
        }

        Console.WriteLine();
        foreach (var (producto, cantidad) in carrito)
        {
            var importe = Dinero.Multiplicar(producto.Precio, cantidad);
            Console.WriteLine($"  {cantidad} x {producto.Nombre,-30} ${Dinero.Formatear(producto.Precio),8} = ${Dinero.Formatear(importe),9}");
        }

        Console.WriteLine($"\n  TOTAL: ${Dinero.Formatear(carrito.Sum(c => Dinero.Multiplicar(c.Producto.Precio, c.Cantidad)))}");
    }

    private static Producto ElegirProducto(List<Producto> productos)
    {
        LimpiarPantalla();
        Console.WriteLine("=== PRODUCTOS ENCONTRADOS ===");
        var maximo = Math.Min(productos.Count, 10);
        for (var i = 0; i < maximo; i++)
        {
            var p = productos[i];
            Console.WriteLine($"  [{i + 1}] {p.Nombre,-32} ${Dinero.Formatear(p.Precio),8}  stock {p.Stock}");
        }

        var indice = LeerEntero("Producto (0 = cancelar)", 0, maximo, 1);
        return indice == 0 ? null : productos[indice - 1];
    }

    private static PagoSolicitado CobrarVenta(long total)
    {
        while (true)
        {
            LimpiarPantalla();
            Console.WriteLine("=== COBRO ===");
            Console.WriteLine($"  Total: ${Dinero.Formatear(total)}\n");
            Console.WriteLine("  [1] Efectivo");
            Console.WriteLine("  [2] Tarjeta");
            Console.WriteLine("  [3] Mixto (efectivo + tarjeta)");
            Console.WriteLine(_configTienda.FiadoActivado
                ? "  [4] Fiado"
                : "  [4] Fiado (desactivado en configuración)");
            Console.WriteLine("  [0] Cancelar");
            Console.Write("\n  Opción: ");

            switch (Console.ReadLine())
            {
                case "1":
                    var recibido = LeerDinero("Efectivo recibido", total, total);
                    Console.WriteLine($"\n  Cambio: ${Dinero.Formatear(recibido - total)}");
                    return new PagoSolicitado { Efectivo = total };
                case "2":
                    Console.WriteLine($"\n  La comisión de tarjeta ({_configTienda.ComisionTarjeta:F2}%) se descuenta de la utilidad.");
                    return new PagoSolicitado { Tarjeta = total };
                case "3":
                    var efectivo = LeerDinero("Parte en efectivo", 0, 0);
                    if (efectivo >= total)
                    {
                        Console.WriteLine($"\n  Cambio: ${Dinero.Formatear(efectivo - total)}");
                        return new PagoSolicitado { Efectivo = total };
                    }
                    return new PagoSolicitado { Efectivo = efectivo, Tarjeta = total - efectivo };
                case "4":
                    if (!_configTienda.FiadoActivado)
                    {
                        Aviso("El fiado está desactivado en la configuración.");
                        continue;
                    }

                    var cliente = ElegirClienteFiado();
                    if (cliente == null) continue;
                    return new PagoSolicitado { Fiado = total, ClienteId = cliente.Id };
                case "0":
                    return null;
                case null:
                    return null;
                default:
                    Aviso("Opción no válida.");
                    break;
            }
        }
    }

    private static ClienteFiado ElegirClienteFiado()
    {
        var clientes = _config.ObtenerTodosLosClientes().Value ?? new List<ClienteFiado>();
        if (clientes.Count == 0)
        {
            Aviso("No hay clientes registrados. Agregue uno en Clientes y fiado.");
            return null;
        }

        LimpiarPantalla();
        Console.WriteLine("=== CLIENTE PARA FIADO ===");
        for (var i = 0; i < clientes.Count; i++)
            Console.WriteLine($"  [{i + 1}] {clientes[i].Nombre,-28} saldo ${Dinero.Formatear(clientes[i].SaldoActual),8}");

        var indice = LeerEntero("Cliente (0 = cancelar)", 0, clientes.Count, 1);
        return indice == 0 ? null : clientes[indice - 1];
    }

    private static void ImprimirTicket(Venta venta, List<(Producto Producto, int Cantidad)> carrito)
    {
        Console.WriteLine($"  === {_configTienda.NombreTienda} ===");
        Console.WriteLine($"  Ticket interno (no fiscal)   Venta #{venta.Id}");
        Console.WriteLine($"  {venta.Fecha:yyyy-MM-dd HH:mm}   Atendió: {venta.Usuario}");
        Console.WriteLine("  " + new string('-', 52));

        foreach (var (producto, cantidad) in carrito)
        {
            Console.WriteLine($"  {cantidad} x {producto.Nombre}");
            Console.WriteLine($"      ${Dinero.Formatear(producto.Precio),8} = ${Dinero.Formatear(Dinero.Multiplicar(producto.Precio, cantidad)),9}");
        }

        Console.WriteLine("  " + new string('-', 52));
        Console.WriteLine($"  TOTAL: ${Dinero.Formatear(venta.Total)}");
        Console.WriteLine($"  Pago: {venta.MetodoPago}");

        if (venta.PagoEfectivo > 0 && venta.PagoTarjeta > 0)
        {
            Console.WriteLine($"    Efectivo: ${Dinero.Formatear(venta.PagoEfectivo)}");
            Console.WriteLine($"    Tarjeta:  ${Dinero.Formatear(venta.PagoTarjeta)}");
        }

        if (venta.PagoFiado > 0)
        {
            var cliente = venta.ClienteId.HasValue
                ? _db.ObtenerClienteFiadoPorId(venta.ClienteId.Value)
                : null;
            Console.WriteLine($"    Fiado a: {cliente?.Nombre ?? "cliente"}");
        }

        if (venta.ComisionTarjeta > 0)
            Console.WriteLine($"  Comisión tarjeta: ${Dinero.Formatear(venta.ComisionTarjeta)} (afecta la utilidad)");

        Console.WriteLine("\n  Gracias por su compra.");
    }

    private static void VentasDelDia()
    {
        LimpiarPantalla();
        var ventas = _ventas.ObtenerVentasDelDia(DateTime.Today);
        Console.WriteLine($"=== VENTAS DE HOY ({DateTime.Today:yyyy-MM-dd}) ===");

        if (ventas.Count == 0)
        {
            Console.WriteLine("  No hay ventas registradas hoy.");
        }
        else
        {
            foreach (var venta in ventas)
                Console.WriteLine($"  #{venta.Id,-5} {venta.Fecha:HH:mm}  ${Dinero.Formatear(venta.Total),9}  {venta.MetodoPago,-8} {venta.Usuario}");

            Console.WriteLine($"\n  Total del día: ${Dinero.Formatear(ventas.Sum(v => v.Total))}");
        }

        Pausa();
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
            Monto = LeerDinero("Monto ($)", 0, 0),
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
                Console.WriteLine($"  {g.Fecha:d}  {g.Concepto,-25} ${Dinero.Formatear(g.Monto),9}  {g.MetodoPago}  ({g.Origen})");
        }
        Pausa();
    }

    private static void ReporteGastos()
    {
        LimpiarPantalla();
        Console.WriteLine("=== RESUMEN DE GASTOS ===");
        Console.WriteLine($"  Total: ${Dinero.Formatear(_config.ObtenerTotalGastos().Value)}\n");
        foreach (var grupo in _config.ObtenerGastosPorConcepto().Value ?? new List<GastoPorConcepto>())
            Console.WriteLine($"  {grupo.Concepto,-30} ${Dinero.Formatear(grupo.Total),9}");
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
            SaldoActual = LeerDinero("Saldo inicial ($)", 0, 0)
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
                Console.WriteLine($"  [{c.Id}] {c.Nombre,-25} ${Dinero.Formatear(c.SaldoActual),9}  {c.Telefono}");
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
            Console.WriteLine($"  [{c.Id}] {c.Nombre,-25} ${Dinero.Formatear(c.SaldoActual),9}");

        var id = LeerEntero("\n  ID del cliente (0 = cancelar)", 0, int.MaxValue, 0);
        if (id == 0) return;

        var cliente = clientes.FirstOrDefault(c => c.Id == id);
        if (cliente == null)
        {
            Aviso("Cliente no encontrado.");
            return;
        }

        var monto = LeerDinero($"Monto del abono (saldo ${Dinero.Formatear(cliente.SaldoActual)})", 1, 0);
        var resultado = _config.RegistrarAbonoCliente(cliente.Id, monto, _usuarioActual.Nombre);
        Aviso(resultado.IsSuccess
            ? $"Abono registrado. Nuevo saldo: ${Dinero.Formatear(resultado.Value.SaldoRestante)}"
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
            SaldoPendiente = LeerDinero("Saldo inicial pendiente ($)", 0, 0)
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
                Console.WriteLine($"  [{p.Id}] {p.Nombre,-25} ${Dinero.Formatear(p.SaldoPendiente),9}  {p.Telefono}");
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
            Console.WriteLine($"  [{p.Id}] {p.Nombre,-25} ${Dinero.Formatear(p.SaldoPendiente),9}");

        var id = LeerEntero("\n  ID del proveedor (0 = cancelar)", 0, int.MaxValue, 0);
        if (id == 0) return;

        var proveedor = conSaldo.FirstOrDefault(p => p.Id == id);
        if (proveedor == null)
        {
            Aviso("Proveedor no encontrado.");
            return;
        }

        var monto = LeerDinero($"Monto del pago (saldo ${Dinero.Formatear(proveedor.SaldoPendiente)})", 1, 0);
        var resultado = _config.RegistrarPagoProveedor(proveedor.Id, monto);
        Aviso(resultado.IsSuccess
            ? $"Pago registrado. Nuevo saldo: ${Dinero.Formatear(resultado.Value.SaldoRestante)}"
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
            Console.WriteLine("  [5] Ventas y utilidad");
            Console.WriteLine("  [0] Regresar");
            Console.Write("\n  Opción: ");

            switch (Console.ReadLine())
            {
                case "1":
                    LimpiarPantalla();
                    Console.WriteLine("=== VALORIZADO DE INVENTARIO ===");
                    Console.WriteLine($"  Valor total al costo: ${Dinero.Formatear(_inventario.ObtenerValorizadoInventario())}");
                    Pausa();
                    break;
                case "2": ListarProductosPaginado(_inventario.ProductosStockBajo()); break;
                case "3": ReporteGastos(); break;
                case "4": ReporteSaldoProveedores(); break;
                case "5": ReporteUtilidad(); break;
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
            Console.WriteLine($"  {p.Nombre,-30} ${Dinero.Formatear(p.SaldoPendiente),9}");
        Console.WriteLine($"\n  Total por pagar: ${Dinero.Formatear(proveedores.Sum(p => p.SaldoPendiente))}");
        Pausa();
    }

    private static void ReporteUtilidad()
    {
        LimpiarPantalla();
        Console.WriteLine("=== VENTAS Y UTILIDAD ===");
        Console.WriteLine("  [1] Hoy");
        Console.WriteLine("  [2] Este mes");
        Console.WriteLine("  [3] Rango de fechas");
        Console.WriteLine("  [0] Regresar");
        Console.Write("\n  Opción: ");

        DateTime desde;
        DateTime hasta;

        switch (Console.ReadLine())
        {
            case "1":
                desde = hasta = DateTime.Today;
                break;
            case "2":
                desde = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                hasta = DateTime.Today;
                break;
            case "3":
                desde = LeerFecha("Desde (dd/mm/aaaa)", DateTime.Today);
                hasta = LeerFecha("Hasta (dd/mm/aaaa)", DateTime.Today);
                break;
            default:
                return;
        }

        var resultado = _reportes.Obtener(desde, hasta);
        if (resultado.IsError)
        {
            Aviso(resultado.Message);
            return;
        }

        var reporte = resultado.Value;
        LimpiarPantalla();
        Console.WriteLine("=== VENTAS Y UTILIDAD ===");
        Console.WriteLine($"  Periodo: {reporte.Desde:dd/MM/yyyy} a {reporte.Hasta:dd/MM/yyyy}");
        Console.WriteLine($"  Ventas: {reporte.NumeroVentas} por ${Dinero.Formatear(reporte.VentasTotal)}");
        Console.WriteLine($"  Costo de ventas: ${Dinero.Formatear(reporte.CostoVentas)}");
        Console.WriteLine($"  Comisión de tarjeta: ${Dinero.Formatear(reporte.ComisionTarjeta)}");
        Console.WriteLine($"  Utilidad bruta: ${Dinero.Formatear(reporte.UtilidadBruta)}");
        Console.WriteLine($"  Mermas: ${Dinero.Formatear(reporte.Mermas)}");
        Console.WriteLine($"  Gastos: ${Dinero.Formatear(reporte.Gastos)}");
        Console.WriteLine($"  Utilidad neta: ${Dinero.Formatear(reporte.UtilidadNeta)}");
        Pausa();
    }

    private static DateTime LeerFecha(string etiqueta, DateTime porDefecto)
    {
        while (true)
        {
            Console.Write($"  {etiqueta} [{porDefecto:dd/MM/yyyy}]: ");
            var valor = Console.ReadLine();
            if (valor == null) throw new OperationCanceledException("Entrada terminada");
            if (string.IsNullOrWhiteSpace(valor)) return porDefecto.Date;

            if (DateTime.TryParseExact(valor.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var fecha))
                return fecha.Date;

            Console.WriteLine("  ⚠ Use el formato dd/mm/aaaa.");
        }
    }

    // ---------------- Corte de caja ----------------

    private static void MenuCorteCaja()
    {
        while (true)
        {
            LimpiarPantalla();
            Console.WriteLine("=== CORTE DE CAJA ===");
            var ultimo = _cortes.ObtenerUltimoCorte().Value;
            Console.WriteLine(ultimo == null
                ? "  Aún no hay cortes registrados.\n"
                : $"  Último corte: #{ultimo.Id}  {ultimo.Hasta:g}  diferencia ${Dinero.Formatear(ultimo.Diferencia)}\n");
            Console.WriteLine("  [1] Resumen y cerrar corte");
            Console.WriteLine("  [2] Registrar ingreso de efectivo");
            Console.WriteLine("  [3] Registrar retiro de efectivo");
            Console.WriteLine("  [4] Historial de cortes");
            Console.WriteLine("  [0] Regresar");
            Console.Write("\n  Opción: ");

            switch (Console.ReadLine())
            {
                case "1": CerrarCorte(); break;
                case "2": RegistrarMovimientoCaja(TipoMovimientoCaja.Ingreso); break;
                case "3": RegistrarMovimientoCaja(TipoMovimientoCaja.Retiro); break;
                case "4": VerCortes(); break;
                case "0": return;
                case null: return;
                default: Aviso("Opción no válida."); break;
            }
        }
    }

    private static void CerrarCorte()
    {
        LimpiarPantalla();
        var preview = _cortes.ObtenerResumenPendiente();
        if (preview.IsError)
        {
            Aviso(preview.Message);
            return;
        }

        var r = preview.Value;
        Console.WriteLine("=== CORTE DE CAJA: PERIODO ABIERTO ===");
        Console.WriteLine($"  Desde: {r.Desde:g}");
        Console.WriteLine($"  Hasta: {r.Hasta:g}\n");
        Console.WriteLine($"  Ventas en efectivo:  ${Dinero.Formatear(r.VentasEfectivo),10}  ({r.NumeroVentasEfectivo})");
        Console.WriteLine($"  Ventas con tarjeta:  ${Dinero.Formatear(r.VentasTarjeta),10}  ({r.NumeroVentasTarjeta})");
        Console.WriteLine($"  Ventas a fiado:      ${Dinero.Formatear(r.VentasFiado),10}  ({r.NumeroVentasFiado})");
        Console.WriteLine($"  Ventas mixtas:       {r.NumeroVentasMixto} transacción(es)");
        Console.WriteLine($"  Total vendido:       ${Dinero.Formatear(r.TotalVentas),10}  ({r.NumeroVentas})");
        Console.WriteLine($"  Abonos en efectivo:  ${Dinero.Formatear(r.AbonosEfectivo),10}");
        Console.WriteLine($"  Ingresos de caja:    ${Dinero.Formatear(r.IngresosCaja),10}");
        Console.WriteLine($"  Retiros de caja:    -${Dinero.Formatear(r.RetirosCaja),10}");
        Console.WriteLine($"  Gastos de caja:     -${Dinero.Formatear(r.GastosCaja),10}\n");

        var fondo = LeerDinero("Fondo inicial del periodo ($)", 0, r.FondoInicial);
        var esperado = r.EfectivoEsperado - r.FondoInicial + fondo;
        Console.WriteLine($"  Efectivo esperado:   ${Dinero.Formatear(esperado),10}");
        var contado = LeerDinero("Efectivo contado en caja ($)", 0, esperado);
        var diferencia = contado - esperado;
        Console.WriteLine($"  Diferencia:          ${Dinero.Formatear(diferencia),10}");

        string observaciones;
        if (diferencia != 0)
        {
            Console.WriteLine(diferencia > 0
                ? "  Hay un sobrante de efectivo. Explique la causa."
                : "  Hay un faltante de efectivo. Explique la causa.");
            observaciones = LeerTexto("Motivo de la diferencia");
        }
        else
        {
            observaciones = LeerTextoConDefecto("Observaciones (opcional)", string.Empty);
        }

        var resultado = _cortes.RegistrarCorte(contado, fondo, _usuarioActual.Nombre, observaciones);
        if (resultado.IsError)
        {
            Aviso(resultado.Message);
            return;
        }

        var c = resultado.Value;
        LimpiarPantalla();
        Console.WriteLine($"=== CORTE #{c.Id} REGISTRADO ===");
        Console.WriteLine($"  Responsable: {c.Usuario}");
        Console.WriteLine($"  Fecha: {c.FechaRegistro:g}");
        Console.WriteLine($"  Efectivo esperado: ${Dinero.Formatear(c.EfectivoEsperado)}");
        Console.WriteLine($"  Efectivo contado:  ${Dinero.Formatear(c.EfectivoContado)}");
        Console.WriteLine($"  Diferencia:        ${Dinero.Formatear(c.Diferencia)}  {EstadoDiferencia(c.Diferencia)}");
        if (!string.IsNullOrWhiteSpace(c.Observaciones))
            Console.WriteLine($"  Observaciones: {c.Observaciones}");
        Console.WriteLine($"\n  El próximo periodo inicia con ${Dinero.Formatear(c.EfectivoContado)} en caja.");
        Pausa();
    }

    private static string EstadoDiferencia(long diferencia) =>
        diferencia == 0 ? "(cuadra)" : diferencia > 0 ? "(sobrante)" : "(faltante)";

    private static void RegistrarMovimientoCaja(string tipo)
    {
        LimpiarPantalla();
        Console.WriteLine(tipo == TipoMovimientoCaja.Ingreso
            ? "=== INGRESO DE EFECTIVO ==="
            : "=== RETIRO DE EFECTIVO ===");

        var monto = LeerDinero("Monto ($)", 0, 0);
        if (monto <= 0)
        {
            Aviso("El monto debe ser mayor a cero.");
            return;
        }

        var motivo = LeerTexto("Motivo");
        var resultado = _cortes.RegistrarMovimiento(tipo, monto, motivo, _usuarioActual.Nombre);
        Aviso(resultado.Message, resultado.IsSuccess);
    }

    private static void VerCortes()
    {
        LimpiarPantalla();
        var cortes = _cortes.ObtenerCortes(20).Value ?? new List<CorteCaja>();
        Console.WriteLine("=== HISTORIAL DE CORTES ===");
        if (cortes.Count == 0)
        {
            Console.WriteLine("  Sin cortes registrados.");
        }
        else
        {
            foreach (var c in cortes)
                Console.WriteLine($"  #{c.Id,-4} {c.Hasta:g}  {c.Usuario,-12} " +
                                  $"esperado ${Dinero.Formatear(c.EfectivoEsperado),8}  contado ${Dinero.Formatear(c.EfectivoContado),8}  " +
                                  $"dif. ${Dinero.Formatear(c.Diferencia),7}");
        }
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

    private static void RevisarRespaldoAutomatico(bool alCerrar)
    {
        var resultado = _respaldos.EjecutarAutomaticoSiCorresponde();

        if (resultado.IsError)
        {
            Console.WriteLine($"\n  ⚠ {resultado.Message}");
            if (!alCerrar) Pausa();
            return;
        }

        if (resultado.Value.Estado != EstadoRespaldoAutomatico.Creado)
            return;

        Console.WriteLine($"\n  ✓ Respaldo automático creado: {Path.GetFileName(resultado.Value.Ruta)}");
        if (!alCerrar) Pausa();
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
        Console.WriteLine($"  Fondo de caja:     ${Dinero.Formatear(c.FondoCajaInicial)}");
        Console.WriteLine($"  Fiado:             {(c.FiadoActivado ? "Activado" : "Desactivado")}");
        Console.WriteLine($"  Límite de crédito: ${Dinero.Formatear(c.LimiCreditoCliente)}");
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
        c.FondoCajaInicial = LeerDinero("Fondo de caja inicial ($)", 0, c.FondoCajaInicial);
        c.FiadoActivado = LeerSiNo("¿Activar fiado?", c.FiadoActivado);
        c.LimiCreditoCliente = c.FiadoActivado
            ? LeerDinero("Límite de crédito por cliente (0 = sin límite)", 0, c.LimiCreditoCliente)
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

    /// <summary>
    /// Lee un importe en pesos y lo devuelve en centavos. El mínimo también va en centavos.
    /// </summary>
    private static long LeerDinero(string etiqueta, long minimoCentavos, long porDefectoCentavos)
    {
        while (true)
        {
            Console.Write($"  {etiqueta} [{Dinero.Formatear(porDefectoCentavos)}]: ");
            var valor = Console.ReadLine();
            if (valor == null) throw new OperationCanceledException("Entrada terminada");
            if (string.IsNullOrWhiteSpace(valor)) return porDefectoCentavos;

            if (decimal.TryParse(valor, out var pesos))
            {
                var centavos = Dinero.DePesos(pesos);
                if (centavos >= minimoCentavos) return centavos;
            }

            Console.WriteLine($"  ⚠ Escriba un importe mayor o igual a ${Dinero.Formatear(minimoCentavos)}.");
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
