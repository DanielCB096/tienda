namespace SistemaTienda.Maui.Pages;

/// <summary>
/// Pantalla de arranque fallido. No continúa con datos dudosos: muestra la causa
/// y las rutas para revisar la base o el respaldo antes de volver a abrir la app.
/// </summary>
public sealed class ErrorPage : ContentPage
{
    public ErrorPage(string mensaje, string rutaBaseDatos, string directorioRespaldos)
    {
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
            Text = "Cierra la aplicación y vuelve a abrirla. Si el problema continúa, revisa el último respaldo antes de reemplazar la base.",
            FontSize = 14
        });

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
}
