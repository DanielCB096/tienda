// Entidad de configuración global de la tienda

public class ConfiguracionTienda
{
    public int Id { get; set; }
    public string NombreTienda { get; set; } = string.Empty;
    public string Giro { get; set; } = string.Empty;
    public decimal ImpuestoPredeterminado { get; set; } // Ej: 16.00 para IVA 16%
    public decimal ComisionTarjeta { get; set; } // Ej: 3.50 para 3.5%
    public decimal FondoCajaInicial { get; set; } // Ej: 200.00
    public bool FiadoActivado { get; set; }
    public decimal LimiCreditoCliente { get; set; } // 0 = sin límite
    public int RespaldoIntervaloDias { get; set; } // 0 = deshabilitado
    public string UsuarioDueñoPINHash { get; set; } = string.Empty; // Hash SHA-256
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
    public string VersionApp { get; set; } = string.Empty;
}

public enum RolUsuario
{
    Dueño,
    Encargado,
    Empleado
}

public class UsuarioSistema
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string PINHash { get; set; } = string.Empty;
    public RolUsuario Rol { get; set; }
    public string PermisosEspeciales { get; set; } = string.Empty; // JSON o lista CSV
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
}

public static class ConfiguracionPorDefecto
{
    // Valores por defecto que usa el asistente inicial
    public static string GiroPredeterminado = "Abarrotes";
    public static decimal ImpuestoPredeterminado = 16.00m;
    public static decimal ComisionTarjetaPredeterminada = 3.50m;
    public static decimal FondoCajaInicialPredeterminado = 200.00m;
    public static int RespaldoIntervaloDiasPredeterminado = 1; // cada 1 día
}