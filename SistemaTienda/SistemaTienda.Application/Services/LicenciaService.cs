using System.Text.Json;
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Application.Services;

/// <summary>
/// Licencia offline y prueba de 30 días. La licencia es informativa: al vencer
/// la prueba o si la licencia falta, la app solo avisa; nunca bloquea ventas.
/// El archivo no se ata al hardware.
/// </summary>
public sealed class LicenciaService
{
    public const int DiasPrueba = 30;
    public const int DiasAviso = 7;

    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly DatabaseTools _db;
    private readonly BitacoraService _bitacora;
    private readonly string _clavePublicaPem;

    public string RutaLicencia { get; }
    public string RutaEstado { get; }

    public LicenciaService(DatabaseTools db, string clavePublicaPem = null)
    {
        _db = db;
        _bitacora = new BitacoraService(db);
        _clavePublicaPem = clavePublicaPem ?? LicenciaPublica.Pem;

        var directorio = Path.GetDirectoryName(db.RutaBaseDatos) ?? ".";
        RutaLicencia = Path.Combine(directorio, "licencia.poslic");
        RutaEstado = Path.Combine(directorio, "licencia-estado.json");
    }

    /// <summary>Se llama al arrancar: registra la primera apertura y actualiza la última.</summary>
    public void Iniciar()
    {
        var estado = LeerEstado() ?? new EstadoArchivo
        {
            InicioPrueba = DateTime.Now
        };

        estado.UltimaApertura = DateTime.Now;
        GuardarEstado(estado);
    }

    public EstadoLicencia ObtenerEstado()
    {
        if (File.Exists(RutaLicencia))
        {
            try
            {
                var contenido = File.ReadAllText(RutaLicencia);
                if (!LicenciaFirma.LeerYVerificar(contenido, _clavePublicaPem,
                        out var licencia, out var error))
                {
                    return new EstadoLicencia
                    {
                        Nivel = NivelLicencia.Invalida,
                        Titulo = "Licencia no válida",
                        Detalle = error + " La app sigue funcionando, pero importa una licencia correcta."
                    };
                }

                var soporteVencido = licencia.SoporteHasta.HasValue &&
                                     licencia.SoporteHasta.Value.Date < DateTime.Today;
                var detalle = $"Cliente: {licencia.Cliente} · Folio: {licencia.LicenciaId} · " +
                              $"{licencia.Cajas} caja(s)";
                if (licencia.SoporteHasta.HasValue)
                {
                    detalle += soporteVencido
                        ? $" · Soporte vencido el {licencia.SoporteHasta:dd/MM/yyyy}"
                        : $" · Soporte hasta {licencia.SoporteHasta:dd/MM/yyyy}";
                }

                return new EstadoLicencia
                {
                    Nivel = NivelLicencia.Licencia,
                    Titulo = "Licencia activa",
                    Detalle = detalle,
                    TieneLicencia = true,
                    SoporteVencido = soporteVencido,
                    Cliente = licencia.Cliente,
                    LicenciaId = licencia.LicenciaId,
                    SoporteHasta = licencia.SoporteHasta,
                    DiasRestantesPrueba = 0
                };
            }
            catch (Exception ex)
            {
                return new EstadoLicencia
                {
                    Nivel = NivelLicencia.Invalida,
                    Titulo = "Licencia no válida",
                    Detalle = ex.Message
                };
            }
        }

        var estado = LeerEstado();
        var inicio = estado?.InicioPrueba.Date ?? DateTime.Today;
        var transcurridos = Math.Max(0, (DateTime.Today - inicio).Days);
        var restantes = Math.Max(0, DiasPrueba - transcurridos);

        if (restantes == 0)
        {
            return new EstadoLicencia
            {
                Nivel = NivelLicencia.Aviso,
                Titulo = "Prueba terminada",
                Detalle = "La prueba de 30 días terminó. Puedes seguir usando la app; " +
                          "importa tu licencia para quedar al día.",
                DiasRestantesPrueba = 0
            };
        }

        if (restantes <= DiasAviso)
        {
            return new EstadoLicencia
            {
                Nivel = NivelLicencia.Aviso,
                Titulo = "Prueba por terminar",
                Detalle = $"Quedan {restantes} día(s) de prueba. Importa tu licencia cuando la tengas.",
                DiasRestantesPrueba = restantes
            };
        }

        return new EstadoLicencia
        {
            Nivel = NivelLicencia.Prueba,
            Titulo = "Prueba",
            Detalle = $"Quedan {restantes} día(s) de prueba.",
            DiasRestantesPrueba = restantes
        };
    }

    public ProcessResult<EstadoLicencia> ImportarContenido(string contenido)
    {
        var autorizacion = AutorizacionService.Exigir(
            SesionActual.Usuario, PermisoSistema.Configurar);
        if (autorizacion.IsError)
            return ProcessResult<EstadoLicencia>.Error(autorizacion.Message);

        if (string.IsNullOrWhiteSpace(contenido))
            return ProcessResult<EstadoLicencia>.Error("Pega el contenido de la licencia");

        if (!LicenciaFirma.LeerYVerificar(contenido, _clavePublicaPem,
                out var licencia, out var error))
            return ProcessResult<EstadoLicencia>.Error(error);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RutaLicencia)!);
            var temporal = RutaLicencia + ".tmp";
            File.WriteAllText(temporal, contenido);
            File.Move(temporal, RutaLicencia, overwrite: true);
        }
        catch (Exception ex)
        {
            return ProcessResult<EstadoLicencia>.Error(
                $"No se pudo guardar la licencia: {ex.Message}");
        }

        _bitacora.Registrar(AccionBitacora.Licencia, "Licencia", licencia.LicenciaId,
            datosDespues: $"Cliente={licencia.Cliente};Cajas={licencia.Cajas};" +
                          $"SoporteHasta={(licencia.SoporteHasta?.ToString("yyyy-MM-dd") ?? "sin fecha")}",
            motivo: "Importación de licencia");

        return ProcessResult<EstadoLicencia>.OK(ObtenerEstado(),
            $"Licencia importada para {licencia.Cliente}");
    }

    public ProcessResult<EstadoLicencia> ImportarArchivo(string ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta))
            return ProcessResult<EstadoLicencia>.Error("No se encontró el archivo de licencia");

        try
        {
            return ImportarContenido(File.ReadAllText(ruta));
        }
        catch (Exception ex)
        {
            return ProcessResult<EstadoLicencia>.Error(
                $"No se pudo leer la licencia: {ex.Message}");
        }
    }

    private EstadoArchivo LeerEstado()
    {
        try
        {
            if (!File.Exists(RutaEstado))
                return null;

            return JsonSerializer.Deserialize<EstadoArchivo>(
                File.ReadAllText(RutaEstado), Opciones);
        }
        catch
        {
            return null;
        }
    }

    private void GuardarEstado(EstadoArchivo estado)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RutaEstado)!);
            File.WriteAllText(RutaEstado, JsonSerializer.Serialize(estado, Opciones));
        }
        catch
        {
            // El estado de prueba es informativo: no debe impedir operar.
        }
    }

    private sealed class EstadoArchivo
    {
        public DateTime InicioPrueba { get; set; } = DateTime.Now;
        public DateTime UltimaApertura { get; set; } = DateTime.Now;
    }
}
