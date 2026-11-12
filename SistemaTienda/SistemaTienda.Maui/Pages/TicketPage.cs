using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Devices;

namespace SistemaTienda.Maui.Pages;

public sealed class TicketPage : ContentPage
{
    private readonly string _texto;

    public TicketPage(string texto)
    {
        _texto = texto;
        Title = "Ticket de venta";

        var encabezado = new Label { Text = "Ticket de venta" };
        encabezado.SetDynamicResource(VisualElement.StyleProperty, "TituloPagina");

        var nota = new Label
        {
            Text = "Comprobante interno, no fiscal. Puedes compartirlo o copiarlo."
        };
        nota.SetDynamicResource(VisualElement.StyleProperty, "TextoNota");

        var ticket = new Label
        {
            Text = texto,
            FontSize = 14,
            LineHeight = 1.3,
            FontFamily = DeviceInfo.Platform == DevicePlatform.WinUI ? "Consolas" : "monospace"
        };

        var marco = new Border
        {
            Content = new ScrollView { Content = ticket }
        };
        marco.SetDynamicResource(VisualElement.StyleProperty, "Tarjeta");

        var compartir = new Button { Text = "Compartir" };
        compartir.SetDynamicResource(VisualElement.StyleProperty, "BotonPrincipal");
        compartir.Clicked += async (_, _) => await Compartir();

        var copiar = new Button { Text = "Copiar" };
        copiar.SetDynamicResource(VisualElement.StyleProperty, "BotonSecundario");
        copiar.Clicked += async (_, _) => await Copiar();

        var cerrar = new Button { Text = "Cerrar" };
        cerrar.SetDynamicResource(VisualElement.StyleProperty, "BotonSecundario");
        cerrar.Clicked += async (_, _) => await Navigation.PopModalAsync();

        var contenido = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 12,
            Children =
            {
                encabezado,
                nota,
                marco,
                compartir,
                copiar,
                cerrar
            }
        };

        var raiz = new Grid { Children = { contenido } };

        Content = raiz;
    }

    private async Task Compartir()
    {
        try
        {
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = "Ticket de venta",
                Text = _texto
            });
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("No se pudo compartir",
                $"Copia el ticket manualmente.\n\n{ex.Message}", "Continuar");
        }
    }

    private async Task Copiar()
    {
        await Clipboard.Default.SetTextAsync(_texto);
        await DisplayAlertAsync("Ticket copiado",
            "El ticket quedó en el portapapeles.", "Continuar");
    }
}
