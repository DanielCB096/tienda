namespace SistemaTienda.Maui.Pages;

/// <summary>
/// Se muestra cuando ya hay otra instancia usando la misma base de datos.
/// </summary>
public sealed class InstanciaDuplicadaPage : ContentPage
{
    public InstanciaDuplicadaPage()
    {
        Title = "POSale";

        Content = new VerticalStackLayout
        {
            Padding = new Thickness(32),
            Spacing = 12,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = "POSale ya está abierto",
                    FontSize = 24,
                    FontAttributes = FontAttributes.Bold,
                    HorizontalTextAlignment = TextAlignment.Center
                },
                new Label
                {
                    Text = "Cierra la otra ventana antes de abrir una nueva. " +
                           "Así evitamos que dos instancias modifiquen la misma base de datos.",
                    HorizontalTextAlignment = TextAlignment.Center
                }
            }
        };
    }
}
