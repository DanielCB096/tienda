// Respaldos locales del archivo SQLite: crear, listar, verificar y restaurar
using Microsoft.Data.Sqlite;

namespace SistemaTienda.Infrastructure;

public sealed class ArchivoRespaldo
{
    public string Ruta { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public DateTime Fecha { get; init; }
    public long TamanoBytes { get; init; }
}

/// <summary>
/// Copias consistentes del archivo .db mediante VACUUM INTO. Sin nube ni servicios externos.
/// La restauración requiere que no haya conexiones abiertas a la base.
/// </summary>
public static class RespaldoLocal
{
    public static string DirectorioPorDefecto(string rutaBaseDatos) =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(rutaBaseDatos)) ?? ".", "respaldos");

    public static string Crear(string rutaBaseDatos, string directorioRespaldos, string etiqueta = null)
    {
        var origen = Path.GetFullPath(rutaBaseDatos);
        if (!File.Exists(origen))
            throw new FileNotFoundException("No existe la base de datos a respaldar.", origen);

        Directory.CreateDirectory(directorioRespaldos);

        var sufijo = string.IsNullOrWhiteSpace(etiqueta) ? string.Empty : "-" + LimpiarEtiqueta(etiqueta);
        var marca = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var destino = Path.Combine(directorioRespaldos, $"tienda-{marca}{sufijo}.db");

        var indice = 1;
        while (File.Exists(destino))
            destino = Path.Combine(directorioRespaldos, $"tienda-{marca}{sufijo}-{indice++}.db");

        using (var conn = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = origen, DefaultTimeout = 30 }.ToString()))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"VACUUM INTO '{destino.Replace("'", "''")}'";
            cmd.ExecuteNonQuery();
        }

        if (!Verificar(destino))
            throw new InvalidDataException($"El respaldo creado no pasó la verificación de integridad: {destino}");

        return destino;
    }

    public static bool Verificar(string rutaRespaldo)
    {
        if (string.IsNullOrWhiteSpace(rutaRespaldo) || !File.Exists(rutaRespaldo))
            return false;

        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = Path.GetFullPath(rutaRespaldo),
                Mode = SqliteOpenMode.ReadOnly,
                DefaultTimeout = 30
            }.ToString();

            using var conn = new SqliteConnection(connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA integrity_check";
            return string.Equals(Convert.ToString(cmd.ExecuteScalar()), "ok", StringComparison.OrdinalIgnoreCase);
        }
        catch (SqliteException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public static IReadOnlyList<ArchivoRespaldo> Listar(string directorioRespaldos)
    {
        if (!Directory.Exists(directorioRespaldos))
            return Array.Empty<ArchivoRespaldo>();

        return new DirectoryInfo(directorioRespaldos)
            .GetFiles("tienda-*.db")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Select(f => new ArchivoRespaldo
            {
                Ruta = f.FullName,
                Nombre = f.Name,
                Fecha = f.LastWriteTime,
                TamanoBytes = f.Length
            })
            .ToList();
    }

    public static DateTime? Ultimo(string directorioRespaldos) =>
        Listar(directorioRespaldos).FirstOrDefault()?.Fecha;

    /// <summary>
    /// Reemplaza la base actual por el respaldo, creando antes un respaldo de seguridad.
    /// Devuelve la ruta del respaldo de seguridad (vacía si no existía base).
    /// </summary>
    public static string Restaurar(string rutaRespaldo, string rutaBaseDatos, string directorioRespaldos)
    {
        var respaldo = Path.GetFullPath(rutaRespaldo);
        var destino = Path.GetFullPath(rutaBaseDatos);

        if (!File.Exists(respaldo))
            throw new FileNotFoundException("No existe el respaldo indicado.", respaldo);

        if (!Verificar(respaldo))
            throw new InvalidDataException($"El respaldo no pasó la verificación de integridad: {respaldo}");

        var respaldoSeguridad = string.Empty;
        if (File.Exists(destino))
            respaldoSeguridad = Crear(destino, directorioRespaldos, "antes-restaurar");

        SqliteConnection.ClearAllPools();
        File.Copy(respaldo, destino, overwrite: true);
        EliminarArchivosWaL(destino);

        if (!Verificar(destino))
        {
            if (!string.IsNullOrEmpty(respaldoSeguridad))
            {
                SqliteConnection.ClearAllPools();
                File.Copy(respaldoSeguridad, destino, overwrite: true);
                EliminarArchivosWaL(destino);
            }

            throw new InvalidDataException(
                string.IsNullOrEmpty(respaldoSeguridad)
                    ? "La restauración falló y no había respaldo de seguridad."
                    : $"La restauración falló y se revirtió al estado anterior. Respaldo de seguridad: {respaldoSeguridad}");
        }

        return respaldoSeguridad;
    }

    private static void EliminarArchivosWaL(string rutaBaseDatos)
    {
        foreach (var sufijo in new[] { "-wal", "-shm" })
        {
            var archivo = rutaBaseDatos + sufijo;
            if (File.Exists(archivo))
                File.Delete(archivo);
        }
    }

    private static string LimpiarEtiqueta(string etiqueta) =>
        new(etiqueta.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '-')
            .ToArray());
}
