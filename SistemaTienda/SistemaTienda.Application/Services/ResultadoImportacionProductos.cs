namespace SistemaTienda.Application.Services;

public sealed record ErrorImportacionCsv(int NumeroFila, string Mensaje);

public sealed class ResultadoImportacionProductos
{
    public int ProductosImportados { get; init; }
    public IReadOnlyList<ErrorImportacionCsv> Errores { get; init; } =
        Array.Empty<ErrorImportacionCsv>();
}
