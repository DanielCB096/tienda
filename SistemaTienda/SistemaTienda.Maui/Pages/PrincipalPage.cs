using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific;

namespace SistemaTienda.Maui.Pages;

/// <summary>
/// Raíz después del login: inicio, punto de venta, inventario, ventas, clientes, caja y ajustes.
/// </summary>
public sealed class PrincipalPage : Microsoft.Maui.Controls.TabbedPage
{
    public PrincipalPage(InicioPage inicio, PosPage pos, InventarioPage inventario,
        VentasPage ventas, ClientesPage clientes, CajaPage caja, AjustesPage ajustes)
    {
        BarBackgroundColor = Color.FromArgb("#2E6B4F");
        BarTextColor = Colors.White;
        UnselectedTabColor = Color.FromArgb("#BFD9CB");

        Children.Add(inicio);
        Children.Add(pos);
        Children.Add(inventario);
        Children.Add(ventas);
        Children.Add(clientes);
        Children.Add(caja);
        Children.Add(ajustes);

        On<Microsoft.Maui.Controls.PlatformConfiguration.Android>()
            .SetToolbarPlacement(ToolbarPlacement.Bottom);
    }
}
