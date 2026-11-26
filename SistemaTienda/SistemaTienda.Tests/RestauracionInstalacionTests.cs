using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Tests;

/// <summary>
/// H2: restauración guiada al instalar en otro equipo. Solo se permite cuando la
/// base todavía no tiene tienda ni usuarios; después se entra con el PIN del respaldo.
/// </summary>
public sealed class RestauracionInstalacionTests
{
    private static string CrearRespaldoDeOrigen(string clave = null)
    {
        using var origen = new BaseDatosPrueba();
        origen.ConfigurarTienda("2468");
        var producto = new Producto
        {
            CodigoBarras = "H2-REST-1",
            Nombre = "Producto restaurado",
            Costo = 1_000,
            Precio = 2_000,
            Stock = 7
        };
        Assert.True(origen.Inventario.AgregarProducto(producto).IsSuccess);

        var respaldo = new RespaldoService(origen.Db).CrearRespaldo("traslado", clave);
        Assert.True(respaldo.IsSuccess, respaldo.Message);

        // Copia fuera del directorio del origen: BaseDatosPrueba lo borra al salir.
        var copia = Path.Combine(Path.GetTempPath(),
            $"respaldo-h2-{Guid.NewGuid():N}{Path.GetExtension(respaldo.Value)}");
        File.Copy(respaldo.Value, copia, overwrite: true);
        return copia;
    }

    [Fact]
    public void InstalacionNueva_RestauraTiendaUsuariosYProductos()
    {
        var respaldo = CrearRespaldoDeOrigen();
        try
        {
            using var destino = new BaseDatosPrueba();
            SesionActual.Cerrar();

            var resultado = new RespaldoService(destino.Db)
                .RestaurarEnInstalacionNueva(respaldo, confirmado: true);

            Assert.True(resultado.IsSuccess, resultado.Message);
            var config = destino.Db.ObtenerConfiguracion();
            Assert.NotNull(config);
            Assert.Equal("Tienda de prueba", config.NombreTienda);
            Assert.True(destino.Db.ContarUsuarios() > 0);
            Assert.Contains(destino.Inventario.ObtenerProductos(),
                producto => producto.CodigoBarras == "H2-REST-1");
        }
        finally
        {
            File.Delete(respaldo);
        }
    }

    [Fact]
    public void InstalacionNueva_RechazaSiYaHayTiendaConfigurada()
    {
        var respaldo = CrearRespaldoDeOrigen();
        try
        {
            using var destino = new BaseDatosPrueba();
            destino.ConfigurarTienda();

            var resultado = new RespaldoService(destino.Db)
                .RestaurarEnInstalacionNueva(respaldo, confirmado: true);

            Assert.True(resultado.IsError);
            Assert.Contains("Ya hay una tienda", resultado.Message);
        }
        finally
        {
            File.Delete(respaldo);
        }
    }

    [Fact]
    public void InstalacionNueva_SinConfirmacion_NoTocaNada()
    {
        var respaldo = CrearRespaldoDeOrigen();
        try
        {
            using var destino = new BaseDatosPrueba();

            var resultado = new RespaldoService(destino.Db)
                .RestaurarEnInstalacionNueva(respaldo, confirmado: false);

            Assert.True(resultado.IsError);
            Assert.Equal(0, destino.Db.ContarUsuarios());
        }
        finally
        {
            File.Delete(respaldo);
        }
    }

    [Fact]
    public void InstalacionNueva_ConClave_RestauraElRespaldoCifrado()
    {
        var clave = CifradoRespaldo.GenerarClave();
        var respaldo = CrearRespaldoDeOrigen(clave);
        try
        {
            Assert.EndsWith(CifradoRespaldo.Extension, respaldo);
            using var destino = new BaseDatosPrueba();

            var resultado = new RespaldoService(destino.Db)
                .RestaurarEnInstalacionNueva(respaldo, confirmado: true, claveRespaldo: clave);

            Assert.True(resultado.IsSuccess, resultado.Message);
            Assert.Equal("Tienda de prueba", destino.Db.ObtenerConfiguracion().NombreTienda);
        }
        finally
        {
            File.Delete(respaldo);
        }
    }

    [Fact]
    public void InstalacionNueva_ConClaveIncorrecta_NoRestaura()
    {
        var clave = CifradoRespaldo.GenerarClave();
        var respaldo = CrearRespaldoDeOrigen(clave);
        try
        {
            using var destino = new BaseDatosPrueba();

            var resultado = new RespaldoService(destino.Db)
                .RestaurarEnInstalacionNueva(respaldo, confirmado: true,
                    claveRespaldo: "000000000000000000000000");

            Assert.True(resultado.IsError);
            Assert.Equal(0, destino.Db.ContarUsuarios());
        }
        finally
        {
            File.Delete(respaldo);
        }
    }

    [Fact]
    public void Recuperacion_ConBaseIlegible_PermiteRestaurarSinSesion()
    {
        var respaldo = CrearRespaldoDeOrigen();
        try
        {
            using var destino = new BaseDatosPrueba();
            SesionActual.Cerrar();

            var resultado = new RespaldoService(destino.Db)
                .RestaurarEnRecuperacion(respaldo, confirmado: true);

            Assert.True(resultado.IsSuccess, resultado.Message);
            Assert.NotNull(destino.Db.ObtenerConfiguracion());
        }
        finally
        {
            File.Delete(respaldo);
        }
    }
}
