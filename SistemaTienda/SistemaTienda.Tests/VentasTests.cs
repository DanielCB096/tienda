using Microsoft.Data.Sqlite;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Tests;

public sealed class VentasTests
{
    private static Producto CrearProducto(BaseDatosPrueba bd, string codigo,
        long precio, long costo, int stock)
    {
        var producto = new Producto
        {
            CodigoBarras = codigo,
            Nombre = $"Producto {codigo}",
            Costo = costo,
            Precio = precio,
            Stock = stock
        };

        var resultado = bd.Inventario.AgregarProducto(producto);
        Assert.True(resultado.IsSuccess, resultado.Message);
        return producto;
    }

    private static int CrearCliente(BaseDatosPrueba bd, string nombre = "Cliente de prueba")
    {
        var resultado = bd.Config.AgregarClienteFiado(new ClienteFiado
        {
            Nombre = nombre,
            Telefono = "5550000000"
        });

        Assert.True(resultado.IsSuccess, resultado.Message);
        return resultado.Value.ClienteId;
    }

    private static ProcessResult<Venta> Vender(BaseDatosPrueba bd, PagoSolicitado pago,
        params (Producto Producto, int Cantidad)[] lineas)
    {
        var solicitud = lineas
            .Select(l => new LineaVentaSolicitada { ProductoId = l.Producto.Id, Cantidad = l.Cantidad })
            .ToList();

        return new VentaService(bd.Db).RegistrarVenta(solicitud, pago, "Cajero");
    }

    [Fact]
    public void RegistrarVenta_Efectivo_DescuentaStockYCongelaCostoYPrecio()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 2_500, costo: 1_000, stock: 10);

        var resultado = Vender(bd, new PagoSolicitado { Efectivo = 7_500 }, (producto, 3));

        Assert.True(resultado.IsSuccess, resultado.Message);
        var venta = resultado.Value;
        Assert.True(venta.Id > 0);
        Assert.Equal(7_500, venta.Total);
        Assert.Equal(3_000, venta.CostoTotal);
        Assert.Equal(MetodoPagoVenta.Efectivo, venta.MetodoPago);
        Assert.Equal(7_500, venta.PagoEfectivo);
        Assert.Equal(0, venta.ComisionTarjeta);

        var actualizado = bd.Inventario.BuscarPorId(producto.Id);
        Assert.Equal(7, actualizado.Stock);

        var linea = Assert.Single(bd.Db.ObtenerLineasVenta(venta.Id));
        Assert.Equal(producto.Id, linea.ProductoId);
        Assert.Equal(3, linea.Cantidad);
        Assert.Equal(2_500, linea.PrecioUnitario);
        Assert.Equal(1_000, linea.CostoUnitario);
        Assert.Equal(7_500, linea.Importe);

        var movimientos = bd.Inventario.HistorialMovimientos(producto.Id);
        Assert.Contains(movimientos, m => m.Tipo == MovimientoTipo.Salida && m.Cantidad == -3);
        Assert.Equal(7, bd.Db.ObtenerStock(producto.Id));
    }

    [Fact]
    public void RegistrarVenta_StockInsuficiente_NoGuardaNada()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 1_000, costo: 500, stock: 2);

        var resultado = Vender(bd, new PagoSolicitado { Efectivo = 3_000 }, (producto, 3));

        Assert.True(resultado.IsError);
        Assert.Equal(2, bd.Inventario.BuscarPorId(producto.Id).Stock);
        Assert.Equal(0, bd.Db.ExecuteScalar<int>("SELECT COUNT(*) FROM Ventas"));
        Assert.Equal(0, bd.Db.ExecuteScalar<int>("SELECT COUNT(*) FROM VentaLineas"));
        Assert.Single(bd.Inventario.HistorialMovimientos(producto.Id));
    }

    [Fact]
    public void RegistrarVenta_DespuesDeCambiarPrecios_MantieneLosValoresDeLaVenta()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 1_500, costo: 1_000, stock: 5);

        var venta = Vender(bd, new PagoSolicitado { Efectivo = 3_000 }, (producto, 2));
        Assert.True(venta.IsSuccess, venta.Message);

        producto.Costo = 1_200;
        producto.Precio = 2_000;
        Assert.True(bd.Inventario.ActualizarProducto(producto).IsSuccess);

        var linea = Assert.Single(bd.Db.ObtenerLineasVenta(venta.Value.Id));
        Assert.Equal(1_500, linea.PrecioUnitario);
        Assert.Equal(1_000, linea.CostoUnitario);
        Assert.Equal(3_000, linea.Importe);

        var guardada = bd.Db.ObtenerVentaPorId(venta.Value.Id);
        Assert.Equal(3_000, guardada.Total);
        Assert.Equal(2_000, guardada.CostoTotal);
    }

    [Fact]
    public void RegistrarVenta_Fiado_CreaSaldoYNoRegistraEfectivo()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda(fiado: true);
        var clienteId = CrearCliente(bd, "Ana");
        var producto = CrearProducto(bd, "A1", precio: 5_000, costo: 2_000, stock: 5);

        var resultado = Vender(bd,
            new PagoSolicitado { Fiado = 10_000, ClienteId = clienteId },
            (producto, 2));

        Assert.True(resultado.IsSuccess, resultado.Message);
        Assert.Equal(MetodoPagoVenta.Fiado, resultado.Value.MetodoPago);
        Assert.Equal(10_000, resultado.Value.PagoFiado);
        Assert.Equal(0, resultado.Value.PagoEfectivo);
        Assert.Equal(10_000, bd.Db.ObtenerClienteFiadoPorId(clienteId).SaldoActual);
        Assert.Equal(3, bd.Inventario.BuscarPorId(producto.Id).Stock);
    }

    [Fact]
    public void RegistrarVenta_FiadoDesactivado_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda(fiado: false);
        var clienteId = CrearCliente(bd);
        var producto = CrearProducto(bd, "A1", precio: 5_000, costo: 2_000, stock: 5);

        var resultado = Vender(bd,
            new PagoSolicitado { Fiado = 5_000, ClienteId = clienteId },
            (producto, 1));

        Assert.True(resultado.IsError);
        Assert.Equal(5, bd.Inventario.BuscarPorId(producto.Id).Stock);
        Assert.Equal(0, bd.Db.ObtenerClienteFiadoPorId(clienteId).SaldoActual);
        Assert.Equal(0, bd.Db.ExecuteScalar<int>("SELECT COUNT(*) FROM Ventas"));
    }

    [Fact]
    public void RegistrarVenta_FiadoExcedeLimite_SeRechazaYConservaSaldoAnterior()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda(fiado: true);
        var clienteId = CrearCliente(bd);
        var producto = CrearProducto(bd, "A1", precio: 40_000, costo: 10_000, stock: 10);

        var primera = Vender(bd,
            new PagoSolicitado { Fiado = 40_000, ClienteId = clienteId },
            (producto, 1));
        Assert.True(primera.IsSuccess, primera.Message);

        var segunda = Vender(bd,
            new PagoSolicitado { Fiado = 40_000, ClienteId = clienteId },
            (producto, 1));

        Assert.True(segunda.IsError);
        Assert.Contains("límite de crédito", segunda.Message);
        Assert.Equal(40_000, bd.Db.ObtenerClienteFiadoPorId(clienteId).SaldoActual);
        Assert.Equal(9, bd.Inventario.BuscarPorId(producto.Id).Stock);
        Assert.Equal(1, bd.Db.ExecuteScalar<int>("SELECT COUNT(*) FROM Ventas"));
    }

    [Fact]
    public void RegistrarVenta_PagoQueNoCoincide_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 5_000, costo: 2_000, stock: 5);

        var resultado = Vender(bd, new PagoSolicitado { Efectivo = 4_000 }, (producto, 1));

        Assert.True(resultado.IsError);
        Assert.Contains("no coinciden", resultado.Message);
        Assert.Equal(0, bd.Db.ExecuteScalar<int>("SELECT COUNT(*) FROM Ventas"));
        Assert.Equal(5, bd.Inventario.BuscarPorId(producto.Id).Stock);
    }

    [Fact]
    public void RegistrarVenta_Mixto_CalculaComisionSobreLaTarjeta()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 5_000, costo: 2_000, stock: 5);

        var resultado = Vender(bd,
            new PagoSolicitado { Efectivo = 3_000, Tarjeta = 7_000 },
            (producto, 2));

        Assert.True(resultado.IsSuccess, resultado.Message);
        Assert.Equal(MetodoPagoVenta.Mixto, resultado.Value.MetodoPago);
        Assert.Equal(3_000, resultado.Value.PagoEfectivo);
        Assert.Equal(7_000, resultado.Value.PagoTarjeta);
        Assert.Equal(245, resultado.Value.ComisionTarjeta);
    }

    [Fact]
    public void RegistrarVenta_LineaInvalidaODuplicada_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 5_000, costo: 2_000, stock: 5);
        var servicio = new VentaService(bd.Db);

        var cantidadCero = servicio.RegistrarVenta(
            new[] { new LineaVentaSolicitada { ProductoId = producto.Id, Cantidad = 0 } },
            new PagoSolicitado { Efectivo = 0 }, "Cajero");

        var duplicada = servicio.RegistrarVenta(
            new[]
            {
                new LineaVentaSolicitada { ProductoId = producto.Id, Cantidad = 1 },
                new LineaVentaSolicitada { ProductoId = producto.Id, Cantidad = 1 }
            },
            new PagoSolicitado { Efectivo = 10_000 }, "Cajero");

        Assert.True(cantidadCero.IsError);
        Assert.True(duplicada.IsError);
        Assert.Equal(0, bd.Db.ExecuteScalar<int>("SELECT COUNT(*) FROM Ventas"));
        Assert.Equal(5, bd.Inventario.BuscarPorId(producto.Id).Stock);
    }

    [Fact]
    public void RegistrarVenta_FiadoCombinadoConEfectivo_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda(fiado: true);
        var clienteId = CrearCliente(bd);
        var producto = CrearProducto(bd, "A1", precio: 5_000, costo: 2_000, stock: 5);

        var resultado = Vender(bd,
            new PagoSolicitado { Fiado = 2_000, Efectivo = 3_000, ClienteId = clienteId },
            (producto, 1));

        Assert.True(resultado.IsError);
        Assert.Contains("no se puede combinar", resultado.Message);
        Assert.Equal(0, bd.Db.ExecuteScalar<int>("SELECT COUNT(*) FROM Ventas"));
    }

    [Fact]
    public void VentasDelDia_IncluyeSoloLasDeLaFechaIndicada()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 1_000, costo: 500, stock: 5);
        Assert.True(Vender(bd, new PagoSolicitado { Efectivo = 1_000 }, (producto, 1)).IsSuccess);

        var servicio = new VentaService(bd.Db);
        Assert.Single(servicio.ObtenerVentasDelDia(DateTime.Today));
        Assert.Empty(servicio.ObtenerVentasDelDia(DateTime.Today.AddDays(-1)));
    }

    [Fact]
    public void Inicializar_BaseNueva_TieneTablasDeVentaYVersionVigente()
    {
        using var bd = new BaseDatosPrueba();

        Assert.Equal(Migraciones.VersionMaxima(Migraciones.ObtenerTodas()),
            bd.Db.ExecuteScalar<int>("PRAGMA user_version"));
        Assert.Equal(1, bd.Db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Ventas'"));
        Assert.Equal(1, bd.Db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'VentaLineas'"));
        Assert.Empty(RespaldoLocal.Listar(bd.Db.DirectorioRespaldos));
    }

    [Fact]
    public void Inicializar_BaseV1Existente_MigraConRespaldoYConservaDatos()
    {
        var directorio = Path.Combine(Path.GetTempPath(), $"sistematienda-v1-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directorio);
        var ruta = Path.Combine(directorio, "tienda.db");

        try
        {
            var original = new DatabaseTools(ruta);
            original.InicializarBaseDeDatos();
            Assert.True(original.AgregarProducto(new Producto
            {
                CodigoBarras = "A1",
                Nombre = "Anterior",
                Costo = 500,
                Precio = 1_000,
                Stock = 4
            }) > 0);

            // Simula una base creada antes de existir las ventas, el corte y los centavos:
            // los importes vuelven a pesos y se eliminan las tablas posteriores.
            original.ExecuteCommand("UPDATE Productos SET Costo = Costo / 100.0, Precio = Precio / 100.0");
            original.ExecuteCommand("UPDATE MovimientoInventario SET CostoUnitario = CostoUnitario / 100.0");
            original.ExecuteCommand("UPDATE ConfiguracionTienda SET FondoCajaInicial = FondoCajaInicial / 100.0, " +
                                    "LimiCreditoCliente = LimiCreditoCliente / 100.0");
            original.ExecuteCommand("DROP TABLE AbonosFiado");
            original.ExecuteCommand("DROP TABLE MovimientosCaja");
            original.ExecuteCommand("DROP TABLE CortesCaja");
            original.ExecuteCommand("DROP TABLE VentaLineas");
            original.ExecuteCommand("DROP TABLE Ventas");
            original.ExecuteCommand("PRAGMA user_version = 1");

            var migrada = new DatabaseTools(ruta);
            migrada.InicializarBaseDeDatos();

            Assert.Equal(Migraciones.VersionMaxima(Migraciones.ObtenerTodas()),
                migrada.ExecuteScalar<int>("PRAGMA user_version"));
            Assert.Equal(1, migrada.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Ventas'"));
            var producto = Assert.Single(migrada.ObtenerProductos());
            Assert.Equal(500, producto.Costo);
            Assert.Equal(1_000, producto.Precio);
            Assert.Equal(4, producto.Stock);
            Assert.Equal(20_000, migrada.ObtenerConfiguracion().FondoCajaInicial);
            Assert.Single(RespaldoLocal.Listar(migrada.DirectorioRespaldos),
                r => r.Nombre.Contains("antes-migrar"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(directorio, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
