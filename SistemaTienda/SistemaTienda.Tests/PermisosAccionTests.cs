using SistemaTienda.Application.Services;
using SistemaTienda.Core;

namespace SistemaTienda.Tests;

public sealed class PermisosAccionTests
{
    private static ClienteFiado NuevoCliente() => new()
    {
        Nombre = "Cliente de prueba",
        Telefono = "555-000-0000"
    };

    private static Proveedor NuevoProveedor() => new()
    {
        Nombre = "Proveedor de prueba",
        Clave = "PR-1"
    };

    private static UsuarioSistema Encargado(BaseDatosPrueba bd, string pin = "5678",
        string permisos = "")
    {
        var alta = bd.Config.AgregarUsuario("Encargada", pin, RolUsuario.Encargado);
        Assert.True(alta.IsSuccess, alta.Message);

        var usuario = bd.Config.ObtenerUsuarios().Value
            .First(u => u.Id == alta.Value.UsuarioId);

        if (!string.IsNullOrEmpty(permisos))
        {
            var cambio = bd.Db.ActualizarPermisosUsuario(usuario.Id, permisos);
            Assert.True(cambio);
            usuario = bd.Db.ObtenerUsuarioPorId(usuario.Id);
        }

        return usuario;
    }

    [Fact]
    public void Empleado_NoPuedeCrearClienteAunqueLlameAlServicio()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        bd.IniciarSesion(RolUsuario.Empleado, "Cajera");

        var resultado = bd.Config.AgregarClienteFiado(NuevoCliente());

        Assert.True(resultado.IsError);
        Assert.Contains("no tiene permiso", resultado.Message);
        Assert.Empty(bd.Config.ObtenerTodosLosClientes().Value);
    }

    [Fact]
    public void Empleado_NoPuedeCrearProveedorAunqueLlameAlServicio()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        bd.IniciarSesion(RolUsuario.Empleado, "Cajera");

        var resultado = bd.Config.AgregarProveedor(NuevoProveedor());

        Assert.True(resultado.IsError);
        Assert.Contains("no tiene permiso", resultado.Message);
        Assert.Empty(bd.Config.ObtenerTodosLosProveedores().Value);
    }

    [Fact]
    public void Encargado_SiPuedeCrearClientesYProveedores()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        bd.IniciarSesion(RolUsuario.Encargado, "Encargada");

        Assert.True(bd.Config.AgregarClienteFiado(NuevoCliente()).IsSuccess);
        Assert.True(bd.Config.AgregarProveedor(NuevoProveedor()).IsSuccess);
    }

    [Fact]
    public void Empleado_NoPuedeVerCostosNiUtilidad()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        bd.IniciarSesion(RolUsuario.Empleado, "Cajera");

        var reporte = new ReporteUtilidadService(bd.Db).Obtener(
            DateTime.Today, DateTime.Today);

        Assert.True(reporte.IsError);
        Assert.Contains("costos", reporte.Message);
    }

    [Fact]
    public void EncargadoSinConcesion_NoPuedeVerCostos()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var encargado = Encargado(bd);
        SesionActual.Iniciar(encargado);

        var reporte = new ReporteUtilidadService(bd.Db).Obtener(
            DateTime.Today, DateTime.Today);

        Assert.True(reporte.IsError);
        Assert.Contains("costos", reporte.Message);
    }

    [Fact]
    public void EncargadoConConcesion_SiPuedeVerCostos()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var encargado = Encargado(bd);

        var concesion = bd.Config.ActualizarPermisoEspecial(
            encargado.Id, PermisoEspecial.VerCostos, activo: true);
        Assert.True(concesion.IsSuccess, concesion.Message);

        var actualizado = bd.Db.ObtenerUsuarioPorId(encargado.Id);
        SesionActual.Iniciar(actualizado);

        var reporte = new ReporteUtilidadService(bd.Db).Obtener(
            DateTime.Today, DateTime.Today);

        Assert.True(reporte.IsSuccess, reporte.Message);
    }

    [Fact]
    public void ConcesionDeCostos_SePersisteYSeRevoca()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var encargado = Encargado(bd);

        bd.Config.ActualizarPermisoEspecial(encargado.Id, PermisoEspecial.VerCostos, true);
        var conPermiso = bd.Db.ObtenerUsuarioPorId(encargado.Id);
        Assert.True(AutorizacionService.Puede(conPermiso, PermisoSistema.VerCostos));

        bd.Config.ActualizarPermisoEspecial(encargado.Id, PermisoEspecial.VerCostos, false);
        var sinPermiso = bd.Db.ObtenerUsuarioPorId(encargado.Id);
        Assert.False(AutorizacionService.Puede(sinPermiso, PermisoSistema.VerCostos));
    }

    [Fact]
    public void ConcesionDeCostos_NoAplicaADuenoNiAEmpleado()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var dueno = bd.Config.ObtenerUsuarios().Value.First(u => u.Rol == RolUsuario.Dueño);
        var aDueno = bd.Config.ActualizarPermisoEspecial(
            dueno.Id, PermisoEspecial.VerCostos, activo: true);
        Assert.True(aDueno.IsError);

        var empleado = bd.Config.AgregarUsuario("Cajera", "4321", RolUsuario.Empleado);
        var aEmpleado = bd.Config.ActualizarPermisoEspecial(
            empleado.Value.UsuarioId, PermisoEspecial.VerCostos, activo: true);
        Assert.True(aEmpleado.IsError);
    }

    [Fact]
    public void CambioDeRol_QuitaElPermisoDeCostos()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var encargado = Encargado(bd);

        bd.Config.ActualizarPermisoEspecial(encargado.Id, PermisoEspecial.VerCostos, activo: true);

        var cambio = bd.Config.ActualizarUsuario(encargado.Id, "Encargada", RolUsuario.Empleado);
        Assert.True(cambio.IsSuccess, cambio.Message);

        var actualizado = bd.Db.ObtenerUsuarioPorId(encargado.Id);
        Assert.Equal(string.Empty, actualizado.PermisosEspeciales);
        Assert.False(AutorizacionService.Puede(actualizado, PermisoSistema.VerCostos));
    }

    [Fact]
    public void ExportarDatos_EsSoloParaDuenoYEncargado()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var dueno = bd.Config.ObtenerUsuarios().Value.First(u => u.Rol == RolUsuario.Dueño);
        Assert.True(AutorizacionService.Puede(dueno, PermisoSistema.ExportarDatos));

        var empleado = bd.Config.AgregarUsuario("Cajera", "4321", RolUsuario.Empleado);
        var usuarioEmpleado = bd.Db.ObtenerUsuarioPorId(empleado.Value.UsuarioId);
        Assert.False(AutorizacionService.Puede(usuarioEmpleado, PermisoSistema.ExportarDatos));
    }

    [Fact]
    public void Empleado_NoPuedeAjustarInventarioNiRetirarCaja()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = new Producto
        {
            CodigoBarras = "P1",
            Nombre = "Producto",
            Costo = 1_000,
            Precio = 2_000,
            Stock = 10
        };
        bd.Inventario.AgregarProducto(producto);

        bd.IniciarSesion(RolUsuario.Empleado, "Cajera");

        Assert.True(bd.Inventario.AjustarStock(producto.Id, 5, "prueba", "Cajera").IsError);
        Assert.True(new CorteCajaService(bd.Db)
            .RegistrarMovimiento(TipoMovimientoCaja.Retiro, 1_000, "prueba", "Cajera").IsError);
    }

    [Fact]
    public void SinSesion_TodoPermisoSeNiega()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var anterior = SesionActual.Usuario;

        try
        {
            SesionActual.Cerrar();

            Assert.False(AutorizacionService.Puede(null, PermisoSistema.Vender));
            Assert.True(bd.Config.AgregarClienteFiado(NuevoCliente()).IsError);
            Assert.True(new ReporteUtilidadService(bd.Db)
                .Obtener(DateTime.Today, DateTime.Today).IsError);
        }
        finally
        {
            if (anterior != null)
                SesionActual.Iniciar(anterior);
        }
    }
}
