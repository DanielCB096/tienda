// Entidad de configuración global de la tienda
namespace SistemaTienda.Core;

public class ConfiguracionTienda
{
    public int Id { get; set; }
    public string NombreTienda { get; set; } = string.Empty;
    public string Giro { get; set; } = string.Empty;
    /// <summary>Porcentaje de impuesto; no es un importe.</summary>
    public decimal ImpuestoPredeterminado { get; set; }
    /// <summary>Porcentaje de comisión bancaria; no es un importe.</summary>
    public decimal ComisionTarjeta { get; set; }
    /// <summary>Fondo de caja en centavos.</summary>
    public long FondoCajaInicial { get; set; }
    public bool FiadoActivado { get; set; }
    /// <summary>Límite de crédito por cliente en centavos; 0 = sin límite.</summary>
    public long LimiCreditoCliente { get; set; }
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
    public const long FondoCajaInicialPredeterminado = 20_000; // $200.00 en centavos
    public const int RespaldoIntervaloDiasPredeterminado = 1;
}
