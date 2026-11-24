using SistemaTienda.Application.Services;

namespace SistemaTienda.Maui.Services;

/// <summary>
/// Muestra el QR del ticket como imagen rasterizada. Dibujar cada módulo con
/// rectángulos dependía del escalado de la ventana y recortaba la última fila
/// del patrón en algunas configuraciones; una imagen se ajusta completa dentro
/// del control y conserva la zona de silencio.
/// </summary>
public static class QrTicketView
{
    private const int LadoVisibleDip = 240;
    private const int ModuloPredeterminado = 8;

    public static View? Crear(string? contenido)
    {
        if (string.IsNullOrWhiteSpace(contenido))
            return null;

        try
        {
            var imagen = QrImagenService.Generar(contenido, ModuloPredeterminado);

            return new Image
            {
                Source = ImageSource.FromStream(() => new MemoryStream(imagen.Png)),
                WidthRequest = LadoVisibleDip,
                HeightRequest = LadoVisibleDip,
                Aspect = Aspect.AspectFit,
                HorizontalOptions = LayoutOptions.Center,
                BackgroundColor = Colors.White
            };
        }
        catch
        {
            return null;
        }
    }
}
