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
    /// Migraciones pendientes de producción. La versión 1 es el esquema base.
    /// Al agregar una migración, sube su versión y añade su prueba correspondiente.
    /// </summary>
    public static IReadOnlyList<Migracion> ObtenerTodas() => Array.Empty<Migracion>();

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

    public ProcessResult<ResultadoMigracion> Migrar()
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
        if (versionInicial > 0)
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

    private static bool IntegridadOk(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA integrity_check";
        return string.Equals(Convert.ToString(cmd.ExecuteScalar()), "ok", StringComparison.OrdinalIgnoreCase);
    }
}
