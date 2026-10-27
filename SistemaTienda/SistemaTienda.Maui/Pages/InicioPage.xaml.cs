using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Maui.Services;

namespace SistemaTienda.Maui.Pages;

public partial class InicioPage : ContentPage
{
    private readonly ConfiguracionService _config;
    private readonly VentaService _ventas;
    private readonly CorteCajaService _cortes;
    private readonly InventoryService _inventario;
    private readonly RespaldoService _respaldos;
    private readonly SesionApp _sesion;

    public InicioPage(ConfiguracionService config, VentaService ventas, CorteCajaService cortes,
        InventoryService inventario, RespaldoService respaldos, SesionApp sesion)
    {
        InitializeComponent();
        _config = config;
        _ventas = ventas;
        _cortes = cortes;
        _inventario = inventario;
        _respaldos = respaldos;
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

        var config = _config.ObtenerConfiguracion().Value;
        NombreTiendaLabel.Text = config?.NombreTienda ?? "Mi tienda";
        FechaLabel.Text = Formato.FechaLarga(DateTime.Now);
        UsuarioLabel.Text = $"Rol: {_sesion.Usuario.Rol}";

        var ventas = _ventas.ObtenerVentasDelDia(DateTime.Now);
        var vigentes = ventas.Where(v => !v.Anulada).ToList();
        VentasHoyLabel.Text = vigentes.Count.ToString();
        VentasDetalleLabel.Text = $"{Formato.Moneda(vigentes.Sum(v => v.Total))} cobrados";
        var anuladas = ventas.Count - vigentes.Count;
        VentasEstadoLabel.Text = anuladas == 0 ? "Sin anulaciones" : $"{anuladas} anulada(s)";

        var pendiente = _cortes.ObtenerResumenPendiente();
        if (pendiente.IsSuccess && pendiente.Value != null)
        {
            EfectivoLabel.Text = Formato.Moneda(pendiente.Value.EfectivoEsperado);
            CajaDetalleLabel.Text =
                $"Desde {Formato.FechaHora(pendiente.Value.Desde)} · Fondo {Formato.Moneda(pendiente.Value.FondoInicial)}";
        }

        var stockBajo = _inventario.ProductosStockBajo();
        StockBajoLabel.Text = stockBajo.Count.ToString();
        StockBajoEstadoLabel.Text = stockBajo.Count == 0 ? "Todo en orden" : "Revisar hoy";

        CargarRespaldo(config?.RespaldoIntervaloDias ?? 0);
    }

    private void CargarRespaldo(int intervaloDias)
    {
        var ultimo = _respaldos.ObtenerUltimoRespaldo();
        DateTime? fecha = ultimo.IsSuccess ? ultimo.Value : null;

        if (fecha.HasValue)
        {
            RespaldoLabel.Text = Formato.FechaHora(fecha.Value);
            RespaldoDetalleLabel.Text = "Copia local verificada.";
        }
        else
        {
            RespaldoLabel.Text = "Sin respaldos";
            RespaldoDetalleLabel.Text = "Crea uno desde Ajustes.";
        }

        var vencido = intervaloDias > 0 &&
                      (!fecha.HasValue || (DateTime.Now.Date - fecha.Value.Date).TotalDays >= intervaloDias);

        AvisoRespaldo.IsVisible = vencido;
        if (vencido)
        {
            AvisoRespaldoLabel.Text =
                "El respaldo está vencido. Se intenta crear uno al iniciar sesión; si no se creó, usa Ajustes → Crear respaldo ahora.";
        }
    }
}
