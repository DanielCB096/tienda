using System.Globalization;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Maui.Models;
using SistemaTienda.Maui.Services;

namespace SistemaTienda.Maui.Pages;

public partial class ClientesPage : ContentPage
{
    private static readonly Color ColorSaldo = Color.FromArgb("#B3261E");
    private static readonly Color ColorAlDia = Color.FromArgb("#2E6B4F");
    private static readonly Color ColorPrimario = Color.FromArgb("#2E6B4F");

    private readonly ConfiguracionService _config;
    private readonly SesionApp _sesion;
    private bool _soloConSaldo;
    private ClienteFila? _clienteAbono;

    public ClientesPage(ConfiguracionService config, SesionApp sesion)
    {
        InitializeComponent();
        _config = config;
        _sesion = sesion;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Cargar();
    }

    private void Cargar()
    {
        _sesion.Aplicar();

        var todos = _config.ObtenerTodosLosClientes().Value ?? new List<ClienteFiado>();
        var filtro = BuscarEntry.Text?.Trim() ?? string.Empty;
        var mostrados = _config.BuscarClientes(filtro, _soloConSaldo).Value ?? new List<ClienteFiado>();

        UsuarioLabel.Text = _sesion.Usuario == null
            ? string.Empty
            : $"Sesión: {_sesion.Usuario.Nombre} · Rol: {_sesion.Usuario.Rol}";

        TotalClientesLabel.Text = todos.Count.ToString(CultureInfo.InvariantCulture);
        ConSaldoLabel.Text = todos.Count(c => c.SaldoActual > 0).ToString(CultureInfo.InvariantCulture);
        PorCobrarLabel.Text = Formato.Moneda(todos.Sum(c => c.SaldoActual));

        var puedeAbonar = _sesion.Usuario != null &&
            AutorizacionService.Puede(_sesion.Usuario, PermisoSistema.RegistrarAbono);

        ListaClientes.ItemsSource = mostrados.Select(c => new ClienteFila
        {
            Id = c.Id,
            Nombre = c.Nombre,
            Telefono = string.IsNullOrWhiteSpace(c.Telefono) ? "Sin teléfono" : c.Telefono,
            Saldo = Formato.Moneda(c.SaldoActual),
            Estado = c.SaldoActual > 0 ? "Con saldo pendiente" : "Al día",
            EstadoColor = c.SaldoActual > 0 ? ColorSaldo : ColorAlDia,
            PuedeAbonar = puedeAbonar && c.SaldoActual > 0
        }).ToList();

        ListaVaciaLabel.Text = filtro.Length > 0 || _soloConSaldo
            ? "No hay clientes que coincidan con el filtro."
            : "Aún no hay clientes registrados. Usa + Nuevo para dar de alta al primero.";

        ActualizarFiltros(todos, mostrados.Count);
    }

    private void ActualizarFiltros(List<ClienteFiado> todos, int mostrados)
    {
        var conSaldo = todos.Count(c => c.SaldoActual > 0);
        FiltroTodosButton.Text = $"Todos ({todos.Count})";
        FiltroSaldoButton.Text = $"Con saldo ({conSaldo})";

        FiltroTodosButton.BackgroundColor = _soloConSaldo ? Colors.Transparent : ColorPrimario;
        FiltroTodosButton.TextColor = _soloConSaldo ? ColorPrimario : Colors.White;
        FiltroSaldoButton.BackgroundColor = _soloConSaldo ? ColorPrimario : Colors.Transparent;
        FiltroSaldoButton.TextColor = _soloConSaldo ? Colors.White : ColorPrimario;

        ResultadosLabel.Text = mostrados == 1
            ? "1 cliente"
            : $"{mostrados} clientes";
    }

    private void OnBuscarCompleted(object sender, EventArgs e) => Cargar();

    private void OnBuscarClicked(object sender, EventArgs e)
    {
        Cargar();
        BuscarEntry.Unfocus();
    }

    private void OnFiltroTodosClicked(object sender, EventArgs e)
    {
        _soloConSaldo = false;
        BuscarEntry.Text = string.Empty;
        Cargar();
    }

    private void OnFiltroSaldoClicked(object sender, EventArgs e)
    {
        _soloConSaldo = true;
        BuscarEntry.Text = string.Empty;
        Cargar();
    }

    private void OnRefrescando(object sender, EventArgs e)
    {
        Cargar();
        Refresco.IsRefreshing = false;
    }

    private void OnNuevoClienteClicked(object sender, EventArgs e)
    {
        NombreEntry.Text = string.Empty;
        TelefonoEntry.Text = string.Empty;
        SaldoInicialEntry.Text = string.Empty;
        NuevoErrorLabel.IsVisible = false;
        NuevoOverlay.IsVisible = true;

        if (DeviceInfo.Idiom == DeviceIdiom.Desktop)
            NombreEntry.Focus();
    }

    private void OnCancelarNuevoClicked(object sender, EventArgs e)
    {
        NuevoOverlay.IsVisible = false;
        if (DeviceInfo.Idiom == DeviceIdiom.Desktop)
            BuscarEntry.Focus();
    }

    private async void OnGuardarClienteClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NombreEntry.Text))
        {
            MostrarError(NuevoErrorLabel, "Escribe el nombre del cliente.");
            return;
        }

        var saldoInicial = string.IsNullOrWhiteSpace(SaldoInicialEntry.Text)
            ? 0
            : LeerCentavos(SaldoInicialEntry.Text);

        if (!saldoInicial.HasValue)
        {
            MostrarError(NuevoErrorLabel, "Captura un saldo inicial válido, por ejemplo 0.00.");
            return;
        }

        NuevoErrorLabel.IsVisible = false;
        GuardarClienteButton.IsEnabled = false;
        try
        {
            _sesion.Aplicar();
            var resultado = _config.AgregarClienteFiado(new ClienteFiado
            {
                Nombre = NombreEntry.Text ?? string.Empty,
                Telefono = TelefonoEntry.Text ?? string.Empty,
                SaldoActual = saldoInicial.Value
            });

            if (resultado.IsError)
            {
                MostrarError(NuevoErrorLabel, resultado.Message);
                return;
            }

            NuevoOverlay.IsVisible = false;
            BuscarEntry.Text = string.Empty;
            _soloConSaldo = false;
            Cargar();
            await DisplayAlertAsync("Cliente agregado", resultado.Message, "Continuar");
        }
        finally
        {
            GuardarClienteButton.IsEnabled = true;
        }
    }

    private void OnAbonarClicked(object sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: ClienteFila fila })
            return;

        if (_sesion.Usuario == null ||
            !AutorizacionService.Puede(_sesion.Usuario, PermisoSistema.RegistrarAbono))
        {
            _ = DisplayAlertAsync("Sin permiso", "Tu rol no permite registrar abonos.", "Continuar");
            return;
        }

        _clienteAbono = fila;
        AbonoClienteLabel.Text = fila.Nombre;
        AbonoSaldoLabel.Text = $"Saldo pendiente: {fila.Saldo}";
        AbonoMontoEntry.Text = string.Empty;
        AbonoErrorLabel.IsVisible = false;
        AbonoOverlay.IsVisible = true;

        if (DeviceInfo.Idiom == DeviceIdiom.Desktop)
            AbonoMontoEntry.Focus();
    }

    private void OnCancelarAbonoClicked(object sender, EventArgs e)
    {
        _clienteAbono = null;
        AbonoOverlay.IsVisible = false;
    }

    private async void OnRegistrarAbonoClicked(object sender, EventArgs e)
    {
        if (_sesion.Usuario == null || _clienteAbono == null)
            return;

        var monto = LeerCentavos(AbonoMontoEntry.Text);
        if (!monto.HasValue || monto.Value <= 0)
        {
            MostrarError(AbonoErrorLabel, "Captura un monto mayor a cero.");
            return;
        }

        AbonoErrorLabel.IsVisible = false;
        RegistrarAbonoButton.IsEnabled = false;
        try
        {
            _sesion.Aplicar();
            var resultado = _config.RegistrarAbonoCliente(
                _clienteAbono.Id, monto.Value, _sesion.Usuario.Nombre);

            if (resultado.IsError)
            {
                MostrarError(AbonoErrorLabel, resultado.Message);
                return;
            }

            AbonoOverlay.IsVisible = false;
            _clienteAbono = null;
            Cargar();
            await DisplayAlertAsync("Abono registrado",
                $"Saldo restante: {Formato.Moneda(resultado.Value.SaldoRestante)}", "Continuar");
        }
        finally
        {
            RegistrarAbonoButton.IsEnabled = true;
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
