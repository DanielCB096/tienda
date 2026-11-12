using System.Globalization;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Maui.Models;
using SistemaTienda.Maui.Services;

namespace SistemaTienda.Maui.Pages;

public partial class GastosPage : ContentPage
{
    private const int MaximoEnLista = 200;
    private static readonly Color ColorCaja = Color.FromArgb("#B3261E");
    private static readonly Color ColorExterno = Color.FromArgb("#2E6B4F");
    private static readonly string[] Metodos = { "Efectivo", "Tarjeta", "Transferencia", "Otro" };

    private readonly ConfiguracionService _config;
    private readonly SesionApp _sesion;

    public GastosPage(ConfiguracionService config, SesionApp sesion)
    {
        InitializeComponent();
        _config = config;
        _sesion = sesion;
        MetodoPicker.ItemsSource = Metodos;
        MetodoPicker.SelectedIndex = 0;
        ActualizarAyudaOrigen(true);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Cargar();
    }

    private void Cargar()
    {
        _sesion.Aplicar();

        var gastos = _config.ObtenerTodosLosGastos().Value ?? new List<Gasto>();
        var resumen = _config.ObtenerResumenGastos().Value ?? new ResumenGastos();

        UsuarioLabel.Text = _sesion.Usuario == null
            ? string.Empty
            : $"Sesión: {_sesion.Usuario.Nombre} · Rol: {_sesion.Usuario.Rol}";

        var puedeRegistrar = _sesion.Usuario != null &&
            AutorizacionService.Puede(_sesion.Usuario, PermisoSistema.RegistrarGasto);
        NuevoGastoButton.IsVisible = puedeRegistrar;

        TotalLabel.Text = Formato.Moneda(resumen.Total);
        CajaLabel.Text = Formato.Moneda(resumen.DeCaja);
        ExternosLabel.Text = Formato.Moneda(resumen.Externos);
        CantidadLabel.Text = resumen.Cantidad switch
        {
            0 => "Sin gastos registrados.",
            1 => "1 gasto registrado.",
            _ => $"{resumen.Cantidad} gastos registrados."
        };

        ListaGastos.ItemsSource = gastos.Take(MaximoEnLista).Select(g => new GastoFila
        {
            Concepto = g.Concepto,
            Monto = Formato.Moneda(g.Monto),
            Detalle = $"{Formato.FechaHora(g.Fecha)} · " +
                (string.IsNullOrWhiteSpace(g.MetodoPago) ? "Sin método" : g.MetodoPago),
            Origen = g.Origen == OrigenGasto.Caja ? "Salió de la caja" : "Gasto externo",
            OrigenColor = g.Origen == OrigenGasto.Caja ? ColorCaja : ColorExterno,
            Observaciones = g.Observaciones ?? string.Empty,
            TieneObservaciones = !string.IsNullOrWhiteSpace(g.Observaciones)
        }).ToList();

        ResultadosLabel.Text = gastos.Count > MaximoEnLista
            ? $"Últimos {MaximoEnLista} de {gastos.Count}"
            : gastos.Count == 1 ? "1 gasto" : $"{gastos.Count} gastos";

        ListaVaciaLabel.Text = puedeRegistrar
            ? "Aún no hay gastos registrados. Usa + Nuevo para registrar el primero."
            : "Aún no hay gastos registrados.";
    }

    private void OnRefrescando(object sender, EventArgs e)
    {
        Cargar();
        Refresco.IsRefreshing = false;
    }

    private void OnNuevoGastoClicked(object sender, EventArgs e)
    {
        if (_sesion.Usuario == null ||
            !AutorizacionService.Puede(_sesion.Usuario, PermisoSistema.RegistrarGasto))
        {
            _ = DisplayAlertAsync("Sin permiso", "Tu rol no permite registrar gastos.", "Continuar");
            return;
        }

        ConceptoEntry.Text = string.Empty;
        MontoEntry.Text = string.Empty;
        MetodoPicker.SelectedIndex = 0;
        OrigenSwitch.IsToggled = true;
        ObservacionesEditor.Text = string.Empty;
        NuevoErrorLabel.IsVisible = false;
        NuevoOverlay.IsVisible = true;

        if (DeviceInfo.Idiom == DeviceIdiom.Desktop)
            ConceptoEntry.Focus();
    }

    private void OnCancelarNuevoClicked(object sender, EventArgs e) =>
        NuevoOverlay.IsVisible = false;

    private void OnOrigenChanged(object sender, ToggledEventArgs e) =>
        ActualizarAyudaOrigen(e.Value);

    private void ActualizarAyudaOrigen(bool deCaja)
    {
        if (OrigenAyudaLabel == null)
            return;

        OrigenAyudaLabel.Text = deCaja
            ? "Se descontará del efectivo esperado del corte."
            : "No toca el efectivo de la caja; solo afecta la utilidad.";
    }

    private async void OnGuardarGastoClicked(object sender, EventArgs e)
    {
        var concepto = ConceptoEntry.Text?.Trim() ?? string.Empty;
        if (concepto.Length == 0)
        {
            MostrarError(NuevoErrorLabel, "Escribe el concepto del gasto.");
            return;
        }

        var monto = LeerCentavos(MontoEntry.Text);
        if (!monto.HasValue || monto.Value <= 0)
        {
            MostrarError(NuevoErrorLabel, "Captura un monto mayor a cero, por ejemplo 150.00.");
            return;
        }

        var metodo = MetodoPicker.SelectedItem as string ?? Metodos[0];
        var origen = OrigenSwitch.IsToggled ? OrigenGasto.Caja : OrigenGasto.Externo;
        var observaciones = ObservacionesEditor.Text?.Trim() ?? string.Empty;

        NuevoErrorLabel.IsVisible = false;
        GuardarGastoButton.IsEnabled = false;
        try
        {
            _sesion.Aplicar();
            var resultado = _config.AgregarGasto(new Gasto
            {
                Concepto = concepto,
                Monto = monto.Value,
                Fecha = DateTime.Now,
                MetodoPago = metodo,
                Origen = origen,
                Observaciones = observaciones
            });

            if (resultado.IsError)
            {
                MostrarError(NuevoErrorLabel, resultado.Message);
                return;
            }

            NuevoOverlay.IsVisible = false;
            Cargar();
            var efecto = origen == OrigenGasto.Caja
                ? "Se descontará del efectivo esperado del corte."
                : "No afecta el efectivo de la caja; solo la utilidad.";
            await DisplayAlertAsync("Gasto registrado", $"{resultado.Message}\n{efecto}", "Continuar");
        }
        finally
        {
            GuardarGastoButton.IsEnabled = true;
        }
    }

    private static void MostrarError(Label label, string mensaje)
    {
        label.Text = mensaje;
        label.IsVisible = true;
    }

    private static long? LeerCentavos(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return null;

        var limpio = texto.Replace("$", string.Empty).Replace(" ", string.Empty).Replace(",", ".");
        if (!decimal.TryParse(limpio, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var pesos))
            return null;

        if (pesos < 0 || pesos > 1_000_000)
            return null;

        return Dinero.DePesos(pesos);
    }
}
