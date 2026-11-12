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
    private readonly SesionApp _sesion;
    private PeriodoSeleccionado _periodo = PeriodoSeleccionado.Hoy;

    public ReportesPage(ReporteUtilidadService reportes, SesionApp sesion)
    {
        InitializeComponent();
        _reportes = reportes;
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

        var periodo = _periodo switch
        {
            PeriodoSeleccionado.Mes => PeriodoReporte.MesActual(DateTime.Today),
            PeriodoSeleccionado.Rango => PeriodoReporte.Entre(
                DesdePicker.Date ?? DateTime.Today,
                HastaPicker.Date ?? DateTime.Today),
            _ => PeriodoReporte.Hoy(DateTime.Today)
        };

        var resultado = _reportes.Obtener(periodo.Desde, periodo.Hasta);
        if (resultado.IsError)
        {
            MostrarMensaje(resultado.Message);
            return;
        }

        MostrarReporte(resultado.Value);
    }

    private void MostrarReporte(ReporteUtilidad reporte)
    {
        MensajeLabel.IsVisible = false;

        PeriodoLabel.Text = $"PERIODO · {reporte.Desde:dd/MM/yyyy} al {reporte.Hasta:dd/MM/yyyy}";
        NumeroVentasLabel.Text = reporte.NumeroVentas.ToString(CultureInfo.InvariantCulture);
        ResumenVentasLabel.Text = reporte.NumeroVentas == 1
            ? $"1 venta por {Formato.Moneda(reporte.VentasTotal)}"
            : $"{reporte.NumeroVentas} ventas por {Formato.Moneda(reporte.VentasTotal)}";

        VentasLabel.Text = Formato.Moneda(reporte.VentasTotal);
        CostoLabel.Text = Formato.Moneda(reporte.CostoVentas);
        ComisionLabel.Text = Formato.Moneda(reporte.ComisionTarjeta);
        MermasLabel.Text = Formato.Moneda(reporte.Mermas);
        GastosLabel.Text = Formato.Moneda(reporte.Gastos);

        UtilidadBrutaLabel.Text = Formato.Moneda(reporte.UtilidadBruta);
        UtilidadBrutaLabel.TextColor = reporte.UtilidadBruta >= 0 ? ColorPositivo : ColorNegativo;

        UtilidadNetaLabel.Text = Formato.Moneda(reporte.UtilidadNeta);
        UtilidadNetaLabel.TextColor = reporte.UtilidadNeta >= 0 ? ColorPositivo : ColorNegativo;
        UtilidadNetaDetalleLabel.Text = Formato.Moneda(reporte.UtilidadNeta);
        UtilidadNetaDetalleLabel.TextColor = reporte.UtilidadNeta >= 0 ? ColorPositivo : ColorNegativo;
    }

    private void MostrarMensaje(string mensaje)
    {
        MensajeLabel.Text = mensaje;
        MensajeLabel.TextColor = ColorNegativo;
        MensajeLabel.IsVisible = true;
    }

    private void OnActualizarClicked(object sender, EventArgs e) => Cargar();

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
