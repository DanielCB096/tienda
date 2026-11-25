using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Maui.Services;

namespace SistemaTienda.Maui.Pages;

/// <summary>
/// Historial de lotes importados. El dueño puede revertir un lote sin borrar
/// historial: el stock se devuelve con ajustes y los productos se desactivan.
/// </summary>
public sealed class ImportacionesPage : ContentPage
{
    private readonly InventoryService _inventario;
    private readonly SesionApp _sesion;
    private readonly VerticalStackLayout _lista;
    private readonly Label _resumen;

    public ImportacionesPage(InventoryService inventario, SesionApp sesion)
    {
        _inventario = inventario;
        _sesion = sesion;
        Title = "Lotes de importación";

        _resumen = new Label { FontSize = 13 };
        _lista = new VerticalStackLayout { Spacing = 10, Padding = new Thickness(16) };

        var cerrar = new Button { Text = "Cerrar", HorizontalOptions = LayoutOptions.End };
        cerrar.Clicked += async (_, _) => await Navigation.PopModalAsync();

        var encabezado = new VerticalStackLayout { Spacing = 6, Padding = new Thickness(16, 16, 16, 0) };
        encabezado.Add(new Label
        {
            Text = "Lotes de importación",
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

        List<Importacion> lotes;
        try
        {
            lotes = _inventario.ObtenerImportaciones();
        }
        catch (Exception ex)
        {
            _resumen.Text = $"No se pudieron leer los lotes: {ex.Message}";
            return;
        }

        _resumen.Text = lotes.Count == 0
            ? "Todavía no hay importaciones registradas."
            : $"{lotes.Count} lote(s). Revertir devuelve el stock y desactiva los productos.";

        var esDueno = _sesion.Usuario?.Rol == RolUsuario.Dueño;

        foreach (var lote in lotes)
        {
            var tarjeta = new Border
            {
                Padding = 12,
                StrokeThickness = 1,
                Stroke = Color.FromArgb("#D0D7D2"),
                Content = ConstruirTarjeta(lote, esDueno)
            };
            _lista.Add(tarjeta);
        }
    }

    private VerticalStackLayout ConstruirTarjeta(Importacion lote, bool esDueno)
    {
        var tarjeta = new VerticalStackLayout { Spacing = 4 };

        tarjeta.Add(new Label
        {
            Text = $"Lote #{lote.Id} · {lote.Fecha:yyyy-MM-dd HH:mm}",
            FontAttributes = FontAttributes.Bold
        });
        tarjeta.Add(new Label
        {
            Text = string.IsNullOrWhiteSpace(lote.NombreArchivo)
                ? "Archivo sin nombre"
                : lote.NombreArchivo,
            FontSize = 13
        });
        tarjeta.Add(new Label
        {
            Text = $"Productos: {lote.ProductosInsertados} · Omitidas: {lote.FilasOmitidas} · " +
                   $"Estado: {lote.Estado}",
            FontSize = 13
        });

        if (lote.Revertida)
        {
            tarjeta.Add(new Label
            {
                Text = $"Revertida el {lote.FechaReversion:yyyy-MM-dd HH:mm} por " +
                       $"{lote.UsuarioReversion}",
                FontSize = 12,
                TextColor = Color.FromArgb("#7A4B12")
            });
            return tarjeta;
        }

        if (esDueno)
        {
            var revertir = new Button
            {
                Text = "Revertir lote",
                HorizontalOptions = LayoutOptions.Start,
                Margin = new Thickness(0, 6, 0, 0)
            };
            revertir.Clicked += async (_, _) => await RevertirAsync(lote);
            tarjeta.Add(revertir);
        }

        return tarjeta;
    }

    private async Task RevertirAsync(Importacion lote)
    {
        var confirmar = await DisplayAlertAsync("Revertir importación",
            $"Se devolverá el stock del lote #{lote.Id} y sus {lote.ProductosInsertados} " +
            "producto(s) quedarán inactivos. No se borra el historial. " +
            "Si el lote ya tuvo ventas o compras, no se podrá revertir.",
            "Revertir", "Cancelar");
        if (!confirmar)
            return;

        _sesion.Aplicar();
        var resultado = _inventario.RevertirImportacion(lote.Id);
        await DisplayAlertAsync(resultado.IsError ? "No se pudo revertir" : "Lote revertido",
            resultado.Message, "Continuar");
        Cargar();
    }
}
