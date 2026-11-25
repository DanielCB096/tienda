// Manifiesto de versión y actualización con respaldo, verificación y reversión.
using System.Reflection;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SistemaTienda.Core;

namespace SistemaTienda.Infrastructure;

/// <summary>
/// Identifica un paquete de la aplicación: versión de la app y versión de esquema
/// que espera. Se publica junto al instalador y la app lo compara con la base.
/// </summary>
public sealed class ManifiestoSistema
{
    public string Producto { get; init; } = "SistemaTienda";
    public string VersionApp { get; init; } = "1.0.0";
    public int VersionEsquema { get; init; } = Migraciones.VersionActual;
    public DateTime Generado { get; init; } = DateTime.Now;

    private static readonly JsonSerializerOptions Opciones = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>Manifiesto de esta compilación.</summary>
    public static ManifiestoSistema Actual() => new()
    {
        VersionApp = VersionDeEstaApp(),
        VersionEsquema = Migraciones.VersionActual,
        Generado = DateTime.Now
    };

    public static string VersionDeEstaApp()
    {
        var version = typeof(ManifiestoSistema).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(ManifiestoSistema).Assembly.GetName().Version?.ToString(3)
            ?? "1.0.0";

        // Quita el sufijo de metadatos del build (+sha) para comparar versiones.
        var corte = version.IndexOf('+');
        return corte < 0 ? version : version[..corte];
    }

    public string ToJson() => JsonSerializer.Serialize(this, Opciones);

    public void Escribir(string ruta)
    {
        var destino = Path.GetFullPath(ruta);
        Directory.CreateDirectory(Path.GetDirectoryName(destino) ?? ".");
        File.WriteAllText(destino, ToJson());
    }

    public static ManifiestoSistema Leer(string ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta))
            throw new FileNotFoundException("No existe el manifiesto indicado.", ruta);

        var manifiesto = JsonSerializer.Deserialize<ManifiestoSistema>(
            File.ReadAllText(ruta), Opciones);

        if (manifiesto == null || string.IsNullOrWhiteSpace(manifiesto.VersionApp))
            throw new InvalidDataException($"El manifiesto no es válido: {ruta}");

        return manifiesto;
    }
}

public sealed class ResultadoActualizacion
{
    public bool Actualizada { get; init; }
    public bool YaAlDia { get; init; }
    public bool Restaurada { get; init; }
    public int VersionInicial { get; init; }
    public int VersionFinal { get; init; }
    public string RutaRespaldo { get; init; } = string.Empty;
    public string Mensaje { get; init; } = string.Empty;
}

/// <summary>
/// Actualiza el esquema con respaldo previo verificado. Si una migración falla o
/// la base no pasa la verificación posterior, restaura el respaldo y deja la
/// versión anterior operativa. Es la ruta que usa la app al arrancar.
/// </summary>
public sealed class ActualizadorEsquema
{
    private readonly string _rutaBaseDatos;
    private readonly string _directorioRespaldos;
    private readonly IReadOnlyList<Migracion> _migraciones;

    public ActualizadorEsquema(string rutaBaseDatos, string directorioRespaldos = null,
        IEnumerable<Migracion> migraciones = null)
    {
        _rutaBaseDatos = Path.GetFullPath(rutaBaseDatos);
        _directorioRespaldos = directorioRespaldos
            ?? RespaldoLocal.DirectorioPorDefecto(_rutaBaseDatos);
        _migraciones = (migraciones ?? Migraciones.ObtenerTodas()).ToList();
    }

    public int VersionObjetivo => Migraciones.VersionMaxima(_migraciones);

    public ResultadoActualizacion Aplicar(bool crearRespaldoPrevio = true)
    {
        if (!File.Exists(_rutaBaseDatos))
            return Error("No existe la base de datos a actualizar.");

        var versionInicial = LeerVersion(_rutaBaseDatos);
        if (versionInicial > VersionObjetivo)
            return new ResultadoActualizacion
            {
                VersionInicial = versionInicial,
                VersionFinal = versionInicial,
                Mensaje = $"La base tiene el esquema {versionInicial} y esta aplicación solo conoce " +
                          $"hasta el {VersionObjetivo}. Instala una versión más nueva."
            };

        if (versionInicial == VersionObjetivo)
            return new ResultadoActualizacion
            {
                YaAlDia = true,
                VersionInicial = versionInicial,
                VersionFinal = versionInicial,
                Mensaje = $"La base ya está en el esquema {versionInicial}."
            };

        var respaldo = string.Empty;
        if (crearRespaldoPrevio)
        {
            try
            {
                respaldo = RespaldoLocal.Crear(_rutaBaseDatos, _directorioRespaldos, "antes-migrar");
            }
            catch (Exception ex)
            {
                return Error($"No se pudo crear el respaldo previo; no se aplicó ningún cambio: {ex.Message}");
            }

            if (!RespaldoLocal.Verificar(respaldo))
                return Error($"El respaldo previo no pasó la verificación; no se aplicó ningún cambio: {respaldo}");
        }

        var migrador = new MigradorEsquema(_rutaBaseDatos, _migraciones, _directorioRespaldos);
        var migracion = migrador.Migrar(crearRespaldoPrevio: false);

        if (migracion.IsError)
        {
            var restaurada = RestaurarSiSePuede(respaldo);
            return new ResultadoActualizacion
            {
                VersionInicial = versionInicial,
                VersionFinal = LeerVersion(_rutaBaseDatos),
                RutaRespaldo = respaldo,
                Restaurada = restaurada,
                Mensaje = migracion.Message + (restaurada
                    ? " Se restauró el respaldo y la versión anterior sigue operativa."
                    : " No había respaldo previo que restaurar.")
            };
        }

        if (!VerificarBaseDatos(_rutaBaseDatos))
        {
            var restaurada = RestaurarSiSePuede(respaldo);
            return new ResultadoActualizacion
            {
                VersionInicial = versionInicial,
                VersionFinal = LeerVersion(_rutaBaseDatos),
                RutaRespaldo = respaldo,
                Restaurada = restaurada,
                Mensaje = "La base no pasó la verificación después de migrar." + (restaurada
                    ? " Se restauró el respaldo y la versión anterior sigue operativa."
                    : " No había respaldo previo que restaurar.")
            };
        }

        var final = migracion.Value?.VersionFinal ?? VersionObjetivo;
        return new ResultadoActualizacion
        {
            Actualizada = true,
            VersionInicial = versionInicial,
            VersionFinal = final,
            RutaRespaldo = respaldo,
            Mensaje = $"Esquema actualizado de la versión {versionInicial} a la {final}."
        };
    }

    /// <summary>Restaura manualmente un respaldo y deja la base como estaba antes de actualizar.</summary>
    public string Revertir(string rutaRespaldo) =>
        RespaldoLocal.Restaurar(rutaRespaldo, _rutaBaseDatos, _directorioRespaldos);

    /// <summary>
    /// Arranque de prueba: integridad, claves foráneas, tablas esenciales y una
    /// escritura que se revierte. No deja rastro en los datos.
    /// </summary>
    public static bool VerificarBaseDatos(string rutaBaseDatos)
    {
        if (string.IsNullOrWhiteSpace(rutaBaseDatos) || !File.Exists(rutaBaseDatos))
            return false;

        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = Path.GetFullPath(rutaBaseDatos),
                ForeignKeys = true,
                DefaultTimeout = 30,
                Pooling = false
            }.ToString();

            using var conn = new SqliteConnection(connectionString);
            conn.Open();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA integrity_check";
                if (!string.Equals(Convert.ToString(cmd.ExecuteScalar()), "ok",
                        StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA foreign_key_check";
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                    return false;
            }

            foreach (var tabla in TablasEsenciales)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @tabla";
                cmd.Parameters.AddWithValue("@tabla", tabla);
                if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
                    return false;
            }

            // Escritura de prueba: comprueba que la base acepta cambios y que los
            // disparadores de la bitácora no bloquean un alta legítima.
            using (var tx = conn.BeginTransaction())
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT INTO Bitacora
                        (Fecha, Usuario, Accion, Entidad, EntidadId, DatosAntes, DatosDespues, Motivo)
                    VALUES (datetime('now'), 'Sistema', 'Verificacion', 'BaseDatos', '',
                            '', '', 'Arranque de prueba');";
                cmd.ExecuteNonQuery();
                tx.Rollback();
            }

            return true;
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

    private static readonly string[] TablasEsenciales =
    {
        "ConfiguracionTienda", "UsuariosSistema", "Productos", "MovimientoInventario",
        "Ventas", "VentaLineas", "CortesCaja", "MovimientosCaja", "Bitacora",
        "ClientesFiado", "Proveedores"
    };

    private bool RestaurarSiSePuede(string respaldo)
    {
        if (string.IsNullOrWhiteSpace(respaldo) || !File.Exists(respaldo))
            return false;

        try
        {
            RespaldoLocal.Restaurar(respaldo, _rutaBaseDatos, _directorioRespaldos);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static ResultadoActualizacion Error(string mensaje) => new() { Mensaje = mensaje };

    private static int LeerVersion(string rutaBaseDatos)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(rutaBaseDatos),
            DefaultTimeout = 30,
            Pooling = false
        }.ToString();

        using var conn = new SqliteConnection(connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
}
