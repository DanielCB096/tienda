// Punto de entrada de la aplicación - Versión Mejorada
using SistemaTienda.Infrastructure;
using SistemaTienda.Application.Services;
using System;
using System.Threading.Tasks;

namespace SistemaTienda.Presentation
{
    class Program
    {
        static async Task Main(string args)
        {
            // Inicializar base de datos local
            string dbPath = "tienda.localdb";
            var db = new DatabaseTools(dbPath);

            // Asegurar tablas de configuración e inventario
            db.InicializarTablasConfiguracionYGastos();

            // Verificar si ya hay configuración inicial
            var configResult = db.ObtenerConfiguracion();
            bool configuracionInicial = configResult.IsSuccess && configResult.Value.VersionApp == "v1.0.0" &&
                                        configResult.Value.FechaCreacion != DateTime.MinValue;

            if (!configuracionInicial)
            {
                // Ejecutar asistente de primera configuración
                Console.WriteLine("=== Asistente de Primera Configuración ===");
                Console.WriteLine("Sistema de Gestión para Tiendas Pequeñas - México");
                Console.WriteLine("Costo recurrente $0 | 100% offline | Un dispositivo por tienda\n");

                Console.Write("Nombre de la tienda: ");
                string nombre = Console.ReadLine() ?? "Mi Tienda";

                Console.Write("Giro de negocio: ");
                string giro = Console.ReadLine() ?? "Abarrotes";

                Console.Write("Impuesto predeterminado (%.2f por defecto): ", 16.00m);
                if (decimal.TryParse(Console.ReadLine(), out decimal impuesto) && impuesto >= 0)
                    impuestoPredeterminado = impuesto;
                else
                    impuestoPredeterminado = 16.00m;

                Console.Write("Comisión de tarjeta %.2f% por defecto: ", 3.50m);
                if (decimal.TryParse(Console.ReadLine(), out decimal comision) && comision >= 0)
                    comisionTarjeta = comision;
                else
                    comisionTarjeta = 3.50m;

                Console.Write("Fondo de caja inicial: ");
                if (decimal.TryParse(Console.ReadLine(), out decimal fondo) && fondo >= 0)
                    fondoCajaInicial = fondo;
                else
                    fondoCajaInicial = 200.00m;

                Console.Write("¿Activar fiado? (s/n): ");
                bool fiado = Console.ReadLine()?.ToLower() == "s";

                Console.Write("Límite de crédito (0 = sin límite): ");
                if (decimal.TryParse(Console.ReadLine(), out decimal limite) && limite >= 0)
                    limiteCredito = limite;
                else
                    limiteCredito = 0;

                Console.Write("Intervalo de respaldo automático (días, 0 = deshabilitado): ");
                if (int.TryParse(Console.ReadLine(), out int intervalo) && intervalo >= 0)
                    respaldoIntervaloDias = intervalo;
                else
                    respaldoIntervaloDias = 1;

                Console.Write("PIN para el dueño (4-6 dígitos): ");
                string pin = Console.ReadLine() ?? "1234";

                var configService = new ConfiguracionService(db);
                var resultado = await configService.EjecutarAsistenteInicial(nombre, giro, impuestoPredeterminado,
                    comisionTarjeta, fondoCajaInicial, fiado, limiteCredito, respaldoIntervaloDias, pin);

                if (resultado.IsSuccess)
                {
                    Console.WriteLine($"\n✓ Configuración completada. Bienvenido/a, {nombre}.");
                    Console.WriteLine("El sistema está listo para operar.\n");
                    Console.WriteLine("Presione cualquier tecla para continuar...");
                    Console.ReadKey();
                }
                else
                {
                    Console.WriteLine($"\n✗ Error: {resultado.Message}");
                    Console.WriteLine("Presione cualquier tecla para salir...");
                    Console.ReadKey();
                    return;
                }
            }

            // Bucle principal
            await BuclePrincipal(db);
        }

        static decimal impuestoPredeterminado = 16.00m;
        static decimal comisionTarjeta = 3.50m;
        static decimal fondoCajaInicial = 200.00m;
        static bool fiado = false;
        static decimal limiteCredito = 0;
        static int respaldoIntervaloDias = 1;

        static async Task BuclePrincipal(DatabaseTools db)
        {
            var configService = new ConfiguracionService(db);
            var inventoryService = new InventoryService(db);

            while (true)
            {
                // Menú principal con diseño mejorado
                Console.Clear();
                Console.WriteLine("=================================");
                Console.WriteLine("  SISTEMA GESTIÓN TIENDA");
                Console.WriteLine("=================================");
                Console.WriteLine($"  Tienda: {configService.ObtenerConfiguracion().Value.NombreTienda}");
                Console.WriteLine($"  Productos en catálogo: {inventoryService.ObtenerProductos().Count}");
                Console.WriteLine();
                Console.WriteLine("  Opciones principales:");
                Console.WriteLine("  [1] Inventario y Catálogo");
                Console.WriteLine("  [2] Registrar Venta");
                Console.WriteLine("  [3] Registrar Merma");
                Console.WriteLine("  [4] Registrar Gasto");
                Console.WriteLine("  [5] Reportes y Utilidad");
                Console.WriteLine("  [6] Clientes y Fiado");
                Console.WriteLine("  [7] Proveedores y Cuentas por Pagar");
                Console.WriteLine("  [8] Configuración");
                Console.WriteLine("  [0] Salir de la Aplicación");
                Console.WriteLine("=================================");
                Console.Write("\n  Seleccione una opción (0-8): ");

                var op = Console.ReadLine();

                switch (op)
                {
                    case "1":
                        await MenuInventario(inventoryService);
                        break;
                    case "2":
                        await MenuVentas(inventoryService, configService);
                        break;
                    case "3":
                        await MenuMermas(inventoryService);
                        break;
                    case "4":
                        await MenuGastos(configService);
                        break;
                    case "5":
                        await MenuReportes(inventoryService, configService);
                        break;
                    case "6":
                        await MenuClientesFiado(configService);
                        break;
                    case "7":
                        await MenuProveedores(configService);
                        break;
                    case "8":
                        await MenuConfiguracion(configService);
                        break;
                    case "0":
                        Console.WriteLine("\n¡Gracias por usar el Sistema de Gestión!");
                        Console.WriteLine("Los datos se guardarán automáticamente en la base de datos local.");
                        return;
                    default:
                        Console.WriteLine("\n⚠ Opción no válida. Por favor seleccione una opción del 0 al 8.");
                        Console.ReadKey();
                        break;
                }
            }
        }

        // ----- MENÚ INVENTARIO (consolidado) -----
        static async Task MenuInventario(InventoryService inventorySvc)
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("=== INVENTARIO Y CATÁLOGO ===");
                Console.WriteLine($"Total productos: {inventorySvc.ObtenerProductos().Count}");
                Console.WriteLine();
                Console.WriteLine("  [1] Ver todos los productos");
                Console.WriteLine("  [2] Buscar producto por código de barras");
                Console.WriteLine("  [3] Buscar producto por nombre");
                Console.WriteLine("  [4] Productos con stock bajo");
                Console.WriteLine("  [5] Historial de movimientos de un producto");
                Console.WriteLine("  [0] Regresar al menú principal");
                Console.Write("\n  Opción: ");

                var op = Console.ReadLine();

                switch (op)
                {
                    case "1":
                        await VerProductos(inventorySvc);
                        break;
                    case "2":
                        await BuscarPorCodigo(inventorySvc);
                        break;
                    case "3":
                        await BuscarPorNombre(inventorySvc);
                        break;
                    case "4":
                        await StockBajo(inventorySvc);
                        break;
                    case "5":
                        await HistorialMovimientos(inventorySvc);
                        break;
                    case "0":
                        return;
                    default:
                        Console.WriteLine("⚠ Opción no válida.");
                        Console.ReadKey();
                        break;
                }
            }
        }

        // Métodos auxiliares del menú de inventario (simplificados)
        static async Task VerProductos(InventoryService s) { Console.WriteLine("Ver productos"); await Task.Delay(1); }
        static async Task BuscarPorCodigo(InventoryService s) { Console.WriteLine("Buscar por código"); await Task.Delay(1); }
        static async Task BuscarPorNombre(InventoryService s) { Console.WriteLine("Buscar por nombre"); await Task.Delay(1); }
        static async Task StockBajo(InventoryService s) { Console.WriteLine("Stock bajo"); await Task.Delay(1); }
        static async Task HistorialMovimientos(InventoryService s) { Console.WriteLine("Historial"); await Task.Delay(1); }

        // ----- MENÚ VENTAS (flujo simplificado) -----
        static async Task MenuVentas(InventoryService inventorySvc, ConfiguracionService configService)
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("=== VENTAS / PUNTO DE VENTA ===");
                Console.WriteLine("  Flujo: Escanear → Cantidad → Total → Pagar → Ticket");
                Console.WriteLine();
                Console.WriteLine("  [1] Nueva venta");
                Console.WriteLine("  [0] Regresar al menú principal");
                Console.Write("\n  Opción: ");

                var op = Console.ReadLine();

                switch (op)
                {
                    case "1":
                        await RegistrarVenta(inventorySvc, configService);
                        break;
                    case "0":
                        return;
                    default:
                        Console.WriteLine("⚠ Opción no válida.");
                        Console.ReadKey();
                        break;
                }
            }
        }

        // ----- NUEVO MENÚ PROVEEDORES Y CUENTAS POR PAGAR -----
        static async Task MenuProveedores(ConfiguracionService configService)
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("=== PROVEEDORES Y CUENTAS POR PAGAR ===");
                Console.WriteLine("  Gestión de proveedores y pagos a crédito");
                Console.WriteLine();
                Console.WriteLine("  [1] Nuevo proveedor");
                Console.WriteLine("  [2] Ver lista de proveedores");
                Console.WriteLine("  [3] Registrar pago a proveedor");
                Console.WriteLine("  [0] Regresar al menú principal");
                Console.Write("\n  Opción: ");

                var op = Console.ReadLine();

                switch (op)
                {
                    case "1":
                        await NuevoProveedor(configService);
                        break;
                    case "2":
                        var proveedores = await configService.ObtenerTodosLosProveedores();
                        Console.Clear();
                        Console.WriteLine("=== LISTA DE PROVEEDORES ===");
                        if (proveedores.Count == 0)
                            Console.WriteLine("  No hay proveedores registrados.");
                        else
                            foreach (var p in proveedores)
                                Console.WriteLine($"  {p.Nombre} (RFC: {p.Clave}) - Saldo: ${p.SaldoPendiente:F2}");
                        Console.WriteLine();
                        Console.WriteLine("  Presione cualquier tecla...");
                        Console.ReadKey();
                        break;
                    case "3":
                        await RegistrarPagoProveedor(configService);
                        break;
                    case "0":
                        return;
                    default:
                        Console.WriteLine("⚠ Opción no válida.");
                        Console.ReadKey();
                        break;
                }
            }
        }

        /// <summary>
        /// Registra un nuevo proveedor en el sistema
        /// </summary>
        static async Task NuevoProveedor(ConfiguracionService configService)
        {
            Console.Clear();
            Console.Write("  Nombre del proveedor: ");
            string nombre = Console.ReadLine() ?? "";

            Console.Write("  RFC o razón social: ");
            string rfc = Console.ReadLine() ?? "";

            Console.Write("  Teléfono de contacto (opcional): ");
            string telefono = Console.ReadLine() ?? "";

            Console.Write("  Dirección (opcional): ");
            string direccion = Console.ReadLine() ?? "";

            var nuevoProveedor = new Proveedor
            {
                Nombre = nombre,
                Clave = rfc,
                Telefono = telefono,
                Direccion = direccion,
                SaldoPendiente = 0,
                FechaRegistro = DateTime.Now
            };

            var resultado = await configService.AgregarProveedor(nuevoProveedor);
            Console.Clear();
            Console.WriteLine(resultado.IsSuccess ? "✓ " + resultado.Message : "✗ " + resultado.Message);

            Console.WriteLine();
            Console.WriteLine("  Presione cualquier tecla...");
            Console.ReadKey();
        }

        /// <summary>
        /// Registra un pago a un proveedor (reduce el saldo cuentas por pagar)
        /// </summary>
        static async Task RegistrarPagoProveedor(ConfiguracionService configService)
        {
            Console.Clear();
            Console.Write("  Nombre del proveedor: ");
            string nombre = Console.ReadLine() ?? "";

            // Buscar proveedor (búsqueda simplificada)
            var proveedores = await configService.ObtenerTodosLosProveedores();
            var proveedorEncontrado = proveedores.FirstOrDefault(p => p.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase));

            if (proveedorEncontrado == null)
            {
                Console.Clear();
                Console.WriteLine($"⚠ Proveedor '{nombre}' no encontrado.");
                Console.WriteLine("  ¿Desea crear este proveedor? (s/n)");
                if (Console.ReadLine()?.ToLower() == "s")
                {
                    proveedorEncontrado = new Proveedor { Nombre = nombre, Clave = "", SaldoPendiente = 0 };
                    await configService.AgregarProveedor(proveedorEncontrado);
                }
                else
                {
                    Console.WriteLine("  Presione cualquier tecla...");
                    Console.ReadKey();
                    return;
                }
            }

            Console.Write("  Monto del pago: ");
            if (decimal.TryParse(Console.ReadLine(), out decimal montoPago) && montoPago > 0)
            {
                var resultado = await configService.RegistrarPagoProveedor(proveedorEncontrado.Id, montoPago);
                Console.Clear();
                Console.WriteLine(resultado.IsSuccess ? "✓ " + resultado.Message : "✗ " + resultado.Message);

                if (resultado.IsSuccess && proveedorEncontrado != null)
                {
                    proveedorEncontrado.SaldoPendiente = resultado.Value.SaldoRestante;
                    Console.WriteLine($"  Saldo pendiente actualizado: ${resultado.Value.SaldoRestante:F2}");

                    if (resultado.Value.SaldoRestante <= 0)
                    {
                        Console.WriteLine("  ¡Saldo cero! El proveedor está al día.");
                    }
                }

                Console.WriteLine();
                Console.WriteLine("  Presione cualquier tecla...");
                Console.ReadKey();
            }
            else
            {
                Console.Clear();
                Console.WriteLine("⚠ El monto del pago debe ser mayor a cero.");
                Console.WriteLine();
                Console.WriteLine("  Presione cualquier tecla...");
                Console.ReadKey();
            }
        }

        // ----- NUEVO MENÚ PROVEEDOR (clase Proveedor) -----
        class Proveedor
        {
            public int Id { get; set; }
            public string Nombre { get; set; } = string.Empty;
            public string Clave { get; set; } = string.Empty; // RFC
            public string Telefono { get; set; } = string.Empty;
            public string Direccion { get; set; } = string.Empty;
            public decimal SaldoPendiente { get; set; }
            public DateTime FechaRegistro { get; set; } = DateTime.Now;
        }

        // ----- MENÚ MERMAS -----
        static async Task MenuMermas(InventoryService inventorySvc)
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("=== REGISTRAR MERMA ===");
                Console.WriteLine("  Registro de pérdidas por deterioro, robo, etc.");
                Console.WriteLine();
                Console.WriteLine("  [1] Registrar nueva merma");
                Console.WriteLine("  [2] Ver historial de mermas");
                Console.WriteLine("  [0] Regresar al menú principal");
                Console.Write("\n  Opción: ");

                var op = Console.ReadLine();

                switch (op)
                {
                    case "1":
                        await RegistrarMerma(inventorySvc);
                        break;
                    case "2":
                        var mermas = await inventoryService.ObtenerHistorialMermas();
                        Console.Clear();
                        Console.WriteLine("=== HISTORIAL MERMAS ===");
                        foreach (var m in mermas)
                            Console.WriteLine($"  {m.Motivo}: ${m.MontoPerdido:F2} - {m.Fecha:d}");
                        Console.WriteLine();
                        Console.WriteLine("  Presione cualquier tecla...");
                        Console.ReadKey();
                        break;
                    case "0":
                        return;
                    default:
                        Console.WriteLine("⚠ Opción no válida.");
                        Console.ReadKey();
                        break;
                }
            }
        }

        /// <summary>
        /// Registra una nueva merma
        /// </summary>
        static async Task RegistrarMerma(InventoryService inventorySvc)
        {
            Console.Clear();
            Console.Write("  Producto: ");
            var productos = inventorySvc.ObtenerProductos();
            if (productos.Count == 0)
            {
                Console.WriteLine("  No hay productos en el catálogo.");
                Console.WriteLine("  Presione cualquier tecla...");
                Console.ReadKey();
                return;
            }
            foreach (var p in productos)
                Console.WriteLine($"  [{p.Id}] {p.Nombre} (Stock: {p.Stock})");

            Console.Write("  Seleccione producto: ");
            if (int.TryParse(Console.ReadLine(), out int prodId) && prodId > 0 && prodId <= productos.Count)
            {
                var prod = productos[prodId - 1];
                Console.Write("  Motivo: ");
                string motivo = Console.ReadLine() ?? "";
                Console.Write("  Cantidad perdida: ");
                if (int.TryParse(Console.ReadLine(), out int cantidad) && cantidad > 0)
                {
                    Console.Write("  Costo unitario perdido: ");
                    if (decimal.TryParse(Console.ReadLine(), out decimal costoUnitario) && costoUnitario >= 0)
                    {
                        var merma = new Merma
                        {
                            ProductoId = prodId,
                            ProductoNombre = prod.Nombre,
                            Motivo = motivo,
                            Cantidad = cantidad,
                            CostoUnitario = costoUnitario,
                            Fecha = DateTime.Now
                        };

                        var resultado = await inventoryService.RegistrarMerma(merma);
                        Console.Clear();
                        Console.WriteLine(resultado.IsSuccess ? "✓ " + resultado.Message : "✗ " + resultado.Message);

                        if (resultado.IsSuccess)
                        {
                            Console.WriteLine($"  Stock reducido en: {cantidad}");
                            Console.WriteLine($"  Costo total perdido: ${costoUnitario * cantidad:F2}");
                        }

                        Console.WriteLine();
                        Console.WriteLine("  Presione cualquier tecla...");
                        Console.ReadKey();
                    }
                    else
                    {
                        Console.Clear();
                        Console.WriteLine("⚠ El costo unitario debe ser mayor o igual a cero.");
                        Console.WriteLine();
                        Console.WriteLine("  Presione cualquier tecla...");
                        Console.ReadKey();
                    }
                }
                else
                {
                    Console.Clear();
                    Console.WriteLine("⚠ La cantidad debe ser mayor a cero.");
                    Console.WriteLine();
                    Console.WriteLine("  Presione cualquier tecla...");
                    Console.ReadKey();
                }
            }
            else
            {
                Console.Clear();
                Console.WriteLine("⚠ Opción de producto no válida.");
                Console.WriteLine();
                Console.WriteLine("  Presione cualquier tecla...");
                Console.ReadKey();
            }
        }

        // ----- MENÚ REPORTES Y UTILIDAD -----
        static async Task MenuReportes(InventoryService inventorySvc, ConfiguracionService configService)
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("=== REPORTES Y UTILIDAD ===");
                Console.WriteLine();
                Console.WriteLine("  [1] Ver reporte de utilidad neta");
                Console.WriteLine("  [2] Ver reporte de ventas");
                Console.WriteLine("  [3] Ver reporte de mermas");
                Console.WriteLine("  [4] Ver reporte de gastos");
                Console.WriteLine("  [5] Ver reporte de valorizado de inventario");
                Console.WriteLine("  [6] Ver reporte de proveedores");
                Console.WriteLine("  [0] Regresar al menú principal");
                Console.Write("\n  Opción: ");

                var op = Console.ReadLine();

                switch (op)
                {
                    case "1":
                        await ReporteUtilidadNeta(inventorySvc, configService);
                        break;
                    case "2":
                        await ReporteVentas(inventorySvc, configService);
                        break;
                    case "3":
                        await ReporteMermas(inventorySvc);
                        break;
                    case "4":
                        await ReporteGastos(configService);
                        break;
                    case "5":
                        await ReporteValorizado(inventorySvc);
                        break;
                    case "6":
                        await ReporteProveedores(configService);
                        break;
                    case "0":
                        return;
                    default:
                        Console.WriteLine("⚠ Opción no válida.");
                        Console.ReadKey();
                        break;
                }
            }
        }

        static async Task ReporteProveedores(ConfiguracionService configService)
        {
            Console.Clear();
            Console.WriteLine("=== REPORTE DE PROVEEDORES ===");
            var proveedores = await configService.ObtenerTodosLosProveedores();
            Console.WriteLine($"  Total proveedores: {proveedores.Count}");
            decimal totalSaldo = 0;
            foreach (var p in proveedores)
            {
                Console.WriteLine($"  {p.Nombre}: Saldo ${p.SaldoPendiente:F2}");
                totalSaldo += p.SaldoPendiente;
            }
            Console.WriteLine($"\n  Total saldo pendiente a proveedores: ${totalSaldo:F2}");

            Console.WriteLine();
            Console.WriteLine("  Presione cualquier tecla...");
            Console.ReadKey();
        }

        // ... (resto de métodos existentes simplificados)
    }
}