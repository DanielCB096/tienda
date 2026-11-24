using Microsoft.Extensions.DependencyInjection;
using SistemaTienda.Maui.Pages;

namespace SistemaTienda.Maui.Services;

/// <summary>
/// Cambia la página raíz de la ventana. El asistente y el login quedan fuera
/// de las pestañas para que no se pueda volver a ellos con el botón Atrás.
/// </summary>
public sealed class NavegacionApp
{
    private readonly IServiceProvider _services;

    public NavegacionApp(IServiceProvider services) => _services = services;

    public void MostrarAsistente() => Cambiar(_services.GetRequiredService<ConfiguracionInicialPage>());

    public void MostrarLogin() => Cambiar(_services.GetRequiredService<LoginPage>());

    public void MostrarPrincipal() => Cambiar(_services.GetRequiredService<PrincipalPage>());

    /// <summary>
    /// Muestra la pantalla de bloqueo por inactividad como modal, sin desmontar
    /// las pestañas ni perder el carrito.
    /// </summary>
    public void MostrarBloqueo()
    {
        var ventana = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
        if (ventana?.Page is not Page raiz)
            return;

        var bloqueo = _services.GetRequiredService<BloqueoPage>();
        _ = raiz.Navigation.PushModalAsync(bloqueo);
    }

    public void Cambiar(Page pagina)
    {
        var ventana = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
        if (ventana != null)
            ventana.Page = pagina;
    }
}
