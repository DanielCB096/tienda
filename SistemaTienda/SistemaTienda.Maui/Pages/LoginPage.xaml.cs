using SistemaTienda.Application.Services;
using SistemaTienda.Maui.Services;

namespace SistemaTienda.Maui.Pages;

public partial class LoginPage : ContentPage
{
    private const int IntentosPermitidos = 3;

    private readonly ConfiguracionService _config;
    private readonly RespaldoService _respaldos;
    private readonly SesionApp _sesion;
    private readonly NavegacionApp _navegacion;
    private int _intentos;

    public LoginPage(ConfiguracionService config, RespaldoService respaldos,
        SesionApp sesion, NavegacionApp navegacion)
    {
        InitializeComponent();
        _config = config;
        _respaldos = respaldos;
        _sesion = sesion;
        _navegacion = navegacion;

        var configuracion = _config.ObtenerConfiguracion();
        NombreTiendaLabel.Text = configuracion.Value?.NombreTienda ?? "Mi tienda";
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        PinEntry.Focus();
    }

    private async void OnEntrarClicked(object sender, EventArgs e)
    {
        if (!EntrarButton.IsEnabled)
            return;

        var pin = PinEntry.Text?.Trim() ?? string.Empty;
        if (pin.Length == 0)
        {
            MostrarError("Escribe tu PIN.");
            return;
        }

        var resultado = _config.VerificarAcceso(pin);
        if (resultado.IsError)
        {
            _intentos++;
            PinEntry.Text = string.Empty;
            MostrarError($"PIN incorrecto. Intento {_intentos} de {IntentosPermitidos}.");

            if (_intentos >= IntentosPermitidos)
            {
                PinEntry.IsEnabled = false;
                EntrarButton.IsEnabled = false;
                MostrarError("Demasiados intentos. Cierra y abre la aplicación para volver a intentarlo.");
            }

            PinEntry.Focus();
            return;
        }

        _sesion.Iniciar(resultado.Value);

        var respaldo = _respaldos.EjecutarAutomaticoSiCorresponde();
        if (respaldo.IsError)
            await DisplayAlertAsync("Respaldo pendiente", respaldo.Message, "Continuar");

        _navegacion.MostrarPrincipal();
    }

    private void MostrarError(string mensaje)
    {
        ErrorLabel.Text = mensaje;
        ErrorLabel.IsVisible = true;
    }
}
