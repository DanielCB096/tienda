// Migraciones de esquema versionadas con PRAGMA user_version
using Microsoft.Data.Sqlite;
using SistemaTienda.Core;

namespace SistemaTienda.Infrastructure;

/// <summary>
/// Paso de migración. Debe ser idempotente en su efecto y no destructivo sin respaldo.
/// </summary>
public sealed record Migracion(int Version, string Descripcion, Action<SqliteConnection, SqliteTransaction> Aplicar);

public sealed class ResultadoMigracion
{
    public int VersionInicial { get; init; }
    public int VersionFinal { get; init; }
    public IReadOnlyList<int> VersionesAplicadas { get; init; } = Array.Empty<int>();
    public string RutaRespaldo { get; init; } = string.Empty;
    public bool HuboCambios => VersionesAplicadas.Count > 0;
}

public static class Migraciones
{
    /// <summary>
    /// Versión del esquema base creado por DatabaseTools antes de aplicar migraciones.
    /// </summary>
    public const int VersionEsquemaBase = 1;

    /// <summary>
    /// Migraciones posteriores al esquema base. La versión 1 es el esquema base.
    /// Al agregar una migración, sube su versión y añade su prueba correspondiente.
    /// </summary>
    public static IReadOnlyList<Migracion> ObtenerTodas() => new[]
    {
        new Migracion(2, "crea ventas y líneas de venta", AplicarVersion2),
        new Migracion(3, "crea cortes de caja, movimientos de caja y abonos de fiado", AplicarVersion3),
        new Migracion(4, "convierte los importes a centavos enteros", AplicarVersion4)
    };

    private static void AplicarVersion2(SqliteConnection conn, SqliteTransaction tx)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Ventas (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Fecha DATETIME NOT NULL,
                Usuario TEXT NOT NULL DEFAULT '',
                ClienteId INTEGER,
                MetodoPago TEXT NOT NULL,
                Total DECIMAL NOT NULL,
                CostoTotal DECIMAL NOT NULL,
                PagoEfectivo DECIMAL NOT NULL DEFAULT 0,
                PagoTarjeta DECIMAL NOT NULL DEFAULT 0,
                PagoFiado DECIMAL NOT NULL DEFAULT 0,
                ComisionTarjeta DECIMAL NOT NULL DEFAULT 0,
                FOREIGN KEY (ClienteId) REFERENCES ClientesFiado(Id)
            );

            CREATE TABLE IF NOT EXISTS VentaLineas (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                VentaId INTEGER NOT NULL,
                ProductoId INTEGER NOT NULL,
                CodigoBarras TEXT NOT NULL,
                Nombre TEXT NOT NULL,
                Cantidad INTEGER NOT NULL,
                PrecioUnitario DECIMAL NOT NULL,
                CostoUnitario DECIMAL NOT NULL,
                Importe DECIMAL NOT NULL,
                FOREIGN KEY (VentaId) REFERENCES Ventas(Id),
                FOREIGN KEY (ProductoId) REFERENCES Productos(Id)
            );

            CREATE INDEX IF NOT EXISTS IX_Ventas_Fecha ON Ventas(Fecha);
            CREATE INDEX IF NOT EXISTS IX_VentaLineas_VentaId ON VentaLineas(VentaId);
            CREATE INDEX IF NOT EXISTS IX_VentaLineas_ProductoId ON VentaLineas(ProductoId);
        ";
        cmd.ExecuteNonQuery();
    }

    private static void AplicarVersion3(SqliteConnection conn, SqliteTransaction tx)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS CortesCaja (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Desde DATETIME NOT NULL,
                Hasta DATETIME NOT NULL,
                FondoInicial DECIMAL NOT NULL DEFAULT 0,
                VentasEfectivo DECIMAL NOT NULL DEFAULT 0,
                VentasTarjeta DECIMAL NOT NULL DEFAULT 0,
                VentasFiado DECIMAL NOT NULL DEFAULT 0,
                TotalVentas DECIMAL NOT NULL DEFAULT 0,
                NumeroVentas INTEGER NOT NULL DEFAULT 0,
                NumeroVentasEfectivo INTEGER NOT NULL DEFAULT 0,
                NumeroVentasTarjeta INTEGER NOT NULL DEFAULT 0,
                NumeroVentasMixto INTEGER NOT NULL DEFAULT 0,
                NumeroVentasFiado INTEGER NOT NULL DEFAULT 0,
                AbonosEfectivo DECIMAL NOT NULL DEFAULT 0,
                IngresosCaja DECIMAL NOT NULL DEFAULT 0,
                RetirosCaja DECIMAL NOT NULL DEFAULT 0,
                GastosCaja DECIMAL NOT NULL DEFAULT 0,
                EfectivoEsperado DECIMAL NOT NULL DEFAULT 0,
                EfectivoContado DECIMAL NOT NULL DEFAULT 0,
                Diferencia DECIMAL NOT NULL DEFAULT 0,
                Usuario TEXT NOT NULL DEFAULT '',
                Observaciones TEXT NOT NULL DEFAULT '',
                FechaRegistro DATETIME NOT NULL
            );

            CREATE TABLE IF NOT EXISTS MovimientosCaja (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Fecha DATETIME NOT NULL,
                Tipo TEXT NOT NULL,
                Monto DECIMAL NOT NULL,
                Motivo TEXT NOT NULL DEFAULT '',
                Usuario TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS AbonosFiado (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ClienteId INTEGER NOT NULL,
                Fecha DATETIME NOT NULL,
                Monto DECIMAL NOT NULL,
                MetodoPago TEXT NOT NULL DEFAULT 'Efectivo',
                Usuario TEXT NOT NULL DEFAULT '',
                FOREIGN KEY (ClienteId) REFERENCES ClientesFiado(Id)
            );

            CREATE INDEX IF NOT EXISTS IX_CortesCaja_Hasta ON CortesCaja(Hasta);
            CREATE INDEX IF NOT EXISTS IX_MovimientosCaja_Fecha ON MovimientosCaja(Fecha);
            CREATE INDEX IF NOT EXISTS IX_AbonosFiado_Fecha ON AbonosFiado(Fecha);
            CREATE INDEX IF NOT EXISTS IX_AbonosFiado_ClienteId ON AbonosFiado(ClienteId);
        ";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Reconstruye cada tabla de dinero con columnas INTEGER y multiplica por 100 los
    /// importes existentes. Los porcentajes (impuesto y comisión) siguen en DECIMAL.
    /// </summary>
    private static void AplicarVersion4(SqliteConnection conn, SqliteTransaction tx)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"
            CREATE TABLE ConfiguracionTienda_Nueva (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                NombreTienda TEXT NOT NULL DEFAULT '',
                Giro TEXT NOT NULL DEFAULT '',
                ImpuestoPredeterminado DECIMAL NOT NULL DEFAULT 16.00,
                ComisionTarjeta DECIMAL NOT NULL DEFAULT 3.50,
                FondoCajaInicial INTEGER NOT NULL DEFAULT 0,
                FiadoActivado INTEGER NOT NULL DEFAULT 0,
                LimiCreditoCliente INTEGER NOT NULL DEFAULT 0,
                RespaldoIntervaloDias INTEGER NOT NULL DEFAULT 1,
                UsuarioDueñoPINHash TEXT NOT NULL DEFAULT '',
                FechaCreacion DATETIME NOT NULL DEFAULT (datetime('now')),
                VersionApp TEXT NOT NULL DEFAULT ''
            );
            INSERT INTO ConfiguracionTienda_Nueva
                (Id, NombreTienda, Giro, ImpuestoPredeterminado, ComisionTarjeta, FondoCajaInicial,
                 FiadoActivado, LimiCreditoCliente, RespaldoIntervaloDias, UsuarioDueñoPINHash,
                 FechaCreacion, VersionApp)
            SELECT Id, NombreTienda, Giro, ImpuestoPredeterminado, ComisionTarjeta,
                   CAST(ROUND(COALESCE(FondoCajaInicial, 0) * 100) AS INTEGER),
                   FiadoActivado,
                   CAST(ROUND(COALESCE(LimiCreditoCliente, 0) * 100) AS INTEGER),
                   RespaldoIntervaloDias, UsuarioDueñoPINHash, FechaCreacion, VersionApp
            FROM ConfiguracionTienda;
            DROP TABLE ConfiguracionTienda;
            ALTER TABLE ConfiguracionTienda_Nueva RENAME TO ConfiguracionTienda;

            CREATE TABLE Productos_Nueva (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CodigoBarras TEXT NOT NULL UNIQUE,
                Nombre TEXT NOT NULL,
                Costo INTEGER NOT NULL DEFAULT 0,
                Precio INTEGER NOT NULL DEFAULT 0,
                Stock INTEGER NOT NULL DEFAULT 0,
                StockMinimo INTEGER NOT NULL DEFAULT 5,
                Categoria TEXT NOT NULL DEFAULT '',
                Impuesto DECIMAL NOT NULL DEFAULT 16.00,
                FechaCreacion DATETIME NOT NULL DEFAULT (datetime('now')),
                UltimaActualizacion DATETIME
            );
            INSERT INTO Productos_Nueva
                (Id, CodigoBarras, Nombre, Costo, Precio, Stock, StockMinimo, Categoria,
                 Impuesto, FechaCreacion, UltimaActualizacion)
            SELECT Id, CodigoBarras, Nombre,
                   CAST(ROUND(COALESCE(Costo, 0) * 100) AS INTEGER),
                   CAST(ROUND(COALESCE(Precio, 0) * 100) AS INTEGER),
                   Stock, StockMinimo, Categoria, Impuesto, FechaCreacion, UltimaActualizacion
            FROM Productos;
            DROP TABLE Productos;
            ALTER TABLE Productos_Nueva RENAME TO Productos;
            CREATE INDEX IF NOT EXISTS IX_Productos_CodigoBarras ON Productos(CodigoBarras);
            CREATE INDEX IF NOT EXISTS IX_Productos_Nombre ON Productos(Nombre);
            CREATE INDEX IF NOT EXISTS IX_Productos_Stock ON Productos(Stock);

            CREATE TABLE MovimientoInventario_Nueva (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProductoId INTEGER NOT NULL,
                Tipo TEXT NOT NULL,
                Cantidad INTEGER NOT NULL,
                CostoUnitario INTEGER NOT NULL DEFAULT 0,
                Motivo TEXT NOT NULL DEFAULT '',
                Usuario TEXT NOT NULL DEFAULT '',
                Fecha DATETIME NOT NULL DEFAULT (datetime('now')),
                FOREIGN KEY (ProductoId) REFERENCES Productos(Id)
            );
            INSERT INTO MovimientoInventario_Nueva
                (Id, ProductoId, Tipo, Cantidad, CostoUnitario, Motivo, Usuario, Fecha)
            SELECT Id, ProductoId, Tipo, Cantidad,
                   CAST(ROUND(COALESCE(CostoUnitario, 0) * 100) AS INTEGER),
                   Motivo, Usuario, Fecha
            FROM MovimientoInventario;
            DROP TABLE MovimientoInventario;
            ALTER TABLE MovimientoInventario_Nueva RENAME TO MovimientoInventario;
            CREATE INDEX IF NOT EXISTS IX_MovimientoInventario_ProductoId ON MovimientoInventario(ProductoId);
            CREATE INDEX IF NOT EXISTS IX_MovimientoInventario_Fecha ON MovimientoInventario(Fecha);

            CREATE TABLE Gastos_Nueva (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Concepto TEXT NOT NULL DEFAULT '',
                Monto INTEGER NOT NULL DEFAULT 0,
                Fecha DATETIME NOT NULL DEFAULT (datetime('now')),
                MetodoPago TEXT NOT NULL DEFAULT '',
                Origen TEXT NOT NULL DEFAULT 'Caja',
                Observaciones TEXT NOT NULL DEFAULT ''
            );
            INSERT INTO Gastos_Nueva
                (Id, Concepto, Monto, Fecha, MetodoPago, Origen, Observaciones)
            SELECT Id, Concepto, CAST(ROUND(COALESCE(Monto, 0) * 100) AS INTEGER),
                   Fecha, MetodoPago, Origen, Observaciones
            FROM Gastos;
            DROP TABLE Gastos;
            ALTER TABLE Gastos_Nueva RENAME TO Gastos;
            CREATE INDEX IF NOT EXISTS IX_Gastos_Fecha ON Gastos(Fecha);

            CREATE TABLE ClientesFiado_Nueva (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Nombre TEXT NOT NULL DEFAULT '',
                Telefono TEXT NOT NULL DEFAULT '',
                SaldoActual INTEGER NOT NULL DEFAULT 0,
                FechaRegistro DATETIME NOT NULL DEFAULT (datetime('now')),
                UltimaActualizacion DATETIME
            );
            INSERT INTO ClientesFiado_Nueva
                (Id, Nombre, Telefono, SaldoActual, FechaRegistro, UltimaActualizacion)
            SELECT Id, Nombre, Telefono,
                   CAST(ROUND(COALESCE(SaldoActual, 0) * 100) AS INTEGER),
                   FechaRegistro, UltimaActualizacion
            FROM ClientesFiado;
            DROP TABLE ClientesFiado;
            ALTER TABLE ClientesFiado_Nueva RENAME TO ClientesFiado;
            CREATE INDEX IF NOT EXISTS IX_ClientesFiado_Nombre ON ClientesFiado(Nombre);

            CREATE TABLE Proveedores_Nueva (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Nombre TEXT NOT NULL DEFAULT '',
                Clave TEXT NOT NULL DEFAULT '',
                Telefono TEXT NOT NULL DEFAULT '',
                Direccion TEXT NOT NULL DEFAULT '',
                SaldoPendiente INTEGER NOT NULL DEFAULT 0,
                FechaRegistro DATETIME NOT NULL DEFAULT (datetime('now')),
                UltimaActualizacion DATETIME
            );
            INSERT INTO Proveedores_Nueva
                (Id, Nombre, Clave, Telefono, Direccion, SaldoPendiente, FechaRegistro, UltimaActualizacion)
            SELECT Id, Nombre, Clave, Telefono, Direccion,
                   CAST(ROUND(COALESCE(SaldoPendiente, 0) * 100) AS INTEGER),
                   FechaRegistro, UltimaActualizacion
            FROM Proveedores;
            DROP TABLE Proveedores;
            ALTER TABLE Proveedores_Nueva RENAME TO Proveedores;
            CREATE INDEX IF NOT EXISTS IX_Proveedores_Nombre ON Proveedores(Nombre);

            CREATE TABLE Ventas_Nueva (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Fecha DATETIME NOT NULL,
                Usuario TEXT NOT NULL DEFAULT '',
                ClienteId INTEGER,
                MetodoPago TEXT NOT NULL,
                Total INTEGER NOT NULL,
                CostoTotal INTEGER NOT NULL,
                PagoEfectivo INTEGER NOT NULL DEFAULT 0,
                PagoTarjeta INTEGER NOT NULL DEFAULT 0,
                PagoFiado INTEGER NOT NULL DEFAULT 0,
                ComisionTarjeta INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (ClienteId) REFERENCES ClientesFiado(Id)
            );
            INSERT INTO Ventas_Nueva
                (Id, Fecha, Usuario, ClienteId, MetodoPago, Total, CostoTotal,
                 PagoEfectivo, PagoTarjeta, PagoFiado, ComisionTarjeta)
            SELECT Id, Fecha, Usuario, ClienteId, MetodoPago,
                   CAST(ROUND(COALESCE(Total, 0) * 100) AS INTEGER),
                   CAST(ROUND(COALESCE(CostoTotal, 0) * 100) AS INTEGER),
                   CAST(ROUND(COALESCE(PagoEfectivo, 0) * 100) AS INTEGER),
                   CAST(ROUND(COALESCE(PagoTarjeta, 0) * 100) AS INTEGER),
                   CAST(ROUND(COALESCE(PagoFiado, 0) * 100) AS INTEGER),
                   CAST(ROUND(COALESCE(ComisionTarjeta, 0) * 100) AS INTEGER)
            FROM Ventas;
            DROP TABLE Ventas;
            ALTER TABLE Ventas_Nueva RENAME TO Ventas;
            CREATE INDEX IF NOT EXISTS IX_Ventas_Fecha ON Ventas(Fecha);

            CREATE TABLE VentaLineas_Nueva (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                VentaId INTEGER NOT NULL,
                ProductoId INTEGER NOT NULL,
                CodigoBarras TEXT NOT NULL,
                Nombre TEXT NOT NULL,
                Cantidad INTEGER NOT NULL,
                PrecioUnitario INTEGER NOT NULL,
                CostoUnitario INTEGER NOT NULL,
                Importe INTEGER NOT NULL,
                FOREIGN KEY (VentaId) REFERENCES Ventas(Id),
                FOREIGN KEY (ProductoId) REFERENCES Productos(Id)
            );
            INSERT INTO VentaLineas_Nueva
                (Id, VentaId, ProductoId, CodigoBarras, Nombre, Cantidad,
                 PrecioUnitario, CostoUnitario, Importe)
            SELECT Id, VentaId, ProductoId, CodigoBarras, Nombre, Cantidad,
                   CAST(ROUND(COALESCE(PrecioUnitario, 0) * 100) AS INTEGER),
                   CAST(ROUND(COALESCE(CostoUnitario, 0) * 100) AS INTEGER),
                   CAST(ROUND(COALESCE(Importe, 0) * 100) AS INTEGER)
            FROM VentaLineas;
            DROP TABLE VentaLineas;
            ALTER TABLE VentaLineas_Nueva RENAME TO VentaLineas;
            CREATE INDEX IF NOT EXISTS IX_VentaLineas_VentaId ON VentaLineas(VentaId);
            CREATE INDEX IF NOT EXISTS IX_VentaLineas_ProductoId ON VentaLineas(ProductoId);

            CREATE TABLE CortesCaja_Nueva (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Desde DATETIME NOT NULL,
                Hasta DATETIME NOT NULL,
                FondoInicial INTEGER NOT NULL DEFAULT 0,
                VentasEfectivo INTEGER NOT NULL DEFAULT 0,
                VentasTarjeta INTEGER NOT NULL DEFAULT 0,
                VentasFiado INTEGER NOT NULL DEFAULT 0,
                TotalVentas INTEGER NOT NULL DEFAULT 0,
                NumeroVentas INTEGER NOT NULL DEFAULT 0,
                NumeroVentasEfectivo INTEGER NOT NULL DEFAULT 0,
                NumeroVentasTarjeta INTEGER NOT NULL DEFAULT 0,
                NumeroVentasMixto INTEGER NOT NULL DEFAULT 0,
                NumeroVentasFiado INTEGER NOT NULL DEFAULT 0,
                AbonosEfectivo INTEGER NOT NULL DEFAULT 0,
                IngresosCaja INTEGER NOT NULL DEFAULT 0,
                RetirosCaja INTEGER NOT NULL DEFAULT 0,
                GastosCaja INTEGER NOT NULL DEFAULT 0,
                EfectivoEsperado INTEGER NOT NULL DEFAULT 0,
                EfectivoContado INTEGER NOT NULL DEFAULT 0,
                Diferencia INTEGER NOT NULL DEFAULT 0,
                Usuario TEXT NOT NULL DEFAULT '',
                Observaciones TEXT NOT NULL DEFAULT '',
                FechaRegistro DATETIME NOT NULL
            );
            INSERT INTO CortesCaja_Nueva
                (Id, Desde, Hasta, FondoInicial, VentasEfectivo, VentasTarjeta, VentasFiado,
                 TotalVentas, NumeroVentas, NumeroVentasEfectivo, NumeroVentasTarjeta,
                 NumeroVentasMixto, NumeroVentasFiado, AbonosEfectivo, IngresosCaja,
                 RetirosCaja, GastosCaja, EfectivoEsperado, EfectivoContado, Diferencia,
                 Usuario, Observaciones, FechaRegistro)
            SELECT Id, Desde, Hasta,
                   CAST(ROUND(COALESCE(FondoInicial, 0) * 100) AS INTEGER),
                   CAST(ROUND(COALESCE(VentasEfectivo, 0) * 100) AS INTEGER),
                   CAST(ROUND(COALESCE(VentasTarjeta, 0) * 100) AS INTEGER),
                   CAST(ROUND(COALESCE(VentasFiado, 0) * 100) AS INTEGER),
                   CAST(ROUND(COALESCE(TotalVentas, 0) * 100) AS INTEGER),
                   NumeroVentas, NumeroVentasEfectivo, NumeroVentasTarjeta,
                   NumeroVentasMixto, NumeroVentasFiado,
                   CAST(ROUND(COALESCE(AbonosEfectivo, 0) * 100) AS INTEGER),
                   CAST(ROUND(COALESCE(IngresosCaja, 0) * 100) AS INTEGER),
                   CAST(ROUND(COALESCE(RetirosCaja, 0) * 100) AS INTEGER),
                   CAST(ROUND(COALESCE(GastosCaja, 0) * 100) AS INTEGER),
                   CAST(ROUND(COALESCE(EfectivoEsperado, 0) * 100) AS INTEGER),
                   CAST(ROUND(COALESCE(EfectivoContado, 0) * 100) AS INTEGER),
                   CAST(ROUND(COALESCE(Diferencia, 0) * 100) AS INTEGER),
                   Usuario, Observaciones, FechaRegistro
            FROM CortesCaja;
            DROP TABLE CortesCaja;
            ALTER TABLE CortesCaja_Nueva RENAME TO CortesCaja;
            CREATE INDEX IF NOT EXISTS IX_CortesCaja_Hasta ON CortesCaja(Hasta);

            CREATE TABLE MovimientosCaja_Nueva (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Fecha DATETIME NOT NULL,
                Tipo TEXT NOT NULL,
                Monto INTEGER NOT NULL,
                Motivo TEXT NOT NULL DEFAULT '',
                Usuario TEXT NOT NULL DEFAULT ''
            );
            INSERT INTO MovimientosCaja_Nueva (Id, Fecha, Tipo, Monto, Motivo, Usuario)
            SELECT Id, Fecha, Tipo, CAST(ROUND(COALESCE(Monto, 0) * 100) AS INTEGER),
                   Motivo, Usuario
            FROM MovimientosCaja;
            DROP TABLE MovimientosCaja;
            ALTER TABLE MovimientosCaja_Nueva RENAME TO MovimientosCaja;
            CREATE INDEX IF NOT EXISTS IX_MovimientosCaja_Fecha ON MovimientosCaja(Fecha);

            CREATE TABLE AbonosFiado_Nueva (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ClienteId INTEGER NOT NULL,
                Fecha DATETIME NOT NULL,
                Monto INTEGER NOT NULL,
                MetodoPago TEXT NOT NULL DEFAULT 'Efectivo',
                Usuario TEXT NOT NULL DEFAULT '',
                FOREIGN KEY (ClienteId) REFERENCES ClientesFiado(Id)
            );
            INSERT INTO AbonosFiado_Nueva (Id, ClienteId, Fecha, Monto, MetodoPago, Usuario)
            SELECT Id, ClienteId, Fecha, CAST(ROUND(COALESCE(Monto, 0) * 100) AS INTEGER),
                   MetodoPago, Usuario
            FROM AbonosFiado;
            DROP TABLE AbonosFiado;
            ALTER TABLE AbonosFiado_Nueva RENAME TO AbonosFiado;
            CREATE INDEX IF NOT EXISTS IX_AbonosFiado_Fecha ON AbonosFiado(Fecha);
            CREATE INDEX IF NOT EXISTS IX_AbonosFiado_ClienteId ON AbonosFiado(ClienteId);
        ";
        cmd.ExecuteNonQuery();
    }

    public static int VersionMaxima(IReadOnlyList<Migracion> migraciones) =>
        migraciones.Count == 0 ? VersionEsquemaBase : migraciones.Max(m => m.Version);
}

/// <summary>
/// Aplica migraciones pendientes en orden, con respaldo previo y verificación de integridad.
/// </summary>
public sealed class MigradorEsquema
{
    private readonly string _rutaBaseDatos;
    private readonly IReadOnlyList<Migracion> _migraciones;
    private readonly string _directorioRespaldos;
    private readonly string _connectionString;

    public MigradorEsquema(string rutaBaseDatos, IEnumerable<Migracion> migraciones,
        string directorioRespaldos = null)
    {
        _rutaBaseDatos = Path.GetFullPath(rutaBaseDatos);
        _migraciones = (migraciones ?? Array.Empty<Migracion>())
            .OrderBy(m => m.Version)
            .ToList();

        var duplicadas = _migraciones.GroupBy(m => m.Version)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicadas.Count > 0)
            throw new ArgumentException($"Hay versiones de migración duplicadas: {string.Join(", ", duplicadas)}");

        if (_migraciones.Any(m => m.Version <= Migraciones.VersionEsquemaBase))
            throw new ArgumentException(
                $"Las migraciones deben tener versión mayor a {Migraciones.VersionEsquemaBase}");

        _directorioRespaldos = directorioRespaldos
            ?? Path.Combine(Path.GetDirectoryName(_rutaBaseDatos) ?? ".", "respaldos");

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _rutaBaseDatos,
            ForeignKeys = true,
            DefaultTimeout = 30
        }.ToString();
    }

    public int VersionObjetivo => Migraciones.VersionMaxima(_migraciones);

    /// <param name="crearRespaldoPrevio">
    /// Las bases recién creadas no tienen datos que perder y no requieren respaldo.
    /// </param>
    public ProcessResult<ResultadoMigracion> Migrar(bool crearRespaldoPrevio = true)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        var versionInicial = LeerVersion(conn);
        var objetivo = VersionObjetivo;

        if (versionInicial > objetivo)
            return ProcessResult<ResultadoMigracion>.Error(
                $"La base de datos tiene la versión {versionInicial} y esta aplicación solo conoce hasta la {objetivo}. Actualice la aplicación.");

        var pendientes = _migraciones.Where(m => m.Version > versionInicial).ToList();
        if (pendientes.Count == 0)
            return ProcessResult<ResultadoMigracion>.OK(new ResultadoMigracion
            {
                VersionInicial = versionInicial,
                VersionFinal = versionInicial
            });

        var rutaRespaldo = string.Empty;
        if (versionInicial > 0 && crearRespaldoPrevio)
        {
            try
            {
                rutaRespaldo = RespaldoLocal.Crear(_rutaBaseDatos, _directorioRespaldos, "antes-migrar");
            }
            catch (Exception ex)
            {
                return ProcessResult<ResultadoMigracion>.Error(
                    $"No se pudo crear el respaldo previo a la migración; no se aplicó ningún cambio: {ex.Message}");
            }

            if (!RespaldoLocal.Verificar(rutaRespaldo))
                return ProcessResult<ResultadoMigracion>.Error(
                    $"El respaldo previo no pasó la verificación de integridad; no se aplicó ningún cambio: {rutaRespaldo}");
        }

        var aplicadas = new List<int>();

        // Las migraciones pueden reconstruir tablas con claves foráneas; se desactivan
        // mientras dura la migración y se comprueban todas antes de confirmar el resultado.
        CambiarClavesForaneas(conn, activas: false);
        try
        {
            foreach (var migracion in pendientes)
            {
                using var tx = conn.BeginTransaction();
                try
                {
                    migracion.Aplicar(conn, tx);
                    EscribirVersion(conn, tx, migracion.Version);
                    tx.Commit();
                    aplicadas.Add(migracion.Version);
                }
                catch (Exception ex)
                {
                    tx.Rollback();
                    var versionFinal = aplicadas.Count > 0 ? aplicadas[^1] : versionInicial;
                    var detalleRespaldo = string.IsNullOrEmpty(rutaRespaldo)
                        ? "No se aplicaron cambios."
                        : $"La base quedó en la versión {versionFinal}. Respaldo previo: {rutaRespaldo}";

                    return ProcessResult<ResultadoMigracion>.Error(
                        $"Falló la migración {migracion.Version} ({migracion.Descripcion}): {ex.Message}. {detalleRespaldo}");
                }
            }

            if (!ClavesForaneasOk(conn))
            {
                var detalleRespaldo = string.IsNullOrEmpty(rutaRespaldo)
                    ? "No había respaldo previo."
                    : $"Restaure el respaldo: {rutaRespaldo}";

                return ProcessResult<ResultadoMigracion>.Error(
                    $"La verificación de claves foráneas falló después de migrar. {detalleRespaldo}");
            }

            if (!IntegridadOk(conn))
            {
                var detalleRespaldo = string.IsNullOrEmpty(rutaRespaldo)
                    ? "No había respaldo previo."
                    : $"Restaure el respaldo: {rutaRespaldo}";

                return ProcessResult<ResultadoMigracion>.Error(
                    $"La verificación de integridad falló después de migrar. {detalleRespaldo}");
            }

            return ProcessResult<ResultadoMigracion>.OK(new ResultadoMigracion
            {
                VersionInicial = versionInicial,
                VersionFinal = aplicadas[^1],
                VersionesAplicadas = aplicadas,
                RutaRespaldo = rutaRespaldo
            });
        }
        finally
        {
            CambiarClavesForaneas(conn, activas: true);
        }
    }

    private static int LeerVersion(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static void EscribirVersion(SqliteConnection conn, SqliteTransaction tx, int version)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"PRAGMA user_version = {version}";
        cmd.ExecuteNonQuery();
    }

    private static void CambiarClavesForaneas(SqliteConnection conn, bool activas)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = activas ? "PRAGMA foreign_keys = ON" : "PRAGMA foreign_keys = OFF";
        cmd.ExecuteNonQuery();
    }

    private static bool ClavesForaneasOk(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_key_check";
        using var reader = cmd.ExecuteReader();
        return !reader.Read();
    }

    private static bool IntegridadOk(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA integrity_check";
        return string.Equals(Convert.ToString(cmd.ExecuteScalar()), "ok", StringComparison.OrdinalIgnoreCase);
    }
}
