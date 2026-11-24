using SistemaTienda.Infrastructure;
using SistemaTienda.Semilla;

namespace SistemaTienda.Tests;

public sealed class SemillaTests : IDisposable
{
    private readonly string _directorio;

    public SemillaTests()
    {
        _directorio = Path.Combine(Path.GetTempPath(), $"sistematienda-semilla-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directorio);
    }

    private string Ruta => Path.Combine(_directorio, "tienda.db");

    private OpcionesSemilla Opciones(string escenario, bool reiniciar = true) => new()
    {
        RutaBaseDatos = Ruta,
        Escenario = escenario,
        Reiniciar = reiniciar,
        Silencioso = true
    };

    [Fact]
    public void Demo_GeneraBaseCoherenteYDejaUnCorteCerrado()
    {
        var codigo = GeneradorSemilla.Ejecutar(Opciones(OpcionesSemilla.EscenarioDemo));

        Assert.Equal(0, codigo);
        Assert.True(File.Exists(Ruta));

        var db = new DatabaseTools(Ruta);
        Assert.Equal(53, db.ExecuteScalar<long>("SELECT COUNT(*) FROM Productos"));
        Assert.Equal(6, db.ExecuteScalar<long>("SELECT COUNT(*) FROM ClientesFiado"));
        Assert.Equal(5, db.ExecuteScalar<long>("SELECT COUNT(*) FROM Proveedores"));
        Assert.Equal(3, db.ExecuteScalar<long>("SELECT COUNT(*) FROM UsuariosSistema"));
        Assert.Equal(1, db.ExecuteScalar<long>("SELECT COUNT(*) FROM CortesCaja"));
        Assert.Equal(1, db.ExecuteScalar<long>("SELECT COUNT(*) FROM AnulacionesVenta"));

        var diferencia = db.ExecuteScalar<long>("SELECT Diferencia FROM CortesCaja LIMIT 1");
        Assert.Equal(0, diferencia);

        var contado = db.ExecuteScalar<long>("SELECT EfectivoContado FROM CortesCaja LIMIT 1");
        var desglose = db.ExecuteScalar<long>(
            "SELECT COALESCE(SUM(ValorCentavos * Cantidad), 0) FROM CorteCajaDesglose");
        Assert.Equal(contado, desglose);

        // El stock es la suma de movimientos, incluso con anulación y compras.
        var descuadres = db.ExecuteScalar<long>(@"
            SELECT COUNT(*) FROM (
                SELECT p.Id
                FROM Productos p
                LEFT JOIN MovimientoInventario m ON m.ProductoId = p.Id
                GROUP BY p.Id
                HAVING p.Stock <> COALESCE(SUM(m.Cantidad), 0)
            )");
        Assert.Equal(0, descuadres);

        Assert.True(db.ExecuteScalar<long>("SELECT COUNT(*) FROM Productos WHERE Activo = 0") >= 1);
        Assert.True(db.ExecuteScalar<long>("SELECT COUNT(*) FROM Ventas") >= 20);
    }

    [Fact]
    public void Configurado_NoGeneraVentasPeroDejaCatalogoYUsuarios()
    {
        var codigo = GeneradorSemilla.Ejecutar(Opciones(OpcionesSemilla.EscenarioConfigurado));

        Assert.Equal(0, codigo);
        var db = new DatabaseTools(Ruta);
        Assert.Equal(53, db.ExecuteScalar<long>("SELECT COUNT(*) FROM Productos"));
        Assert.Equal(3, db.ExecuteScalar<long>("SELECT COUNT(*) FROM UsuariosSistema"));
        Assert.Equal(0, db.ExecuteScalar<long>("SELECT COUNT(*) FROM Ventas"));
        Assert.Equal(0, db.ExecuteScalar<long>("SELECT COUNT(*) FROM ClientesFiado"));
    }

    [Fact]
    public void Limpio_BorraBaseYRespaldos()
    {
        GeneradorSemilla.Ejecutar(Opciones(OpcionesSemilla.EscenarioDemo));
        Assert.True(File.Exists(Ruta));
        Assert.True(Directory.Exists(Path.Combine(_directorio, "respaldos")));

        var codigo = GeneradorSemilla.Ejecutar(Opciones(OpcionesSemilla.EscenarioLimpio));

        Assert.Equal(0, codigo);
        Assert.False(File.Exists(Ruta));
        Assert.False(Directory.Exists(Path.Combine(_directorio, "respaldos")));
    }

    [Fact]
    public void SinReset_NoSobrescribeUnaBaseExistente()
    {
        GeneradorSemilla.Ejecutar(Opciones(OpcionesSemilla.EscenarioDemo));

        var excepcion = Assert.Throws<InvalidOperationException>(
            () => GeneradorSemilla.Ejecutar(Opciones(OpcionesSemilla.EscenarioDemo, reiniciar: false)));

        Assert.Contains("--reset", excepcion.Message);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_directorio))
                Directory.Delete(_directorio, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
