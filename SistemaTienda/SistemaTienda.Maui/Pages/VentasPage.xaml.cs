using SistemaTienda.Application.Services;
using SistemaTienda.Maui.Models;
using SistemaTienda.Maui.Services;

namespace SistemaTienda.Maui.Pages;

public partial class VentasPage : ContentPage
{
    private readonly VentaService _ventas;
    private readonly SesionApp _sesion;

    public VentasPage(VentaService ventas, SesionApp sesion)
    {
        InitializeComponent();
        _ventas = ventas;
        _sesion = sesion;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
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
        if (_sesion.Usuario == null)
            return;

        _sesion.Aplicar();

        var ventas = _ventas.ObtenerVentasDelDia(DateTime.Now)
            .OrderByDescending(v => v.Fecha)
            .ToList();

        ListaVentas.ItemsSource = ventas.Select(v => new VentaFila
        {
            Folio = $"Venta #{v.Id}",
            Total = Formato.Moneda(v.Total),
            Detalle = $"{v.MetodoPago} · {Formato.Hora(v.Fecha)}",
            Estado = v.Anulada ? "Anulada" : "Vigente",
            EstadoColor = v.Anulada ? Color.FromArgb("#B3261E") : Color.FromArgb("#2E6B4F")
        }).ToList();

        var vigentes = ventas.Where(v => !v.Anulada).ToList();
        var anuladas = ventas.Count - vigentes.Count;
        var resumen = $"{vigentes.Count} venta(s) · {Formato.Moneda(vigentes.Sum(v => v.Total))} cobrados";
        if (anuladas > 0)
            resumen += $" · {anuladas} anulada(s)";

        ResumenLabel.Text = resumen;
    }
}
