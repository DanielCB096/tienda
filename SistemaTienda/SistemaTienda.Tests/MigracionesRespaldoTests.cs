using Microsoft.Data.Sqlite;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Tests;

public sealed class MigracionesRespaldoTests
{
    private static int VersionActual => Migraciones.VersionMaxima(Migraciones.ObtenerTodas());

    private static Migracion MigracionCreaTabla(int version = 0) =>
        new(version == 0 ? VersionActual + 1 : version, "crea TablaPrueba", (conn, tx) =>
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "CREATE TABLE TablaPrueba (Id INTEGER PRIMARY KEY, Nota TEXT NOT NULL DEFAULT '')";
            cmd.ExecuteNonQuery();
        });

    [Fact]
    public void Migrar_BaseV1_AplicaPendientesYRespaldaAntes()
    {
        using var entorno = new EntornoMigracion();
        var migrador = new MigradorEsquema(entorno.RutaDb, new[] { MigracionCreaTabla() }, entorno.DirectorioRespaldos);

        var resultado = migrador.Migrar();

        Assert.True(resultado.IsSuccess, resultado.Message);
        Assert.Equal(VersionActual, resultado.Value.VersionInicial);
        Assert.Equal(VersionActual + 1, resultado.Value.VersionFinal);
        Assert.Equal(new[] { VersionActual + 1 }, resultado.Value.VersionesAplicadas);
        Assert.True(resultado.Value.HuboCambios);

        Assert.True(File.Exists(resultado.Value.RutaRespaldo));
        Assert.True(RespaldoLocal.Verificar(resultado.Value.RutaRespaldo));
        Assert.Equal(VersionActual + 1, entorno.Db.ExecuteScalar<int>("PRAGMA user_version"));
        Assert.Equal(1, entorno.Db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'TablaPrueba'"));
    }

    [Fact]
    public void Migrar_SinPendientes_EsIdempotenteYNoCreaOtroRespaldo()
    {
        using var entorno = new EntornoMigracion();
        var migrador = new MigradorEsquema(entorno.RutaDb, new[] { MigracionCreaTabla() }, entorno.DirectorioRespaldos);

        Assert.True(migrador.Migrar().IsSuccess);
        var respaldosTrasPrimera = RespaldoLocal.Listar(entorno.DirectorioRespaldos).Count;

        var segunda = migrador.Migrar();

        Assert.True(segunda.IsSuccess, segunda.Message);
        Assert.False(segunda.Value.HuboCambios);
        Assert.Empty(segunda.Value.VersionesAplicadas);
        Assert.Equal(respaldosTrasPrimera, RespaldoLocal.Listar(entorno.DirectorioRespaldos).Count);
        Assert.Equal(VersionActual + 1, entorno.Db.ExecuteScalar<int>("PRAGMA user_version"));
    }

    [Fact]
    public void Migrar_SiUnaMigracionFalla_DejaLaVersionYLosDatosIntactos()
    {
        using var entorno = new EntornoMigracion();

        var migracionQueFalla = new Migracion(VersionActual + 1, "falla a propósito", (conn, tx) =>
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                CREATE TABLE TablaParcial (Id INTEGER PRIMARY KEY);
                INSERT INTO TablaInexistente VALUES (1);";
            cmd.ExecuteNonQuery();
        });

        var migrador = new MigradorEsquema(entorno.RutaDb, new[] { migracionQueFalla }, entorno.DirectorioRespaldos);
        var resultado = migrador.Migrar();

        Assert.True(resultado.IsError);
        Assert.Equal(VersionActual, entorno.Db.ExecuteScalar<int>("PRAGMA user_version"));
        Assert.Equal(0, entorno.Db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'TablaParcial'"));
        Assert.Equal(1, entorno.Db.ExecuteScalar<int>("SELECT COUNT(*) FROM ConfiguracionTienda"));

        var respaldos = RespaldoLocal.Listar(entorno.DirectorioRespaldos);
        var respaldo = Assert.Single(respaldos);
        Assert.True(RespaldoLocal.Verificar(respaldo.Ruta));
    }

    [Fact]
    public void Migrar_BaseMasNuevaQueLaApp_SeRechazaSinTocarNada()
    {
        using var entorno = new EntornoMigracion();
        entorno.Db.ExecuteCommand("PRAGMA user_version = 99");

        var migrador = new MigradorEsquema(entorno.RutaDb, new[] { MigracionCreaTabla() }, entorno.DirectorioRespaldos);
        var resultado = migrador.Migrar();

        Assert.True(resultado.IsError);
        Assert.Equal(99, entorno.Db.ExecuteScalar<int>("PRAGMA user_version"));
        Assert.Empty(RespaldoLocal.Listar(entorno.DirectorioRespaldos));
    }

    [Fact]
    public void Migrador_RechazaVersionesInvalidasODuplicadas()
    {
        var ruta = Path.Combine(Path.GetTempPath(), $"no-se-usa-{Guid.NewGuid():N}.db");

        Assert.Throws<ArgumentException>(() =>
            new MigradorEsquema(ruta, new[] { MigracionCreaTabla(version: 1) }));

        Assert.Throws<ArgumentException>(() =>
            new MigradorEsquema(ruta, new[] { MigracionCreaTabla(VersionActual + 1), MigracionCreaTabla(VersionActual + 1) }));
    }

    [Fact]
    public void Respaldo_SeCreaVerificaYLista()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var servicio = new RespaldoService(bd.Db);

        var creado = servicio.CrearRespaldo("manual");

        Assert.True(creado.IsSuccess, creado.Message);
        Assert.True(File.Exists(creado.Value));
        Assert.True(RespaldoLocal.Verificar(creado.Value));
        Assert.Contains(RespaldoLocal.Listar(bd.Db.DirectorioRespaldos), r => r.Ruta == creado.Value);
        Assert.True(servicio.ObtenerUltimoRespaldo().Value.HasValue);
    }

    [Fact]
    public void Restaurar_DevuelveLaBaseAlEstadoDelRespaldo()
    {
        using var bd = new BaseDatosPrueba();
        bd.Inventario.AgregarProducto(new Producto
        {
            CodigoBarras = "A1", Nombre = "Antes", Costo = 100, Precio = 200, Stock = 5
        });

        var servicio = new RespaldoService(bd.Db);
        var respaldo = servicio.CrearRespaldo("prueba");
        Assert.True(respaldo.IsSuccess, respaldo.Message);

        bd.Inventario.AgregarProducto(new Producto
        {
            CodigoBarras = "A2", Nombre = "Después", Costo = 100, Precio = 200, Stock = 3
        });
        Assert.Equal(2, bd.Db.ObtenerProductos().Count);

        var restauracion = servicio.Restaurar(respaldo.Value, confirmado: true);

        Assert.True(restauracion.IsSuccess, restauracion.Message);
        var producto = Assert.Single(bd.Db.ObtenerProductos());
        Assert.Equal("A1", producto.CodigoBarras);
        Assert.Equal(5, producto.Stock);

        // Además del respaldo original, queda el de seguridad previo a restaurar.
        Assert.True(RespaldoLocal.Listar(bd.Db.DirectorioRespaldos).Count >= 2);
    }

    [Fact]
    public void Restaurar_SinConfirmacionOFueraDeRuta_Falla()
    {
        using var bd = new BaseDatosPrueba();
        var servicio = new RespaldoService(bd.Db);
        var respaldo = servicio.CrearRespaldo("prueba");

        Assert.True(servicio.Restaurar(respaldo.Value, confirmado: false).IsError);
        Assert.True(servicio.Restaurar(Path.Combine(bd.Db.DirectorioRespaldos, "no-existe.db"), true).IsError);
    }

    [Fact]
    public void Migrar_BaseV3_ConvierteImportesACentavosYConservaLaIntegridad()
    {
        using var entorno = new EntornoMigracion();

        // Simula una base v3: importes en pesos con hasta tres decimales.
        entorno.Db.ExecuteCommand(@"
            UPDATE ConfiguracionTienda
            SET FondoCajaInicial = 200.00, LimiCreditoCliente = 500.00;

            INSERT INTO ClientesFiado (Nombre, Telefono, SaldoActual)
                VALUES ('Cliente', '555-000-0000', 300.00);
            INSERT INTO Productos (CodigoBarras, Nombre, Costo, Precio, Stock, Impuesto)
                VALUES ('A1', 'Producto', 12.345, 20.00, 4, 16.00);
            INSERT INTO MovimientoInventario (ProductoId, Tipo, Cantidad, CostoUnitario, Motivo)
                SELECT Id, 'Entrada', 4, 12.345, 'Inicial' FROM Productos WHERE CodigoBarras = 'A1';
            INSERT INTO Gastos (Concepto, Monto, MetodoPago, Origen)
                VALUES ('Luz', 25.50, 'Efectivo', 'Caja');
            INSERT INTO Proveedores (Nombre, Clave, SaldoPendiente)
                VALUES ('Proveedor', 'P1', 500.00);
            INSERT INTO Ventas (Fecha, Usuario, ClienteId, MetodoPago, Total, CostoTotal,
                PagoEfectivo, PagoTarjeta, PagoFiado, ComisionTarjeta)
                VALUES (datetime('now'), 'Cajero', (SELECT Id FROM ClientesFiado LIMIT 1), 'Efectivo',
                        20.00, 12.345, 20.00, 0, 0, 0);
            INSERT INTO VentaLineas (VentaId, ProductoId, CodigoBarras, Nombre, Cantidad,
                PrecioUnitario, CostoUnitario, Importe)
                SELECT (SELECT Id FROM Ventas LIMIT 1), Id, 'A1', 'Producto', 1, 20.00, 12.345, 20.00
                FROM Productos WHERE CodigoBarras = 'A1';
            INSERT INTO CortesCaja (Desde, Hasta, FondoInicial, VentasEfectivo, VentasTarjeta,
                VentasFiado, TotalVentas, NumeroVentas, NumeroVentasEfectivo, NumeroVentasTarjeta,
                NumeroVentasMixto, NumeroVentasFiado, AbonosEfectivo, IngresosCaja, RetirosCaja,
                GastosCaja, EfectivoEsperado, EfectivoContado, Diferencia, Usuario, Observaciones,
                FechaRegistro)
                VALUES (datetime('now'), datetime('now'), 200.00, 20.00, 0, 0, 20.00, 1, 1, 0, 0, 0,
                        0, 0, 0, 25.50, 194.50, 200.00, 5.50, 'Cajero', '', datetime('now'));
            INSERT INTO MovimientosCaja (Fecha, Tipo, Monto, Motivo, Usuario)
                VALUES (datetime('now'), 'Ingreso', 5.25, 'Cambio', 'Cajero');
            INSERT INTO AbonosFiado (ClienteId, Fecha, Monto, MetodoPago, Usuario)
                SELECT Id, datetime('now'), 10.00, 'Efectivo', 'Cajero' FROM ClientesFiado LIMIT 1;

            PRAGMA user_version = 3;
        ");

        entorno.Db.InicializarBaseDeDatos();

        Assert.Equal(VersionActual, entorno.Db.ExecuteScalar<int>("PRAGMA user_version"));
        Assert.Single(RespaldoLocal.Listar(entorno.DirectorioRespaldos), r => r.Nombre.Contains("antes-migrar"));

        var config = entorno.Db.ObtenerConfiguracion();
        Assert.Equal(20_000, config.FondoCajaInicial);
        Assert.Equal(50_000, config.LimiCreditoCliente);

        // ROUND(12.345 * 100) = 1235 centavos.
        Assert.Equal(1_235, entorno.Db.ExecuteScalar<long>(
            "SELECT Costo FROM Productos WHERE CodigoBarras = 'A1'"));
        Assert.Equal(2_000, entorno.Db.ExecuteScalar<long>(
            "SELECT Precio FROM Productos WHERE CodigoBarras = 'A1'"));
        Assert.Equal(1_235, entorno.Db.ExecuteScalar<long>(
            "SELECT CostoUnitario FROM MovimientoInventario LIMIT 1"));
        Assert.Equal(2_550, entorno.Db.ExecuteScalar<long>("SELECT Monto FROM Gastos LIMIT 1"));
        Assert.Equal(30_000, entorno.Db.ExecuteScalar<long>("SELECT SaldoActual FROM ClientesFiado LIMIT 1"));
        Assert.Equal(50_000, entorno.Db.ExecuteScalar<long>("SELECT SaldoPendiente FROM Proveedores LIMIT 1"));

        Assert.Equal(2_000, entorno.Db.ExecuteScalar<long>("SELECT Total FROM Ventas LIMIT 1"));
        Assert.Equal(1_235, entorno.Db.ExecuteScalar<long>("SELECT CostoTotal FROM Ventas LIMIT 1"));
        Assert.Equal(2_000, entorno.Db.ExecuteScalar<long>("SELECT PagoEfectivo FROM Ventas LIMIT 1"));
        Assert.Equal(2_000, entorno.Db.ExecuteScalar<long>("SELECT Importe FROM VentaLineas LIMIT 1"));
        Assert.Equal(1_235, entorno.Db.ExecuteScalar<long>("SELECT CostoUnitario FROM VentaLineas LIMIT 1"));

        Assert.Equal(20_000, entorno.Db.ExecuteScalar<long>("SELECT FondoInicial FROM CortesCaja LIMIT 1"));
        Assert.Equal(2_000, entorno.Db.ExecuteScalar<long>("SELECT VentasEfectivo FROM CortesCaja LIMIT 1"));
        Assert.Equal(2_550, entorno.Db.ExecuteScalar<long>("SELECT GastosCaja FROM CortesCaja LIMIT 1"));
        Assert.Equal(550, entorno.Db.ExecuteScalar<long>("SELECT Diferencia FROM CortesCaja LIMIT 1"));
        Assert.Equal(525, entorno.Db.ExecuteScalar<long>("SELECT Monto FROM MovimientosCaja LIMIT 1"));
        Assert.Equal(1_000, entorno.Db.ExecuteScalar<long>("SELECT Monto FROM AbonosFiado LIMIT 1"));

        // Todos los importes quedan como INTEGER; los porcentajes siguen en DECIMAL.
        Assert.Equal(0, entorno.Db.ExecuteScalar<int>(@"
            SELECT COUNT(*) FROM pragma_table_info('CortesCaja')
            WHERE name IN ('FondoInicial', 'VentasEfectivo', 'VentasTarjeta', 'VentasFiado',
                           'TotalVentas', 'AbonosEfectivo', 'IngresosCaja', 'RetirosCaja',
                           'GastosCaja', 'EfectivoEsperado', 'EfectivoContado', 'Diferencia')
              AND type <> 'INTEGER'"));
        Assert.Equal(0, entorno.Db.ExecuteScalar<int>(@"
            SELECT COUNT(*) FROM pragma_table_info('Ventas')
            WHERE name IN ('Total', 'CostoTotal', 'PagoEfectivo', 'PagoTarjeta', 'PagoFiado', 'ComisionTarjeta')
              AND type <> 'INTEGER'"));
        Assert.Equal("DECIMAL", entorno.Db.ExecuteScalar<string>(
            "SELECT type FROM pragma_table_info('Productos') WHERE name = 'Impuesto'"));
        Assert.Equal("DECIMAL", entorno.Db.ExecuteScalar<string>(
            "SELECT type FROM pragma_table_info('ConfiguracionTienda') WHERE name = 'ComisionTarjeta'"));

        Assert.Equal(1, entorno.Db.ExecuteScalar<int>("PRAGMA foreign_keys"));
        Assert.Equal(0, entorno.Db.ExecuteScalar<int>("SELECT COUNT(*) FROM pragma_foreign_key_check"));
        Assert.Equal(1, entorno.Db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Bitacora'"));
    }

    [Fact]
    public void Inicializar_BaseNueva_QuedaEnLaVersionObjetivoSinRespaldo()
    {
        using var entorno = new EntornoMigracion();

        Assert.Equal(Migraciones.VersionMaxima(Migraciones.ObtenerTodas()),
            entorno.Db.ExecuteScalar<int>("PRAGMA user_version"));
        Assert.Empty(RespaldoLocal.Listar(entorno.DirectorioRespaldos));
        Assert.Empty(entorno.Db.UltimoRespaldoCreado);
    }

    private sealed class EntornoMigracion : IDisposable
    {
        public string Directorio { get; }
        public string RutaDb { get; }
        public DatabaseTools Db { get; }
        public string DirectorioRespaldos => Db.DirectorioRespaldos;

        public EntornoMigracion()
        {
            Directorio = Path.Combine(Path.GetTempPath(), $"sistematienda-mig-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Directorio);
            RutaDb = Path.Combine(Directorio, "tienda.db");
            Db = new DatabaseTools(RutaDb);
            Db.InicializarBaseDeDatos();
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            try
            {
                if (Directory.Exists(Directorio))
                    Directory.Delete(Directorio, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
