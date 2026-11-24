using System.Globalization;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Maui.Services;

namespace SistemaTienda.Maui.Pages;

public partial class ReportesPage : ContentPage
{
    private static readonly Color ColorPositivo = Color.FromArgb("#2E6B4F");
    private static readonly Color ColorNegativo = Color.FromArgb("#B3261E");
    private static readonly Color ColorPrimario = Color.FromArgb("#2E6B4F");

    private enum PeriodoSeleccionado
    {
        Hoy,
        Mes,
        Rango
    }

    private readonly ReporteUtilidadService _reportes;
    private readonly ConfiguracionService _config;
    private readonly SesionApp _sesion;
    private PeriodoSeleccionado _periodo = PeriodoSeleccionado.Hoy;
    private ReporteUtilidad? _reporteActual;

    public ReportesPage(ReporteUtilidadService reportes, ConfiguracionService config,
        SesionApp sesion)
    {
        InitializeComponent();
        _reportes = reportes;
        _config = config;
        _sesion = sesion;

        DesdePicker.Date = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        HastaPicker.Date = DateTime.Today;
        ActualizarBotones();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Cargar();
    }

    private void Cargar()
    {
        _sesion.Aplicar();

        UsuarioLabel.Text = _sesion.Usuario != null
            ? $"Consultando como {_sesion.Usuario.Nombre} ({_sesion.Usuario.Rol})"
            : "Sesión no iniciada";

        var periodo = PeriodoActual();

        var resultado = _reportes.Obtener(periodo.Desde, periodo.Hasta);
        if (resultado.IsError)
        {
            MostrarMensaje(resultado.Message);
            return;
        }

        MostrarReporte(resultado.Value);

        if (ComparacionPanel.IsVisible)
            CargarComparacion(periodo);
    }

    private PeriodoReporte PeriodoActual() =>
        _periodo switch
        {
            PeriodoSeleccionado.Mes => PeriodoReporte.MesActual(DateTime.Today),
            PeriodoSeleccionado.Rango => PeriodoReporte.Entre(
                DesdePicker.Date ?? DateTime.Today,
                HastaPicker.Date ?? DateTime.Today),
            _ => PeriodoReporte.Hoy(DateTime.Today)
        };

    private void MostrarReporte(ReporteUtilidad reporte)
    {
        _reporteActual = reporte;
        ExportarCsvButton.IsEnabled = true;
        ExportarPdfButton.IsEnabled = true;
        MensajeLabel.IsVisible = false;

        PeriodoLabel.Text = $"PERIODO · {reporte.Desde:dd/MM/yyyy} al {reporte.Hasta:dd/MM/yyyy}";
        NumeroVentasLabel.Text = reporte.NumeroVentas.ToString(CultureInfo.InvariantCulture);
        ResumenVentasLabel.Text = reporte.NumeroVentas == 1
            ? $"1 venta por {Formato.Moneda(reporte.VentasTotal)}"
            : $"{reporte.NumeroVentas} ventas por {Formato.Moneda(reporte.VentasTotal)}";

        VentasLabel.Text = Formato.Moneda(reporte.VentasTotal);
        ImpuestoLabel.Text = Formato.Moneda(reporte.ImpuestoIncluido);
        CostoLabel.Text = Formato.Moneda(reporte.CostoVentas);
        ComisionLabel.Text = Formato.Moneda(reporte.ComisionTarjeta);
        MermasLabel.Text = Formato.Moneda(reporte.Mermas);
        GastosLabel.Text = Formato.Moneda(reporte.Gastos);

        MostrarCobros(reporte);

        UtilidadBrutaLabel.Text = Formato.Moneda(reporte.UtilidadBruta);
        UtilidadBrutaLabel.TextColor = reporte.UtilidadBruta >= 0 ? ColorPositivo : ColorNegativo;

        UtilidadNetaLabel.Text = Formato.Moneda(reporte.UtilidadNeta);
        UtilidadNetaLabel.TextColor = reporte.UtilidadNeta >= 0 ? ColorPositivo : ColorNegativo;
        UtilidadNetaDetalleLabel.Text = Formato.Moneda(reporte.UtilidadNeta);
        UtilidadNetaDetalleLabel.TextColor = reporte.UtilidadNeta >= 0 ? ColorPositivo : ColorNegativo;
    }

    private void MostrarCobros(ReporteUtilidad reporte)
    {
        var efectivo = DesgloseDe(reporte, MetodoPagoVenta.Efectivo);
        var tarjeta = DesgloseDe(reporte, MetodoPagoVenta.Tarjeta);
        var fiado = DesgloseDe(reporte, MetodoPagoVenta.Fiado);

        EfectivoTituloLabel.Text = TituloCobro("Efectivo", efectivo.NumeroVentas);
        TarjetaTituloLabel.Text = TituloCobro("Tarjeta", tarjeta.NumeroVentas);
        FiadoTituloLabel.Text = TituloCobro("Fiado", fiado.NumeroVentas);

        EfectivoMontoLabel.Text = Formato.Moneda(efectivo.Monto);
        TarjetaMontoLabel.Text = Formato.Moneda(tarjeta.Monto);
        FiadoMontoLabel.Text = Formato.Moneda(fiado.Monto);
    }

    private static DesgloseMetodoPago DesgloseDe(ReporteUtilidad reporte, string metodo) =>
        reporte.DesglosePagos.FirstOrDefault(d => d.Metodo == metodo)
        ?? new DesgloseMetodoPago { Metodo = metodo };

    private static string TituloCobro(string nombre, int ventas) =>
        ventas == 1 ? $"{nombre} (1 venta)" : $"{nombre} ({ventas} ventas)";

    private void CargarComparacion(PeriodoReporte periodo)
    {
        _sesion.Aplicar();
        var resultado = _reportes.ObtenerComparacion(periodo.Desde, periodo.Hasta);
        if (resultado.IsError)
        {
            MostrarMensaje(resultado.Message);
            return;
        }

        MostrarComparacion(resultado.Value);
    }

    private void MostrarComparacion(ComparacionReporte comparacion)
    {
        var actual = comparacion.Actual;
        var anterior = comparacion.Anterior;

        ComparacionPeriodoLabel.Text =
            $"Actual: {actual.Desde:dd/MM/yyyy} al {actual.Hasta:dd/MM/yyyy} · " +
            $"Anterior: {anterior.Desde:dd/MM/yyyy} al {anterior.Hasta:dd/MM/yyyy}";

        CompVentasActual.Text = Formato.Moneda(actual.VentasTotal);
        CompVentasAnterior.Text = Formato.Moneda(anterior.VentasTotal);
        CompVentasDelta.Text = TextoDelta(comparacion.DiferenciaMontoVentas, comparacion.VariacionVentas);
        CompVentasDelta.TextColor = comparacion.DiferenciaMontoVentas >= 0 ? ColorPositivo : ColorNegativo;

        CompUtilidadActual.Text = Formato.Moneda(actual.UtilidadNeta);
        CompUtilidadAnterior.Text = Formato.Moneda(anterior.UtilidadNeta);
        CompUtilidadDelta.Text = TextoDelta(comparacion.DiferenciaUtilidadNeta, comparacion.VariacionUtilidadNeta);
        CompUtilidadDelta.TextColor = comparacion.DiferenciaUtilidadNeta >= 0 ? ColorPositivo : ColorNegativo;

        CompGastosActual.Text = Formato.Moneda(actual.Gastos);
        CompGastosAnterior.Text = Formato.Moneda(anterior.Gastos);
        CompGastosDelta.Text = TextoDelta(comparacion.DiferenciaGastos, comparacion.VariacionGastos);
        CompGastosDelta.TextColor = comparacion.DiferenciaGastos <= 0 ? ColorPositivo : ColorNegativo;
    }

    private static string TextoDelta(long diferencia, decimal? variacion)
    {
        var signo = diferencia > 0 ? "+" : diferencia < 0 ? "−" : string.Empty;
        var texto = $"{signo}{Formato.Moneda(Math.Abs(diferencia))}";
        if (variacion.HasValue)
            texto += $" ({variacion.Value:+0.0;-0.0;0.0}%)";

        return texto;
    }

    private void OnCompararClicked(object sender, EventArgs e)
    {
        ComparacionPanel.IsVisible = !ComparacionPanel.IsVisible;
        if (ComparacionPanel.IsVisible)
            CargarComparacion(PeriodoActual());
    }

    private void MostrarMensaje(string mensaje)
    {
        MensajeLabel.Text = mensaje;
        MensajeLabel.TextColor = ColorNegativo;
        MensajeLabel.IsVisible = true;
    }

    private void OnActualizarClicked(object sender, EventArgs e) => Cargar();

    private async void OnExportarCsvClicked(object sender, EventArgs e)
    {
        if (_sesion.Usuario == null ||
            !AutorizacionService.Puede(_sesion.Usuario, PermisoSistema.ExportarDatos) ||
            !AutorizacionService.Puede(_sesion.Usuario, PermisoSistema.VerCostos))
        {
            await DisplayAlertAsync("Sin permiso",
                "Los reportes con costo y utilidad requieren autorización del dueño.", "Continuar");
            return;
        }

        if (_reporteActual == null)
            return;

        try
        {
            var nombre = $"reporte-utilidad-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
            await ExportacionCsvApp.CompartirAsync(
                nombre, ExportadorCsv.ReporteUtilidad(_reporteActual));
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("No se pudo exportar", ex.Message, "Continuar");
        }
    }

    private async void OnExportarPdfClicked(object sender, EventArgs e)
    {
        if (_sesion.Usuario == null ||
            !AutorizacionService.Puede(_sesion.Usuario, PermisoSistema.ExportarDatos) ||
            !AutorizacionService.Puede(_sesion.Usuario, PermisoSistema.VerCostos))
        {
            await DisplayAlertAsync("Sin permiso",
                "Los reportes con costo y utilidad requieren autorización del dueño.", "Continuar");
            return;
        }

        if (_reporteActual == null)
            return;

        try
        {
            var nombre = $"reporte-utilidad-{DateTime.Now:yyyyMMdd-HHmmss}.pdf";
            var tienda = _config.ObtenerConfiguracion().Value?.NombreTienda;
            await ExportacionPdfApp.CompartirAsync(
                nombre, ExportadorPdf.ReporteUtilidad(_reporteActual, tienda));
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("No se pudo exportar", ex.Message, "Continuar");
        }
    }

    private void OnHoyClicked(object sender, EventArgs e)
    {
        _periodo = PeriodoSeleccionado.Hoy;
        RangoPanel.IsVisible = false;
        ActualizarBotones();
        Cargar();
    }

    private void OnMesClicked(object sender, EventArgs e)
    {
        _periodo = PeriodoSeleccionado.Mes;
        RangoPanel.IsVisible = false;
        ActualizarBotones();
        Cargar();
    }

    private void OnRangoClicked(object sender, EventArgs e)
    {
        _periodo = PeriodoSeleccionado.Rango;
        RangoPanel.IsVisible = true;
        ActualizarBotones();
        Cargar();
    }

    private void OnVerRangoClicked(object sender, EventArgs e)
    {
        _periodo = PeriodoSeleccionado.Rango;
        Cargar();
    }

    private void ActualizarBotones()
    {
        AplicarEstado(HoyButton, _periodo == PeriodoSeleccionado.Hoy);
        AplicarEstado(MesButton, _periodo == PeriodoSeleccionado.Mes);
        AplicarEstado(RangoButton, _periodo == PeriodoSeleccionado.Rango);
    }

    private static void AplicarEstado(Button boton, bool activo)
    {
        boton.BackgroundColor = activo ? ColorPrimario : Colors.Transparent;
        boton.TextColor = activo ? Colors.White : ColorPrimario;
    }
}
