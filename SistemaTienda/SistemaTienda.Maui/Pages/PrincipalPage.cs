using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific;

namespace SistemaTienda.Maui.Pages;

/// <summary>
/// Raíz después del login: cuatro pestañas de solo lectura por ahora.
/// La captura de ventas, el cierre de corte y la configuración se agregan en tareas posteriores.
/// </summary>
public sealed class PrincipalPage : Microsoft.Maui.Controls.TabbedPage
{
    public PrincipalPage(InicioPage inicio, VentasPage ventas, CajaPage caja, AjustesPage ajustes)
    {
        BarBackgroundColor = Color.FromArgb("#2E6B4F");
        BarTextColor = Colors.White;
        UnselectedTabColor = Color.FromArgb("#BFD9CB");

        Children.Add(inicio);
        Children.Add(ventas);
        Children.Add(caja);
        Children.Add(ajustes);

        On<Microsoft.Maui.Controls.PlatformConfiguration.Android>()
            .SetToolbarPlacement(ToolbarPlacement.Bottom);
    }
}
