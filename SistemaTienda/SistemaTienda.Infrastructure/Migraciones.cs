// Runner idempotente de migraciones versionadas de esquema (regla de negocio #11).
// Cada migración va de una versión a la siguiente y se aplica dentro de una
// transacción: si algo falla, todo se revierte y la base queda intacta.
// Antes de migrar una base que ya tiene datos se crea un respaldo obligatorio
// y se verifica su integridad; sin respaldo verificado no hay migración.
using Microsoft.Data.Sqlite;

namespace SistemaTienda.Infrastructure;

/// <summary>
/// Una migración de esquema: de <see cref="DesdeVersion"/> a <see cref="HastaVersion"/>.
/// La acción recibe la conexión y la transacción abiertas por el runner.
/// </summary>
public sealed class Migracion
{
    public required int DesdeVersion { get; init; }
    public required int HastaVersion { get; init; }
    public required string Nombre { get; init; }
    public required Action<SqliteConnection, SqliteTransaction> Aplicar { get; init; }
}

/// <summary>
/// Resultado de un intento de migración. No lanza excepciones por fallas de
/// migración: el error viene en <see cref="IsError"/> y <see cref="Message"/>.
/// </summary>
public sealed class ResultadoMigracion
{
    public bool FueNecesaria { get; init; }
    public int VersionInicial { get; init; }
    public int VersionFinal { get; init; }
    public string RespaldoRuta { get; init; } = string.Empty;
    public bool IsError { get; init; }
    public string Message { get; init; } = string.Empty;

    public static ResultadoMigracion SinCambios(int version) => new()
    {
        FueNecesaria = false,
        VersionInicial = version,
        VersionFinal = version
    };

    public static ResultadoMigracion Error(string message, int versionInicial) => new()
    {
        FueNecesaria = true,
        VersionInicial = versionInicial,
        VersionFinal = versionInicial,
        IsError = true,
        Message = message
    };
}

/// <summary>
/// Migraciones registradas de la aplicación. La versión actual del esquema es
/// la versión destino de la última migración de esta lista.
/// </summary>
public static class MigracionesRegistradas
{
    public const int VersionActual = 2;

    public static IReadOnlyList<Migracion> Lista() => new[]
    {
        new Migracion
        {
            DesdeVersion = 1,
            HastaVersion = 2,
            Nombre = "Agrega Activo a Productos y tabla MigracionesAplicadas",
            Aplicar = (conn, tx) =>
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    ALTER TABLE Productos ADD COLUMN Activo INTEGER NOT NULL DEFAULT 1;";
                cmd.ExecuteNonQuery();
            }
        }
    };
}

/// <summary>
/// Aplica las migraciones pendientes en orden, de forma idempotente.
/// </summary>
public sealed class MigracionRunner
{
    private readonly DatabaseTools _db;
    private readonly IReadOnlyList<Migracion> _migraciones;

    public MigracionRunner(DatabaseTools db, IEnumerable<Migracion> migraciones)
    {
        _db = db;
        _migraciones = migraciones.OrderBy(m => m.DesdeVersion).ToList();
    }

    /// <summary>
    /// Lleva la base a la última versión registrada.
    /// </summary>
    /// <param name="respaldoObligatorio">
    /// Cuando es true (valor por defecto) y hay migraciones pendientes sobre una
    /// base que ya tiene versión, se crea y verifica un respaldo antes de migrar.
    /// Solo se omite para una base recién creada, que no tiene datos que proteger.
    /// </param>
    public ResultadoMigracion Migrar(bool respaldoObligatorio = true)
    {
        var versionInicial = _db.ObtenerVersionEsquema();
        var versionDestino = _migraciones.Count == 0
            ? versionInicial
            : _migraciones.Max(m => m.HastaVersion);

        if (versionInicial > versionDestino)
        {
            return ResultadoMigracion.Error(
                $"La base de datos es más nueva que la aplicación (versión {versionInicial}, " +
                $"la aplicación solo conoce hasta la versión {versionDestino}). " +
                "Actualiza la aplicación antes de abrir esta base de datos.",
                versionInicial);
        }

        var pendientes = _migraciones.Where(m => m.DesdeVersion >= versionInicial).ToList();
        if (pendientes.Count == 0)
            return ResultadoMigracion.SinCambios(versionInicial);

        // La cadena debe ser continua desde la versión actual.
        if (pendientes[0].DesdeVersion != versionInicial)
        {
            return ResultadoMigracion.Error(
                $"No hay migración registrada desde la versión {versionInicial} del esquema. " +
                "La base de datos no se modificó.",
                versionInicial);
        }

        for (var i = 1; i < pendientes.Count; i++)
        {
            if (pendientes[i].DesdeVersion != pendientes[i - 1].HastaVersion)
            {
                return ResultadoMigracion.Error(
                    $"La cadena de migraciones está rota entre la versión {pendientes[i - 1].HastaVersion} " +
                    $"y la versión {pendientes[i].DesdeVersion}. La base de datos no se modificó.",
                    versionInicial);
            }
        }

        var respaldoRuta = string.Empty;
        if (respaldoObligatorio && versionInicial >= 1)
        {
            var respaldo = _db.CrearRespaldoSeguro();
            if (respaldo.IsError)
            {
                return ResultadoMigracion.Error(
                    $"No se pudo crear el respaldo previo a la migración: {respaldo.Message} " +
                    "La base de datos no se modificó.",
                    versionInicial);
            }

            respaldoRuta = respaldo.Value;
        }

        try
        {
            using var conn = _db.CreateConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS MigracionesAplicadas (
                        Version INTEGER PRIMARY KEY,
                        Nombre TEXT NOT NULL DEFAULT '',
                        FechaAplicacion DATETIME NOT NULL DEFAULT (datetime('now')),
                        RespaldoRuta TEXT NOT NULL DEFAULT ''
                    );";
                cmd.ExecuteNonQuery();
            }

            foreach (var migracion in pendientes)
            {
                migracion.Aplicar(conn, tx);

                using var registro = conn.CreateCommand();
                registro.Transaction = tx;
                registro.CommandText = @"
                    INSERT INTO MigracionesAplicadas (Version, Nombre, RespaldoRuta)
                    VALUES (@version, @nombre, @respaldo);";
                registro.Parameters.AddWithValue("@version", migracion.HastaVersion);
                registro.Parameters.AddWithValue("@nombre", migracion.Nombre);
                registro.Parameters.AddWithValue("@respaldo", respaldoRuta);
                registro.ExecuteNonQuery();

                using var version = conn.CreateCommand();
                version.Transaction = tx;
                version.CommandText = $"PRAGMA user_version = {migracion.HastaVersion}";
                version.ExecuteNonQuery();
            }

            tx.Commit();

            return new ResultadoMigracion
            {
                FueNecesaria = true,
                VersionInicial = versionInicial,
                VersionFinal = versionDestino,
                RespaldoRuta = respaldoRuta
            };
        }
        catch (Exception ex)
        {
            // La transacción se revierte al salir del using sin commit. Como
            // seguridad extra, se restaura el respaldo si existe.
            if (!string.IsNullOrEmpty(respaldoRuta))
                _db.RestaurarDesdeRespaldo(respaldoRuta);

            return ResultadoMigracion.Error(
                $"La migración de la versión {versionInicial} a la versión {versionDestino} falló: {ex.Message} " +
                "La base de datos quedó en su estado original.",
                versionInicial);
        }
    }
}
