namespace SistemaTienda.Core;

/// <summary>
/// Lote de importación de productos. Agrupa las filas que entraron juntas para
/// poder auditarlas y revertirlas sin borrar historial.
/// </summary>
public class Importacion
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    public string Usuario { get; set; } = string.Empty;
    public string NombreArchivo { get; set; } = string.Empty;
    public string HashArchivo { get; set; } = string.Empty;
    public int FilasLeidas { get; set; }
    public int FilasValidas { get; set; }
    public int FilasOmitidas { get; set; }
    public int ProductosInsertados { get; set; }
    public string Estado { get; set; } = EstadoImportacion.Aplicada;
    public DateTime? FechaReversion { get; set; }
    public string UsuarioReversion { get; set; } = string.Empty;

    public bool Revertida => string.Equals(Estado, EstadoImportacion.Revertida,
        StringComparison.OrdinalIgnoreCase);
}

public static class EstadoImportacion
{
    public const string Aplicada = "Aplicada";
    public const string Revertida = "Revertida";
}
