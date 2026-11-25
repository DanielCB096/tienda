using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Maui.Services;

namespace SistemaTienda.Maui.Pages;

/// <summary>
/// Lista de carritos guardados. Retomar devuelve el ticket al POS; descartar deja
/// rastro en la bitácora.
/// </summary>
public sealed class TicketsEsperaPage : ContentPage
{
    private readonly TicketEsperaService _tickets;
    private readonly SesionApp _sesion;
    private readonly Action<TicketEspera?> _alCerrar;
    private readonly VerticalStackLayout _lista;
    private readonly Label _resumen;
    private bool _cerrado;

    public TicketsEsperaPage(TicketEsperaService tickets, SesionApp sesion,
        Action<TicketEspera?> alCerrar)
    {
        _tickets = tickets;
        _sesion = sesion;
        _alCerrar = alCerrar;
        Title = "Tickets en espera";

        _resumen = new Label { FontSize = 13 };
        _lista = new VerticalStackLayout { Spacing = 10, Padding = new Thickness(16) };

        var cerrar = new Button { Text = "Cerrar", HorizontalOptions = LayoutOptions.End };
        cerrar.Clicked += async (_, _) => await CerrarAsync();

        var encabezado = new VerticalStackLayout { Spacing = 6, Padding = new Thickness(16, 16, 16, 0) };
        encabezado.Add(new Label
        {
            Text = "Tickets en espera",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold
        });
        encabezado.Add(_resumen);
        encabezado.Add(cerrar);

        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star }
            }
        };
        grid.Add(encabezado, 0, 0);
        grid.Add(new ScrollView { Content = _lista }, 0, 1);
        Content = grid;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _sesion.Aplicar();
        Cargar();
    }

    private void Cargar()
    {
        _lista.Clear();

        var resultado = _tickets.Listar();
        if (resultado.IsError)
        {
            _resumen.Text = resultado.Message;
            return;
        }

        var tickets = resultado.Value;
        _resumen.Text = tickets.Count == 0
            ? "No hay tickets en espera."
            : $"{tickets.Count} ticket(s). No reservan stock: se valida al cobrar.";

        foreach (var ticket in tickets)
        {
            _lista.Add(new Border
            {
                Padding = 12,
                StrokeThickness = 1,
                Stroke = Color.FromArgb("#D0D7D2"),
                Content = ConstruirTarjeta(ticket)
            });
        }
    }

    private VerticalStackLayout ConstruirTarjeta(TicketEspera ticket)
    {
        var usuario = _sesion.Usuario?.Nombre ?? string.Empty;
        var esMio = string.Equals(ticket.RetomadoPor, usuario, StringComparison.OrdinalIgnoreCase);
        var esDueno = _sesion.Usuario?.Rol == RolUsuario.Dueño;

        var tarjeta = new VerticalStackLayout { Spacing = 4 };
        tarjeta.Add(new Label
        {
            Text = string.IsNullOrWhiteSpace(ticket.Nombre)
                ? $"Ticket #{ticket.Id}"
                : ticket.Nombre,
            FontAttributes = FontAttributes.Bold
        });
        tarjeta.Add(new Label
        {
            Text = $"#{ticket.Id} · {ticket.Fecha:HH:mm} · {ticket.Usuario}",
            FontSize = 12
        });
        tarjeta.Add(new Label
        {
            Text = $"{ticket.Piezas} pieza(s) · {Dinero.Formatear(ticket.TotalEstimado)}",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold
        });

        var detalle = string.Join(", ", ticket.Lineas.Take(3)
            .Select(linea => $"{linea.ProductoNombre} x{linea.Cantidad}"));
        if (ticket.Lineas.Count > 3)
            detalle += $" y {ticket.Lineas.Count - 3} más";
        tarjeta.Add(new Label { Text = detalle, FontSize = 12 });

        if (ticket.Estado == EstadoTicketEspera.Retomado)
        {
            tarjeta.Add(new Label
            {
                Text = esMio ? "Retomado por ti (sin cobrar)" : $"En uso por {ticket.RetomadoPor}",
                FontSize = 12,
                TextColor = Color.FromArgb("#7A4B12")
            });
        }

        var puedeRetomar = ticket.Estado == EstadoTicketEspera.Disponible ||
                           (ticket.Estado == EstadoTicketEspera.Retomado && esMio);
        var puedeDescartar = ticket.Estado == EstadoTicketEspera.Disponible || esMio || esDueno;

        var acciones = new HorizontalStackLayout { Spacing = 8, Margin = new Thickness(0, 6, 0, 0) };
        if (puedeRetomar)
        {
            var retomar = new Button { Text = "Retomar" };
            retomar.Clicked += async (_, _) => await RetomarAsync(ticket);
            acciones.Add(retomar);
        }
        if (puedeDescartar)
        {
            var descartar = new Button { Text = "Descartar" };
            descartar.Clicked += async (_, _) => await DescartarAsync(ticket);
            acciones.Add(descartar);
        }

        if (acciones.Count > 0)
            tarjeta.Add(acciones);

        return tarjeta;
    }

    private async Task RetomarAsync(TicketEspera ticket)
    {
        _sesion.Aplicar();
        var resultado = _tickets.Retomar(ticket.Id);
        if (resultado.IsError)
        {
            await DisplayAlertAsync("No se pudo retomar", resultado.Message, "Continuar");
            Cargar();
            return;
        }

        _cerrado = true;
        _alCerrar(resultado.Value);
        await Navigation.PopModalAsync();
    }

    private async Task DescartarAsync(TicketEspera ticket)
    {
        var confirmar = await DisplayAlertAsync("Descartar ticket",
            $"Se descartará \"{(string.IsNullOrWhiteSpace(ticket.Nombre) ? $"Ticket #{ticket.Id}" : ticket.Nombre)}\" " +
            $"con {ticket.Piezas} pieza(s) y {Dinero.Formatear(ticket.TotalEstimado)}. " +
            "Quedará en la bitácora.",
            "Descartar", "Cancelar");
        if (!confirmar)
            return;

        _sesion.Aplicar();
        var resultado = _tickets.Descartar(ticket.Id);
        if (resultado.IsError)
            await DisplayAlertAsync("No se pudo descartar", resultado.Message, "Continuar");
        Cargar();
    }

    private async Task CerrarAsync()
    {
        if (_cerrado)
            return;

        _cerrado = true;
        _alCerrar(null);
        await Navigation.PopModalAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        if (_cerrado)
            return base.OnBackButtonPressed();

        _cerrado = true;
        _alCerrar(null);
        return base.OnBackButtonPressed();
    }
}
