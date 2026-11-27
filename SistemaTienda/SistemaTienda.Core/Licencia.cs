namespace SistemaTienda.Core;

/// <summary>
/// Contenido firmado de una licencia de POSale. No se ata al hardware: el
/// archivo se puede mover a otra instalación y la verificación es solo de
/// firma y datos. La licencia es perpetua; el soporte tiene su propia fecha.
/// </summary>
public sealed class Licencia
{
    public int Version { get; set; } = 1;
    public string Producto { get; set; } = LicenciaFirma.Producto;
    public string Cliente { get; set; } = string.Empty;
    public string LicenciaId { get; set; } = string.Empty;
    public DateTime Emitida { get; set; } = DateTime.Today;
    public string Tipo { get; set; } = "Perpetua";
    public int Cajas { get; set; } = 1;
    public DateTime? SoporteHasta { get; set; }
    public string Notas { get; set; } = string.Empty;
}

/// <summary>Archivo portable: el payload va en base64 y la firma RSA-PSS.</summary>
public sealed class LicenciaArchivo
{
    public string Payload { get; set; } = string.Empty;
    public string Firma { get; set; } = string.Empty;
}

public enum NivelLicencia
{
    Licencia,
    Prueba,
    Aviso,
    Invalida
}

/// <summary>Estado que la app muestra sin bloquear la operación.</summary>
public sealed class EstadoLicencia
{
    public NivelLicencia Nivel { get; set; } = NivelLicencia.Prueba;
    public string Titulo { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
    public bool TieneLicencia { get; set; }
    public bool SoporteVencido { get; set; }
    public string Cliente { get; set; } = string.Empty;
    public string LicenciaId { get; set; } = string.Empty;
    public DateTime? SoporteHasta { get; set; }
    public int DiasRestantesPrueba { get; set; }

    /// <summary>Se muestra en Inicio cuando la prueba vence o la licencia falla.</summary>
    public bool MostrarAviso => Nivel != NivelLicencia.Licencia || SoporteVencido;
}
