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
}
