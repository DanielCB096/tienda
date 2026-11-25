using System.Globalization;
using System.Text;

namespace SistemaTienda.Core;

/// <summary>
/// Utilidades de texto para búsquedas y comparaciones que deben ignorar
/// mayúsculas y acentos. No modifica el texto que se guarda.
/// </summary>
public static class Texto
{
    public static string SinAcentos(string texto)
    {
        if (string.IsNullOrEmpty(texto))
            return string.Empty;

        // Camino rápido: códigos de barras y nombres sin acentos no necesitan
        // normalizar ni recorrer la cadena. Es la mayoría del catálogo.
        var esAscii = true;
        foreach (var caracter in texto)
        {
            if (caracter > 127)
            {
                esAscii = false;
                break;
            }
        }

        if (esAscii)
            return texto;

        var normalizado = texto.Normalize(NormalizationForm.FormD);
        var constructor = new StringBuilder(normalizado.Length);
        foreach (var caracter in normalizado)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
                constructor.Append(caracter);
        }

        return constructor.ToString().Normalize(NormalizationForm.FormC);
    }
}
