using SistemaTienda.Application.Services;
using SistemaTienda.Core;

namespace SistemaTienda.Tests;

/// <summary>
/// Las fechas de alta de clientes y proveedores son fechas de negocio y deben
/// quedar en la hora local del equipo, no en UTC como hace datetime('now').
/// </summary>
public sealed class FechasLocalesTests
{
    [Fact]
    public void AltaClienteYProveedor_GuardanFechaLocal()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var antes = DateTime.Now.AddMinutes(-1);

        var cliente = bd.Config.AgregarClienteFiado(new ClienteFiado { Nombre = "Cliente" });
        var proveedor = bd.Config.AgregarProveedor(new Proveedor { Nombre = "Proveedor" });
        var despues = DateTime.Now.AddMinutes(1);

        var registradoCliente = bd.Db.ObtenerClienteFiadoPorId(cliente.Value.ClienteId).FechaRegistro;
        var registradoProveedor = bd.Db.ObtenerProveedorPorId(proveedor.Value.ProveedorId).FechaRegistro;

        Assert.InRange(registradoCliente, antes, despues);
        Assert.InRange(registradoProveedor, antes, despues);
    }

    /// <summary>
    /// H5: los filtros por día usan date(Fecha) sobre la hora local guardada,
    /// así una venta de las 23:59 pertenece a su día y no al siguiente.
    /// </summary>
    [Fact]
    public void VentasDelDia_RespetaElDiaLocalEnLosBordes()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        bd.Db.ExecuteCommand(@"
            INSERT INTO Ventas (Fecha, Usuario, MetodoPago, Total, CostoTotal)
            VALUES ('2026-11-25 23:59:00', 'Cajera', 'Efectivo', 1000, 500),
                   ('2026-11-26 00:01:00', 'Cajera', 'Efectivo', 2000, 1000)");

        var del25 = bd.Db.ObtenerVentasDelDia(new DateTime(2026, 11, 25));
        var del26 = bd.Db.ObtenerVentasDelDia(new DateTime(2026, 11, 26));

        Assert.Single(del25);
        Assert.Equal(1_000, del25[0].Total);
        Assert.Single(del26);
        Assert.Equal(2_000, del26[0].Total);
    }
}
