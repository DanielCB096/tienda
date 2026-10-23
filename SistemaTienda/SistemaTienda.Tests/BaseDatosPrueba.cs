using Microsoft.Data.Sqlite;
using SistemaTienda.Application.Services;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Tests;

/// <summary>
/// Base de datos SQLite temporal y aislada por prueba.
/// </summary>
public sealed class BaseDatosPrueba : IDisposable
{
    public string Ruta { get; }
    public DatabaseTools Db { get; }
    public ConfiguracionService Config { get; }
    public InventoryService Inventario { get; }

    public BaseDatosPrueba()
    {
        Ruta = Path.Combine(Path.GetTempPath(), $"sistematienda-test-{Guid.NewGuid():N}.db");
        Db = new DatabaseTools(Ruta);
        Db.InicializarBaseDeDatos();
        Config = new ConfiguracionService(Db);
        Inventario = new InventoryService(Db);
    }

    public void ConfigurarTienda(string pin = "1234", bool fiado = true)
    {
        var resultado = Config.EjecutarAsistenteInicial(
            "Tienda de prueba", "Abarrotes", 16m, 3.5m, 200m, fiado, 500m, 1, pin);

        Assert.True(resultado.IsSuccess, resultado.Message);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var sufijo in new[] { "", "-shm", "-wal" })
        {
            var archivo = Ruta + sufijo;
            if (File.Exists(archivo)) File.Delete(archivo);
        }
    }
}
