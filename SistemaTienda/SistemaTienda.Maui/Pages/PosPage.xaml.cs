using System.Globalization;
using System.Text;
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
    private readonly SesionApp _sesion;

    private readonly List<CarritoFila> _carrito = new();
    private ConfiguracionTienda? _config;

    public PosPage(VentaService ventas, InventoryService inventario,
        ConfiguracionService configuracion, SesionApp sesion)
    {
        InitializeComponent();
        _ventas = ventas;
        _inventario = inventario;
        _configuracionService = configuracion;
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

    private void OnBuscarCompleted(object sender, EventArgs e) => Buscar();

    private void OnBuscarClicked(object sender, EventArgs e) => Buscar();

    private void Buscar()
    {
        if (_sesion.Usuario == null)
            return;

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
            BuscarEntry.Text = string.Empty;
            OcultarResultados();
            AgregarAlCarrito(producto);
            return;
        }

        var coincidencias = _inventario.BuscarPorNombre(texto);
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
        BuscarEntry.Text = string.Empty;
    }

    private void OnAgregarResultadoClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not ProductoFila fila)
            return;

        OcultarResultados();
        AgregarAlCarrito(fila.Producto);
    }

    private void AgregarAlCarrito(Producto producto)
    {
        var enCarrito = _carrito
            .Where(c => c.Producto.Id == producto.Id)
            .Sum(c => c.Cantidad);

        if (enCarrito >= producto.Stock)
        {
            MostrarMensaje($"No hay stock disponible de {producto.Nombre} (stock {producto.Stock}).");
            return;
        }

        var fila = _carrito.FirstOrDefault(c => c.Producto.Id == producto.Id);
        if (fila == null)
            _carrito.Add(new CarritoFila { Producto = producto, Cantidad = 1 });
        else
            fila.Cantidad++;

        MostrarMensaje(null);
        ActualizarCarrito();
        BuscarEntry.Focus();
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
    }

    private void OnQuitarClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not CarritoFila fila)
            return;

        _carrito.Remove(fila);
        MostrarMensaje(null);
        ActualizarCarrito();
    }

    private void ActualizarCarrito()
    {
        ListaCarrito.ItemsSource = null;
        ListaCarrito.ItemsSource = _carrito.ToList();
        TotalLabel.Text = Formato.Moneda(Total());
        CobrarButton.IsEnabled = _carrito.Count > 0;
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

                pago = new PagoSolicitado { Efectivo = total };
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
            var lineas = _carrito
                .Select(c => new LineaVentaSolicitada
                {
                    ProductoId = c.Producto.Id,
                    Cantidad = c.Cantidad
                })
                .ToList();

            resultado = _ventas.RegistrarVenta(lineas, pago!, _sesion.Usuario.Nombre);
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
        var ticket = ConstruirTicket(resultado.Value, nombreCliente, efectivoRecibido);

        _carrito.Clear();
        ActualizarCarrito();
        PanelCobro.IsVisible = false;
        BuscarEntry.Text = string.Empty;
        ConfirmarCobroButton.IsEnabled = true;
        MostrarMensaje($"Venta #{resultado.Value.Id} registrada y stock actualizado.");

        try
        {
            await Navigation.PushModalAsync(new TicketPage(ticket));
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

    private string ConstruirTicket(Venta venta, string? nombreCliente, long? efectivoRecibido = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine(_config?.NombreTienda ?? "Mi tienda");
        sb.AppendLine($"Ticket interno (no fiscal) · Venta #{venta.Id}");
        sb.AppendLine($"{Formato.FechaHora(venta.Fecha)} · Atendió: {venta.Usuario}");
        sb.AppendLine(new string('-', 40));

        foreach (var fila in _carrito)
        {
            sb.AppendLine($"{fila.Cantidad} x {fila.Producto.Nombre}");
            sb.AppendLine(
                $"   {Formato.Moneda(fila.Producto.Precio)} c/u = {Formato.Moneda(Dinero.Multiplicar(fila.Producto.Precio, fila.Cantidad))}");
        }

        sb.AppendLine(new string('-', 40));
        sb.AppendLine($"TOTAL: {Formato.Moneda(venta.Total)}");
        sb.AppendLine($"Pago: {venta.MetodoPago}");

        if (venta.MetodoPago == MetodoPagoVenta.Efectivo && efectivoRecibido.HasValue)
        {
            sb.AppendLine($"Recibido: {Formato.Moneda(efectivoRecibido.Value)}");
            sb.AppendLine($"Cambio: {Formato.Moneda(efectivoRecibido.Value - venta.Total)}");
        }

        if (venta.PagoEfectivo > 0 && venta.PagoTarjeta > 0)
        {
            sb.AppendLine($"   Efectivo: {Formato.Moneda(venta.PagoEfectivo)}");
            sb.AppendLine($"   Tarjeta: {Formato.Moneda(venta.PagoTarjeta)}");
        }

        if (venta.PagoFiado > 0)
            sb.AppendLine($"   Fiado a: {nombreCliente ?? "cliente"}");

        if (venta.ComisionTarjeta > 0)
            sb.AppendLine($"Comisión tarjeta: {Formato.Moneda(venta.ComisionTarjeta)} (afecta la utilidad)");

        sb.AppendLine();
        sb.AppendLine("Gracias por su compra.");
        return sb.ToString();
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
