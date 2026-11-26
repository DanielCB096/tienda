namespace SistemaTienda.Core;

/// <summary>
/// Registro inmutable de una acción sensible. No se edita ni se borra:
/// las correcciones se hacen con otro registro.
/// </summary>
public class RegistroBitacora
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    public string Usuario { get; set; } = string.Empty;
    public string Accion { get; set; } = string.Empty;
    public string Entidad { get; set; } = string.Empty;
    public string EntidadId { get; set; } = string.Empty;
    public string DatosAntes { get; set; } = string.Empty;
    public string DatosDespues { get; set; } = string.Empty;
    public string Motivo { get; set; } = string.Empty;
}

/// <summary>
/// Permisos que la capa de aplicación valida antes de ejecutar operaciones sensibles.
/// </summary>
public enum PermisoSistema
{
    Vender,
    AnularVenta,
    Consultar,
    RegistrarMerma,
    RegistrarGasto,
    RegistrarAbono,
    EditarCliente,
    CambiarEstadoCliente,
    PagarProveedor,
    EditarProveedor,
    CambiarEstadoProveedor,
    RegistrarCompra,
    EditarProducto,
    DesactivarProducto,
    CambiarPrecios,
    AjustarInventario,
    RegistrarEntrada,
    CorteCaja,
    IngresoCaja,
    RetiroCaja,
    Configurar,
    GestionarUsuarios,
    ConsultarBitacora,
    CrearCliente,
    CrearProveedor,
    ExportarDatos,
    VerCostos
}

/// <summary>
/// Permisos que el dueño puede conceder a un usuario concreto además de los de
/// su rol. Se guardan separados por comas en <c>UsuariosSistema.PermisosEspeciales</c>.
/// </summary>
public static class PermisoEspecial
{
    public const string VerCostos = "VerCostos";

    public static bool Tiene(string permisos, string permiso)
    {
        if (string.IsNullOrWhiteSpace(permisos) || string.IsNullOrWhiteSpace(permiso))
            return false;

        return permisos
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(p => string.Equals(p, permiso, StringComparison.OrdinalIgnoreCase));
    }

    public static string Establecer(string permisos, string permiso, bool activo)
    {
        var actuales = (permisos ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => !string.Equals(p, permiso, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (activo)
            actuales.Add(permiso);

        return string.Join(",", actuales);
    }
}

public static class AccionBitacora
{
    public const string AjusteInventario = "AjusteInventario";
    public const string CambioPrecio = "CambioPrecio";
    public const string EditarProducto = "EditarProducto";
    public const string CambiarEstadoProducto = "CambiarEstadoProducto";
    public const string Merma = "Merma";
    public const string EntradaInventario = "EntradaInventario";
    public const string Gasto = "Gasto";
    public const string EditarGasto = "EditarGasto";
    public const string CorteCaja = "CorteCaja";
    public const string MovimientoCaja = "MovimientoCaja";
    public const string Venta = "Venta";
    public const string AnulacionVenta = "AnulacionVenta";
    public const string Cliente = "Cliente";
    public const string EditarCliente = "EditarCliente";
    public const string CambiarEstadoCliente = "CambiarEstadoCliente";
    public const string Abono = "Abono";
    public const string Proveedor = "Proveedor";
    public const string EditarProveedor = "EditarProveedor";
    public const string CambiarEstadoProveedor = "CambiarEstadoProveedor";
    public const string PagoProveedor = "PagoProveedor";
    public const string CompraProveedor = "CompraProveedor";
    public const string Usuario = "Usuario";
    public const string EditarUsuario = "EditarUsuario";
    public const string CambiarPin = "CambiarPin";
    public const string Configuracion = "Configuracion";
    public const string ImportacionProductos = "ImportacionProductos";
    public const string ReversionImportacion = "ReversionImportacion";
    public const string DejarTicketEspera = "DejarTicketEspera";
    public const string RetomarTicketEspera = "RetomarTicketEspera";
    public const string DescartarTicketEspera = "DescartarTicketEspera";
    public const string CobrarTicketEspera = "CobrarTicketEspera";
    public const string Respaldo = "Respaldo";
    public const string AccesoFallido = "AccesoFallido";
}
