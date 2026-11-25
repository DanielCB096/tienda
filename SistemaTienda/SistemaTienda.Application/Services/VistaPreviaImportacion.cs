namespace SistemaTienda.Application.Services;

/// <summary>
/// Resultado de validar un archivo de productos sin escribir nada. La interfaz
/// lo muestra antes de confirmar la importación.
/// </summary>
public sealed class VistaPreviaImportacion
{
    public int FilasLeidas { get; init; }
    public int FilasValidas { get; init; }
    public int FilasOmitidas { get; init; }
    public IReadOnlyList<ErrorImportacionProducto> Errores { get; init; } =
        Array.Empty<ErrorImportacionProducto>();

    /// <summary>Primeras 20 filas válidas, solo para confirmar que es el archivo correcto.</summary>
    public IReadOnlyList<FilaVistaPreviaImportacion> Muestra { get; init; } =
        Array.Empty<FilaVistaPreviaImportacion>();
}

public sealed class FilaVistaPreviaImportacion
{
    public int NumeroFila { get; init; }
    public string CodigoBarras { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public long Precio { get; init; }
    public int Stock { get; init; }
}
