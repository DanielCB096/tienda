using System.Globalization;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Maui.Models;
using SistemaTienda.Maui.Services;

namespace SistemaTienda.Maui.Pages;

public partial class ProveedoresPage : ContentPage
{
    private enum FiltroProveedores
    {
        Todos,
        ConSaldo,
        Inactivos
    }

    private static readonly Color ColorSaldo = Color.FromArgb("#B3261E");
    private static readonly Color ColorAlDia = Color.FromArgb("#2E6B4F");
    private static readonly Color ColorPrimario = Color.FromArgb("#2E6B4F");

    private readonly ConfiguracionService _config;
    private readonly SesionApp _sesion;
    private static readonly List<string> MetodosPago = new(MetodoPagoProveedor.Todos);
    private FiltroProveedores _filtro;
    private ProveedorFila? _proveedorPago;
    private ProveedorFila? _proveedorEstado;

    public ProveedoresPage(ConfiguracionService config, SesionApp sesion)
    {
        InitializeComponent();
        _config = config;
        _sesion = sesion;
        PagoMetodoPicker.ItemsSource = MetodosPago;
        PagoMetodoPicker.SelectedIndex = 0;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Cargar();
    }

    private void Cargar()
    {
        _sesion.Aplicar();

        var todos = _config.ObtenerTodosLosProveedores().Value ?? new List<Proveedor>();
        var filtro = BuscarEntry.Text?.Trim() ?? string.Empty;
        var mostrados = _config.BuscarProveedores(filtro, _filtro == FiltroProveedores.ConSaldo).Value
            ?? new List<Proveedor>();
        if (_filtro == FiltroProveedores.Inactivos)
            mostrados = mostrados.Where(p => !p.Activo).ToList();

        UsuarioLabel.Text = _sesion.Usuario == null
            ? string.Empty
            : $"Sesión: {_sesion.Usuario.Nombre} · Rol: {_sesion.Usuario.Rol}";

        TotalProveedoresLabel.Text = todos.Count.ToString(CultureInfo.InvariantCulture);
        ConSaldoLabel.Text = todos.Count(p => p.SaldoPendiente > 0).ToString(CultureInfo.InvariantCulture);
        PorPagarLabel.Text = Formato.Moneda(todos.Sum(p => p.SaldoPendiente));

        var puedePagar = _sesion.Usuario != null &&
            AutorizacionService.Puede(_sesion.Usuario, PermisoSistema.PagarProveedor);
        var puedeEditar = _sesion.Usuario != null &&
            AutorizacionService.Puede(_sesion.Usuario, PermisoSistema.EditarProveedor);
        var puedeCambiarEstado = _sesion.Usuario != null &&
            AutorizacionService.Puede(_sesion.Usuario, PermisoSistema.CambiarEstadoProveedor);

        ListaProveedores.ItemsSource = mostrados.Select(p => new ProveedorFila
        {
            Id = p.Id,
            Nombre = p.Nombre,
            Clave = string.IsNullOrWhiteSpace(p.Clave) ? "Sin clave" : p.Clave,
            ClaveReal = p.Clave ?? string.Empty,
            Telefono = string.IsNullOrWhiteSpace(p.Telefono) ? "Sin teléfono" : p.Telefono,
            TelefonoReal = p.Telefono ?? string.Empty,
            Direccion = p.Direccion ?? string.Empty,
            DireccionReal = p.Direccion ?? string.Empty,
            TieneDireccion = !string.IsNullOrWhiteSpace(p.Direccion),
            Saldo = Formato.Moneda(p.SaldoPendiente),
            Estado = !p.Activo
                ? p.SaldoPendiente > 0 ? "Inactivo · Saldo pendiente" : "Inactivo · Al día"
                : p.SaldoPendiente > 0 ? "Saldo pendiente" : "Al día",
            EstadoColor = !p.Activo ? Colors.Gray : p.SaldoPendiente > 0 ? ColorSaldo : ColorAlDia,
            Activo = p.Activo,
            PuedePagar = puedePagar && p.SaldoPendiente > 0,
            PuedeEditar = puedeEditar,
            PuedeCambiarEstado = puedeCambiarEstado
        }).ToList();

        ListaVaciaLabel.Text = filtro.Length > 0 || _filtro != FiltroProveedores.Todos
            ? "No hay proveedores que coincidan con el filtro."
            : "Aún no hay proveedores registrados. Usa + Nuevo para dar de alta al primero.";

        ActualizarFiltros(todos, mostrados.Count);
    }

    private void ActualizarFiltros(List<Proveedor> todos, int mostrados)
    {
        var conSaldo = todos.Count(p => p.SaldoPendiente > 0);
        var inactivos = todos.Count(p => !p.Activo);
        FiltroTodosButton.Text = $"Todos ({todos.Count})";
        FiltroSaldoButton.Text = $"Con saldo ({conSaldo})";
        FiltroInactivosButton.Text = $"Inactivos ({inactivos})";

        MarcarFiltro(FiltroTodosButton, _filtro == FiltroProveedores.Todos);
        MarcarFiltro(FiltroSaldoButton, _filtro == FiltroProveedores.ConSaldo);
        MarcarFiltro(FiltroInactivosButton, _filtro == FiltroProveedores.Inactivos);

        ResultadosLabel.Text = mostrados == 1
            ? "1 proveedor"
            : $"{mostrados} proveedores";
    }

    private void OnBuscarCompleted(object sender, EventArgs e) => Cargar();

    private void OnBuscarClicked(object sender, EventArgs e)
    {
        Cargar();
        BuscarEntry.Unfocus();
    }

    private void OnFiltroTodosClicked(object sender, EventArgs e)
    {
        _filtro = FiltroProveedores.Todos;
        BuscarEntry.Text = string.Empty;
        Cargar();
    }

    private void OnFiltroSaldoClicked(object sender, EventArgs e)
    {
        _filtro = FiltroProveedores.ConSaldo;
        BuscarEntry.Text = string.Empty;
        Cargar();
    }

    private void OnFiltroInactivosClicked(object sender, EventArgs e)
    {
        _filtro = FiltroProveedores.Inactivos;
        BuscarEntry.Text = string.Empty;
        Cargar();
    }

    private void MarcarFiltro(Button boton, bool seleccionado)
    {
        boton.BackgroundColor = seleccionado ? ColorPrimario : Colors.Transparent;
        boton.TextColor = seleccionado ? Colors.White : ColorPrimario;
    }

    private async void OnCambiarEstadoClicked(object sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: ProveedorFila fila })
            return;

        if (_sesion.Usuario == null ||
            !AutorizacionService.Puede(_sesion.Usuario, PermisoSistema.CambiarEstadoProveedor))
        {
            await DisplayAlertAsync("Sin permiso", "Solo el dueño puede dar de baja o reactivar proveedores.", "Continuar");
            return;
        }

        var reactivar = !fila.Activo;
        var confirmar = await DisplayAlertAsync(
            reactivar ? "Reactivar proveedor" : "Dar de baja al proveedor",
            reactivar
                ? "El proveedor volverá a aparecer como activo en el directorio."
                : "Se conservará su historial. Si tiene saldo pendiente, podrás registrar pagos para liquidarlo.",
            reactivar ? "Reactivar" : "Dar de baja",
            "Cancelar");
        if (!confirmar)
            return;

        _sesion.Aplicar();
        var resultado = reactivar
            ? _config.ReactivarProveedor(fila.Id)
            : _config.DesactivarProveedor(fila.Id);
        if (resultado.IsError)
        {
            await DisplayAlertAsync("No se pudo cambiar el estado", resultado.Message, "Continuar");
            return;
        }

        Cargar();
        await DisplayAlertAsync(reactivar ? "Proveedor reactivado" : "Proveedor dado de baja",
            resultado.Message, "Continuar");
    }

    private void OnRefrescando(object sender, EventArgs e)
    {
        Cargar();
        Refresco.IsRefreshing = false;
    }

    private void OnNuevoProveedorClicked(object sender, EventArgs e)
    {
        NombreEntry.Text = string.Empty;
        ClaveEntry.Text = string.Empty;
        TelefonoEntry.Text = string.Empty;
        DireccionEntry.Text = string.Empty;
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

    private async void OnGuardarProveedorClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NombreEntry.Text))
        {
            MostrarError(NuevoErrorLabel, "Escribe el nombre del proveedor.");
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
        GuardarProveedorButton.IsEnabled = false;
        try
        {
            _sesion.Aplicar();
            if (!await ConfirmarPosiblesDuplicadosProveedorAsync(
                    NombreEntry.Text ?? string.Empty,
                    ClaveEntry.Text ?? string.Empty,
                    null,
                    NuevoErrorLabel))
                return;

            var resultado = _config.AgregarProveedor(new Proveedor
            {
                Nombre = NombreEntry.Text ?? string.Empty,
                Clave = ClaveEntry.Text ?? string.Empty,
                Telefono = TelefonoEntry.Text ?? string.Empty,
                Direccion = DireccionEntry.Text ?? string.Empty,
                SaldoPendiente = saldoInicial.Value
            });

            if (resultado.IsError)
            {
                MostrarError(NuevoErrorLabel, resultado.Message);
                return;
            }

            NuevoOverlay.IsVisible = false;
            BuscarEntry.Text = string.Empty;
            _filtro = FiltroProveedores.Todos;
            Cargar();
            await DisplayAlertAsync("Proveedor agregado", resultado.Message, "Continuar");
        }
        finally
        {
            GuardarProveedorButton.IsEnabled = true;
        }
    }

    private void OnEstadoCuentaClicked(object sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: ProveedorFila fila })
            return;

        _proveedorEstado = fila;
        AbrirEstadoCuenta();
    }

    private void AbrirEstadoCuenta()
    {
        if (_proveedorEstado == null)
            return;

        _sesion.Aplicar();
        var resultado = _config.ObtenerEstadoCuentaProveedor(_proveedorEstado.Id);
        if (resultado.IsError)
        {
            _ = DisplayAlertAsync("No se pudo abrir", resultado.Message, "Continuar");
            return;
        }

        var estado = resultado.Value;
        EstadoProveedorLabel.Text = estado.Proveedor.Nombre;

        var partes = new List<string>();
        if (!string.IsNullOrWhiteSpace(estado.Proveedor.Clave))
            partes.Add($"Clave/RFC {estado.Proveedor.Clave}");
        if (!string.IsNullOrWhiteSpace(estado.Proveedor.Telefono))
            partes.Add(estado.Proveedor.Telefono);
        if (!string.IsNullOrWhiteSpace(estado.Proveedor.Direccion))
            partes.Add(estado.Proveedor.Direccion);
        partes.Add($"Proveedor desde {Formato.FechaHora(estado.Proveedor.FechaRegistro)}");
        EstadoContactoLabel.Text = string.Join(" · ", partes);

        EstadoSaldoLabel.Text = Formato.Moneda(estado.SaldoPendiente);
        EstadoSaldoLabel.TextColor = estado.SaldoPendiente > 0 ? ColorSaldo : ColorAlDia;
        EstadoPagadoLabel.Text = Formato.Moneda(estado.TotalPagado);
        EstadoNumeroPagosLabel.Text = estado.Pagos.Count == 1
            ? "1"
            : estado.Pagos.Count.ToString(CultureInfo.InvariantCulture);

        var movimientos = estado.Pagos.Select(p => new EstadoCuentaProveedorFila
        {
            FechaOrden = p.Fecha,
            Fecha = Formato.FechaHora(p.Fecha),
            Tipo = "Pago",
            Detalle = DetallePago(p),
            Monto = Formato.MonedaConSigno(-p.Monto),
            MontoColor = ColorAlDia
        }).ToList();

        ListaMovimientos.ItemsSource = movimientos;
        EstadoMovimientosLabel.Text = movimientos.Count == 1
            ? "1 movimiento"
            : $"{movimientos.Count} movimientos";

        var puedeEditar = _sesion.Usuario != null &&
            AutorizacionService.Puede(_sesion.Usuario, PermisoSistema.EditarProveedor);
        EditarProveedorButton.IsVisible = puedeEditar;
        PagarDesdeEstadoButton.IsVisible = _sesion.Usuario != null &&
            AutorizacionService.Puede(_sesion.Usuario, PermisoSistema.PagarProveedor) &&
            estado.SaldoPendiente > 0;

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
        _proveedorEstado = null;
    }

    private void OnEditarProveedorClicked(object sender, EventArgs e)
    {
        if (_proveedorEstado == null)
            return;

        if (_sesion.Usuario == null ||
            !AutorizacionService.Puede(_sesion.Usuario, PermisoSistema.EditarProveedor))
        {
            _ = DisplayAlertAsync("Sin permiso", "Tu rol no permite editar proveedores.", "Continuar");
            return;
        }

        EditarNombreEntry.Text = _proveedorEstado.Nombre;
        EditarClaveEntry.Text = _proveedorEstado.ClaveReal;
        EditarTelefonoEntry.Text = _proveedorEstado.TelefonoReal;
        EditarDireccionEntry.Text = _proveedorEstado.DireccionReal;
        EditarErrorLabel.IsVisible = false;
        EstadoOverlay.IsVisible = false;
        EditarOverlay.IsVisible = true;

        if (DeviceInfo.Idiom == DeviceIdiom.Desktop)
            EditarNombreEntry.Focus();
    }

    private void OnCancelarEdicionClicked(object sender, EventArgs e)
    {
        EditarOverlay.IsVisible = false;
        if (_proveedorEstado != null)
            AbrirEstadoCuenta();
    }

    private async void OnGuardarEdicionClicked(object sender, EventArgs e)
    {
        if (_proveedorEstado == null)
            return;

        if (string.IsNullOrWhiteSpace(EditarNombreEntry.Text))
        {
            MostrarError(EditarErrorLabel, "Escribe el nombre del proveedor.");
            return;
        }

        EditarErrorLabel.IsVisible = false;
        GuardarEdicionButton.IsEnabled = false;
        try
        {
            _sesion.Aplicar();
            if (!await ConfirmarPosiblesDuplicadosProveedorAsync(
                    EditarNombreEntry.Text ?? string.Empty,
                    EditarClaveEntry.Text ?? string.Empty,
                    _proveedorEstado.Id,
                    EditarErrorLabel))
                return;

            var resultado = _config.EditarProveedor(
                _proveedorEstado.Id,
                EditarNombreEntry.Text ?? string.Empty,
                EditarClaveEntry.Text ?? string.Empty,
                EditarTelefonoEntry.Text ?? string.Empty,
                EditarDireccionEntry.Text ?? string.Empty);

            if (resultado.IsError)
            {
                MostrarError(EditarErrorLabel, resultado.Message);
                return;
            }

            EditarOverlay.IsVisible = false;
            Cargar();

            var nombre = (EditarNombreEntry.Text ?? string.Empty).Trim();
            var clave = (EditarClaveEntry.Text ?? string.Empty).Trim();
            var telefono = (EditarTelefonoEntry.Text ?? string.Empty).Trim();
            var direccion = (EditarDireccionEntry.Text ?? string.Empty).Trim();
            _proveedorEstado.Nombre = nombre;
            _proveedorEstado.ClaveReal = clave;
            _proveedorEstado.Clave = string.IsNullOrWhiteSpace(clave) ? "Sin clave" : clave;
            _proveedorEstado.TelefonoReal = telefono;
            _proveedorEstado.Telefono = string.IsNullOrWhiteSpace(telefono) ? "Sin teléfono" : telefono;
            _proveedorEstado.DireccionReal = direccion;
            _proveedorEstado.Direccion = direccion;
            _proveedorEstado.TieneDireccion = !string.IsNullOrWhiteSpace(direccion);

            AbrirEstadoCuenta();
            await DisplayAlertAsync("Proveedor actualizado", resultado.Message, "Continuar");
        }
        finally
        {
            GuardarEdicionButton.IsEnabled = true;
        }
    }

    private async Task<bool> ConfirmarPosiblesDuplicadosProveedorAsync(
        string nombre, string clave, int? excluirProveedorId, Label errorLabel)
    {
        var resultado = _config.DetectarDuplicadosProveedor(nombre, clave, excluirProveedorId);
        if (resultado.IsError)
        {
            MostrarError(errorLabel, resultado.Message);
            return false;
        }

        var coincidencias = resultado.Value;
        if (coincidencias.Count == 0)
            return true;

        var detalle = string.Join("\n", coincidencias.Select(proveedor =>
            $"- {proveedor.Nombre} · " +
            (string.IsNullOrWhiteSpace(proveedor.Clave) ? "sin clave" : proveedor.Clave)));
        return await DisplayAlertAsync(
            "Posible proveedor duplicado",
            $"Coincide el nombre o clave con:\n{detalle}\n\n¿Deseas guardar de todos modos?",
            "Guardar",
            "Revisar");
    }

    private void OnPagarClicked(object sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: ProveedorFila fila })
            return;

        if (_sesion.Usuario == null ||
            !AutorizacionService.Puede(_sesion.Usuario, PermisoSistema.PagarProveedor))
        {
            _ = DisplayAlertAsync("Sin permiso", "Tu rol no permite registrar pagos a proveedores.", "Continuar");
            return;
        }

        AbrirPago(fila);
    }

    private void AbrirPago(ProveedorFila fila)
    {
        _proveedorPago = fila;
        PagoProveedorLabel.Text = fila.Nombre;
        PagoSaldoLabel.Text = $"Saldo pendiente: {fila.Saldo}";
        PagoMontoEntry.Text = string.Empty;
        PagoMetodoPicker.SelectedIndex = 0;
        ActualizarAvisoMetodoPago();
        PagoErrorLabel.IsVisible = false;
        PagoOverlay.IsVisible = true;

        if (DeviceInfo.Idiom == DeviceIdiom.Desktop)
            PagoMontoEntry.Focus();
    }

    private void OnPagarDesdeEstadoClicked(object sender, EventArgs e)
    {
        if (_proveedorEstado == null)
            return;

        var fila = _proveedorEstado;
        EstadoOverlay.IsVisible = false;
        AbrirPago(fila);
    }

    private void OnCancelarPagoClicked(object sender, EventArgs e)
    {
        _proveedorPago = null;
        PagoOverlay.IsVisible = false;
        if (_proveedorEstado != null)
            AbrirEstadoCuenta();
    }

    private void OnPagoMetodoChanged(object sender, EventArgs e) => ActualizarAvisoMetodoPago();

    private void ActualizarAvisoMetodoPago()
    {
        var metodo = PagoMetodoPicker.SelectedItem as string ?? MetodoPagoProveedor.Efectivo;
        PagoAvisoLabel.Text = metodo == MetodoPagoProveedor.Efectivo
            ? "El pago en efectivo genera automáticamente un retiro de caja por el mismo monto y reduce el efectivo esperado del corte."
            : "Este pago no afecta el efectivo de la caja; solo reduce el saldo del proveedor.";
    }

    private async void OnRegistrarPagoClicked(object sender, EventArgs e)
    {
        if (_sesion.Usuario == null || _proveedorPago == null)
            return;

        var monto = LeerCentavos(PagoMontoEntry.Text);
        if (!monto.HasValue || monto.Value <= 0)
        {
            MostrarError(PagoErrorLabel, "Captura un monto mayor a cero.");
            return;
        }

        var metodo = PagoMetodoPicker.SelectedItem as string ?? MetodoPagoProveedor.Efectivo;

        PagoErrorLabel.IsVisible = false;
        RegistrarPagoButton.IsEnabled = false;
        try
        {
            _sesion.Aplicar();
            var resultado = _config.RegistrarPagoProveedor(_proveedorPago.Id, monto.Value, metodo);

            if (resultado.IsError)
            {
                MostrarError(PagoErrorLabel, resultado.Message);
                return;
            }

            PagoOverlay.IsVisible = false;
            _proveedorPago = null;
            Cargar();
            if (_proveedorEstado != null)
                AbrirEstadoCuenta();
            var detalle = $"Método: {resultado.Value.MetodoPago}\n" +
                          $"Saldo restante: {Formato.Moneda(resultado.Value.SaldoRestante)}";
            if (resultado.Value.MovimientoCajaId.HasValue)
                detalle += $"\nRetiro de caja #{resultado.Value.MovimientoCajaId.Value} registrado.";
            await DisplayAlertAsync("Pago registrado", detalle, "Continuar");
        }
        finally
        {
            RegistrarPagoButton.IsEnabled = true;
        }
    }

    private static string DetallePago(PagoProveedor pago)
    {
        var metodo = string.IsNullOrWhiteSpace(pago.MetodoPago)
            ? "Método no registrado (pago anterior a v9)"
            : pago.MetodoPago;
        var usuario = string.IsNullOrWhiteSpace(pago.Usuario)
            ? string.Empty
            : $"Registró {pago.Usuario} · ";
        var retiro = pago.MovimientoCajaId.HasValue
            ? $" · retiro de caja #{pago.MovimientoCajaId.Value}"
            : string.Empty;
        return $"{usuario}{metodo} · saldo {Formato.Moneda(pago.SaldoAnterior)} → {Formato.Moneda(pago.SaldoNuevo)}{retiro}";
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
