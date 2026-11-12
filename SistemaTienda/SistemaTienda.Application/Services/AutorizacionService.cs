using SistemaTienda.Core;

namespace SistemaTienda.Application.Services;

/// <summary>
/// Usuario de la sesión actual. La consola lo establece al iniciar sesión y los
/// servicios lo consultan para autorizar y para firmar la bitácora.
/// </summary>
public static class SesionActual
{
    private static readonly AsyncLocal<UsuarioSistema> _usuario = new();

    public static UsuarioSistema Usuario => _usuario.Value;

    public static void Iniciar(UsuarioSistema usuario) => _usuario.Value = usuario;

    public static void Cerrar() => _usuario.Value = null;
}

/// <summary>
/// Matriz de permisos por rol. Se valida en la capa de aplicación, no en los menús.
/// </summary>
public static class AutorizacionService
{
    public static bool Puede(UsuarioSistema usuario, PermisoSistema permiso)
    {
        if (usuario == null)
            return false;

        if (usuario.Rol == RolUsuario.Dueño)
            return true;

        return usuario.Rol switch
        {
            RolUsuario.Encargado => permiso is
                PermisoSistema.Vender or
                PermisoSistema.AnularVenta or
                PermisoSistema.Consultar or
                PermisoSistema.RegistrarMerma or
                PermisoSistema.RegistrarGasto or
                PermisoSistema.RegistrarAbono or
                PermisoSistema.PagarProveedor or
                PermisoSistema.EditarProducto or
                PermisoSistema.RegistrarEntrada or
                PermisoSistema.CorteCaja or
                PermisoSistema.IngresoCaja,
            RolUsuario.Empleado => permiso is
                PermisoSistema.Vender or
                PermisoSistema.Consultar,
            _ => false
        };
    }

    public static ProcessResult Exigir(UsuarioSistema usuario, PermisoSistema permiso)
    {
        if (usuario == null)
            return ProcessResult.Error("No hay una sesión de usuario activa");

        return Puede(usuario, permiso)
            ? ProcessResult.Success("Autorizado")
            : ProcessResult.Error(
                $"El usuario {usuario.Nombre} no tiene permiso para {Describir(permiso)}");
    }

    private static string Describir(PermisoSistema permiso) => permiso switch
    {
        PermisoSistema.Vender => "vender",
        PermisoSistema.AnularVenta => "anular ventas",
        PermisoSistema.Consultar => "consultar",
        PermisoSistema.RegistrarMerma => "registrar mermas",
        PermisoSistema.RegistrarGasto => "registrar gastos",
        PermisoSistema.RegistrarAbono => "registrar abonos",
        PermisoSistema.PagarProveedor => "pagar proveedores",
        PermisoSistema.EditarProducto => "editar productos",
        PermisoSistema.CambiarPrecios => "cambiar precios o costos",
        PermisoSistema.AjustarInventario => "ajustar inventario",
        PermisoSistema.RegistrarEntrada => "registrar entradas de inventario",
        PermisoSistema.CorteCaja => "cerrar el corte de caja",
        PermisoSistema.IngresoCaja => "registrar ingresos de caja",
        PermisoSistema.RetiroCaja => "registrar retiros de caja",
        PermisoSistema.Configurar => "cambiar la configuración",
        PermisoSistema.GestionarUsuarios => "gestionar usuarios",
        _ => permiso.ToString()
    };
}
