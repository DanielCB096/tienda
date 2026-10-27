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
    PagarProveedor,
    EditarProducto,
    CambiarPrecios,
    AjustarInventario,
    RegistrarEntrada,
    CorteCaja,
    IngresoCaja,
    RetiroCaja,
    Configurar,
    GestionarUsuarios
}

public static class AccionBitacora
{
    public const string AjusteInventario = "AjusteInventario";
    public const string CambioPrecio = "CambioPrecio";
    public const string EditarProducto = "EditarProducto";
    public const string Merma = "Merma";
    public const string EntradaInventario = "EntradaInventario";
    public const string Gasto = "Gasto";
    public const string CorteCaja = "CorteCaja";
    public const string MovimientoCaja = "MovimientoCaja";
    public const string Venta = "Venta";
    public const string AnulacionVenta = "AnulacionVenta";
    public const string Cliente = "Cliente";
    public const string Abono = "Abono";
    public const string PagoProveedor = "PagoProveedor";
    public const string Usuario = "Usuario";
    public const string Configuracion = "Configuracion";
    public const string Respaldo = "Respaldo";
}
