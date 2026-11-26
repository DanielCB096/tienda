using System.Globalization;
using SistemaTienda.Core;

namespace SistemaTienda.Maui.Pages;

/// <summary>Descuento capturado para un renglón o para toda la venta.</summary>
public sealed record DescuentoSeleccion(bool EsPorcentaje, decimal Valor);

/// <summary>
/// Captura un descuento como porcentaje o como monto. Devuelve null si se
/// cancela y <see cref="DescuentoSeleccion"/> con Valor 0 para quitarlo.
/// </summary>
public sealed class DescuentoPage : ContentPage
{
    private readonly long _subtotal;
    private readonly Entry _valor;
    private readonly Picker _tipo;
    private readonly Label _error;
    private readonly TaskCompletionSource<DescuentoSeleccion?> _resultado = new();
    private bool _finalizado;

    public Task<DescuentoSeleccion?> Resultado => _resultado.Task;

    public DescuentoPage(string titulo, string detalle, long subtotal,
        decimal porcentajeActual, long montoActual)
    {
        _subtotal = subtotal;
        Title = "Descuento";

        var encabezado = new Label { Text = titulo, FontSize = 22, FontAttributes = FontAttributes.Bold };
        var nota = new Label { Text = detalle, FontSize = 14 };
        nota.SetDynamicResource(VisualElement.StyleProperty, "TextoNota");

        var subtotalLabel = new Label
        {
            Text = $"Importe antes del descuento: {Formato.Moneda(subtotal)}",
            FontSize = 13
        };
        subtotalLabel.SetDynamicResource(VisualElement.StyleProperty, "TextoNota");

        _valor = new Entry
        {
            Placeholder = "0",
            Keyboard = Keyboard.Numeric,
            FontSize = 20,
            HorizontalTextAlignment = TextAlignment.End
        };
        _valor.SetDynamicResource(VisualElement.StyleProperty, "CampoTexto");

        _tipo = new Picker
        {
            ItemsSource = new List<string> { "%", "$" },
            SelectedIndex = 0,
            WidthRequest = 72
        };

        if (porcentajeActual > 0)
        {
            _valor.Text = porcentajeActual.ToString("0.##", CultureInfo.InvariantCulture);
        }
        else if (montoActual > 0)
        {
            _tipo.SelectedIndex = 1;
            _valor.Text = Dinero.APesos(montoActual).ToString("F2", CultureInfo.InvariantCulture);
        }

        var fila = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 8
        };
        fila.Add(_valor, 0, 0);
        fila.Add(_tipo, 1, 0);

        _error = new Label { TextColor = Color.FromArgb("#B3261E"), FontSize = 13, IsVisible = false };

        var quitar = new Button { Text = "Quitar descuento" };
        quitar.SetDynamicResource(VisualElement.StyleProperty, "BotonSecundario");
        quitar.IsVisible = porcentajeActual > 0 || montoActual > 0;
        quitar.Clicked += async (_, _) => await CerrarAsync(new DescuentoSeleccion(true, 0m));

        var cancelar = new Button { Text = "Cancelar" };
        cancelar.SetDynamicResource(VisualElement.StyleProperty, "BotonSecundario");
        cancelar.Clicked += async (_, _) => await CerrarAsync(null);

        var aplicar = new Button { Text = "Aplicar" };
        aplicar.SetDynamicResource(VisualElement.StyleProperty, "BotonPrincipal");
        aplicar.Clicked += async (_, _) => await AplicarAsync();

        var botones = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            },
            ColumnSpacing = 8
        };
        botones.Add(cancelar, 0, 0);
        botones.Add(aplicar, 1, 0);

        var contenido = new VerticalStackLayout
        {
            Spacing = 10,
            Padding = 16,
            Children =
            {
                encabezado,
                nota,
                subtotalLabel,
                new Label { Text = "Descuento", FontSize = 13, FontAttributes = FontAttributes.Bold },
                fila,
                _error,
                quitar,
                botones
            }
        };

        Content = new ScrollView { Content = contenido };
    }

    private async Task AplicarAsync()
    {
        var texto = (_valor.Text ?? string.Empty)
            .Replace("$", string.Empty)
            .Replace(" ", string.Empty)
            .Replace(",", ".");

        if (!decimal.TryParse(texto, NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var capturado))
        {
            MostrarError("Escribe un número válido.");
            return;
        }

        if (capturado < 0)
        {
            MostrarError("El descuento no puede ser negativo.");
            return;
        }

        var esPorcentaje = _tipo.SelectedIndex <= 0;
        if (capturado == 0)
        {
            await CerrarAsync(new DescuentoSeleccion(esPorcentaje, 0m));
            return;
        }

        if (esPorcentaje)
        {
            if (!DescuentoVenta.EsPorcentajeValido(capturado))
            {
                MostrarError("El porcentaje debe ser mayor a 0 y menor a 100.");
                return;
            }

            await CerrarAsync(new DescuentoSeleccion(true, capturado));
            return;
        }

        var centavos = (long)decimal.Round(capturado * 100m, 0, MidpointRounding.AwayFromZero);
        if (!DescuentoVenta.EsMontoValido(centavos, _subtotal))
        {
            MostrarError($"El monto debe ser mayor a $0.00 y menor a {Formato.Moneda(_subtotal)}.");
            return;
        }

        await CerrarAsync(new DescuentoSeleccion(false, centavos));
    }

    private void MostrarError(string mensaje)
    {
        _error.Text = mensaje;
        _error.IsVisible = true;
    }

    private async Task CerrarAsync(DescuentoSeleccion? seleccion)
    {
        if (_finalizado)
            return;

        _finalizado = true;
        _resultado.TrySetResult(seleccion);
        await Navigation.PopModalAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CerrarAsync(null);
        return true;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (!_finalizado)
        {
            _finalizado = true;
            _resultado.TrySetResult(null);
        }
    }
}
