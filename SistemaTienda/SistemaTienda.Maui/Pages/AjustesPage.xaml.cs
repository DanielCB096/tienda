using System.Globalization;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Maui.Services;

namespace SistemaTienda.Maui.Pages;

public partial class AjustesPage : ContentPage
{
    private readonly ConfiguracionService _config;
    private readonly RespaldoService _respaldos;
    private readonly SesionApp _sesion;
    private readonly NavegacionApp _navegacion;

    public AjustesPage(ConfiguracionService config, RespaldoService respaldos,
        SesionApp sesion, NavegacionApp navegacion)
    {
        InitializeComponent();
        _config = config;
        _respaldos = respaldos;
        _sesion = sesion;
        _navegacion = navegacion;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Cargar();
    }

    private void Cargar()
    {
        var config = _config.ObtenerConfiguracion().Value;
        if (config != null)
        {
            TiendaLabel.Text = config.NombreTienda;
            DetalleTiendaLabel.Text =
                $"{config.Giro} · Fondo inicial {Formato.Moneda(config.FondoCajaInicial)}";
            ReglasLabel.Text =
                $"Impuesto {config.ImpuestoPredeterminado.ToString("F2", CultureInfo.InvariantCulture)} % · " +
                $"Comisión tarjeta {config.ComisionTarjeta.ToString("F2", CultureInfo.InvariantCulture)} % · " +
                $"Fiado {(config.FiadoActivado ? "activado" : "desactivado")} · " +
                (config.RespaldoIntervaloDias > 0
                    ? $"respaldo cada {config.RespaldoIntervaloDias} día(s)"
                    : "respaldos automáticos desactivados");
        }

        UsuarioLabel.Text = _sesion.Usuario == null
            ? string.Empty
            : $"Sesión: {_sesion.Usuario.Nombre} · Rol: {_sesion.Usuario.Rol}";

        EditarConfiguracionButton.IsVisible = _sesion.Usuario != null &&
            AutorizacionService.Puede(_sesion.Usuario, PermisoSistema.Configurar);

        RutaBaseLabel.Text = _respaldos.RutaBaseDatos;
        RutaRespaldosLabel.Text = _respaldos.DirectorioRespaldos;

        var ultimo = _respaldos.ObtenerUltimoRespaldo();
        var lista = _respaldos.ListarRespaldos();
        var cantidad = lista.IsSuccess ? lista.Value.Count : 0;

        UltimoRespaldoLabel.Text = ultimo.IsSuccess && ultimo.Value.HasValue
            ? Formato.FechaHora(ultimo.Value.Value)
            : "Sin respaldos";
        RespaldoDetalleLabel.Text = cantidad == 1
            ? "1 copia local verificada."
            : $"{cantidad} copias locales verificadas.";
    }

    private void OnEditarConfiguracionClicked(object sender, EventArgs e)
    {
        if (_sesion.Usuario == null ||
            !AutorizacionService.Puede(_sesion.Usuario, PermisoSistema.Configurar))
        {
            _ = DisplayAlertAsync("Sin permiso",
                "Solo el dueño puede cambiar la configuración.", "Continuar");
            return;
        }

        var config = _config.ObtenerConfiguracion().Value;
        if (config == null)
        {
            _ = DisplayAlertAsync("No se pudo abrir",
                "No se encontró la configuración de la tienda.", "Continuar");
            return;
        }

        ConfigNombreEntry.Text = config.NombreTienda;
        ConfigGiroEntry.Text = config.Giro;
        ConfigImpuestoEntry.Text =
            config.ImpuestoPredeterminado.ToString("0.##", CultureInfo.InvariantCulture);
        ConfigComisionEntry.Text =
            config.ComisionTarjeta.ToString("0.##", CultureInfo.InvariantCulture);
        ConfigFondoEntry.Text = Pesos(config.FondoCajaInicial);
        ConfigFiadoSwitch.IsToggled = config.FiadoActivado;
        ConfigLimiteEntry.Text = Pesos(config.LimiCreditoCliente);
        ConfigRespaldoEntry.Text =
            config.RespaldoIntervaloDias.ToString(CultureInfo.InvariantCulture);
        ConfigErrorLabel.IsVisible = false;
        ConfigOverlay.IsVisible = true;
    }

    private void OnCancelarConfiguracionClicked(object sender, EventArgs e)
    {
        ConfigOverlay.IsVisible = false;
        ConfigErrorLabel.IsVisible = false;
    }

    private async void OnGuardarConfiguracionClicked(object sender, EventArgs e)
    {
        if (_sesion.Usuario == null ||
            !AutorizacionService.Puede(_sesion.Usuario, PermisoSistema.Configurar))
        {
            MostrarErrorConfig("Solo el dueño puede cambiar la configuración.");
            return;
        }

        ConfigErrorLabel.IsVisible = false;
        var nombre = ConfigNombreEntry.Text?.Trim() ?? string.Empty;
        var giro = ConfigGiroEntry.Text?.Trim() ?? string.Empty;
        var impuesto = LeerPorcentaje(ConfigImpuestoEntry.Text);
        var comision = LeerPorcentaje(ConfigComisionEntry.Text);
        var fondo = LeerCentavos(ConfigFondoEntry.Text);
        var fiado = ConfigFiadoSwitch.IsToggled;
        var limite = fiado ? LeerCentavos(ConfigLimiteEntry.Text) : 0L;
        var respaldo = LeerDias(ConfigRespaldoEntry.Text);

        if (nombre.Length == 0 || giro.Length == 0 ||
            !impuesto.HasValue || !comision.HasValue || !fondo.HasValue ||
            !limite.HasValue || !respaldo.HasValue)
        {
            MostrarErrorConfig(
                "Revisa los datos: nombre y giro son obligatorios; los importes y días no pueden ser negativos.");
            return;
        }

        var config = new ConfiguracionTienda
        {
            NombreTienda = nombre,
            Giro = giro,
            ImpuestoPredeterminado = impuesto.Value,
            ComisionTarjeta = comision.Value,
            FondoCajaInicial = fondo.Value,
            FiadoActivado = fiado,
            LimiCreditoCliente = limite.Value,
            RespaldoIntervaloDias = respaldo.Value
        };

        GuardarConfiguracionButton.IsEnabled = false;
        try
        {
            _sesion.Aplicar();
            var resultado = _config.ActualizarConfiguracion(config);
            if (resultado.IsError)
            {
                MostrarErrorConfig(resultado.Message);
                return;
            }

            ConfigOverlay.IsVisible = false;
            Cargar();
            await DisplayAlertAsync("Configuración actualizada", resultado.Message, "Continuar");
        }
        finally
        {
            GuardarConfiguracionButton.IsEnabled = true;
        }
    }

    private void MostrarErrorConfig(string mensaje)
    {
        ConfigErrorLabel.Text = mensaje;
        ConfigErrorLabel.IsVisible = true;
    }

    private static decimal? LeerPorcentaje(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return null;

        var limpio = texto.Replace("%", string.Empty).Replace(" ", string.Empty).Replace(",", ".");
        if (!decimal.TryParse(limpio, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture,
                out var valor))
            return null;

        return valor is >= 0 and <= 100 ? valor : null;
    }

    private static int? LeerDias(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return null;

        if (!int.TryParse(texto.Trim(), NumberStyles.None, CultureInfo.InvariantCulture,
                out var valor) || valor is < 0 or > 365)
            return null;

        return valor;
    }

    private static long? LeerCentavos(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return null;

        var limpio = texto.Replace("$", string.Empty).Replace(" ", string.Empty).Replace(",", ".");
        if (!decimal.TryParse(limpio, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture,
                out var pesos))
            return null;

        if (pesos < 0 || pesos > 1_000_000)
            return null;

        return Dinero.DePesos(pesos);
    }

    private static string Pesos(long centavos) =>
        (centavos / 100m).ToString("0.00", CultureInfo.InvariantCulture);

    private async void OnCrearRespaldoClicked(object sender, EventArgs e)
    {        CrearRespaldoButton.IsEnabled = false;
        try
        {
            _sesion.Aplicar();
            var resultado = _respaldos.CrearRespaldo("manual");
            await DisplayAlertAsync(
                resultado.IsError ? "No se pudo respaldar" : "Respaldo creado",
                resultado.IsError ? resultado.Message : $"{resultado.Message}\n\n{resultado.Value}",
                "Continuar");
            Cargar();
        }
        finally
        {
            CrearRespaldoButton.IsEnabled = true;
        }
    }

    private async void OnCerrarSesionClicked(object sender, EventArgs e)
    {
        var confirmar = await DisplayAlertAsync("Cerrar sesión", "¿Quieres cerrar la sesión actual?", "Sí", "Cancelar");
        if (!confirmar)
            return;

        _sesion.Aplicar();
        var respaldo = _respaldos.EjecutarAutomaticoSiCorresponde();
        if (respaldo.IsError)
            await DisplayAlertAsync("Respaldo pendiente", respaldo.Message, "Continuar");

        _sesion.Cerrar();
        _navegacion.MostrarLogin();
    }
}
