using System.Globalization;
using SistemaTienda.Application.Services;
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

    private async void OnCrearRespaldoClicked(object sender, EventArgs e)
    {
        CrearRespaldoButton.IsEnabled = false;
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
