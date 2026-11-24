// Genera bases de datos de prueba con datos ficticios y realistas.
// No toca la app real salvo que se pida con --forzar.
using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Semilla;

public static class GeneradorSemilla
{
    private const long FondoCaja = 50_000;

    private sealed record ProductoSemilla(
        string Codigo, string Nombre, string Categoria,
        long Costo, long Precio, int Stock, int Minimo = 5, bool Activo = true);

    private static readonly ProductoSemilla[] Catalogo =
    {
        new("7501000000001", "Arroz 1 kg", "Abarrotes", 2_100, 3_200, 40),
        new("7501000000002", "Frijol negro 1 kg", "Abarrotes", 2_400, 3_600, 35),
        new("7501000000003", "Aceite vegetal 900 ml", "Abarrotes", 3_200, 4_500, 30),
        new("7501000000004", "Azúcar 1 kg", "Abarrotes", 2_000, 2_900, 45),
        new("7501000000005", "Sal 1 kg", "Abarrotes", 800, 1_400, 50),
        new("7501000000006", "Sopa de pasta 200 g", "Abarrotes", 900, 1_550, 60),
        new("7501000000007", "Harina de maíz 1 kg", "Abarrotes", 1_700, 2_500, 40),
        new("7501000000008", "Atún en agua 140 g", "Abarrotes", 1_500, 2_300, 25),
        new("7501000000009", "Sardina en tomate 425 g", "Abarrotes", 1_400, 2_200, 20),
        new("7501000000010", "Café soluble 100 g", "Abarrotes", 4_500, 6_500, 18),
        new("7501000000011", "Mayonesa 390 g", "Abarrotes", 2_800, 4_200, 22),
        new("7501000000012", "Mostaza 320 g", "Abarrotes", 1_600, 2_600, 15),
        new("7501000000013", "Salsa catsup 320 g", "Abarrotes", 1_800, 2_800, 20),
        new("7501000000014", "Chocolate en polvo 400 g", "Abarrotes", 3_800, 5_400, 16),
        new("7501000000015", "Gelatina 120 g", "Abarrotes", 700, 1_300, 30),
        new("7501000000016", "Sopa instantánea", "Abarrotes", 1_100, 1_800, 40),
        new("7501000000017", "Chicles de menta", "Abarrotes", 300, 700, 80),
        new("7501000000018", "Refresco cola 600 ml", "Bebidas", 1_100, 1_800, 60),
        new("7501000000019", "Refresco toronja 2 L", "Bebidas", 2_200, 3_400, 30),
        new("7501000000020", "Agua natural 1.5 L", "Bebidas", 700, 1_300, 70),
        new("7501000000021", "Jugo de naranja 1 L", "Bebidas", 1_800, 2_900, 20),
        new("7501000000022", "Bebida isotónica 600 ml", "Bebidas", 1_500, 2_400, 24),
        new("7501000000023", "Leche entera 1 L", "Lácteos", 1_900, 2_800, 36),
        new("7501000000024", "Yogur natural 1 kg", "Lácteos", 2_900, 4_200, 12),
        new("7501000000025", "Queso panela 400 g", "Lácteos", 5_200, 7_500, 10),
        new("7501000000026", "Crema 200 ml", "Lácteos", 1_600, 2_500, 18),
        new("7501000000027", "Mantequilla 90 g", "Lácteos", 1_400, 2_200, 15),
        new("7501000000028", "Pan blanco grande", "Panadería", 2_200, 3_400, 14),
        new("7501000000029", "Pan de caja integral", "Panadería", 2_800, 4_200, 10),
        new("7501000000030", "Galletas María 170 g", "Panadería", 1_000, 1_700, 30),
        new("7501000000031", "Pastelito de chocolate", "Panadería", 1_200, 2_000, 24),
        new("7501000000032", "Papas fritas 45 g", "Botanas", 900, 1_600, 40),
        new("7501000000033", "Cacahuates 150 g", "Botanas", 1_200, 2_000, 25),
        new("7501000000034", "Palomitas 90 g", "Botanas", 800, 1_500, 20),
        new("7501000000035", "Jabón de barra 150 g", "Limpieza", 1_100, 1_900, 30),
        new("7501000000036", "Detergente 1 kg", "Limpieza", 2_600, 3_900, 22),
        new("7501000000037", "Cloro 1 L", "Limpieza", 1_200, 2_100, 26),
        new("7501000000038", "Papel higiénico 4 rollos", "Limpieza", 3_200, 4_900, 18),
        new("7501000000039", "Suavizante 850 ml", "Limpieza", 2_100, 3_300, 14),
        new("7501000000040", "Jabón de tocador", "Higiene", 800, 1_500, 40),
        new("7501000000041", "Pasta dental 100 ml", "Higiene", 1_800, 2_900, 26),
        new("7501000000042", "Cepillo dental", "Higiene", 1_200, 2_200, 30),
        new("7501000000043", "Shampoo 375 ml", "Higiene", 3_400, 5_200, 16),
        new("7501000000044", "Desodorante 150 ml", "Higiene", 2_900, 4_500, 12),
        new("7501000000045", "Huevo 12 piezas", "Abarrotes", 3_600, 4_800, 20),
        new("7501000000046", "Tortillas 1 kg", "Abarrotes", 1_600, 2_400, 18),
        new("7501000000047", "Chiles jalapeños 220 g", "Abarrotes", 900, 1_600, 24),
        new("7501000000048", "Frijoles refritos 430 g", "Abarrotes", 1_500, 2_400, 22),
        new("7501000000049", "Pilas AA (par)", "Varios", 2_400, 3_900, 15),
        new("7501000000050", "Bolsas para mandado 10 pzas", "Varios", 500, 1_200, 3),
        new("7501000000051", "Detergente en polvo 500 g", "Limpieza", 1_500, 2_400, 2),
        new("7501000000052", "Leche condensada 397 g", "Abarrotes", 2_600, 3_900, 0),
        new("7501000000053", "Refresco de temporada 600 ml", "Bebidas", 1_100, 1_800, 6)
    };

    private static readonly (string Nombre, string Telefono)[] Clientes =
    {
        ("María López", "555-100-2030"),
        ("José Ramírez", "555-100-2031"),
        ("Ana Torres", "555-100-2032"),
        ("Luis Hernández", "555-100-2033"),
        ("Carmen Ruiz", "555-100-2034"),
        ("Rosa Mendoza", "555-100-2035")
    };

    private static readonly (string Nombre, string Clave, string Telefono, string Direccion)[] Proveedores =
    {
        ("Distribuidora La Central", "DLC-001", "555-200-3001", "Av. Central 120"),
        ("Lácteos del Valle", "LDV-002", "555-200-3002", "Camino al Valle 45"),
        ("Bebidas del Norte", "BDN-003", "555-200-3003", "Blvd. Norte 890"),
        ("Abarrotes Mayoreo Sánchez", "AMS-004", "555-200-3004", "Calle 5 de Mayo 33"),
        ("Limpieza Total", "LT-005", "555-200-3005", "Priv. Industrial 7")
    };

    public static int Ejecutar(OpcionesSemilla opciones)
    {
        if (opciones.MostrarAyuda)
        {
            OpcionesSemilla.ImprimirAyuda();
            return 0;
        }

        var ruta = opciones.RutaBaseDatos;

        if (!opciones.Forzar && EsCarpetaDeLaAppReal(ruta))
            throw new InvalidOperationException(
                "La ruta apunta a la app real (com.sistematienda.tienda). " +
                "Usa otra base o pasa --forzar si de verdad quieres datos ficticios ahí.");

        if (File.Exists(ruta) && !opciones.Reiniciar)
            throw new InvalidOperationException(
                $"Ya existe una base en {ruta}. Pasa --reset para reemplazarla.");

        Limpiar(ruta);

        if (opciones.Escenario == OpcionesSemilla.EscenarioLimpio)
        {
            Console.WriteLine($"Base limpia: {ruta}");
            Console.WriteLine("Sin archivos, sin respaldos y sin exportaciones. Lista para el asistente inicial.");
            return 0;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);

        var db = new DatabaseTools(ruta);
        db.InicializarBaseDeDatos();

        var config = new ConfiguracionService(db);
        var inventario = new InventoryService(db);

        var asistente = config.EjecutarAsistenteInicial(
            "Abarrotes La Esquina", "Abarrotes", 16m, 3.5m,
            FondoCaja, fiadoActivado: true, limiteCredito: 100_000,
            respaldoIntervaloDias: 1, pinDueño: "1234");
        Exigir(asistente, "asistente inicial");

        var dueño = config.ObtenerUsuarios().Value
            .FirstOrDefault(u => u.Rol == RolUsuario.Dueño)
            ?? throw new InvalidOperationException("No se creó el usuario dueño");
        SesionActual.Iniciar(dueño);

        Exigir(config.AgregarUsuario("Encargada", "5678", RolUsuario.Encargado), "usuario encargada");
        Exigir(config.AgregarUsuario("Empleado", "4321", RolUsuario.Empleado), "usuario empleado");

        var productos = new List<Producto>();
        foreach (var definicion in Catalogo)
        {
            var producto = new Producto
            {
                CodigoBarras = definicion.Codigo,
                Nombre = definicion.Nombre,
                Categoria = definicion.Categoria,
                Costo = definicion.Costo,
                Precio = definicion.Precio,
                Stock = definicion.Stock,
                StockMinimo = definicion.Minimo,
                Impuesto = 16m
            };

            Exigir(inventario.AgregarProducto(producto), $"producto {definicion.Nombre}");
            productos.Add(producto);
        }

        var descontinuado = productos[^1];
        Exigir(inventario.DesactivarProducto(descontinuado.Id), "desactivar producto de temporada");

        if (opciones.Escenario == OpcionesSemilla.EscenarioConfigurado)
        {
            Resumen(db, opciones, productos.Count);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            return 0;
        }

        GenerarDemo(db, config, productos);
        Resumen(db, opciones, productos.Count);
        // Cierra las conexiones en colchón para que la app pueda abrir la base al instante.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        return 0;
    }

    private static void GenerarDemo(DatabaseTools db, ConfiguracionService config, List<Producto> productos)
    {
        var ventas = new VentaService(db);
        var caja = new CorteCajaService(db);

        var clientes = new List<ClienteFiado>();
        foreach (var (nombre, telefono) in Clientes)
        {
            var cliente = new ClienteFiado { Nombre = nombre, Telefono = telefono };
            Exigir(config.AgregarClienteFiado(cliente), $"cliente {nombre}");
            clientes.Add(cliente);
        }

        var proveedores = new List<Proveedor>();
        foreach (var (nombre, clave, telefono, direccion) in Proveedores)
        {
            var proveedor = new Proveedor
            {
                Nombre = nombre,
                Clave = clave,
                Telefono = telefono,
                Direccion = direccion
            };
            Exigir(config.AgregarProveedor(proveedor), $"proveedor {nombre}");
            proveedores.Add(proveedor);
        }

        var ventasDelDia = new List<Venta>();

        // Ventas antes del corte: los cuatro métodos de pago.
        Vender(ventas, productos, ventasDelDia, MetodoPagoVenta.Efectivo, 1_000, (17, 2), (32, 1));
        Vender(ventas, productos, ventasDelDia, MetodoPagoVenta.Tarjeta, 0, (22, 2), (30, 1));
        Vender(ventas, productos, ventasDelDia, MetodoPagoVenta.Mixto, 0, (18, 3), (31, 2));
        Vender(ventas, productos, ventasDelDia, MetodoPagoVenta.Fiado, 0, clientes[0], (0, 1), (1, 1));
        Vender(ventas, productos, ventasDelDia, MetodoPagoVenta.Efectivo, 0, (19, 1));
        Vender(ventas, productos, ventasDelDia, MetodoPagoVenta.Fiado, 0, clientes[1], (23, 2));
        Vender(ventas, productos, ventasDelDia, MetodoPagoVenta.Efectivo, 500, (25, 1), (27, 1), (45, 1));
        Vender(ventas, productos, ventasDelDia, MetodoPagoVenta.Tarjeta, 0, (35, 2));
        Vender(ventas, productos, ventasDelDia, MetodoPagoVenta.Efectivo, 500, (3, 2), (4, 1));
        Vender(ventas, productos, ventasDelDia, MetodoPagoVenta.Fiado, 0, clientes[2], (7, 2));
        Vender(ventas, productos, ventasDelDia, MetodoPagoVenta.Efectivo, 0, (20, 3));
        Vender(ventas, productos, ventasDelDia, MetodoPagoVenta.Mixto, 0, (38, 1), (39, 1));
        Vender(ventas, productos, ventasDelDia, MetodoPagoVenta.Efectivo, 0, (41, 1), (42, 1));
        Vender(ventas, productos, ventasDelDia, MetodoPagoVenta.Tarjeta, 0, (43, 1));
        var aAnular = Vender(ventas, productos, ventasDelDia, MetodoPagoVenta.Fiado, 0, clientes[3], (8, 1));
        Vender(ventas, productos, ventasDelDia, MetodoPagoVenta.Efectivo, 0, (24, 1), (26, 1));

        // Se anula antes del corte para que el corte excluya la venta y su reverso.
        Exigir(ventas.AnularVenta(aAnular.Id, "El cliente canceló la compra", "Dueño"),
            "anulación de venta");

        Exigir(config.RegistrarAbonoCliente(clientes[0].Id, 5_000, "Dueño", MetodoPagoAbono.Efectivo),
            "abono en efectivo");
        Exigir(config.RegistrarAbonoCliente(clientes[1].Id, 3_000, "Dueño", MetodoPagoAbono.Transferencia),
            "abono por transferencia");

        Exigir(caja.RegistrarMovimiento(TipoMovimientoCaja.Ingreso, 20_000,
            "Fondo adicional para cambio", "Dueño"), "ingreso de caja");
        Exigir(caja.RegistrarMovimiento(TipoMovimientoCaja.Retiro, 15_000,
            "Pago del servicio de luz", "Dueño"), "retiro de caja");

        Exigir(config.AgregarGasto(new Gasto
            {
                Concepto = "Bolsas para mandado",
                Monto = 4_500,
                MetodoPago = MetodoPagoAbono.Efectivo,
                Origen = OrigenGasto.Caja,
                Observaciones = "Compra semanal"
            }), "gasto de caja");
        Exigir(config.AgregarGasto(new Gasto
            {
                Concepto = "Limpieza del local",
                Monto = 12_000,
                MetodoPago = MetodoPagoAbono.Efectivo,
                Origen = OrigenGasto.Caja
            }), "gasto de caja");
        Exigir(config.AgregarGasto(new Gasto
            {
                Concepto = "Renta del local",
                Monto = 250_000,
                MetodoPago = MetodoPagoAbono.Transferencia,
                Origen = OrigenGasto.Externo
            }), "gasto externo");
        Exigir(config.AgregarGasto(new Gasto
            {
                Concepto = "Internet",
                Monto = 49_900,
                MetodoPago = MetodoPagoAbono.Tarjeta,
                Origen = OrigenGasto.Externo
            }), "gasto externo");

        Exigir(config.RegistrarCompraProveedor(proveedores[0].Id, new[]
        {
            new LineaCompraSolicitada { ProductoId = productos[0].Id, Cantidad = 12, CostoUnitario = 2_100 },
            new LineaCompraSolicitada { ProductoId = productos[1].Id, Cantidad = 10, CostoUnitario = 2_400 },
            new LineaCompraSolicitada { ProductoId = productos[3].Id, Cantidad = 15, CostoUnitario = 2_000 }
        }, "Reposición de abarrotes"), "compra a proveedor");

        Exigir(config.RegistrarCompraProveedor(proveedores[2].Id, new[]
        {
            new LineaCompraSolicitada { ProductoId = productos[17].Id, Cantidad = 24, CostoUnitario = 1_100 },
            new LineaCompraSolicitada { ProductoId = productos[19].Id, Cantidad = 24, CostoUnitario = 700 }
        }, "Pedido de bebidas"), "compra a proveedor");

        Exigir(config.RegistrarPagoProveedor(proveedores[2].Id, 30_000,
            MetodoPagoProveedor.Efectivo), "pago a proveedor");

        // Corte cerrado con conteo por denominaciones.
        var pendiente = caja.ObtenerResumenPendiente();
        Exigir(pendiente, "resumen de caja");
        var conteo = DescomponerEnDenominaciones(pendiente.Value.EfectivoEsperado);
        Exigir(caja.RegistrarCorte(pendiente.Value.EfectivoEsperado, FondoCaja,
            "Dueño", "Corte de demostración", conteo), "corte de caja");

        // Ventas después del corte para dejar un periodo abierto.
        Vender(ventas, productos, ventasDelDia, MetodoPagoVenta.Efectivo, 100, (44, 1));
        Vender(ventas, productos, ventasDelDia, MetodoPagoVenta.Tarjeta, 0, (21, 1));
        Vender(ventas, productos, ventasDelDia, MetodoPagoVenta.Fiado, 0, clientes[0], (10, 1));
        Vender(ventas, productos, ventasDelDia, MetodoPagoVenta.Efectivo, 0, (2, 1), (5, 2));
        Vender(ventas, productos, ventasDelDia, MetodoPagoVenta.Mixto, 0, (34, 1), (33, 1));

        var respaldos = new RespaldoService(db);
        Exigir(respaldos.CrearRespaldoManual("demo"), "respaldo de demostración");
    }

    private static Venta Vender(
        VentaService servicio, List<Producto> productos, List<Venta> acumuladas,
        string metodo, long recibidoExtra,
        params (int Indice, int Cantidad)[] lineas)
    {
        if (lineas.Length > 0)
        {
            var solicitadas = lineas
                .Select(l => new LineaVentaSolicitada
                {
                    ProductoId = productos[l.Indice].Id,
                    Cantidad = l.Cantidad
                })
                .ToList();

            var total = lineas.Sum(l => productos[l.Indice].Precio * l.Cantidad);
            var pago = new PagoSolicitado();

            switch (metodo)
            {
                case MetodoPagoVenta.Efectivo:
                    pago.Efectivo = total;
                    pago.EfectivoRecibido = total + recibidoExtra;
                    break;
                case MetodoPagoVenta.Tarjeta:
                    pago.Tarjeta = total;
                    break;
                case MetodoPagoVenta.Mixto:
                    pago.Efectivo = total / 2;
                    pago.Tarjeta = total - total / 2;
                    break;
                default:
                    pago.Fiado = total;
                    break;
            }

            var resultado = servicio.RegistrarVenta(solicitadas, pago, "Dueño");
            Exigir(resultado, "venta " + metodo);
            acumuladas.Add(resultado.Value);
            return resultado.Value;
        }

        throw new ArgumentException("Una venta debe tener al menos una línea");
    }

    private static Venta Vender(
        VentaService servicio, List<Producto> productos, List<Venta> acumuladas,
        string metodo, long recibidoExtra, ClienteFiado cliente,
        params (int Indice, int Cantidad)[] lineas)
    {
        var total = lineas.Sum(l => productos[l.Indice].Precio * l.Cantidad);
        var solicitadas = lineas
            .Select(l => new LineaVentaSolicitada
            {
                ProductoId = productos[l.Indice].Id,
                Cantidad = l.Cantidad
            })
            .ToList();

        var resultado = servicio.RegistrarVenta(solicitadas, new PagoSolicitado
        {
            Fiado = total,
            ClienteId = cliente.Id
        }, "Dueño");

        Exigir(resultado, "venta fiada");
        acumuladas.Add(resultado.Value);
        return resultado.Value;
    }

    private static IReadOnlyList<(DenominacionEfectivo Denominacion, int Cantidad)>
        DescomponerEnDenominaciones(long centavos)
    {
        var restante = centavos;
        var conteo = new List<(DenominacionEfectivo, int)>();

        foreach (var denominacion in ConteoEfectivo.Denominaciones)
        {
            var cantidad = (int)(restante / denominacion.ValorCentavos);
            if (cantidad <= 0)
                continue;

            conteo.Add((denominacion, cantidad));
            restante -= cantidad * denominacion.ValorCentavos;
        }

        if (restante != 0)
            throw new InvalidOperationException(
                $"No se pudo descomponer {centavos} centavos en denominaciones; sobran {restante}");

        return conteo;
    }

    private static void Resumen(DatabaseTools db, OpcionesSemilla opciones,
        int productos)
    {
        if (opciones.Silencioso)
            return;

        var conteos = new (string Nombre, string Tabla)[]
        {
            ("Productos", "Productos"),
            ("Clientes", "ClientesFiado"),
            ("Proveedores", "Proveedores"),
            ("Ventas", "Ventas"),
            ("Cortes de caja", "CortesCaja"),
            ("Gastos", "Gastos"),
            ("Compras", "ComprasProveedor"),
            ("Pagos a proveedor", "PagosProveedor"),
            ("Usuarios", "UsuariosSistema")
        };

        Console.WriteLine($"Escenario: {opciones.Escenario}");
        Console.WriteLine($"Base: {db.RutaBaseDatos}");
        Console.WriteLine($"Tienda: Abarrotes La Esquina");
        Console.WriteLine("PIN: dueño 1234 · encargada 5678 · empleado 4321");
        Console.WriteLine();

        foreach (var (nombre, tabla) in conteos)
            Console.WriteLine($"  {nombre,-20} {db.ExecuteScalar<long>($"SELECT COUNT(*) FROM {tabla}"),5}");

        Console.WriteLine();
        Console.WriteLine("Respaldo: revisa la carpeta respaldos junto a la base.");
        Console.WriteLine("Siguiente paso: abre la app, entra con el PIN y prueba el flujo.");
    }

    private static void Limpiar(string ruta)
    {
        // El pooling de SQLite puede mantener el archivo abierto y bloquear el borrado.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        var carpeta = Path.GetDirectoryName(ruta);
        if (string.IsNullOrWhiteSpace(carpeta))
            return;

        foreach (var archivo in new[] { ruta, ruta + "-wal", ruta + "-shm" })
        {
            if (File.Exists(archivo))
                File.Delete(archivo);
        }

        foreach (var sub in new[] { "respaldos", "exportaciones" })
        {
            var destino = Path.Combine(carpeta, sub);
            if (Directory.Exists(destino))
                Directory.Delete(destino, recursive: true);
        }
    }

    private static bool EsCarpetaDeLaAppReal(string ruta) =>
        ruta.Contains($"com.sistematienda.tienda{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase) ||
        ruta.Contains($"com.sistematienda.tienda/", StringComparison.OrdinalIgnoreCase);

    private static void Exigir(ProcessResult resultado, string contexto)
    {
        if (resultado.IsError)
            throw new InvalidOperationException($"{contexto}: {resultado.Message}");
    }

    private static void Exigir<T>(ProcessResult<T> resultado, string contexto)
    {
        if (resultado.IsError)
            throw new InvalidOperationException($"{contexto}: {resultado.Message}");
    }
}
