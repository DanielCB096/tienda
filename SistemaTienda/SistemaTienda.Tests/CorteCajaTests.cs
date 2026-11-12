using Microsoft.Data.Sqlite;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Tests;

public sealed class CorteCajaTests
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

    private static int CrearCliente(BaseDatosPrueba bd, long saldo = 0)
    {
        var resultado = bd.Config.AgregarClienteFiado(new ClienteFiado
        {
            Nombre = "Cliente de prueba",
            Telefono = "5550000000",
            SaldoActual = saldo
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
    public void CerrarCorte_CalculaElEfectivoEsperadoConTodasLasFuentes()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda(fiado: true);
        var producto = CrearProducto(bd, "A1", precio: 2_500, costo: 1_000, stock: 20);
        var clienteId = CrearCliente(bd);

        Assert.True(Vender(bd, new PagoSolicitado { Efectivo = 7_500 }, (producto, 3)).IsSuccess);
        Assert.True(Vender(bd, new PagoSolicitado { Tarjeta = 5_000 }, (producto, 2)).IsSuccess);
        Assert.True(Vender(bd,
            new PagoSolicitado { Fiado = 2_500, ClienteId = clienteId }, (producto, 1)).IsSuccess);
        Assert.True(bd.Config.RegistrarAbonoCliente(clienteId, 1_000, "Cajero").IsSuccess);
        Assert.True(bd.Config.AgregarGasto(new Gasto
        {
            Concepto = "Luz",
            Monto = 2_000,
            MetodoPago = "Efectivo",
            Origen = OrigenGasto.Caja
        }).IsSuccess);

        var servicio = new CorteCajaService(bd.Db);
        Assert.True(servicio.RegistrarMovimiento(TipoMovimientoCaja.Ingreso, 500, "Cambio extra", "Cajero").IsSuccess);
        Assert.True(servicio.RegistrarMovimiento(TipoMovimientoCaja.Retiro, 1_500, "Compra de bolsas", "Cajero").IsSuccess);

        var corte = servicio.RegistrarCorte(25_500, 20_000, "Cajero");

        Assert.True(corte.IsSuccess, corte.Message);
        var c = corte.Value;
        Assert.Equal(20_000, c.FondoInicial);
        Assert.Equal(7_500, c.VentasEfectivo);
        Assert.Equal(5_000, c.VentasTarjeta);
        Assert.Equal(2_500, c.VentasFiado);
        Assert.Equal(15_000, c.TotalVentas);
        Assert.Equal(3, c.NumeroVentas);
        Assert.Equal(1, c.NumeroVentasEfectivo);
        Assert.Equal(1, c.NumeroVentasTarjeta);
        Assert.Equal(1, c.NumeroVentasFiado);
        Assert.Equal(1_000, c.AbonosEfectivo);
        Assert.Equal(500, c.IngresosCaja);
        Assert.Equal(1_500, c.RetirosCaja);
        Assert.Equal(2_000, c.GastosCaja);
        Assert.Equal(25_500, c.EfectivoEsperado);
        Assert.Equal(25_500, c.EfectivoContado);
        Assert.Equal(0, c.Diferencia);
        Assert.Equal("Cajero", c.Usuario);
    }

    [Fact]
    public void CerrarCorte_ConFaltanteSinMotivo_NoSeGuarda()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var servicio = new CorteCajaService(bd.Db);

        var sinMotivo = servicio.RegistrarCorte(19_000, 20_000, "Cajero");

        Assert.True(sinMotivo.IsError);
        Assert.Contains("diferencia", sinMotivo.Message);
        Assert.Equal(0, bd.Db.ExecuteScalar<int>("SELECT COUNT(*) FROM CortesCaja"));

        var conMotivo = servicio.RegistrarCorte(19_000, 20_000, "Cajero", "Faltante por revisar");

        Assert.True(conMotivo.IsSuccess, conMotivo.Message);
        Assert.Equal(-1_000, conMotivo.Value.Diferencia);
        Assert.Equal("Faltante por revisar", conMotivo.Value.Observaciones);
    }

    [Fact]
    public void CerrarCorte_ConSobrante_GuardaDiferenciaPositiva()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var resultado = new CorteCajaService(bd.Db)
            .RegistrarCorte(21_200, 20_000, "Cajero", "Propina no registrada");

        Assert.True(resultado.IsSuccess, resultado.Message);
        Assert.Equal(1_200, resultado.Value.Diferencia);
    }

    [Fact]
    public void CerrarCorte_GastoExternoNoReduceElEfectivoEsperado()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        Assert.True(bd.Config.AgregarGasto(new Gasto
        {
            Concepto = "Renta",
            Monto = 50_000,
            MetodoPago = "Transferencia",
            Origen = OrigenGasto.Externo
        }).IsSuccess);

        var corte = new CorteCajaService(bd.Db).RegistrarCorte(20_000, 20_000, "Cajero");

        Assert.True(corte.IsSuccess, corte.Message);
        Assert.Equal(0, corte.Value.GastosCaja);
        Assert.Equal(20_000, corte.Value.EfectivoEsperado);
    }

    [Fact]
    public void CerrarCorte_MixtoSoloSumaLaParteEnEfectivo()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 5_000, costo: 2_000, stock: 5);

        Assert.True(Vender(bd,
            new PagoSolicitado { Efectivo = 3_000, Tarjeta = 7_000 }, (producto, 2)).IsSuccess);

        var corte = new CorteCajaService(bd.Db).RegistrarCorte(23_000, 20_000, "Cajero");

        Assert.True(corte.IsSuccess, corte.Message);
        Assert.Equal(3_000, corte.Value.VentasEfectivo);
        Assert.Equal(7_000, corte.Value.VentasTarjeta);
        Assert.Equal(10_000, corte.Value.TotalVentas);
        Assert.Equal(1, corte.Value.NumeroVentasMixto);
        Assert.Equal(23_000, corte.Value.EfectivoEsperado);
        Assert.Equal(0, corte.Value.Diferencia);
    }

    [Fact]
    public void CerrarCorte_SoloIncluyeMovimientosDelPeriodo()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 1_000, costo: 500, stock: 10);

        Assert.True(Vender(bd, new PagoSolicitado { Efectivo = 1_000 }, (producto, 1)).IsSuccess);
        bd.Db.ExecuteCommand("UPDATE Ventas SET Fecha = datetime('now', '-2 days')");

        var servicio = new CorteCajaService(bd.Db);
        var primero = servicio.RegistrarCorte(21_000, 20_000, "Cajero");
        Assert.True(primero.IsSuccess, primero.Message);
        Assert.Equal(1_000, primero.Value.VentasEfectivo);

        // Separa los periodos para que la segunda venta caiga después del cierre.
        bd.Db.ExecuteCommand("UPDATE CortesCaja SET Hasta = datetime('now', '-1 day') WHERE Id = @id",
            new SqliteParameter("@id", primero.Value.Id));

        Assert.True(Vender(bd, new PagoSolicitado { Efectivo = 3_000 }, (producto, 3)).IsSuccess);

        var segundo = servicio.RegistrarCorte(24_000, 21_000, "Cajero");

        Assert.True(segundo.IsSuccess, segundo.Message);
        Assert.Equal(3_000, segundo.Value.VentasEfectivo);
        Assert.Equal(3_000, segundo.Value.TotalVentas);
        Assert.Equal(1, segundo.Value.NumeroVentas);
        Assert.Equal(24_000, segundo.Value.EfectivoEsperado);
        Assert.Equal(0, segundo.Value.Diferencia);
    }

    [Fact]
    public void ResumenPendiente_SugiereElUltimoContadoComoFondo()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var servicio = new CorteCajaService(bd.Db);

        Assert.Equal(20_000, servicio.ObtenerResumenPendiente().Value.FondoInicial);
        Assert.True(servicio.RegistrarCorte(21_500, 20_000, "Cajero", "Sobrante revisado").IsSuccess);

        var preview = servicio.ObtenerResumenPendiente();

        Assert.Equal(21_500, preview.Value.FondoInicial);
        Assert.Equal(0, preview.Value.VentasEfectivo);
        Assert.Equal(21_500, preview.Value.EfectivoEsperado);
    }

    [Fact]
    public void CerrarCorte_SinActividad_CuadraConElFondo()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var corte = new CorteCajaService(bd.Db).RegistrarCorte(20_000, 20_000, "Cajero");

        Assert.True(corte.IsSuccess, corte.Message);
        Assert.Equal(0, corte.Value.TotalVentas);
        Assert.Equal(0, corte.Value.NumeroVentas);
        Assert.Equal(20_000, corte.Value.EfectivoEsperado);
        Assert.Equal(0, corte.Value.Diferencia);
    }

    [Fact]
    public void RegistrarCorte_ConMontosNegativosOSinUsuario_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var servicio = new CorteCajaService(bd.Db);

        Assert.True(servicio.RegistrarCorte(-1, 20_000, "Cajero").IsError);
        Assert.True(servicio.RegistrarCorte(20_000, -1, "Cajero").IsError);
        Assert.True(servicio.RegistrarCorte(20_000, 20_000, " ").IsError);
        Assert.Equal(0, bd.Db.ExecuteScalar<int>("SELECT COUNT(*) FROM CortesCaja"));
    }

    [Fact]
    public void RegistrarMovimiento_Invalido_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var servicio = new CorteCajaService(bd.Db);

        Assert.True(servicio.RegistrarMovimiento("Otro", 1_000, "Motivo", "Cajero").IsError);
        Assert.True(servicio.RegistrarMovimiento(TipoMovimientoCaja.Ingreso, 0, "Motivo", "Cajero").IsError);
        Assert.True(servicio.RegistrarMovimiento(TipoMovimientoCaja.Retiro, 1_000, " ", "Cajero").IsError);
        Assert.Equal(0, bd.Db.ExecuteScalar<int>("SELECT COUNT(*) FROM MovimientosCaja"));
    }

    [Fact]
    public void RegistrarAbono_GuardaHistorialYUsuarioYEfectaElCorte()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var clienteId = CrearCliente(bd, saldo: 10_000);

        var abono = bd.Config.RegistrarAbonoCliente(clienteId, 4_000, "Cajero");

        Assert.True(abono.IsSuccess, abono.Message);
        Assert.Equal(6_000, abono.Value.SaldoRestante);
        Assert.Equal(1, bd.Db.ExecuteScalar<int>("SELECT COUNT(*) FROM AbonosFiado"));
        Assert.Equal(4_000, bd.Db.ExecuteScalar<long>("SELECT Monto FROM AbonosFiado LIMIT 1"));
        Assert.Equal("Cajero", bd.Db.ExecuteScalar<string>("SELECT Usuario FROM AbonosFiado LIMIT 1"));
        Assert.Equal("Efectivo", bd.Db.ExecuteScalar<string>("SELECT MetodoPago FROM AbonosFiado LIMIT 1"));

        var corte = new CorteCajaService(bd.Db).RegistrarCorte(24_000, 20_000, "Cajero");

        Assert.True(corte.IsSuccess, corte.Message);
        Assert.Equal(4_000, corte.Value.AbonosEfectivo);
        Assert.Equal(24_000, corte.Value.EfectivoEsperado);
    }

    [Fact]
    public void ObtenerCortes_DevuelveDelMasRecienteAlMasAntiguo()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var servicio = new CorteCajaService(bd.Db);

        Assert.True(servicio.RegistrarCorte(20_000, 20_000, "Cajero").IsSuccess);
        var segundo = servicio.RegistrarCorte(20_000, 20_000, "Cajero");
        Assert.True(segundo.IsSuccess, segundo.Message);

        var cortes = servicio.ObtenerCortes();

        Assert.Equal(2, cortes.Value.Count);
        Assert.Equal(segundo.Value.Id, cortes.Value[0].Id);
    }

    [Fact]
    public void Inicializar_BaseV2_MigraAv4ConRespaldoYConservaDatos()
    {
        using var bd = new BaseDatosPrueba();
        Assert.True(bd.Inventario.AgregarProducto(new Producto
        {
            CodigoBarras = "A1",
            Nombre = "Anterior",
            Costo = 500,
            Precio = 1_000,
            Stock = 4
        }).IsSuccess);

        // Simula una base v2: importes en pesos y sin las tablas del corte de caja.
        bd.Db.ExecuteCommand("UPDATE Productos SET Costo = Costo / 100.0, Precio = Precio / 100.0");
        bd.Db.ExecuteCommand("UPDATE MovimientoInventario SET CostoUnitario = CostoUnitario / 100.0");
        bd.Db.ExecuteCommand("UPDATE ConfiguracionTienda SET FondoCajaInicial = FondoCajaInicial / 100.0");
        bd.Db.ExecuteCommand("DROP TABLE AbonosFiado");
        bd.Db.ExecuteCommand("DROP TABLE MovimientosCaja");
        bd.Db.ExecuteCommand("DROP TABLE CortesCaja");
        bd.Db.ExecuteCommand("PRAGMA user_version = 2");

        bd.Db.InicializarBaseDeDatos();

        Assert.Equal(Migraciones.VersionMaxima(Migraciones.ObtenerTodas()),
            bd.Db.ExecuteScalar<int>("PRAGMA user_version"));
        foreach (var tabla in new[] { "CortesCaja", "MovimientosCaja", "AbonosFiado" })
        {
            Assert.Equal(1, bd.Db.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @nombre",
                new SqliteParameter("@nombre", tabla)));
        }
        var producto = Assert.Single(bd.Db.ObtenerProductos());
        Assert.Equal(500, producto.Costo);
        Assert.Equal(1_000, producto.Precio);
        Assert.Single(RespaldoLocal.Listar(bd.Db.DirectorioRespaldos),
            r => r.Nombre.Contains("antes-migrar"));
    }
}
