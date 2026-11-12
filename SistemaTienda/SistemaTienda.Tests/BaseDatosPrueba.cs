using Microsoft.Data.Sqlite;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Tests;

/// <summary>
/// Base de datos SQLite temporal y aislada por prueba.
/// </summary>
public sealed class BaseDatosPrueba : IDisposable
{
    private readonly string _directorio;

    public string Ruta { get; }
    public DatabaseTools Db { get; }
    public ConfiguracionService Config { get; }
    public InventoryService Inventario { get; }

    public BaseDatosPrueba()
    {
        _directorio = Path.Combine(Path.GetTempPath(), $"sistematienda-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directorio);
        Ruta = Path.Combine(_directorio, "tienda.db");

        Db = new DatabaseTools(Ruta);
        Db.InicializarBaseDeDatos();
        Config = new ConfiguracionService(Db);
        Inventario = new InventoryService(Db);

        // Las pruebas que no ejercitan permisos corren como dueño del sistema.
        SesionActual.Iniciar(new UsuarioSistema { Nombre = "Sistema", Rol = RolUsuario.Dueño });
    }

    public void ConfigurarTienda(string pin = "1234", bool fiado = true)
    {
        var resultado = Config.EjecutarAsistenteInicial(
            "Tienda de prueba", "Abarrotes", 16m, 3.5m, 20_000, fiado, 50_000, 1, pin);

        Assert.True(resultado.IsSuccess, resultado.Message);

        var dueno = Config.ObtenerUsuarios().Value.FirstOrDefault(u => u.Rol == RolUsuario.Dueño);
        if (dueno != null)
            SesionActual.Iniciar(dueno);
    }

    public UsuarioSistema IniciarSesion(RolUsuario rol, string nombre = "Usuario de prueba")
    {
        var usuario = new UsuarioSistema { Nombre = nombre, Rol = rol };
        SesionActual.Iniciar(usuario);
        return usuario;
    }

    public void Dispose()
    {
        SesionActual.Cerrar();
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_directorio))
                Directory.Delete(_directorio, recursive: true);
        }
        catch (IOException)
        {
            // Un archivo aún bloqueado no debe hacer fallar la prueba.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
