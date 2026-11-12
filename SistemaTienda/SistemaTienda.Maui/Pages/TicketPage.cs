using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Devices;
using SistemaTienda.Maui.Services;

namespace SistemaTienda.Maui.Pages;

public sealed class TicketPage : ContentPage
{
    private readonly string _texto;

    public TicketPage(string texto, string? contenidoQr = null)
    {
        _texto = texto;
        Title = "Ticket de venta";

        var encabezado = new Label { Text = "Ticket de venta" };
        encabezado.SetDynamicResource(VisualElement.StyleProperty, "TituloPagina");

        var qr = QrTicketView.Crear(contenidoQr);
        var nota = new Label
        {
            Text = qr == null
                ? "Comprobante interno, no fiscal. Puedes compartirlo o copiarlo."
                : "Comprobante interno, no fiscal. El cliente puede escanear el QR; el texto se puede compartir o copiar."
        };
        nota.SetDynamicResource(VisualElement.StyleProperty, "TextoNota");

        var ticket = new Label
        {
            Text = texto,
            FontSize = 14,
            LineHeight = 1.3,
            FontFamily = DeviceInfo.Platform == DevicePlatform.WinUI ? "Consolas" : "monospace"
        };

        var visor = new VerticalStackLayout { Spacing = 10 };
        if (qr != null)
        {
            visor.Children.Add(qr);
            var notaQr = new Label
            {
                Text = "QR informativo de la venta",
                HorizontalTextAlignment = TextAlignment.Center
            };
            notaQr.SetDynamicResource(VisualElement.StyleProperty, "TextoNota");
            visor.Children.Add(notaQr);
        }
        visor.Children.Add(ticket);

        var marco = new Border
        {
            Content = new ScrollView { Content = visor }
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

        var contenido = new Grid
        {
            Padding = 16,
            RowSpacing = 12,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
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
        Grid.SetRow(encabezado, 0);
        Grid.SetRow(nota, 1);
        Grid.SetRow(marco, 2);
        Grid.SetRow(compartir, 3);
        Grid.SetRow(copiar, 4);
        Grid.SetRow(cerrar, 5);

        Content = contenido;
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
