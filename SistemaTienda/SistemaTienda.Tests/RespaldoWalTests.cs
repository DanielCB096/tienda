using System.IO.Compression;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;
using Xunit;

namespace SistemaTienda.Tests;

public class RespaldoWalTests
{
    [Fact]
    public void BaseNueva_UsaWalYBusyTimeout()
    {
        using var bd = new BaseDatosPrueba();

        using var conn = bd.Db.CreateConnection();
        conn.Open();

        using var modo = conn.CreateCommand();
        modo.CommandText = "PRAGMA journal_mode";
        Assert.Equal("wal", Convert.ToString(modo.ExecuteScalar())?.ToLowerInvariant());

        using var timeout = conn.CreateCommand();
        timeout.CommandText = "PRAGMA busy_timeout";
        Assert.Equal(30_000, Convert.ToInt32(timeout.ExecuteScalar()));
    }

    [Fact]
    public void Respaldo_ConWal_ConservaDatosYRestaura()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        bd.Db.AgregarProducto(new Producto
        {
            CodigoBarras = "WAL-1",
            Nombre = "Producto antes del respaldo",
            Costo = 1_000,
            Precio = 1_500
        });

        var respaldo = RespaldoLocal.Crear(
            bd.Db.RutaBaseDatos, bd.Db.DirectorioRespaldos, "prueba-wal");

        Assert.True(RespaldoLocal.Verificar(respaldo));

        bd.Db.AgregarProducto(new Producto
        {
            CodigoBarras = "WAL-2",
            Nombre = "Producto posterior al respaldo",
            Costo = 2_000,
            Precio = 2_500
        });

        Assert.Equal(2, bd.Db.ObtenerProductos().Count);

        var seguridad = RespaldoLocal.Restaurar(
            respaldo, bd.Db.RutaBaseDatos, bd.Db.DirectorioRespaldos);

        Assert.True(File.Exists(seguridad));
        var producto = Assert.Single(bd.Db.ObtenerProductos());
        Assert.Equal("WAL-1", producto.CodigoBarras);
    }

    [Fact]
    public void CrearRespaldoManual_SinSesion_Falla()
    {
        using var bd = new BaseDatosPrueba();
        SesionActual.Cerrar();

        var servicio = new RespaldoService(bd.Db);
        var resultado = servicio.CrearRespaldoManual();

        Assert.True(resultado.IsError);
        Assert.Empty(RespaldoLocal.Listar(bd.Db.DirectorioRespaldos));
    }

    [Fact]
    public void PaqueteSoporte_SinBase_GeneraResumenSinDatos()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var salida = Path.Combine(Path.GetTempPath(), $"soporte-{Guid.NewGuid():N}.zip");
        var servicio = new PaqueteSoporteService(bd.Db);

        try
        {
            var resultado = servicio.Crear(salida, incluirBaseDatos: false);

            Assert.True(resultado.IsSuccess, resultado.Message);
            Assert.True(File.Exists(salida));

            using var zip = ZipFile.OpenRead(salida);
            Assert.Contains(zip.Entries, e => e.FullName == "resumen.txt");
            Assert.Contains(zip.Entries, e => e.FullName == "LEEME.txt");
            Assert.DoesNotContain(zip.Entries, e => e.FullName == "datos.db");
        }
        finally
        {
            if (File.Exists(salida))
                File.Delete(salida);
        }
    }

    [Fact]
    public void PaqueteSoporte_ConBase_IncluyeCopiaConsistente()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        bd.Db.AgregarProducto(new Producto
        {
            CodigoBarras = "SOP-1",
            Nombre = "Producto de soporte",
            Costo = 500,
            Precio = 800
        });

        var salida = Path.Combine(Path.GetTempPath(), $"soporte-{Guid.NewGuid():N}.zip");
        var servicio = new PaqueteSoporteService(bd.Db);

        try
        {
            var resultado = servicio.Crear(salida, incluirBaseDatos: true);

            Assert.True(resultado.IsSuccess, resultado.Message);

            using var zip = ZipFile.OpenRead(salida);
            var entrada = Assert.Single(zip.Entries, e => e.FullName == "datos.db");

            var extraido = Path.Combine(Path.GetTempPath(), $"soporte-db-{Guid.NewGuid():N}.db");
            try
            {
                entrada.ExtractToFile(extraido);
                Assert.True(RespaldoLocal.Verificar(extraido));
            }
            finally
            {
                if (File.Exists(extraido))
                    File.Delete(extraido);
            }
        }
        finally
        {
            if (File.Exists(salida))
                File.Delete(salida);
        }
    }
}
