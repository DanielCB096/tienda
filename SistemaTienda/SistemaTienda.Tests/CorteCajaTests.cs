using Microsoft.Data.Sqlite;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Tests;

public sealed class CorteCajaTests
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

    private static int CrearCliente(BaseDatosPrueba bd, decimal saldo = 0m)
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
        var producto = CrearProducto(bd, "A1", precio: 25m, costo: 10m, stock: 20);
        var clienteId = CrearCliente(bd);

        Assert.True(Vender(bd, new PagoSolicitado { Efectivo = 75m }, (producto, 3)).IsSuccess);
        Assert.True(Vender(bd, new PagoSolicitado { Tarjeta = 50m }, (producto, 2)).IsSuccess);
        Assert.True(Vender(bd,
            new PagoSolicitado { Fiado = 25m, ClienteId = clienteId }, (producto, 1)).IsSuccess);
        Assert.True(bd.Config.RegistrarAbonoCliente(clienteId, 10m, "Cajero").IsSuccess);
        Assert.True(bd.Config.AgregarGasto(new Gasto
        {
            Concepto = "Luz",
            Monto = 20m,
            MetodoPago = "Efectivo",
            Origen = OrigenGasto.Caja
        }).IsSuccess);

        var servicio = new CorteCajaService(bd.Db);
        Assert.True(servicio.RegistrarMovimiento(TipoMovimientoCaja.Ingreso, 5m, "Cambio extra", "Cajero").IsSuccess);
        Assert.True(servicio.RegistrarMovimiento(TipoMovimientoCaja.Retiro, 15m, "Compra de bolsas", "Cajero").IsSuccess);

        var corte = servicio.RegistrarCorte(255m, 200m, "Cajero");

        Assert.True(corte.IsSuccess, corte.Message);
        var c = corte.Value;
        Assert.Equal(200m, c.FondoInicial);
        Assert.Equal(75m, c.VentasEfectivo);
        Assert.Equal(50m, c.VentasTarjeta);
        Assert.Equal(25m, c.VentasFiado);
        Assert.Equal(150m, c.TotalVentas);
        Assert.Equal(3, c.NumeroVentas);
        Assert.Equal(1, c.NumeroVentasEfectivo);
        Assert.Equal(1, c.NumeroVentasTarjeta);
        Assert.Equal(1, c.NumeroVentasFiado);
        Assert.Equal(10m, c.AbonosEfectivo);
        Assert.Equal(5m, c.IngresosCaja);
        Assert.Equal(15m, c.RetirosCaja);
        Assert.Equal(20m, c.GastosCaja);
        Assert.Equal(255m, c.EfectivoEsperado);
        Assert.Equal(255m, c.EfectivoContado);
        Assert.Equal(0m, c.Diferencia);
        Assert.Equal("Cajero", c.Usuario);
    }

    [Fact]
    public void CerrarCorte_ConFaltanteSinMotivo_NoSeGuarda()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var servicio = new CorteCajaService(bd.Db);

        var sinMotivo = servicio.RegistrarCorte(190m, 200m, "Cajero");

        Assert.True(sinMotivo.IsError);
        Assert.Contains("diferencia", sinMotivo.Message);
        Assert.Equal(0, bd.Db.ExecuteScalar<int>("SELECT COUNT(*) FROM CortesCaja"));

        var conMotivo = servicio.RegistrarCorte(190m, 200m, "Cajero", "Faltante por revisar");

        Assert.True(conMotivo.IsSuccess, conMotivo.Message);
        Assert.Equal(-10m, conMotivo.Value.Diferencia);
        Assert.Equal("Faltante por revisar", conMotivo.Value.Observaciones);
    }

    [Fact]
    public void CerrarCorte_ConSobrante_GuardaDiferenciaPositiva()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var resultado = new CorteCajaService(bd.Db)
            .RegistrarCorte(212m, 200m, "Cajero", "Propina no registrada");

        Assert.True(resultado.IsSuccess, resultado.Message);
        Assert.Equal(12m, resultado.Value.Diferencia);
    }

    [Fact]
    public void CerrarCorte_GastoExternoNoReduceElEfectivoEsperado()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        Assert.True(bd.Config.AgregarGasto(new Gasto
        {
            Concepto = "Renta",
            Monto = 500m,
            MetodoPago = "Transferencia",
            Origen = OrigenGasto.Externo
        }).IsSuccess);

        var corte = new CorteCajaService(bd.Db).RegistrarCorte(200m, 200m, "Cajero");

        Assert.True(corte.IsSuccess, corte.Message);
        Assert.Equal(0m, corte.Value.GastosCaja);
        Assert.Equal(200m, corte.Value.EfectivoEsperado);
    }

    [Fact]
    public void CerrarCorte_MixtoSoloSumaLaParteEnEfectivo()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 50m, costo: 20m, stock: 5);

        Assert.True(Vender(bd,
            new PagoSolicitado { Efectivo = 30m, Tarjeta = 70m }, (producto, 2)).IsSuccess);

        var corte = new CorteCajaService(bd.Db).RegistrarCorte(230m, 200m, "Cajero");

        Assert.True(corte.IsSuccess, corte.Message);
        Assert.Equal(30m, corte.Value.VentasEfectivo);
        Assert.Equal(70m, corte.Value.VentasTarjeta);
        Assert.Equal(100m, corte.Value.TotalVentas);
        Assert.Equal(1, corte.Value.NumeroVentasMixto);
        Assert.Equal(230m, corte.Value.EfectivoEsperado);
        Assert.Equal(0m, corte.Value.Diferencia);
    }

    [Fact]
    public void CerrarCorte_SoloIncluyeMovimientosDelPeriodo()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 10m, costo: 5m, stock: 10);

        Assert.True(Vender(bd, new PagoSolicitado { Efectivo = 10m }, (producto, 1)).IsSuccess);
        bd.Db.ExecuteCommand("UPDATE Ventas SET Fecha = datetime('now', '-2 days')");

        var servicio = new CorteCajaService(bd.Db);
        var primero = servicio.RegistrarCorte(210m, 200m, "Cajero");
        Assert.True(primero.IsSuccess, primero.Message);
        Assert.Equal(10m, primero.Value.VentasEfectivo);

        // Separa los periodos para que la segunda venta caiga después del cierre.
        bd.Db.ExecuteCommand("UPDATE CortesCaja SET Hasta = datetime('now', '-1 day') WHERE Id = @id",
            new SqliteParameter("@id", primero.Value.Id));

        Assert.True(Vender(bd, new PagoSolicitado { Efectivo = 30m }, (producto, 3)).IsSuccess);

        var segundo = servicio.RegistrarCorte(240m, 210m, "Cajero");

        Assert.True(segundo.IsSuccess, segundo.Message);
        Assert.Equal(30m, segundo.Value.VentasEfectivo);
        Assert.Equal(30m, segundo.Value.TotalVentas);
        Assert.Equal(1, segundo.Value.NumeroVentas);
        Assert.Equal(240m, segundo.Value.EfectivoEsperado);
        Assert.Equal(0m, segundo.Value.Diferencia);
    }

    [Fact]
    public void ResumenPendiente_SugiereElUltimoContadoComoFondo()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var servicio = new CorteCajaService(bd.Db);

        Assert.Equal(200m, servicio.ObtenerResumenPendiente().Value.FondoInicial);
        Assert.True(servicio.RegistrarCorte(215m, 200m, "Cajero", "Sobrante revisado").IsSuccess);

        var preview = servicio.ObtenerResumenPendiente();

        Assert.Equal(215m, preview.Value.FondoInicial);
        Assert.Equal(0m, preview.Value.VentasEfectivo);
        Assert.Equal(215m, preview.Value.EfectivoEsperado);
    }

    [Fact]
    public void CerrarCorte_SinActividad_CuadraConElFondo()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var corte = new CorteCajaService(bd.Db).RegistrarCorte(200m, 200m, "Cajero");

        Assert.True(corte.IsSuccess, corte.Message);
        Assert.Equal(0m, corte.Value.TotalVentas);
        Assert.Equal(0, corte.Value.NumeroVentas);
        Assert.Equal(200m, corte.Value.EfectivoEsperado);
        Assert.Equal(0m, corte.Value.Diferencia);
    }

    [Fact]
    public void RegistrarCorte_ConMontosNegativosOSinUsuario_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var servicio = new CorteCajaService(bd.Db);

        Assert.True(servicio.RegistrarCorte(-1m, 200m, "Cajero").IsError);
        Assert.True(servicio.RegistrarCorte(200m, -1m, "Cajero").IsError);
        Assert.True(servicio.RegistrarCorte(200m, 200m, " ").IsError);
        Assert.Equal(0, bd.Db.ExecuteScalar<int>("SELECT COUNT(*) FROM CortesCaja"));
    }

    [Fact]
    public void RegistrarMovimiento_Invalido_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var servicio = new CorteCajaService(bd.Db);

        Assert.True(servicio.RegistrarMovimiento("Otro", 10m, "Motivo", "Cajero").IsError);
        Assert.True(servicio.RegistrarMovimiento(TipoMovimientoCaja.Ingreso, 0m, "Motivo", "Cajero").IsError);
        Assert.True(servicio.RegistrarMovimiento(TipoMovimientoCaja.Retiro, 10m, " ", "Cajero").IsError);
        Assert.Equal(0, bd.Db.ExecuteScalar<int>("SELECT COUNT(*) FROM MovimientosCaja"));
    }

    [Fact]
    public void RegistrarAbono_GuardaHistorialYUsuarioYEfectaElCorte()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var clienteId = CrearCliente(bd, saldo: 100m);

        var abono = bd.Config.RegistrarAbonoCliente(clienteId, 40m, "Cajero");

        Assert.True(abono.IsSuccess, abono.Message);
        Assert.Equal(60m, abono.Value.SaldoRestante);
        Assert.Equal(1, bd.Db.ExecuteScalar<int>("SELECT COUNT(*) FROM AbonosFiado"));
        Assert.Equal(40m, bd.Db.ExecuteScalar<decimal>("SELECT Monto FROM AbonosFiado LIMIT 1"));
        Assert.Equal("Cajero", bd.Db.ExecuteScalar<string>("SELECT Usuario FROM AbonosFiado LIMIT 1"));
        Assert.Equal("Efectivo", bd.Db.ExecuteScalar<string>("SELECT MetodoPago FROM AbonosFiado LIMIT 1"));

        var corte = new CorteCajaService(bd.Db).RegistrarCorte(240m, 200m, "Cajero");

        Assert.True(corte.IsSuccess, corte.Message);
        Assert.Equal(40m, corte.Value.AbonosEfectivo);
        Assert.Equal(240m, corte.Value.EfectivoEsperado);
    }

    [Fact]
    public void ObtenerCortes_DevuelveDelMasRecienteAlMasAntiguo()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var servicio = new CorteCajaService(bd.Db);

        Assert.True(servicio.RegistrarCorte(200m, 200m, "Cajero").IsSuccess);
        var segundo = servicio.RegistrarCorte(200m, 200m, "Cajero");
        Assert.True(segundo.IsSuccess, segundo.Message);

        var cortes = servicio.ObtenerCortes();

        Assert.Equal(2, cortes.Value.Count);
        Assert.Equal(segundo.Value.Id, cortes.Value[0].Id);
    }

    [Fact]
    public void Inicializar_BaseV2_MigraAv3ConRespaldoYConservaDatos()
    {
        using var bd = new BaseDatosPrueba();
        Assert.True(bd.Inventario.AgregarProducto(new Producto
        {
            CodigoBarras = "A1",
            Nombre = "Anterior",
            Costo = 5m,
            Precio = 10m,
            Stock = 4
        }).IsSuccess);

        // Simula una base creada antes del corte de caja.
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
        Assert.Single(bd.Db.ObtenerProductos());
        Assert.Single(RespaldoLocal.Listar(bd.Db.DirectorioRespaldos),
            r => r.Nombre.Contains("antes-migrar"));
    }
}
