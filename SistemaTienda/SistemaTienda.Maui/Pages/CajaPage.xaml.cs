using System.Globalization;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Maui.Models;
using SistemaTienda.Maui.Services;

namespace SistemaTienda.Maui.Pages;

public partial class CajaPage : ContentPage
{
    private static readonly Color ColorCuadra = Color.FromArgb("#2E6B4F");
    private static readonly Color ColorDiferencia = Color.FromArgb("#B3261E");
    private static readonly Color ColorNeutro = Color.FromArgb("#6B7280");

    private readonly CorteCajaService _cortes;
    private readonly ConfiguracionService _config;
    private readonly SesionApp _sesion;
    private readonly List<LineaConteo> _lineasConteo = new();
    private CorteCaja? _pendiente;
    private bool _actualizandoConteo;
    private bool _conteoAplicado;

    public CajaPage(CorteCajaService cortes, ConfiguracionService config, SesionApp sesion)
    {
        InitializeComponent();
        _cortes = cortes;
        _config = config;
        _sesion = sesion;

        MovimientoPicker.ItemsSource = new[] { TipoMovimientoCaja.Ingreso, TipoMovimientoCaja.Retiro };
        MovimientoPicker.SelectedIndex = 0;
        ConstruirConteo();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Cargar(reiniciarCierre: true);
    }

    private void OnActualizarClicked(object sender, EventArgs e) => Cargar(reiniciarCierre: true);

    private void Cargar(bool reiniciarCierre)
    {
        if (_sesion.Usuario == null)
            return;

        _sesion.Aplicar();

        var pendiente = _cortes.ObtenerResumenPendiente();
        _pendiente = pendiente.IsSuccess ? pendiente.Value : null;

        if (_pendiente != null)
        {
            var corte = _pendiente;
            PeriodoLabel.Text =
                $"Periodo abierto desde {Formato.FechaHora(corte.Desde)} · {corte.NumeroVentas} venta(s)";
            EfectivoLabel.Text = Formato.Moneda(corte.EfectivoEsperado);
            FondoLabel.Text = $"Incluye un fondo inicial de {Formato.Moneda(corte.FondoInicial)}.";
            VentasEfectivoLabel.Text = Formato.Moneda(corte.VentasEfectivo);
            VentasTarjetaLabel.Text = Formato.Moneda(corte.VentasTarjeta);
            VentasFiadoLabel.Text = Formato.Moneda(corte.VentasFiado);
            AbonosLabel.Text = Formato.Moneda(corte.AbonosEfectivo);
            MovimientosLabel.Text = $"+{Formato.Moneda(corte.IngresosCaja)} / -{Formato.Moneda(corte.RetirosCaja)}";
            GastosLabel.Text = Formato.Moneda(corte.GastosCaja);
        }
        else
        {
            PeriodoLabel.Text = "No hay un periodo de caja abierto.";
            EfectivoLabel.Text = "—";
            FondoLabel.Text = string.Empty;
        }

        var configuracion = _config.ObtenerConfiguracion().Value;
        var fiadoActivo = configuracion?.FiadoActivado == true;
        PanelAbono.IsVisible = fiadoActivo;
        if (fiadoActivo)
        {
            CargarClientesAbono();
        }
        else
        {
            ClienteAbonoPicker.ItemsSource = null;
            AbonoSaldoLabel.Text = string.Empty;
        }

        var historial = _cortes.ObtenerCortes(20);
        var cortesCerrados = historial.IsSuccess && historial.Value != null
            ? historial.Value
            : new List<CorteCaja>();
        BindableLayout.SetItemsSource(HistorialCortes,
            cortesCerrados.Select(CrearCorteFila).ToList());
        HistorialVacioLabel.IsVisible = cortesCerrados.Count == 0;

        if (reiniciarCierre)
        {
            FondoEntry.Text = _pendiente != null ? Pesos(_pendiente.FondoInicial) : string.Empty;
            LimpiarConteo(limpiarContado: true);
            ObservacionesEditor.Text = string.Empty;
            MovimientoPicker.SelectedIndex = 0;
            CierreErrorLabel.IsVisible = false;
            MovimientoErrorLabel.IsVisible = false;
            AbonoErrorLabel.IsVisible = false;
        }

        ActualizarCierre();
    }

    private void CargarClientesAbono()
    {
        var clientes = _config.ObtenerClientesConSaldo().Value ?? new List<ClienteFiado>();
        var clienteActual = (ClienteAbonoPicker.SelectedItem as ClienteOpcion)?.Cliente.Id;

        var opciones = clientes
            .OrderBy(c => c.Nombre, StringComparer.CurrentCultureIgnoreCase)
            .Select(c => new ClienteOpcion { Cliente = c })
            .ToList();

        ClienteAbonoPicker.ItemsSource = opciones;
        if (clienteActual.HasValue)
            ClienteAbonoPicker.SelectedItem = opciones.FirstOrDefault(o => o.Cliente.Id == clienteActual.Value);

        ActualizarSaldoAbono();
    }

    private void OnCierreChanged(object sender, TextChangedEventArgs e) => ActualizarCierre();

    private void ConstruirConteo()
    {
        var grupo = string.Empty;
        foreach (var denominacion in ConteoEfectivo.Denominaciones)
        {
            if (denominacion.Grupo != grupo)
            {
                grupo = denominacion.Grupo;
                var titulo = new Label
                {
                    Text = grupo,
                    Margin = new Thickness(0, 6, 0, 0)
                };
                titulo.SetDynamicResource(Label.StyleProperty, "EtiquetaSeccion");
                DenominacionesPanel.Children.Add(titulo);
            }

            var fila = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 10
            };

            var etiqueta = new Label
            {
                Text = denominacion.Etiqueta,
                VerticalOptions = LayoutOptions.Center
            };

            var entrada = new Entry
            {
                Placeholder = "0",
                Keyboard = Keyboard.Numeric,
                WidthRequest = 76,
                MaxLength = 6,
                HorizontalTextAlignment = TextAlignment.End
            };
            entrada.SetDynamicResource(Entry.StyleProperty, "CampoTexto");
            entrada.TextChanged += OnConteoChanged;

            var subtotal = new Label
            {
                Text = Formato.Moneda(0),
                WidthRequest = 92,
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.End,
                VerticalOptions = LayoutOptions.Center
            };

            Grid.SetColumn(entrada, 1);
            Grid.SetColumn(subtotal, 2);
            fila.Children.Add(etiqueta);
            fila.Children.Add(entrada);
            fila.Children.Add(subtotal);

            DenominacionesPanel.Children.Add(fila);
            _lineasConteo.Add(new LineaConteo(denominacion, entrada, subtotal));
        }
    }

    private void OnConteoChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_actualizandoConteo)
            ActualizarConteo();
    }

    private void ActualizarConteo()
    {
        _actualizandoConteo = true;
        try
        {
            long total = 0;
            var hayCaptura = false;
            var hayInvalido = false;

            foreach (var linea in _lineasConteo)
            {
                var texto = linea.Entrada.Text?.Trim() ?? string.Empty;
                if (texto.Length > 0)
                    hayCaptura = true;

                var cantidad = LeerCantidad(texto);
                if (!cantidad.HasValue)
                {
                    hayInvalido = true;
                    linea.Subtotal.Text = "—";
                    continue;
                }

                var subtotal = linea.Denominacion.Subtotal(cantidad.Value);
                linea.Subtotal.Text = Formato.Moneda(subtotal);
                total = checked(total + subtotal);
            }

            ConteoErrorLabel.IsVisible = hayInvalido;
            if (hayInvalido)
            {
                ConteoErrorLabel.Text = "Revisa las cantidades: solo se aceptan números enteros.";
                ConteoTotalLabel.Text = "—";
                ContadoEntry.IsReadOnly = true;
                ContadoEntry.Text = string.Empty;
                ContadoAyudaLabel.Text = "Corrige el conteo para calcular el total contado.";
                _conteoAplicado = false;
            }
            else if (hayCaptura)
            {
                ConteoTotalLabel.Text = Formato.Moneda(total);
                ContadoEntry.IsReadOnly = true;
                ContadoEntry.Text = Pesos(total);
                ContadoAyudaLabel.Text = "El total contado se calcula del conteo por denominaciones.";
                _conteoAplicado = true;
            }
            else
            {
                ConteoTotalLabel.Text = "—";
                ContadoEntry.IsReadOnly = false;
                if (_conteoAplicado)
                {
                    ContadoEntry.Text = string.Empty;
                    _conteoAplicado = false;
                }
                ContadoAyudaLabel.Text =
                    "Captura el total directamente o usa el conteo por denominaciones.";
            }
        }
        finally
        {
            _actualizandoConteo = false;
        }

        ActualizarCierre();
    }

    private void OnLimpiarConteoClicked(object sender, EventArgs e)
    {
        LimpiarConteo(limpiarContado: true);
        ActualizarCierre();
    }

    private void LimpiarConteo(bool limpiarContado)
    {
        _actualizandoConteo = true;
        try
        {
            foreach (var linea in _lineasConteo)
            {
                linea.Entrada.Text = string.Empty;
                linea.Subtotal.Text = Formato.Moneda(0);
            }

            ConteoTotalLabel.Text = "—";
            ConteoErrorLabel.IsVisible = false;
            ContadoEntry.IsReadOnly = false;
            _conteoAplicado = false;
            ContadoAyudaLabel.Text =
                "Captura el total directamente o usa el conteo por denominaciones.";
            if (limpiarContado)
                ContadoEntry.Text = string.Empty;
        }
        finally
        {
            _actualizandoConteo = false;
        }
    }

    private static int? LeerCantidad(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return 0;

        if (!int.TryParse(texto.Trim(), NumberStyles.None, CultureInfo.InvariantCulture,
                out var cantidad))
            return null;

        return cantidad is >= 0 and <= 100_000 ? cantidad : null;
    }

    private static CorteFila CrearCorteFila(CorteCaja corte) => new()
    {
        Folio = $"Corte #{corte.Id}",
        Periodo = $"Del {Formato.FechaHora(corte.Desde)} al {Formato.FechaHora(corte.Hasta)}",
        Montos = $"Esperado {Formato.Moneda(corte.EfectivoEsperado)} · " +
                 $"Contado {Formato.Moneda(corte.EfectivoContado)}",
        Cierre = $"Cerrado el {Formato.FechaHora(corte.FechaRegistro)} por {corte.Usuario}",
        Diferencia = Formato.MonedaConSigno(corte.Diferencia),
        DiferenciaColor = corte.Diferencia == 0 ? ColorCuadra : ColorDiferencia,
        Observaciones = string.IsNullOrWhiteSpace(corte.Observaciones)
            ? string.Empty
            : $"Nota: {corte.Observaciones}"
    };

    private void ActualizarCierre()
    {
        var fondo = LeerCentavos(FondoEntry.Text);
        var contado = LeerCentavos(ContadoEntry.Text);
        var esperado = fondo.HasValue && _pendiente != null
            ? _pendiente.EfectivoEsperado - _pendiente.FondoInicial + fondo.Value
            : (long?)null;

        EsperadoCierreLabel.Text = esperado.HasValue ? Formato.Moneda(esperado.Value) : "—";
        CerrarCorteButton.IsEnabled = esperado.HasValue && contado.HasValue;

        if (!esperado.HasValue)
        {
            DiferenciaCorteLabel.Text = "—";
            DiferenciaCorteLabel.TextColor = ColorNeutro;
            DiferenciaAyudaLabel.Text = _pendiente == null
                ? "No hay un periodo abierto para cerrar."
                : "Captura un fondo inicial válido.";
            return;
        }

        if (!contado.HasValue)
        {
            DiferenciaCorteLabel.Text = "—";
            DiferenciaCorteLabel.TextColor = ColorNeutro;
            DiferenciaAyudaLabel.Text = "Captura el efectivo contado para calcular la diferencia.";
            return;
        }

        var diferencia = contado.Value - esperado.Value;
        DiferenciaCorteLabel.Text = Formato.MonedaConSigno(diferencia);
        DiferenciaCorteLabel.TextColor = diferencia == 0 ? ColorCuadra : ColorDiferencia;
        DiferenciaAyudaLabel.Text = diferencia switch
        {
            0 => "El corte cuadra con lo esperado.",
            > 0 => "Hay un sobrante. Explica el motivo en las observaciones.",
            _ => "Hay un faltante. Explica el motivo en las observaciones."
        };
    }

    private async void OnCerrarCorteClicked(object sender, EventArgs e)
    {
        if (_pendiente == null || _sesion.Usuario == null)
            return;

        var fondo = LeerCentavos(FondoEntry.Text);
        var contado = LeerCentavos(ContadoEntry.Text);

        if (!fondo.HasValue || !contado.HasValue)
        {
            MostrarError(CierreErrorLabel, "Captura un fondo inicial y un efectivo contado válidos.");
            return;
        }

        var esperado = _pendiente.EfectivoEsperado - _pendiente.FondoInicial + fondo.Value;
        var diferencia = contado.Value - esperado;
        var observaciones = ObservacionesEditor.Text?.Trim() ?? string.Empty;

        if (diferencia != 0 && observaciones.Length == 0)
        {
            MostrarError(CierreErrorLabel, "La diferencia exige explicar el motivo antes de cerrar.");
            ObservacionesEditor.Focus();
            return;
        }

        CierreErrorLabel.IsVisible = false;
        var confirmar = await DisplayAlertAsync(
            "Cerrar el corte",
            $"Esperado: {Formato.Moneda(esperado)}\n" +
            $"Contado: {Formato.Moneda(contado.Value)}\n" +
            $"Diferencia: {Formato.MonedaConSigno(diferencia)}\n\n" +
            "El periodo quedará cerrado y no se podrá editar. ¿Continuar?",
            "Cerrar corte", "Cancelar");
        if (!confirmar)
            return;

        CerrarCorteButton.IsEnabled = false;
        try
        {
            _sesion.Aplicar();
            var resultado = _cortes.RegistrarCorte(contado.Value, fondo.Value,
                _sesion.Usuario.Nombre, observaciones);

            if (resultado.IsError)
            {
                MostrarError(CierreErrorLabel, resultado.Message);
                return;
            }

            var corte = resultado.Value;
            await DisplayAlertAsync(
                $"Corte #{corte.Id} registrado",
                $"Esperado: {Formato.Moneda(corte.EfectivoEsperado)}\n" +
                $"Contado: {Formato.Moneda(corte.EfectivoContado)}\n" +
                $"Diferencia: {Formato.MonedaConSigno(corte.Diferencia)}\n\n" +
                $"El próximo periodo inicia con {Formato.Moneda(corte.EfectivoContado)}.",
                "Continuar");

            Cargar(reiniciarCierre: true);
        }
        finally
        {
            ActualizarCierre();
        }
    }

    private async void OnRegistrarMovimientoClicked(object sender, EventArgs e)
    {
        if (_sesion.Usuario == null)
            return;

        var tipo = MovimientoPicker.SelectedItem as string ?? TipoMovimientoCaja.Ingreso;
        var monto = LeerCentavos(MovimientoMontoEntry.Text);
        var motivo = MovimientoMotivoEntry.Text?.Trim() ?? string.Empty;

        if (!monto.HasValue || monto.Value <= 0)
        {
            MostrarError(MovimientoErrorLabel, "Captura un monto mayor a cero.");
            return;
        }

        if (motivo.Length == 0)
        {
            MostrarError(MovimientoErrorLabel, "El motivo es obligatorio.");
            return;
        }

        if (tipo == TipoMovimientoCaja.Retiro)
        {
            var confirmar = await DisplayAlertAsync(
                "Registrar retiro",
                $"Se retirarán {Formato.Moneda(monto.Value)} de la caja.\n\nMotivo: {motivo}",
                "Retirar", "Cancelar");
            if (!confirmar)
                return;
        }

        MovimientoErrorLabel.IsVisible = false;
        MovimientoButton.IsEnabled = false;
        try
        {
            _sesion.Aplicar();
            var resultado = _cortes.RegistrarMovimiento(tipo, monto.Value, motivo, _sesion.Usuario.Nombre);

            if (resultado.IsError)
            {
                MostrarError(MovimientoErrorLabel, resultado.Message);
                return;
            }

            MovimientoMontoEntry.Text = string.Empty;
            MovimientoMotivoEntry.Text = string.Empty;
            await DisplayAlertAsync("Movimiento registrado", resultado.Message, "Continuar");
            Cargar(reiniciarCierre: false);
        }
        finally
        {
            MovimientoButton.IsEnabled = true;
        }
    }

    private void OnClienteAbonoChanged(object sender, EventArgs e) => ActualizarSaldoAbono();

    private void ActualizarSaldoAbono()
    {
        AbonoSaldoLabel.Text = ClienteAbonoPicker.SelectedItem is ClienteOpcion opcion
            ? $"Saldo pendiente: {Formato.Moneda(opcion.Cliente.SaldoActual)}"
            : "Selecciona un cliente con saldo pendiente.";
    }

    private async void OnRegistrarAbonoClicked(object sender, EventArgs e)
    {
        if (_sesion.Usuario == null)
            return;

        if (ClienteAbonoPicker.SelectedItem is not ClienteOpcion opcion)
        {
            MostrarError(AbonoErrorLabel, "Selecciona el cliente del abono.");
            return;
        }

        var monto = LeerCentavos(AbonoMontoEntry.Text);
        if (!monto.HasValue || monto.Value <= 0)
        {
            MostrarError(AbonoErrorLabel, "Captura un monto mayor a cero.");
            return;
        }

        AbonoErrorLabel.IsVisible = false;
        AbonoButton.IsEnabled = false;
        try
        {
            _sesion.Aplicar();
            var resultado = _config.RegistrarAbonoCliente(opcion.Cliente.Id, monto.Value, _sesion.Usuario.Nombre);

            if (resultado.IsError)
            {
                MostrarError(AbonoErrorLabel, resultado.Message);
                return;
            }

            AbonoMontoEntry.Text = string.Empty;
            await DisplayAlertAsync("Abono registrado",
                $"Saldo restante: {Formato.Moneda(resultado.Value.SaldoRestante)}", "Continuar");
            Cargar(reiniciarCierre: false);
        }
        finally
        {
            AbonoButton.IsEnabled = true;
        }
    }

    private static void MostrarError(Label label, string mensaje)
    {
        label.Text = mensaje;
        label.IsVisible = true;
    }

    private static string Pesos(long centavos) =>
        Dinero.APesos(centavos).ToString("0.00", CultureInfo.InvariantCulture);

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

    private sealed record LineaConteo(
        DenominacionEfectivo Denominacion, Entry Entrada, Label Subtotal);
}
