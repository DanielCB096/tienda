using System.Globalization;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Maui.Models;
using SistemaTienda.Maui.Services;

namespace SistemaTienda.Maui.Pages;

public partial class PosPage : ContentPage
{
    private readonly VentaService _ventas;
    private readonly InventoryService _inventario;
    private readonly ConfiguracionService _configuracionService;
    private readonly TicketService _ticketService;
    private readonly TicketEsperaService _ticketsEspera;
    private readonly SesionApp _sesion;

    private readonly List<CarritoFila> _carrito = new();
    private ConfiguracionTienda? _config;
    private CancellationTokenSource? _sugerenciasCts;
    private bool _limpiandoBuscador;
    private int? _ticketEsperaId;

    public PosPage(VentaService ventas, InventoryService inventario,
        ConfiguracionService configuracion, TicketService ticketService,
        TicketEsperaService ticketsEspera, SesionApp sesion)
    {
        InitializeComponent();
        _ventas = ventas;
        _inventario = inventario;
        _configuracionService = configuracion;
        _ticketService = ticketService;
        _ticketsEspera = ticketsEspera;
        _sesion = sesion;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_sesion.Usuario == null)
            return;

        _config = _configuracionService.ObtenerConfiguracion().Value;
        UsuarioLabel.Text = $"Atiende: {_sesion.Usuario.Nombre}";

        var seleccionado = MetodoPicker.SelectedItem as string;
        var metodos = new List<string>
        {
            MetodoPagoVenta.Efectivo,
            MetodoPagoVenta.Tarjeta,
            MetodoPagoVenta.Mixto
        };
        if (_config?.FiadoActivado == true)
            metodos.Add(MetodoPagoVenta.Fiado);

        MetodoPicker.ItemsSource = metodos;
        MetodoPicker.SelectedItem = metodos.Contains(seleccionado ?? string.Empty)
            ? seleccionado
            : metodos[0];

        CargarClientes();
        ActualizarCarrito();
        ActualizarVistaMetodo();
    }

    private long Total() =>
        _carrito.Sum(c => Dinero.Multiplicar(c.Producto.Precio, c.Cantidad));

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        CancelarSugerencias();
    }

    private void OnBuscarCompleted(object sender, EventArgs e) => Buscar();

    private void OnBuscarClicked(object sender, EventArgs e) => Buscar();

    private void OnBuscarTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_limpiandoBuscador)
            return;

        CancelarSugerencias();

        var texto = e.NewTextValue?.Trim() ?? string.Empty;
        if (texto.Length < 2)
        {
            OcultarResultados();
            return;
        }

        // Espera a que la persona deje de escribir antes de consultar el catálogo.
        var cts = new CancellationTokenSource();
        _sugerenciasCts = cts;
        _ = MostrarSugerenciasConRetrasoAsync(texto, cts);
    }

    private async Task MostrarSugerenciasConRetrasoAsync(string texto, CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(250, cts.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (cts.IsCancellationRequested || _sesion.Usuario == null)
            return;

        try
        {
            _sesion.Aplicar();
            var sugerencias = _inventario.SugerirProductos(texto, 8);
            if (sugerencias.Count == 0)
            {
                OcultarResultados();
                return;
            }

            ListaResultados.ItemsSource = sugerencias
                .Select(p => new ProductoFila { Producto = p })
                .ToList();
            PanelResultados.IsVisible = true;
            MostrarMensaje(null);
        }
        catch (Exception ex)
        {
            OcultarResultados();
            MostrarMensaje($"No se pudo buscar: {ex.Message}");
        }
    }

    private void CancelarSugerencias()
    {
        _sugerenciasCts?.Cancel();
        _sugerenciasCts?.Dispose();
        _sugerenciasCts = null;
    }

    private void LimpiarBuscador()
    {
        CancelarSugerencias();
        _limpiandoBuscador = true;
        try
        {
            BuscarEntry.Text = string.Empty;
        }
        finally
        {
            _limpiandoBuscador = false;
        }
    }

    private void Buscar()
    {
        if (_sesion.Usuario == null)
            return;

        CancelarSugerencias();
        _sesion.Aplicar();

        var texto = BuscarEntry.Text?.Trim() ?? string.Empty;
        if (texto.Length == 0)
        {
            MostrarMensaje("Escribe un código o el nombre del producto.");
            return;
        }

        var producto = _inventario.BuscarPorCodigo(texto);
        if (producto != null)
        {
            LimpiarBuscador();
            OcultarResultados();
            if (!producto.Activo)
            {
                MostrarMensaje($"{producto.Nombre} está desactivado y no se puede vender. " +
                    "Reactívalo en Inventario (solo el dueño).");
                BuscarEntry.Focus();
                return;
            }

            AgregarAlCarrito(producto);
            return;
        }

        var coincidencias = _inventario.BuscarPorNombre(texto, soloActivos: true);
        if (coincidencias.Count == 0)
        {
            OcultarResultados();
            MostrarMensaje($"No se encontró \"{texto}\".");
            return;
        }

        ListaResultados.ItemsSource = coincidencias
            .Take(10)
            .Select(p => new ProductoFila { Producto = p })
            .ToList();
        PanelResultados.IsVisible = true;
        MostrarMensaje(null);
        LimpiarBuscador();
    }

    private void OnAgregarResultadoClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not ProductoFila fila)
            return;

        if (!AgregarAlCarrito(fila.Producto))
            return;

        OcultarResultados();
        LimpiarBuscador();
    }

    private bool AgregarAlCarrito(Producto producto)
    {
        if (!producto.Activo)
        {
            MostrarMensaje($"{producto.Nombre} está desactivado y no se puede vender.");
            return false;
        }

        var enCarrito = _carrito
            .Where(c => c.Producto.Id == producto.Id)
            .Sum(c => c.Cantidad);

        if (enCarrito >= producto.Stock)
        {
            MostrarMensaje($"No hay stock disponible de {producto.Nombre} (stock {producto.Stock}).");
            return false;
        }

        var fila = _carrito.FirstOrDefault(c => c.Producto.Id == producto.Id);
        if (fila == null)
            _carrito.Add(new CarritoFila { Producto = producto, Cantidad = 1 });
        else
            fila.Cantidad++;

        MostrarMensaje(null);
        ActualizarCarrito();
        BuscarEntry.Focus();
        return true;
    }

    private void OnMasClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not CarritoFila fila)
            return;

        var enCarrito = _carrito
            .Where(c => c.Producto.Id == fila.Producto.Id)
            .Sum(c => c.Cantidad);

        if (enCarrito >= fila.Producto.Stock)
        {
            MostrarMensaje($"No hay más stock de {fila.Producto.Nombre} (stock {fila.Producto.Stock}).");
            return;
        }

        fila.Cantidad++;
        MostrarMensaje(null);
        ActualizarCarrito();
    }

    private void OnMenosClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not CarritoFila fila)
            return;

        if (fila.Cantidad > 1)
            fila.Cantidad--;
        else
            _carrito.Remove(fila);

        MostrarMensaje(null);
        ActualizarCarrito();
        SoltarTicketSiVacio();
    }

    private void OnQuitarClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not CarritoFila fila)
            return;

        _carrito.Remove(fila);
        MostrarMensaje(null);
        ActualizarCarrito();
        SoltarTicketSiVacio();
    }

    private void ActualizarCarrito()
    {
        ListaCarrito.ItemsSource = null;
        ListaCarrito.ItemsSource = _carrito.ToList();
        TotalLabel.Text = Formato.Moneda(Total());
        CobrarButton.IsEnabled = _carrito.Count > 0;
        GuardarEsperaButton.IsEnabled = _carrito.Count > 0;
    }

    private void SoltarTicketSiVacio()
    {
        if (_carrito.Count > 0 || !_ticketEsperaId.HasValue)
            return;

        var ticketId = _ticketEsperaId.Value;
        _ticketEsperaId = null;
        _sesion.Aplicar();
        _ticketsEspera.Liberar(ticketId);
    }

    private async void OnGuardarEsperaClicked(object sender, EventArgs e)
    {
        if (_sesion.Usuario == null || _carrito.Count == 0)
            return;

        var nombre = await DisplayPromptAsync("Guardar en espera",
            "Nombre o nota para identificar el ticket (opcional)",
            "Guardar", "Cancelar", "Ej. Señora blusa roja", 40);
        if (nombre == null)
            return;

        _sesion.Aplicar();
        var lineas = _carrito
            .Select(fila => new LineaTicketEsperaSolicitada(fila.Producto.Id, fila.Cantidad))
            .ToList();

        var anterior = _ticketEsperaId;
        var resultado = _ticketsEspera.Dejar(lineas, nombre);
        if (resultado.IsError)
        {
            MostrarMensaje(resultado.Message);
            return;
        }

        // Si el carrito venía de un ticket retomado, se devuelve el original.
        if (anterior.HasValue)
            _ticketsEspera.Liberar(anterior.Value);

        _ticketEsperaId = null;
        _carrito.Clear();
        ActualizarCarrito();
        MostrarMensaje(resultado.Message);
    }

    private async void OnTicketsEsperaClicked(object sender, EventArgs e)
    {
        if (_sesion.Usuario == null)
            return;

        var espera = new TaskCompletionSource<TicketEspera?>();
        await Navigation.PushModalAsync(new TicketsEsperaPage(_ticketsEspera, _sesion,
            ticket => espera.TrySetResult(ticket)));
        var ticket = await espera.Task;
        if (ticket != null)
            await CargarTicketEsperaAsync(ticket);
    }

    private async Task CargarTicketEsperaAsync(TicketEspera ticket)
    {
        var problemas = new List<string>();
        var filas = new List<CarritoFila>();

        foreach (var linea in ticket.Lineas)
        {
            var producto = _inventario.BuscarPorId(linea.ProductoId);
            if (producto == null || !producto.Activo)
            {
                problemas.Add($"{linea.ProductoNombre} ya no está disponible.");
                continue;
            }

            if (linea.Cantidad > producto.Stock)
            {
                problemas.Add($"{producto.Nombre}: pides {linea.Cantidad} y hay {producto.Stock}.");
                continue;
            }

            filas.Add(new CarritoFila { Producto = producto, Cantidad = linea.Cantidad });
        }

        if (problemas.Count > 0)
        {
            await DisplayAlertAsync("Revisa el ticket",
                string.Join(Environment.NewLine, problemas) + Environment.NewLine +
                "Ajusta el inventario y vuelve a retomarlo.", "Continuar");
            return;
        }

        var cambios = ticket.Lineas
            .Where(linea => linea.CambioPrecio)
            .Select(linea =>
                $"{linea.ProductoNombre}: {Dinero.Formatear(linea.PrecioUnitario)} → " +
                $"{Dinero.Formatear(linea.PrecioActual)}")
            .ToList();

        if (cambios.Count > 0)
        {
            var continuar = await DisplayAlertAsync("Precios actualizados",
                string.Join(Environment.NewLine, cambios) + Environment.NewLine +
                "Se cobrarán los precios actuales.", "Usar nuevos", "Cancelar");
            if (!continuar)
            {
                _sesion.Aplicar();
                _ticketsEspera.Liberar(ticket.Id);
                return;
            }
        }

        _carrito.Clear();
        _carrito.AddRange(filas);
        _ticketEsperaId = ticket.Id;
        ActualizarCarrito();
        MostrarMensaje($"Ticket #{ticket.Id} retomado; revisa y cobra.");
    }

    private void MostrarMensaje(string? mensaje)
    {
        MensajeLabel.Text = mensaje ?? string.Empty;
        MensajeLabel.IsVisible = !string.IsNullOrEmpty(mensaje);
    }

    private void OcultarResultados()
    {
        PanelResultados.IsVisible = false;
        ListaResultados.ItemsSource = null;
    }

    private void OnCobrarClicked(object sender, EventArgs e)
    {
        if (_carrito.Count == 0)
            return;

        OcultarResultados();
        PagoErrorLabel.IsVisible = false;
        RecibidoLabel.Text = "—";
        CambioLabel.Text = "—";
        SumaMixtoLabel.Text = "Suma: —";
        EfectivoEntry.Text = Dinero.APesos(Total()).ToString("0.00", CultureInfo.InvariantCulture);
        EfectivoMixtoEntry.Text = string.Empty;
        TarjetaMixtoEntry.Text = string.Empty;
        CargarClientes();
        MetodoPicker.SelectedIndex = 0;
        TotalCobroLabel.Text = Formato.Moneda(Total());
        PanelCobro.IsVisible = true;
        ActualizarVistaMetodo();
        ActualizarCambio();
    }

    private void OnCancelarCobroClicked(object sender, EventArgs e)
    {
        PanelCobro.IsVisible = false;
        PagoErrorLabel.IsVisible = false;
    }

    private void OnMetodoChanged(object sender, EventArgs e)
    {
        PagoErrorLabel.IsVisible = false;
        ActualizarVistaMetodo();
    }

    private void ActualizarVistaMetodo()
    {
        var metodo = MetodoPicker.SelectedItem as string ?? MetodoPagoVenta.Efectivo;
        CamposEfectivo.IsVisible = metodo == MetodoPagoVenta.Efectivo;
        CamposMixto.IsVisible = metodo == MetodoPagoVenta.Mixto;
        CamposFiado.IsVisible = metodo == MetodoPagoVenta.Fiado;

        var comision = _config?.ComisionTarjeta ?? 0m;
        ComisionLabel.IsVisible = comision > 0 &&
                                  (metodo == MetodoPagoVenta.Tarjeta || metodo == MetodoPagoVenta.Mixto);
        ComisionLabel.Text =
            $"La comisión de tarjeta ({comision.ToString("F2", CultureInfo.InvariantCulture)} %) se descuenta de la utilidad.";

        MostrarAvisoFiado();
    }

    private void OnEfectivoChanged(object sender, TextChangedEventArgs e) => ActualizarCambio();

    private void ActualizarCambio()
    {
        var total = Total();
        var recibido = LeerCentavos(EfectivoEntry.Text);

        if (!recibido.HasValue)
        {
            RecibidoLabel.Text = "—";
            CambioLabel.Text = "—";
            return;
        }

        RecibidoLabel.Text = Formato.Moneda(recibido.Value);
        CambioLabel.Text = recibido.Value >= total
            ? Formato.Moneda(recibido.Value - total)
            : $"Faltan {Formato.Moneda(total - recibido.Value)}";
    }

    private void OnMixtoChanged(object sender, TextChangedEventArgs e)
    {
        var efectivo = LeerCentavos(EfectivoMixtoEntry.Text);
        var tarjeta = LeerCentavos(TarjetaMixtoEntry.Text);

        SumaMixtoLabel.Text = efectivo.HasValue || tarjeta.HasValue
            ? $"Suma: {Formato.Moneda((efectivo ?? 0) + (tarjeta ?? 0))} de {Formato.Moneda(Total())}"
            : "Suma: —";
    }

    private void OnClienteChanged(object sender, EventArgs e) => MostrarAvisoFiado();

    private void MostrarAvisoFiado()
    {
        if (ClientePicker.SelectedItem is ClienteOpcion opcion)
        {
            var limite = _config?.LimiCreditoCliente ?? 0;
            FiadoAvisoLabel.Text = limite > 0
                ? $"Saldo actual {Formato.Moneda(opcion.Cliente.SaldoActual)} · Límite {Formato.Moneda(limite)}"
                : $"Saldo actual {Formato.Moneda(opcion.Cliente.SaldoActual)} · Sin límite configurado";
        }
        else
        {
            var total = Total();
            FiadoAvisoLabel.Text = total > 0
                ? $"Se cargará {Formato.Moneda(total)} a la cuenta del cliente."
                : "Selecciona el cliente para la venta a fiado.";
        }
    }

    private void CargarClientes()
    {
        var clientes = _configuracionService.ObtenerTodosLosClientes().Value ?? new List<ClienteFiado>();
        ClientePicker.ItemsSource = clientes
            .Where(c => c.Activo)
            .OrderBy(c => c.Nombre, StringComparer.CurrentCultureIgnoreCase)
            .Select(c => new ClienteOpcion { Cliente = c })
            .ToList();
    }

    private bool PuedeCobrar(out PagoSolicitado? pago, out string error)
    {
        pago = null;
        error = string.Empty;
        var total = Total();
        var metodo = MetodoPicker.SelectedItem as string ?? MetodoPagoVenta.Efectivo;

        switch (metodo)
        {
            case MetodoPagoVenta.Efectivo:
                var recibido = LeerCentavos(EfectivoEntry.Text);
                if (!recibido.HasValue)
                {
                    error = "Escribe el efectivo recibido.";
                    return false;
                }

                if (recibido.Value < total)
                {
                    error = $"Faltan {Formato.Moneda(total - recibido.Value)} de efectivo.";
                    return false;
                }

                pago = new PagoSolicitado { Efectivo = total, EfectivoRecibido = recibido.Value };
                return true;

            case MetodoPagoVenta.Tarjeta:
                pago = new PagoSolicitado { Tarjeta = total };
                return true;

            case MetodoPagoVenta.Mixto:
                var efectivo = LeerCentavos(EfectivoMixtoEntry.Text);
                var tarjeta = LeerCentavos(TarjetaMixtoEntry.Text);
                if (!efectivo.HasValue || !tarjeta.HasValue || efectivo.Value <= 0 || tarjeta.Value <= 0)
                {
                    error = "Captura la parte en efectivo y la parte con tarjeta.";
                    return false;
                }

                if (efectivo.Value + tarjeta.Value != total)
                {
                    error = "La suma de efectivo y tarjeta debe ser igual al total.";
                    return false;
                }

                pago = new PagoSolicitado { Efectivo = efectivo.Value, Tarjeta = tarjeta.Value };
                return true;

            case MetodoPagoVenta.Fiado:
                if (_config?.FiadoActivado != true)
                {
                    error = "El fiado está desactivado en la configuración.";
                    return false;
                }

                if (ClientePicker.SelectedItem is not ClienteOpcion cliente)
                {
                    error = "Selecciona el cliente para la venta a fiado.";
                    return false;
                }

                pago = new PagoSolicitado { Fiado = total, ClienteId = cliente.Cliente.Id };
                return true;

            default:
                error = "Selecciona la forma de pago.";
                return false;
        }
    }

    private async void OnConfirmarCobroClicked(object sender, EventArgs e)
    {
        if (_sesion.Usuario == null || _carrito.Count == 0)
            return;

        if (!PuedeCobrar(out var pago, out var error))
        {
            MostrarErrorCobro(error);
            return;
        }

        _sesion.Aplicar();
        ConfirmarCobroButton.IsEnabled = false;

        ProcessResult<Venta> resultado;
        try
        {
            var solicitadas = _carrito
                .Select(c => new LineaVentaSolicitada
                {
                    ProductoId = c.Producto.Id,
                    Cantidad = c.Cantidad
                })
                .ToList();

            resultado = _ventas.RegistrarVenta(solicitadas, pago!, _sesion.Usuario.Nombre,
                _ticketEsperaId);
        }
        catch (Exception ex)
        {
            MostrarErrorCobro($"No se pudo registrar la venta: {ex.Message}");
            ConfirmarCobroButton.IsEnabled = true;
            return;
        }

        if (resultado.IsError)
        {
            MostrarErrorCobro(resultado.Message);
            ConfirmarCobroButton.IsEnabled = true;
            return;
        }

        var nombreCliente = (ClientePicker.SelectedItem as ClienteOpcion)?.Cliente.Nombre;
        var efectivoRecibido = resultado.Value.MetodoPago == MetodoPagoVenta.Efectivo
            ? LeerCentavos(EfectivoEntry.Text)
            : null;
        var lineas = _ventas.ObtenerLineas(resultado.Value.Id);
        var ticket = _ticketService.Generar(
            resultado.Value, lineas, _config, nombreCliente, efectivoRecibido);

        _ticketEsperaId = null;
        _carrito.Clear();
        ActualizarCarrito();
        PanelCobro.IsVisible = false;
        LimpiarBuscador();
        ConfirmarCobroButton.IsEnabled = true;
        MostrarMensaje($"Venta #{resultado.Value.Id} registrada y stock actualizado.");

        try
        {
            await Navigation.PushModalAsync(new TicketPage(ticket.Texto, ticket.ContenidoQr));
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Venta registrada",
                $"La venta #{resultado.Value.Id} se guardó, pero el ticket no se pudo abrir. " +
                $"Puedes consultarla en la pestaña Ventas.\n\n{ex.Message}", "Continuar");
        }
    }

    private void MostrarErrorCobro(string mensaje)
    {
        PagoErrorLabel.Text = mensaje;
        PagoErrorLabel.IsVisible = true;
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
