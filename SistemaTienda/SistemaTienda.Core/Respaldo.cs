namespace SistemaTienda.Core;

public enum EstadoRespaldoAutomatico
{
    Deshabilitado,
    Vigente,
    Creado
}

/// <summary>
/// Resultado de revisar el intervalo de respaldo configurado al iniciar o cerrar la aplicación.
/// </summary>
public sealed class ResultadoRespaldoAutomatico
{
    public EstadoRespaldoAutomatico Estado { get; init; }
    public DateTime? UltimoRespaldo { get; init; }
    public string Ruta { get; init; } = string.Empty;
}
