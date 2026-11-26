using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using SistemaTienda.Application.Services;
using SistemaTienda.Maui.Services;

namespace SistemaTienda.Maui.Pages;

/// <summary>
/// Pantalla de arranque fallido. No continúa con datos dudosos: muestra la causa
/// y permite restaurar un respaldo o revisar las rutas antes de volver a abrir.
/// </summary>
public sealed class ErrorPage : ContentPage
{
    private static readonly FilePickerFileType TiposRespaldo = new(
        new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.WinUI] = new[] { ".db", ".stb" },
            [DevicePlatform.Android] = new[]
            {
                "application/octet-stream",
                "application/x-sqlite3",
                "application/vnd.sqlite3",
                "*/*"
            },
            [DevicePlatform.iOS] = new[] { "public.database" },
            [DevicePlatform.MacCatalyst] = new[] { "public.database" }
        });

    private readonly RespaldoService _respaldos;
    private readonly NavegacionApp _navegacion;
    private readonly Label _estado;

    public ErrorPage(string mensaje, string rutaBaseDatos, string directorioRespaldos,
        RespaldoService respaldos, NavegacionApp navegacion)
    {
        _respaldos = respaldos;
        _navegacion = navegacion;
        Title = "No se pudo iniciar";

        var contenido = new VerticalStackLayout
        {
            Padding = new Thickness(24, 40),
            Spacing = 16,
            VerticalOptions = LayoutOptions.Center
        };

        contenido.Add(new Label
        {
            Text = "No se pudo iniciar el sistema",
            FontSize = 26,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center
        });

        contenido.Add(new Border
        {
            Padding = 16,
            StrokeThickness = 0,
            BackgroundColor = Color.FromArgb("#FDECEA"),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            Content = new Label
            {
                Text = mensaje,
                FontSize = 15,
                TextColor = Color.FromArgb("#B3261E")
            }
        });

        contenido.Add(new Label
        {
            Text = $"Base de datos: {rutaBaseDatos}\nRespaldos: {directorioRespaldos}",
            FontSize = 12,
            TextColor = Colors.Gray
        });

        contenido.Add(new Label
        {
            Text = "Si tienes un respaldo, restáuralo aquí. Se guarda una copia previa y se pedirá la clave si está cifrado. Si no, cierra la aplicación y revisa la base.",
            FontSize = 14
        });

        var restaurar = new Button
        {
            Text = "Restaurar un respaldo",
            HeightRequest = 52,
            CornerRadius = 12,
            BackgroundColor = Color.FromArgb("#2E6B4F"),
            TextColor = Colors.White
        };
        restaurar.Clicked += OnRestaurarClicked;
        contenido.Add(restaurar);

        _estado = new Label
        {
            FontSize = 13,
            TextColor = Color.FromArgb("#B3261E"),
            HorizontalTextAlignment = TextAlignment.Center,
            IsVisible = false
        };
        contenido.Add(_estado);

        var cerrar = new Button
        {
            Text = "Cerrar aplicación",
            HeightRequest = 52,
            CornerRadius = 12,
            BackgroundColor = Color.FromArgb("#2E6B4F"),
            TextColor = Colors.White
        };
        cerrar.Clicked += (_, _) => Microsoft.Maui.Controls.Application.Current?.Quit();
        contenido.Add(cerrar);

        Content = new ScrollView { Content = contenido };
    }

    private async void OnRestaurarClicked(object? sender, EventArgs e)
    {
        if (sender is Button boton)
            boton.IsEnabled = false;

        try
        {
            var archivo = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Selecciona el respaldo de POSale",
                FileTypes = TiposRespaldo
            });
            if (archivo == null)
                return;

            var confirmar = await DisplayAlertAsync("Restaurar respaldo",
                $"Se reemplazará la base actual por:\n{archivo.FileName}\n\n" +
                "Se guardará un respaldo previo. La aplicación volverá al inicio de sesión.",
                "Restaurar", "Cancelar");
            if (!confirmar)
                return;

            string? clave = null;
            if (archivo.FileName.EndsWith(CifradoRespaldo.Extension,
                    StringComparison.OrdinalIgnoreCase))
            {
                clave = await DisplayPromptAsync("Respaldo cifrado",
                    "Escribe la clave de respaldo de 24 caracteres.",
                    accept: "Restaurar", cancel: "Cancelar", maxLength: 40);
                if (clave == null)
                    return;
            }

            var resultado = _respaldos.RestaurarEnRecuperacion(
                archivo.FullPath, confirmado: true, claveRespaldo: clave);
            if (resultado.IsError)
            {
                _estado.Text = resultado.Message;
                _estado.IsVisible = true;
                return;
            }

            await DisplayAlertAsync("Respaldo restaurado",
                "La base quedó restaurada. Inicia sesión con el PIN de ese respaldo.",
                "Continuar");
            _navegacion.MostrarLogin();
        }
        catch (Exception ex)
        {
            _estado.Text = $"No se pudo restaurar: {ex.Message}";
            _estado.IsVisible = true;
        }
        finally
        {
            if (sender is Button botonFinal)
                botonFinal.IsEnabled = true;
        }
    }
}
