using System.Globalization;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Maui.Services;

namespace SistemaTienda.Maui.Pages;

public partial class ConfiguracionInicialPage : ContentPage
{
    private readonly ConfiguracionService _config;
    private readonly NavegacionApp _navegacion;

    public ConfiguracionInicialPage(ConfiguracionService config, NavegacionApp navegacion)
    {
        InitializeComponent();
        _config = config;
        _navegacion = navegacion;

        GiroEntry.Text = ConfiguracionPorDefecto.GiroPredeterminado;
        ImpuestoEntry.Text = ConfiguracionPorDefecto.ImpuestoPredeterminado.ToString(CultureInfo.InvariantCulture);
        ComisionEntry.Text = ConfiguracionPorDefecto.ComisionTarjetaPredeterminada.ToString(CultureInfo.InvariantCulture);
        FondoEntry.Text = Dinero.APesos(ConfiguracionPorDefecto.FondoCajaInicialPredeterminado)
            .ToString("F2", CultureInfo.InvariantCulture);
        LimiteEntry.Text = "0";
        RespaldoEntry.Text = ConfiguracionPorDefecto.RespaldoIntervaloDiasPredeterminado.ToString(CultureInfo.InvariantCulture);
    }

    private async void OnCrearClicked(object sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;

        if (string.IsNullOrWhiteSpace(NombreEntry.Text))
        {
            MostrarError("Escribe el nombre de la tienda.");
            return;
        }

        if (string.IsNullOrWhiteSpace(GiroEntry.Text))
        {
            MostrarError("Escribe el giro del negocio.");
            return;
        }

        if (!LeerDecimal(ImpuestoEntry.Text, out var impuesto) ||
            !LeerDecimal(ComisionEntry.Text, out var comision) ||
            !LeerDecimal(FondoEntry.Text, out var fondo) ||
            !LeerDecimal(LimiteEntry.Text, out var limite))
        {
            MostrarError("Revisa los importes y porcentajes: deben ser números.");
            return;
        }

        if (!int.TryParse(RespaldoEntry.Text, out var intervalo) || intervalo < 0 || intervalo > 365)
        {
            MostrarError("Los días entre respaldos deben ser un número entre 0 y 365.");
            return;
        }

        var pin = PinEntry.Text?.Trim() ?? string.Empty;
        if (pin != (PinConfirmacionEntry.Text?.Trim() ?? string.Empty))
        {
            MostrarError("Los PIN no coinciden.");
            return;
        }

        CrearButton.IsEnabled = false;
        try
        {
            var resultado = _config.EjecutarAsistenteInicial(
                NombreEntry.Text.Trim(),
                GiroEntry.Text.Trim(),
                impuesto,
                comision,
                Dinero.DePesos(fondo),
                FiadoSwitch.IsToggled,
                FiadoSwitch.IsToggled ? Dinero.DePesos(limite) : 0,
                intervalo,
                pin);

            if (resultado.IsError)
            {
                MostrarError(resultado.Message);
                return;
            }

            await DisplayAlertAsync("Tienda configurada", resultado.Message, "Continuar");
            _navegacion.MostrarLogin();
        }
        finally
        {
            CrearButton.IsEnabled = true;
        }
    }

    private static bool LeerDecimal(string? texto, out decimal valor)
    {
        var limpio = (texto ?? string.Empty).Trim();
        return decimal.TryParse(limpio, NumberStyles.Number, CultureInfo.CurrentCulture, out valor) ||
               decimal.TryParse(limpio, NumberStyles.Number, CultureInfo.InvariantCulture, out valor) ||
               decimal.TryParse(limpio.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out valor);
    }

    private void MostrarError(string mensaje)
    {
        ErrorLabel.Text = mensaje;
        ErrorLabel.IsVisible = true;
    }
}
