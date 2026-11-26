using System.Globalization;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Maui.Services;

namespace SistemaTienda.Maui.Pages;

public partial class ConfiguracionInicialPage : ContentPage
{
    private static readonly FilePickerFileType TiposRespaldo = new(
        new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.WinUI] = new[] { ".db", ".stb" },
            [DevicePlatform.Android] = new[]
            {
                "application/octet-stream",
                "application/x-sqlite3",
                "application/vnd.sqlite3",
                "*/*"
            },
            [DevicePlatform.iOS] = new[] { "public.database" },
            [DevicePlatform.MacCatalyst] = new[] { "public.database" }
        });

    private readonly ConfiguracionService _config;
    private readonly RespaldoService _respaldos;
    private readonly NavegacionApp _navegacion;

    public ConfiguracionInicialPage(ConfiguracionService config, RespaldoService respaldos,
        NavegacionApp navegacion)
    {
        InitializeComponent();
        _config = config;
        _respaldos = respaldos;
        _navegacion = navegacion;

        GiroEntry.Text = ConfiguracionPorDefecto.GiroPredeterminado;
        ImpuestoEntry.Text = ConfiguracionPorDefecto.ImpuestoPredeterminado.ToString(CultureInfo.InvariantCulture);
        ComisionEntry.Text = ConfiguracionPorDefecto.ComisionTarjetaPredeterminada.ToString(CultureInfo.InvariantCulture);
        FondoEntry.Text = Dinero.APesos(ConfiguracionPorDefecto.FondoCajaInicialPredeterminado)
            .ToString("F2", CultureInfo.InvariantCulture);
        LimiteEntry.Text = "0";
        RespaldoEntry.Text = ConfiguracionPorDefecto.RespaldoIntervaloDiasPredeterminado.ToString(CultureInfo.InvariantCulture);
    }

    private async void OnRestaurarRespaldoClicked(object sender, EventArgs e)
    {
        RestaurarRespaldoButton.IsEnabled = false;
        try
        {
            var ruta = await SelectorArchivoRespaldo.ElegirAsync(this);
            if (string.IsNullOrEmpty(ruta))
                return;

            var confirmar = await DisplayAlertAsync("Restaurar respaldo",
                $"Se reemplazará la base de este equipo por:\n{Path.GetFileName(ruta)}\n\n" +
                "Se guardará un respaldo previo. Después inicia sesión con el PIN de ese respaldo.",
                "Restaurar", "Cancelar");
            if (!confirmar)
                return;

            string? clave = null;
            if (ruta.EndsWith(CifradoRespaldo.Extension,
                    StringComparison.OrdinalIgnoreCase))
            {
                clave = await DisplayPromptAsync("Respaldo cifrado",
                    "Escribe la clave de respaldo de 24 caracteres.",
                    accept: "Restaurar", cancel: "Cancelar", maxLength: 40);
                if (clave == null)
                    return;
            }

            var resultado = _respaldos.RestaurarEnInstalacionNueva(
                ruta, confirmado: true, claveRespaldo: clave);
            if (resultado.IsError)
            {
                MostrarError(resultado.Message);
                return;
            }

            await DisplayAlertAsync("Respaldo restaurado",
                "La tienda quedó restaurada. Inicia sesión con el PIN de ese respaldo.",
                "Continuar");
            _navegacion.MostrarLogin();
        }
        catch (Exception ex)
        {
            MostrarError($"No se pudo restaurar: {ex.Message}");
        }
        finally
        {
            RestaurarRespaldoButton.IsEnabled = true;
        }
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
            await OfrecerCifradoAsync();
            _navegacion.MostrarLogin();
        }
        finally
        {
            CrearButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// Ofrece activar el cifrado en la tienda nueva. Los expertos pidieron que
    /// la instalación nazca protegida, pero sin bloquear la operación: si el
    /// dueño dice "ahora no", se le advierte que los respaldos quedan sin cifrar.
    /// </summary>
    private async Task OfrecerCifradoAsync()
    {
        var activar = await DisplayAlertAsync("Protege tus respaldos",
            "Puedes cifrar los respaldos con una clave que solo tú tengas. " +
            "Si la pierdes, no se podrán abrir. ¿Activar ahora?",
            "Activar", "Ahora no");

        if (!activar)
        {
            await DisplayAlertAsync("Respaldos sin cifrado",
                "Los respaldos se guardarán sin contraseña. Puedes activar el cifrado después en Ajustes → Respaldo cifrado.",
                "Continuar");
            return;
        }

        try
        {
            var clave = CifradoRespaldo.GenerarClave();
            await AlmacenClaveRespaldo.GuardarAsync(clave);

            var config = _config.ObtenerConfiguracion().Value;
            var dueno = _config.ObtenerUsuarios().Value?
                .FirstOrDefault(u => u.Rol == RolUsuario.Dueño);

            if (config == null || dueno == null)
            {
                await DisplayAlertAsync("Cifrado",
                    "La tienda quedó creada, pero no se pudo activar el cifrado. Hazlo desde Ajustes.",
                    "Continuar");
                return;
            }

            SesionActual.Iniciar(dueno);
            config.RespaldoCifradoActivado = true;
            var guardado = _config.ActualizarConfiguracion(config);
            if (guardado.IsError)
            {
                await DisplayAlertAsync("Cifrado", guardado.Message, "Continuar");
                return;
            }

            var entrada = await DisplayPromptAsync("Tarjeta de recuperación",
                "Guarda esta clave fuera del equipo. La necesitarás para restaurar en otro equipo.",
                accept: "Ya la guardé", cancel: "Copiar clave",
                initialValue: clave, maxLength: 40);

            if (entrada == null)
            {
                await Clipboard.Default.SetTextAsync(clave);
                await DisplayAlertAsync("Clave copiada",
                    "Pega la clave en un lugar seguro fuera de este equipo.", "Continuar");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Cifrado",
                $"No se pudo activar el cifrado: {ex.Message}. Puedes hacerlo desde Ajustes.", "Continuar");
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
