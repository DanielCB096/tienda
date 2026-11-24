using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Maui.Services;

namespace SistemaTienda.Maui.Pages;

/// <summary>
/// Raíz después del login. Las pestañas se agregan según lo que el rol puede
/// hacer: el empleado no ve clientes, proveedores, gastos, caja ni reportes, y
/// los reportes con costo solo aparecen con el permiso VerCostos.
/// </summary>
public sealed class PrincipalPage : Microsoft.Maui.Controls.TabbedPage
{
    public PrincipalPage(InicioPage inicio, PosPage pos, InventarioPage inventario,
        VentasPage ventas, ClientesPage clientes, ProveedoresPage proveedores,
        GastosPage gastos, CajaPage caja, ReportesPage reportes, AjustesPage ajustes,
        SesionApp sesion)
    {
        BarBackgroundColor = Color.FromArgb("#2E6B4F");
        BarTextColor = Colors.White;
        UnselectedTabColor = Color.FromArgb("#BFD9CB");

        var usuario = sesion.Usuario;

        Children.Add(inicio);
        Children.Add(pos);
        Children.Add(inventario);
        Children.Add(ventas);

        if (AutorizacionService.Puede(usuario, PermisoSistema.CrearCliente) ||
            AutorizacionService.Puede(usuario, PermisoSistema.EditarCliente))
            Children.Add(clientes);

        if (AutorizacionService.Puede(usuario, PermisoSistema.CrearProveedor) ||
            AutorizacionService.Puede(usuario, PermisoSistema.EditarProveedor))
            Children.Add(proveedores);

        if (AutorizacionService.Puede(usuario, PermisoSistema.RegistrarGasto))
            Children.Add(gastos);

        if (AutorizacionService.Puede(usuario, PermisoSistema.CorteCaja))
            Children.Add(caja);

        if (AutorizacionService.Puede(usuario, PermisoSistema.VerCostos))
            Children.Add(reportes);

        Children.Add(ajustes);

        On<Microsoft.Maui.Controls.PlatformConfiguration.Android>()
            .SetToolbarPlacement(ToolbarPlacement.Bottom);
    }
}
