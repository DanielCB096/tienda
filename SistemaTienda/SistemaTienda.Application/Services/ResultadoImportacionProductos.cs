namespace SistemaTienda.Application.Services;

public sealed record ErrorImportacionProducto(int NumeroFila, string Mensaje);

public sealed class ResultadoImportacionProductos
{
    public int ProductosImportados { get; init; }
    public int FilasOmitidas { get; init; }
    public int ImportacionId { get; init; }
    public IReadOnlyList<ErrorImportacionProducto> Errores { get; init; } =
        Array.Empty<ErrorImportacionProducto>();
}
