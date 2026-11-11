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
    private ClienteFila? _clienteEstado;

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
        var puedeEditar = _sesion.Usuario != null &&
            AutorizacionService.Puede(_sesion.Usuario, PermisoSistema.EditarCliente);

        ListaClientes.ItemsSource = mostrados.Select(c => new ClienteFila
        {
            Id = c.Id,
            Nombre = c.Nombre,
            Telefono = string.IsNullOrWhiteSpace(c.Telefono) ? "Sin teléfono" : c.Telefono,
            TelefonoReal = c.Telefono ?? string.Empty,
            Saldo = Formato.Moneda(c.SaldoActual),
            Estado = c.SaldoActual > 0 ? "Con saldo pendiente" : "Al día",
            EstadoColor = c.SaldoActual > 0 ? ColorSaldo : ColorAlDia,
            PuedeAbonar = puedeAbonar && c.SaldoActual > 0,
            PuedeEditar = puedeEditar
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

        AbrirAbono(fila);
    }

    private void AbrirAbono(ClienteFila fila)
    {
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

    private void OnEstadoCuentaClicked(object sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: ClienteFila fila })
            return;

        _clienteEstado = fila;
        AbrirEstadoCuenta();
    }

    private void AbrirEstadoCuenta()
    {
        if (_clienteEstado == null)
            return;

        _sesion.Aplicar();
        var resultado = _config.ObtenerEstadoCuentaCliente(_clienteEstado.Id);
        if (resultado.IsError)
        {
            _ = DisplayAlertAsync("No se pudo abrir", resultado.Message, "Continuar");
            return;
        }

        var estado = resultado.Value;
        EstadoClienteLabel.Text = estado.Cliente.Nombre;
        EstadoContactoLabel.Text = string.IsNullOrWhiteSpace(estado.Cliente.Telefono)
            ? $"Sin teléfono · Cliente desde {Formato.FechaHora(estado.Cliente.FechaRegistro)}"
            : $"{estado.Cliente.Telefono} · Cliente desde {Formato.FechaHora(estado.Cliente.FechaRegistro)}";
        EstadoSaldoLabel.Text = Formato.Moneda(estado.SaldoActual);
        EstadoSaldoLabel.TextColor = estado.SaldoActual > 0 ? ColorSaldo : ColorAlDia;
        EstadoFiadoLabel.Text = Formato.Moneda(estado.TotalFiado);
        EstadoAbonadoLabel.Text = Formato.Moneda(estado.TotalAbonado);

        var movimientos = estado.Ventas
            .Select(v => new EstadoCuentaFila
            {
                FechaOrden = v.Fecha,
                Fecha = Formato.FechaHora(v.Fecha),
                Tipo = $"Venta #{v.Id} a fiado",
                Detalle = $"Atendió {v.Usuario}",
                Monto = Formato.MonedaConSigno(v.PagoFiado),
                MontoColor = ColorSaldo
            })
            .Concat(estado.Abonos.Select(a => new EstadoCuentaFila
            {
                FechaOrden = a.Fecha,
                Fecha = Formato.FechaHora(a.Fecha),
                Tipo = "Abono",
                Detalle = string.IsNullOrWhiteSpace(a.Usuario)
                    ? a.MetodoPago
                    : $"{a.MetodoPago} · Registró {a.Usuario}",
                Monto = Formato.MonedaConSigno(-a.Monto),
                MontoColor = ColorAlDia
            }))
            .OrderByDescending(m => m.FechaOrden)
            .ToList();

        ListaMovimientos.ItemsSource = movimientos;
        EstadoMovimientosLabel.Text = movimientos.Count == 1
            ? "1 movimiento"
            : $"{movimientos.Count} movimientos";

        var puedeEditar = _sesion.Usuario != null &&
            AutorizacionService.Puede(_sesion.Usuario, PermisoSistema.EditarCliente);
        EditarClienteButton.IsVisible = puedeEditar;
        AbonarDesdeEstadoButton.IsVisible = _sesion.Usuario != null &&
            AutorizacionService.Puede(_sesion.Usuario, PermisoSistema.RegistrarAbono) &&
            estado.SaldoActual > 0;

        EstadoOverlay.IsVisible = true;
    }

    private void OnEstadoRefrescando(object sender, EventArgs e)
    {
        AbrirEstadoCuenta();
        EstadoRefresco.IsRefreshing = false;
    }

    private void OnCerrarEstadoClicked(object sender, EventArgs e)
    {
        EstadoOverlay.IsVisible = false;
        _clienteEstado = null;
    }

    private void OnEditarClienteClicked(object sender, EventArgs e)
    {
        if (_clienteEstado == null)
            return;

        if (_sesion.Usuario == null ||
            !AutorizacionService.Puede(_sesion.Usuario, PermisoSistema.EditarCliente))
        {
            _ = DisplayAlertAsync("Sin permiso", "Tu rol no permite editar clientes.", "Continuar");
            return;
        }

        EditarNombreEntry.Text = _clienteEstado.Nombre;
        EditarTelefonoEntry.Text = _clienteEstado.TelefonoReal;
        EditarErrorLabel.IsVisible = false;
        EstadoOverlay.IsVisible = false;
        EditarOverlay.IsVisible = true;

        if (DeviceInfo.Idiom == DeviceIdiom.Desktop)
            EditarNombreEntry.Focus();
    }

    private void OnCancelarEdicionClicked(object sender, EventArgs e)
    {
        EditarOverlay.IsVisible = false;
        if (_clienteEstado != null)
            AbrirEstadoCuenta();
    }

    private async void OnGuardarEdicionClicked(object sender, EventArgs e)
    {
        if (_clienteEstado == null)
            return;

        if (string.IsNullOrWhiteSpace(EditarNombreEntry.Text))
        {
            MostrarError(EditarErrorLabel, "Escribe el nombre del cliente.");
            return;
        }

        EditarErrorLabel.IsVisible = false;
        GuardarEdicionButton.IsEnabled = false;
        try
        {
            _sesion.Aplicar();
            var resultado = _config.EditarClienteFiado(
                _clienteEstado.Id,
                EditarNombreEntry.Text ?? string.Empty,
                EditarTelefonoEntry.Text ?? string.Empty);

            if (resultado.IsError)
            {
                MostrarError(EditarErrorLabel, resultado.Message);
                return;
            }

            EditarOverlay.IsVisible = false;
            Cargar();

            var nombre = (EditarNombreEntry.Text ?? string.Empty).Trim();
            var telefono = (EditarTelefonoEntry.Text ?? string.Empty).Trim();
            _clienteEstado.Nombre = nombre;
            _clienteEstado.TelefonoReal = telefono;
            _clienteEstado.Telefono = string.IsNullOrWhiteSpace(telefono) ? "Sin teléfono" : telefono;

            AbrirEstadoCuenta();
            await DisplayAlertAsync("Cliente actualizado", resultado.Message, "Continuar");
        }
        finally
        {
            GuardarEdicionButton.IsEnabled = true;
        }
    }

    private void OnAbonarDesdeEstadoClicked(object sender, EventArgs e)
    {
        if (_clienteEstado == null)
            return;

        var fila = _clienteEstado;
        EstadoOverlay.IsVisible = false;
        AbrirAbono(fila);
    }

    private void OnCancelarAbonoClicked(object sender, EventArgs e)
    {
        _clienteAbono = null;
        AbonoOverlay.IsVisible = false;
        if (_clienteEstado != null)
            AbrirEstadoCuenta();
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
            if (_clienteEstado != null)
                AbrirEstadoCuenta();
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
