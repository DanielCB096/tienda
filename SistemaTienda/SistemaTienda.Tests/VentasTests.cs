using Microsoft.Data.Sqlite;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Tests;

public sealed class VentasTests
{
    private static Producto CrearProducto(BaseDatosPrueba bd, string codigo,
        decimal precio, decimal costo, int stock)
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
        var producto = CrearProducto(bd, "A1", precio: 25m, costo: 10m, stock: 10);

        var resultado = Vender(bd, new PagoSolicitado { Efectivo = 75m }, (producto, 3));

        Assert.True(resultado.IsSuccess, resultado.Message);
        var venta = resultado.Value;
        Assert.True(venta.Id > 0);
        Assert.Equal(75m, venta.Total);
        Assert.Equal(30m, venta.CostoTotal);
        Assert.Equal(MetodoPagoVenta.Efectivo, venta.MetodoPago);
        Assert.Equal(75m, venta.PagoEfectivo);
        Assert.Equal(0m, venta.ComisionTarjeta);

        var actualizado = bd.Inventario.BuscarPorId(producto.Id);
        Assert.Equal(7, actualizado.Stock);

        var linea = Assert.Single(bd.Db.ObtenerLineasVenta(venta.Id));
        Assert.Equal(producto.Id, linea.ProductoId);
        Assert.Equal(3, linea.Cantidad);
        Assert.Equal(25m, linea.PrecioUnitario);
        Assert.Equal(10m, linea.CostoUnitario);
        Assert.Equal(75m, linea.Importe);

        var movimientos = bd.Inventario.HistorialMovimientos(producto.Id);
        Assert.Contains(movimientos, m => m.Tipo == MovimientoTipo.Salida && m.Cantidad == -3);
        Assert.Equal(7, bd.Db.ObtenerStock(producto.Id));
    }

    [Fact]
    public void RegistrarVenta_StockInsuficiente_NoGuardaNada()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 10m, costo: 5m, stock: 2);

        var resultado = Vender(bd, new PagoSolicitado { Efectivo = 30m }, (producto, 3));

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
        var producto = CrearProducto(bd, "A1", precio: 15m, costo: 10m, stock: 5);

        var venta = Vender(bd, new PagoSolicitado { Efectivo = 30m }, (producto, 2));
        Assert.True(venta.IsSuccess, venta.Message);

        producto.Costo = 12m;
        producto.Precio = 20m;
        Assert.True(bd.Inventario.ActualizarProducto(producto).IsSuccess);

        var linea = Assert.Single(bd.Db.ObtenerLineasVenta(venta.Value.Id));
        Assert.Equal(15m, linea.PrecioUnitario);
        Assert.Equal(10m, linea.CostoUnitario);
        Assert.Equal(30m, linea.Importe);

        var guardada = bd.Db.ObtenerVentaPorId(venta.Value.Id);
        Assert.Equal(30m, guardada.Total);
        Assert.Equal(20m, guardada.CostoTotal);
    }

    [Fact]
    public void RegistrarVenta_Fiado_CreaSaldoYNoRegistraEfectivo()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda(fiado: true);
        var clienteId = CrearCliente(bd, "Ana");
        var producto = CrearProducto(bd, "A1", precio: 50m, costo: 20m, stock: 5);

        var resultado = Vender(bd,
            new PagoSolicitado { Fiado = 100m, ClienteId = clienteId },
            (producto, 2));

        Assert.True(resultado.IsSuccess, resultado.Message);
        Assert.Equal(MetodoPagoVenta.Fiado, resultado.Value.MetodoPago);
        Assert.Equal(100m, resultado.Value.PagoFiado);
        Assert.Equal(0m, resultado.Value.PagoEfectivo);
        Assert.Equal(100m, bd.Db.ObtenerClienteFiadoPorId(clienteId).SaldoActual);
        Assert.Equal(3, bd.Inventario.BuscarPorId(producto.Id).Stock);
    }

    [Fact]
    public void RegistrarVenta_FiadoDesactivado_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda(fiado: false);
        var clienteId = CrearCliente(bd);
        var producto = CrearProducto(bd, "A1", precio: 50m, costo: 20m, stock: 5);

        var resultado = Vender(bd,
            new PagoSolicitado { Fiado = 50m, ClienteId = clienteId },
            (producto, 1));

        Assert.True(resultado.IsError);
        Assert.Equal(5, bd.Inventario.BuscarPorId(producto.Id).Stock);
        Assert.Equal(0m, bd.Db.ObtenerClienteFiadoPorId(clienteId).SaldoActual);
        Assert.Equal(0, bd.Db.ExecuteScalar<int>("SELECT COUNT(*) FROM Ventas"));
    }

    [Fact]
    public void RegistrarVenta_FiadoExcedeLimite_SeRechazaYConservaSaldoAnterior()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda(fiado: true);
        var clienteId = CrearCliente(bd);
        var producto = CrearProducto(bd, "A1", precio: 400m, costo: 100m, stock: 10);

        var primera = Vender(bd,
            new PagoSolicitado { Fiado = 400m, ClienteId = clienteId },
            (producto, 1));
        Assert.True(primera.IsSuccess, primera.Message);

        var segunda = Vender(bd,
            new PagoSolicitado { Fiado = 400m, ClienteId = clienteId },
            (producto, 1));

        Assert.True(segunda.IsError);
        Assert.Contains("límite de crédito", segunda.Message);
        Assert.Equal(400m, bd.Db.ObtenerClienteFiadoPorId(clienteId).SaldoActual);
        Assert.Equal(9, bd.Inventario.BuscarPorId(producto.Id).Stock);
        Assert.Equal(1, bd.Db.ExecuteScalar<int>("SELECT COUNT(*) FROM Ventas"));
    }

    [Fact]
    public void RegistrarVenta_PagoQueNoCoincide_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 50m, costo: 20m, stock: 5);

        var resultado = Vender(bd, new PagoSolicitado { Efectivo = 40m }, (producto, 1));

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
        var producto = CrearProducto(bd, "A1", precio: 50m, costo: 20m, stock: 5);

        var resultado = Vender(bd,
            new PagoSolicitado { Efectivo = 30m, Tarjeta = 70m },
            (producto, 2));

        Assert.True(resultado.IsSuccess, resultado.Message);
        Assert.Equal(MetodoPagoVenta.Mixto, resultado.Value.MetodoPago);
        Assert.Equal(30m, resultado.Value.PagoEfectivo);
        Assert.Equal(70m, resultado.Value.PagoTarjeta);
        Assert.Equal(2.45m, resultado.Value.ComisionTarjeta);
    }

    [Fact]
    public void RegistrarVenta_LineaInvalidaODuplicada_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 50m, costo: 20m, stock: 5);
        var servicio = new VentaService(bd.Db);

        var cantidadCero = servicio.RegistrarVenta(
            new[] { new LineaVentaSolicitada { ProductoId = producto.Id, Cantidad = 0 } },
            new PagoSolicitado { Efectivo = 0m }, "Cajero");

        var duplicada = servicio.RegistrarVenta(
            new[]
            {
                new LineaVentaSolicitada { ProductoId = producto.Id, Cantidad = 1 },
                new LineaVentaSolicitada { ProductoId = producto.Id, Cantidad = 1 }
            },
            new PagoSolicitado { Efectivo = 100m }, "Cajero");

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
        var producto = CrearProducto(bd, "A1", precio: 50m, costo: 20m, stock: 5);

        var resultado = Vender(bd,
            new PagoSolicitado { Fiado = 20m, Efectivo = 30m, ClienteId = clienteId },
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
        var producto = CrearProducto(bd, "A1", precio: 10m, costo: 5m, stock: 5);
        Assert.True(Vender(bd, new PagoSolicitado { Efectivo = 10m }, (producto, 1)).IsSuccess);

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
                Costo = 5m,
                Precio = 10m,
                Stock = 4
            }) > 0);

            // Simula una base creada antes de existir las ventas.
            original.ExecuteCommand("DROP TABLE VentaLineas");
            original.ExecuteCommand("DROP TABLE Ventas");
            original.ExecuteCommand("PRAGMA user_version = 1");

            var migrada = new DatabaseTools(ruta);
            migrada.InicializarBaseDeDatos();

            Assert.Equal(Migraciones.VersionMaxima(Migraciones.ObtenerTodas()),
                migrada.ExecuteScalar<int>("PRAGMA user_version"));
            Assert.Equal(1, migrada.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Ventas'"));
            Assert.Single(migrada.ObtenerProductos());
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
