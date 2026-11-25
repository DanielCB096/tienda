using System.Text;

namespace SistemaTienda.Application.Services;

/// <summary>
/// Plantilla fija de productos para el dueño. El orden de las columnas se puede
/// cambiar; los encabezados deben conservarse.
/// </summary>
public static class PlantillaProductos
{
    public const string NombreArchivo = "plantilla-productos.csv";

    public const string Encabezados =
        "CodigoBarras,Nombre,Costo,Precio,Stock,StockMinimo,Categoria,Impuesto";

    public static string Csv =>
        Encabezados + "\n" +
        "7501000000010,Producto de ejemplo,10.00,20.00,5,2,Abarrotes,16\n" +
        "7501000000027,Otro producto,15.50,25.00,3,2,Bebidas,16\n";

    /// <summary>Contenido con BOM para que Excel respete los acentos.</summary>
    public static byte[] ComoBytes() => new UTF8Encoding(true).GetBytes(Csv);
}
