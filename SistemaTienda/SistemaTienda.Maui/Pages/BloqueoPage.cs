using SistemaTienda.Application.Services;
using SistemaTienda.Maui.Services;

namespace SistemaTienda.Maui.Pages;

/// <summary>
/// Pantalla de bloqueo por inactividad. Se muestra como modal sobre las pestañas,
/// así que el carrito y la venta en curso no se pierden. Acepta el PIN del mismo
/// usuario o de otro; la operación siguiente queda a nombre de quien entró.
/// </summary>
public sealed class BloqueoPage : ContentPage
{
    private readonly ConfiguracionService _config;
    private readonly SesionApp _sesion;
    private readonly BloqueoSesion _bloqueo;

    private readonly Entry _pin;
    private readonly Label _error;

    public BloqueoPage(ConfiguracionService config, SesionApp sesion, BloqueoSesion bloqueo)
    {
        _config = config;
        _sesion = sesion;
        _bloqueo = bloqueo;

        Title = "Caja bloqueada";
        BackgroundColor = Color.FromArgb("#F6F3EE");

        var titulo = new Label
        {
            Text = "Caja bloqueada",
            FontSize = 26,
            FontAttributes = FontAttributes.Bold
        };
        titulo.SetDynamicResource(VisualElement.StyleProperty, "TituloPagina");

        var nota = new Label
        {
            Text = "Caja bloqueada por inactividad. Ingresa tu PIN para continuar o cambia de cajero.",
            HorizontalTextAlignment = TextAlignment.Center
        };
        nota.SetDynamicResource(VisualElement.StyleProperty, "TextoNota");

        var usuario = _sesion.Usuario?.Nombre ?? "Sin sesión";
        var actual = new Label
        {
            Text = $"Sesión: {usuario}",
            HorizontalTextAlignment = TextAlignment.Center,
            FontAttributes = FontAttributes.Bold
        };

        _pin = new Entry
        {
            Placeholder = "PIN de 4 a 6 dígitos",
            IsPassword = true,
            Keyboard = Keyboard.Numeric,
            MaxLength = 6,
            HorizontalTextAlignment = TextAlignment.Center
        };
        _pin.SetDynamicResource(VisualElement.StyleProperty, "CampoTexto");
        _pin.Completed += (_, _) => Desbloquear();

        _error = new Label
        {
            IsVisible = false,
            HorizontalTextAlignment = TextAlignment.Center
        };
        _error.SetDynamicResource(VisualElement.StyleProperty, "Error");

        var entrar = new Button { Text = "Desbloquear" };
        entrar.SetDynamicResource(VisualElement.StyleProperty, "BotonPrincipal");
        entrar.Clicked += (_, _) => Desbloquear();

        var contenido = new VerticalStackLayout
        {
            Spacing = 12,
            Padding = 24,
            VerticalOptions = LayoutOptions.Center,
            MaximumWidthRequest = 420,
            HorizontalOptions = LayoutOptions.Center,
            Children = { titulo, nota, actual, _pin, _error, entrar }
        };

        Content = new ScrollView { Content = contenido };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _pin.Focus();
    }

    protected override bool OnBackButtonPressed()
    {
        // No se puede cerrar con Atrás: la caja debe desbloquearse con PIN.
        return true;
    }

    private async void Desbloquear()
    {
        var pin = _pin.Text?.Trim() ?? string.Empty;
        if (pin.Length == 0)
        {
            MostrarError("Escribe tu PIN.");
            return;
        }

        var resultado = _config.VerificarAcceso(pin);
        if (resultado.IsError)
        {
            _pin.Text = string.Empty;
            MostrarError(resultado.Message);
            _pin.Focus();
            return;
        }

        _sesion.Iniciar(resultado.Value);
        _bloqueo.Desbloquear();
        _error.IsVisible = false;

        await Navigation.PopModalAsync();
    }

    private void MostrarError(string mensaje)
    {
        _error.Text = mensaje;
        _error.IsVisible = true;
    }
}
