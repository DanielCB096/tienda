using System.Globalization;
using SistemaTienda.Core;

namespace SistemaTienda.Maui;

public static class Formato
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-MX");

    public static string Moneda(long centavos) => $"${Dinero.Formatear(centavos)}";

    public static string FechaHora(DateTime fecha) => fecha.ToString("dd/MM/yyyy HH:mm", Cultura);

    public static string FechaLarga(DateTime fecha) =>
        fecha.ToString("dddd, d 'de' MMMM 'de' yyyy", Cultura);

    public static string Hora(DateTime fecha) => fecha.ToString("HH:mm", Cultura);
}
