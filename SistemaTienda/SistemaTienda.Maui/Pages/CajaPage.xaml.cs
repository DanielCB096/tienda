using SistemaTienda.Application.Services;
using SistemaTienda.Maui.Services;

namespace SistemaTienda.Maui.Pages;

public partial class CajaPage : ContentPage
{
    private readonly CorteCajaService _cortes;
    private readonly SesionApp _sesion;

    public CajaPage(CorteCajaService cortes, SesionApp sesion)
    {
        InitializeComponent();
        _cortes = cortes;
        _sesion = sesion;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Cargar();
    }

    private void OnActualizarClicked(object sender, EventArgs e) => Cargar();

    private void Cargar()
    {
        if (_sesion.Usuario == null)
            return;

        _sesion.Aplicar();

        var pendiente = _cortes.ObtenerResumenPendiente();
        if (pendiente.IsSuccess && pendiente.Value != null)
        {
            var corte = pendiente.Value;
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

        var ultimo = _cortes.ObtenerUltimoCorte();
        if (ultimo.IsSuccess && ultimo.Value != null)
        {
            var corte = ultimo.Value;
            UltimoCorteLabel.Text = $"Cerrado el {Formato.FechaHora(corte.FechaRegistro)}";
            UltimoCorteDetalleLabel.Text =
                $"Contado {Formato.Moneda(corte.EfectivoContado)} · Diferencia {Formato.Moneda(corte.Diferencia)} · {corte.Usuario}";
        }
        else
        {
            UltimoCorteLabel.Text = "Todavía no hay cortes cerrados.";
            UltimoCorteDetalleLabel.Text = string.Empty;
        }
    }
}
