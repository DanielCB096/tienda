// Entidad de configuración global de la tienda
namespace SistemaTienda.Core;

public class ConfiguracionTienda
{
    public int Id { get; set; }
    public string NombreTienda { get; set; } = string.Empty;
    public string Giro { get; set; } = string.Empty;
    public decimal ImpuestoPredeterminado { get; set; }
    public decimal ComisionTarjeta { get; set; }
    public decimal FondoCajaInicial { get; set; }
    public bool FiadoActivado { get; set; }
    public decimal LimiCreditoCliente { get; set; }
    public int RespaldoIntervaloDias { get; set; }
    public string UsuarioDueñoPINHash { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
    public string VersionApp { get; set; } = string.Empty;
}

public enum RolUsuario
{
    Dueño = 0,
    Encargado = 1,
    Empleado = 2
}

public class UsuarioSistema
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string PINHash { get; set; } = string.Empty;
    public RolUsuario Rol { get; set; }
    public string PermisosEspeciales { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
}

public static class ConfiguracionPorDefecto
{
    public const string GiroPredeterminado = "Abarrotes";
    public const decimal ImpuestoPredeterminado = 16.00m;
    public const decimal ComisionTarjetaPredeterminada = 3.50m;
    public const decimal FondoCajaInicialPredeterminado = 200.00m;
    public const int RespaldoIntervaloDiasPredeterminado = 1;
}
