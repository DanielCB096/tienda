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
    private readonly Application.Services.LicenciaService _licencia;

    public ArranqueApp(DatabaseTools db, IServiceProvider services,
        Application.Services.LicenciaService licencia)
    {
        _db = db;
        _services = services;
        _licencia = licencia;
    }

    public Page CrearPaginaInicial()
    {
        try
        {
            _db.InicializarBaseDeDatos();
            _licencia.Iniciar();
            return _db.ContarUsuarios() == 0
                ? _services.GetRequiredService<ConfiguracionInicialPage>()
                : _services.GetRequiredService<LoginPage>();
        }
        catch (Exception ex)
        {
            return new ErrorPage(ex.Message, _db.RutaBaseDatos, _db.DirectorioRespaldos,
                _services.GetRequiredService<Application.Services.RespaldoService>(),
                _services.GetRequiredService<NavegacionApp>());
        }
    }
}
