using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Maui.Models;
using SistemaTienda.Maui.Services;

namespace SistemaTienda.Maui.Pages;

public partial class VentasPage : ContentPage
{
    private static readonly Color ColorVigente = Color.FromArgb("#2E6B4F");
    private static readonly Color ColorAnulada = Color.FromArgb("#B3261E");

    private readonly VentaService _ventas;
    private readonly ConfiguracionService _configuracion;
    private readonly TicketService _tickets;
    private readonly SesionApp _sesion;

    private ConfiguracionTienda? _config;
    private Venta? _ventaActual;
    private List<VentaLinea> _lineasActuales = new();
    private string? _nombreClienteActual;
    private bool _puedeAnular;

    public VentasPage(VentaService ventas, ConfiguracionService configuracion,
        TicketService tickets, SesionApp sesion)
    {
        InitializeComponent();
        _ventas = ventas;
        _configuracion = configuracion;
        _tickets = tickets;
        _sesion = sesion;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        DetalleOverlay.IsVisible = false;
        _ventaActual = null;
        _lineasActuales = new();
        Cargar();
    }

    private void OnActualizarClicked(object sender, EventArgs e) => Cargar();

    private void OnRefrescando(object sender, EventArgs e)
    {
        Cargar();
        Refresco.IsRefreshing = false;
    }

    private void Cargar()
    {
        var usuario = _sesion.Usuario;
        if (usuario == null)
            return;

        _sesion.Aplicar();
        _config = _configuracion.ObtenerConfiguracion().Value;
        _puedeAnular = AutorizacionService.Puede(usuario, PermisoSistema.AnularVenta);

        var ventas = _ventas.ObtenerVentasDelDia(DateTime.Now)
            .OrderByDescending(v => v.Fecha)
            .ToList();

        ListaVentas.ItemsSource = ventas.Select(v => new VentaFila
        {
            VentaId = v.Id,
            Folio = $"Venta #{v.Id}",
            Total = Formato.Moneda(v.Total),
            Detalle = $"{v.MetodoPago} · {Formato.Hora(v.Fecha)}",
            Estado = v.Anulada ? "Anulada" : "Vigente",
            EstadoColor = v.Anulada ? ColorAnulada : ColorVigente
        }).ToList();

        var vigentes = ventas.Where(v => !v.Anulada).ToList();
        var anuladas = ventas.Count - vigentes.Count;
        var resumen = $"{vigentes.Count} venta(s) · {Formato.Moneda(vigentes.Sum(v => v.Total))} cobrados";
        if (anuladas > 0)
            resumen += $" · {anuladas} anulada(s)";

        ResumenLabel.Text = resumen;
    }

    private async void OnVentaTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is VentaFila fila)
            await AbrirDetalleAsync(fila.VentaId);
    }

    private async Task AbrirDetalleAsync(int ventaId)
    {
        var usuario = _sesion.Usuario;
        if (usuario == null)
            return;

        _sesion.Aplicar();
        var venta = _ventas.ObtenerVentaPorId(ventaId);
        if (venta == null)
        {
            await DisplayAlertAsync("Venta no encontrada",
                $"La venta #{ventaId} ya no está disponible.", "Continuar");
            return;
        }

        _ventaActual = venta;
        _lineasActuales = _ventas.ObtenerLineas(ventaId);
        _nombreClienteActual = venta.ClienteId.HasValue
            ? _configuracion.ObtenerTodosLosClientes().Value?
                .FirstOrDefault(c => c.Id == venta.ClienteId.Value)?.Nombre
            : null;

        DetalleTituloLabel.Text = $"Venta #{venta.Id}";
        DetalleFechaLabel.Text = $"{Formato.FechaHora(venta.Fecha)} · Atendió: {venta.Usuario}";
        DetalleEstadoLabel.Text = venta.Anulada ? "Anulada" : "Vigente";
        DetalleEstadoBorder.BackgroundColor = venta.Anulada ? ColorAnulada : ColorVigente;
        DetalleMetodoLabel.Text = $"Pago: {venta.MetodoPago}";
        DetalleClienteLabel.Text = _nombreClienteActual == null
            ? string.Empty
            : $"Cliente: {_nombreClienteActual}";
        DetalleClienteLabel.IsVisible = _nombreClienteActual != null;

        BindableLayout.SetItemsSource(DetalleLineas, _lineasActuales.Select(l => new LineaVentaFila
        {
            Nombre = $"{l.Cantidad} x {l.Nombre}",
            Detalle = $"{Formato.Moneda(l.PrecioUnitario)} c/u",
            Importe = Formato.Moneda(l.Importe)
        }).ToList());

        DetalleTotalLabel.Text = Formato.Moneda(venta.Total);

        var pagos = new List<string>();
        if (venta.PagoEfectivo > 0)
            pagos.Add($"Efectivo {Formato.Moneda(venta.PagoEfectivo)}");
        if (venta.PagoTarjeta > 0)
            pagos.Add($"Tarjeta {Formato.Moneda(venta.PagoTarjeta)}");
        if (venta.PagoFiado > 0)
            pagos.Add($"Fiado {Formato.Moneda(venta.PagoFiado)}");
        DetallePagosLabel.Text = string.Join(" · ", pagos);

        DetalleComisionLabel.Text = $"Comisión tarjeta: {Formato.Moneda(venta.ComisionTarjeta)}";
        DetalleComisionLabel.IsVisible = venta.ComisionTarjeta > 0;

        var anulacion = venta.Anulada ? _ventas.ObtenerAnulacion(venta.Id) : null;
        DetalleAnulacionPanel.IsVisible = venta.Anulada;
        DetalleAnulacionLabel.Text = anulacion == null
            ? "No se encontró el detalle de la anulación."
            : $"Por {anulacion.Usuario} el {Formato.FechaHora(anulacion.Fecha)}.\nMotivo: {anulacion.Motivo}";

        AnularButton.IsVisible = _puedeAnular && !venta.Anulada;
        AnularButton.IsEnabled = true;
        NotaAnulacionLabel.IsVisible = _puedeAnular && !venta.Anulada;
        VerTicketButton.IsVisible = _lineasActuales.Count > 0;
        DetalleOverlay.IsVisible = true;
    }

    private async void OnAnularClicked(object sender, EventArgs e)
    {
        if (_ventaActual == null || _sesion.Usuario == null)
            return;

        var venta = _ventaActual;
        var motivo = await DisplayPromptAsync(
            "Anular venta",
            $"Venta #{venta.Id} por {Formato.Moneda(venta.Total)}.\nMotivo de la anulación:",
            "Continuar", "Cancelar", "Ej. cobro duplicado", 200, Keyboard.Text);

        if (motivo == null)
            return;

        motivo = motivo.Trim();
        if (motivo.Length == 0)
        {
            await DisplayAlertAsync("Falta el motivo",
                "La anulación requiere un motivo.", "Continuar");
            return;
        }

        var confirmacion = await DisplayPromptAsync(
            "Confirmar anulación",
            "Se devolverá el stock y se revertirá el fiado. El reembolso en efectivo se entrega manualmente.\n\nEscribe ANULAR para confirmar.",
            "Anular venta", "Cancelar", "ANULAR", 10, Keyboard.Text);

        if (confirmacion == null)
            return;

        if (!string.Equals(confirmacion.Trim(), "ANULAR", StringComparison.OrdinalIgnoreCase))
        {
            await DisplayAlertAsync("Anulación cancelada",
                "No se escribió ANULAR; la venta no se modificó.", "Continuar");
            return;
        }

        AnularButton.IsEnabled = false;
        _sesion.Aplicar();
        var resultado = _ventas.AnularVenta(venta.Id, motivo, _sesion.Usuario.Nombre);
        if (resultado.IsError)
        {
            AnularButton.IsEnabled = true;
            await DisplayAlertAsync("No se pudo anular", resultado.Message, "Continuar");
            return;
        }

        Cargar();
        await AbrirDetalleAsync(venta.Id);
        await DisplayAlertAsync("Venta anulada",
            $"{resultado.Message}\n\nEl reembolso en efectivo se entrega manualmente. " +
            "Si la venta ya estaba en un corte cerrado, no se permite la anulación.",
            "Continuar");
    }

    private async void OnVerTicketClicked(object sender, EventArgs e)
    {
        if (_ventaActual == null)
            return;

        var anulacion = _ventaActual.Anulada ? _ventas.ObtenerAnulacion(_ventaActual.Id) : null;
        var ticket = _tickets.Generar(
            _ventaActual, _lineasActuales, _config!,
            _nombreClienteActual, null, anulacion);

        try
        {
            await Navigation.PushModalAsync(new TicketPage(ticket.Texto, ticket.ContenidoQr));
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("No se pudo abrir el ticket", ex.Message, "Continuar");
        }
    }

    private void OnCerrarDetalleClicked(object sender, EventArgs e)
    {
        DetalleOverlay.IsVisible = false;
        _ventaActual = null;
        _lineasActuales = new();
        _nombreClienteActual = null;
    }
}
