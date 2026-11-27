using SistemaTienda.Application.Services;
using SistemaTienda.Maui.Services;

namespace SistemaTienda.Maui.Pages;

/// <summary>
/// Importa una licencia pegando el contenido del archivo .poslic. Se usa texto
/// para evitar los selectores de archivo que fallan en Windows sin empaquetar.
/// </summary>
public sealed class LicenciaPage : ContentPage
{
    private readonly LicenciaService _licencia;
    private readonly SesionApp _sesion;
    private readonly Editor _contenido;
    private readonly Label _error;
    private readonly Button _importar;
    private readonly TaskCompletionSource<bool> _resultado = new();

    public Task<bool> Resultado => _resultado.Task;

    public LicenciaPage(LicenciaService licencia, SesionApp sesion)
    {
        _licencia = licencia;
        _sesion = sesion;
        Title = "Importar licencia";

        var encabezado = new Label
        {
            Text = "Importar licencia",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold
        };

        var nota = new Label
        {
            Text = "Copia el contenido del archivo .poslic que te enviamos y pégalo aquí. " +
                   "La licencia es perpetua y no bloquea la operación.",
            FontSize = 13
        };
        nota.SetDynamicResource(VisualElement.StyleProperty, "TextoNota");

        _contenido = new Editor
        {
            Placeholder = "{ \"payload\": \"...\", \"firma\": \"...\" }",
            HeightRequest = 240,
            FontSize = 12,
            AutoSize = EditorAutoSizeOption.Disabled
        };
        _contenido.SetDynamicResource(VisualElement.StyleProperty, "CampoTexto");

        _error = new Label
        {
            TextColor = Color.FromArgb("#B3261E"),
            FontSize = 13,
            IsVisible = false
        };

        var cancelar = new Button { Text = "Cancelar" };
        cancelar.SetDynamicResource(VisualElement.StyleProperty, "BotonSecundario");
        cancelar.Clicked += async (_, _) => await CerrarAsync(false);

        _importar = new Button { Text = "Validar y guardar" };
        _importar.SetDynamicResource(VisualElement.StyleProperty, "BotonPrincipal");
        _importar.Clicked += async (_, _) => await ImportarAsync();

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
        botones.Add(_importar, 1, 0);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Padding = 16,
                Children =
                {
                    encabezado,
                    nota,
                    new Label { Text = "Contenido de la licencia", FontSize = 13, FontAttributes = FontAttributes.Bold },
                    _contenido,
                    _error,
                    botones
                }
            }
        };
    }

    private async Task ImportarAsync()
    {
        _sesion.Aplicar();
        _importar.IsEnabled = false;
        var resultado = _licencia.ImportarContenido(_contenido.Text);
        if (resultado.IsError)
        {
            _error.Text = resultado.Message;
            _error.IsVisible = true;
            _importar.IsEnabled = true;
            return;
        }

        await DisplayAlertAsync("Licencia importada", resultado.Message, "Continuar");
        await CerrarAsync(true);
    }

    private async Task CerrarAsync(bool importada)
    {
        if (_resultado.Task.IsCompleted)
            return;

        _resultado.TrySetResult(importada);
        await Navigation.PopModalAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CerrarAsync(false);
        return true;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (!_resultado.Task.IsCompleted)
            _resultado.TrySetResult(false);
    }
}
