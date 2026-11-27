using System.Globalization;
using SistemaTienda.Core;

namespace SistemaTienda.Maui.Pages;

public sealed record ReembolsoSeleccion(string Metodo, long Monto);

/// <summary>
/// Captura un reembolso de una venta anulada. Devuelve null si se cancela.
/// </summary>
public sealed class ReembolsoPage : ContentPage
{
    private readonly long _pendiente;
    private readonly Picker _metodo;
    private readonly Entry _monto;
    private readonly Label _error;
    private readonly Label _ayuda;
    private readonly TaskCompletionSource<ReembolsoSeleccion?> _resultado = new();
    private bool _finalizado;

    public Task<ReembolsoSeleccion?> Resultado => _resultado.Task;

    public ReembolsoPage(long pendiente)
    {
        _pendiente = pendiente;
        Title = "Registrar reembolso";

        var encabezado = new Label
        {
            Text = "Registrar reembolso",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold
        };

        var pendienteLabel = new Label
        {
            Text = $"Pendiente de reembolso: {Formato.Moneda(pendiente)}",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold
        };

        var nota = new Label
        {
            Text = "Puedes registrar una parte ahora y el resto después. El fiado no se reembolsa: la anulación ya revirtió el saldo.",
            FontSize = 13
        };
        nota.SetDynamicResource(VisualElement.StyleProperty, "TextoNota");

        _metodo = new Picker
        {
            ItemsSource = MetodoPagoAbono.Todos.ToList(),
            SelectedIndex = 0
        };

        _monto = new Entry
        {
            Text = Dinero.APesos(pendiente).ToString("F2", CultureInfo.InvariantCulture),
            Keyboard = Keyboard.Numeric,
            FontSize = 20,
            HorizontalTextAlignment = TextAlignment.End
        };
        _monto.SetDynamicResource(VisualElement.StyleProperty, "CampoTexto");

        _ayuda = new Label { Text = AyudaMetodo(), FontSize = 13 };
        _ayuda.SetDynamicResource(VisualElement.StyleProperty, "TextoNota");

        _error = new Label
        {
            TextColor = Color.FromArgb("#B3261E"),
            FontSize = 13,
            IsVisible = false
        };

        _metodo.SelectedIndexChanged += (_, _) =>
        {
            _ayuda.Text = AyudaMetodo();
            _error.IsVisible = false;
        };

        var cancelar = new Button { Text = "Cancelar" };
        cancelar.SetDynamicResource(VisualElement.StyleProperty, "BotonSecundario");
        cancelar.Clicked += async (_, _) => await CerrarAsync(null);

        var registrar = new Button { Text = "Registrar reembolso" };
        registrar.SetDynamicResource(VisualElement.StyleProperty, "BotonPrincipal");
        registrar.Clicked += async (_, _) => await RegistrarAsync();

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
        botones.Add(registrar, 1, 0);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Padding = 16,
                Children =
                {
                    encabezado,
                    pendienteLabel,
                    nota,
                    new Label { Text = "Método", FontSize = 13, FontAttributes = FontAttributes.Bold },
                    _metodo,
                    _ayuda,
                    new Label { Text = "Monto", FontSize = 13, FontAttributes = FontAttributes.Bold },
                    _monto,
                    _error,
                    botones
                }
            }
        };
    }

    private string AyudaMetodo()
    {
        var metodo = _metodo.SelectedItem as string ?? MetodoPagoAbono.Efectivo;
        return metodo == MetodoPagoAbono.Efectivo
            ? "El efectivo genera un retiro de caja en el periodo abierto."
            : "Este método reduce el pendiente, pero no mueve el efectivo de la caja.";
    }

    private async Task RegistrarAsync()
    {
        var texto = (_monto.Text ?? string.Empty)
            .Replace("$", string.Empty)
            .Replace(" ", string.Empty)
            .Replace(",", ".");

        if (!decimal.TryParse(texto, NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var pesos))
        {
            MostrarError("Escribe un monto válido.");
            return;
        }

        var centavos = (long)decimal.Round(pesos * 100m, 0, MidpointRounding.AwayFromZero);
        if (centavos <= 0)
        {
            MostrarError("El monto debe ser mayor a cero.");
            return;
        }

        if (centavos > _pendiente)
        {
            MostrarError($"El monto no puede superar el pendiente de {Formato.Moneda(_pendiente)}.");
            return;
        }

        var metodo = _metodo.SelectedItem as string ?? MetodoPagoAbono.Efectivo;
        await CerrarAsync(new ReembolsoSeleccion(metodo, centavos));
    }

    private void MostrarError(string mensaje)
    {
        _error.Text = mensaje;
        _error.IsVisible = true;
    }

    private async Task CerrarAsync(ReembolsoSeleccion? seleccion)
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
