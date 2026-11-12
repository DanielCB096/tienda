using Microsoft.Extensions.DependencyInjection;
using SistemaTienda.Infrastructure;
using SistemaTienda.Maui.Pages;

namespace SistemaTienda.Maui.Services;

/// <summary>
/// Prepara la base local antes de mostrar la primera pantalla. Si la migración
/// o la verificación fallan se muestra un error en vez de continuar con datos dudosos.
/// </summary>
public sealed class ArranqueApp
{
    private readonly DatabaseTools _db;
    private readonly IServiceProvider _services;

    public ArranqueApp(DatabaseTools db, IServiceProvider services)
    {
        _db = db;
        _services = services;
    }

    public Page CrearPaginaInicial()
    {
        try
        {
            _db.InicializarBaseDeDatos();
            return _db.ContarUsuarios() == 0
                ? _services.GetRequiredService<ConfiguracionInicialPage>()
                : _services.GetRequiredService<LoginPage>();
        }
        catch (Exception ex)
        {
            return new ErrorPage(ex.Message, _db.RutaBaseDatos, _db.DirectorioRespaldos);
        }
    }
}
