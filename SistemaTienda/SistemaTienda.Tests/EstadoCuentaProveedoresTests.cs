using Microsoft.Data.Sqlite;
using SistemaTienda.Core;

namespace SistemaTienda.Tests;

public sealed class EstadoCuentaProveedoresTests
{
    private static int CrearProveedor(BaseDatosPrueba bd, string nombre = "Distribuidora del Norte",
        string clave = "RFC123", string telefono = "5550000000", string direccion = "Calle 1",
        long saldoInicial = 0)
    {
        var resultado = bd.Config.AgregarProveedor(new Proveedor
        {
            Nombre = nombre,
            Clave = clave,
            Telefono = telefono,
            Direccion = direccion,
            SaldoPendiente = saldoInicial
        });

        Assert.True(resultado.IsSuccess, resultado.Message);
        return resultado.Value.ProveedorId;
    }

    [Fact]
    public void EditarProveedor_ComoEncargado_ActualizaDatosYRegistraBitacora()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var proveedorId = CrearProveedor(bd);
        bd.IniciarSesion(RolUsuario.Encargado, "Encargada");

        var resultado = bd.Config.EditarProveedor(proveedorId,
            "  Distribuidora del Sur  ", " RFC999 ", " 5552222222 ", " Avenida 2 ");

        Assert.True(resultado.IsSuccess, resultado.Message);
        var proveedor = bd.Db.ObtenerProveedorPorId(proveedorId);
        Assert.Equal("Distribuidora del Sur", proveedor.Nombre);
        Assert.Equal("RFC999", proveedor.Clave);
        Assert.Equal("5552222222", proveedor.Telefono);
        Assert.Equal("Avenida 2", proveedor.Direccion);

        var registro = Assert.Single(bd.Db.ObtenerBitacora(accion: AccionBitacora.EditarProveedor));
        Assert.Contains("Distribuidora del Sur", registro.DatosDespues);
        Assert.Contains("RFC999", registro.DatosDespues);
        Assert.Contains("Distribuidora del Norte", registro.DatosAntes);
        Assert.Equal("Encargada", registro.Usuario);
    }

    [Fact]
    public void EditarProveedor_ComoEmpleado_NoCambiaNada()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var proveedorId = CrearProveedor(bd);
        bd.IniciarSesion(RolUsuario.Empleado, "Empleado");

        var resultado = bd.Config.EditarProveedor(proveedorId, "Otro nombre", "X", "1", "2");

        Assert.True(resultado.IsError);
        var proveedor = bd.Db.ObtenerProveedorPorId(proveedorId);
        Assert.Equal("Distribuidora del Norte", proveedor.Nombre);
        Assert.Empty(bd.Db.ObtenerBitacora(accion: AccionBitacora.EditarProveedor));
    }

    [Fact]
    public void EditarProveedor_NoTocaElSaldoPendiente()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var proveedorId = CrearProveedor(bd, saldoInicial: 45_000);

        var resultado = bd.Config.EditarProveedor(proveedorId, "Nombre nuevo", "C", "T", "D");

        Assert.True(resultado.IsSuccess, resultado.Message);
        Assert.Equal(45_000, bd.Db.ObtenerProveedorPorId(proveedorId).SaldoPendiente);
    }

    [Fact]
    public void EditarProveedor_ConNombreVacio_Falla()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var proveedorId = CrearProveedor(bd);

        var resultado = bd.Config.EditarProveedor(proveedorId, "   ", "C", "T", "D");

        Assert.True(resultado.IsError);
        Assert.Equal("Distribuidora del Norte", bd.Db.ObtenerProveedorPorId(proveedorId).Nombre);
    }

    [Fact]
    public void EditarProveedor_Inexistente_Falla()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var resultado = bd.Config.EditarProveedor(9999, "Nombre", "C", "T", "D");

        Assert.True(resultado.IsError);
    }

    [Fact]
    public void RegistrarPago_GuardaHistorialYActualizaSaldoEnLaMismaTransaccion()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var proveedorId = CrearProveedor(bd, saldoInicial: 30_000);
        bd.IniciarSesion(RolUsuario.Dueño, "Dueña");

        var resultado = bd.Config.RegistrarPagoProveedor(proveedorId, 12_000);

        Assert.True(resultado.IsSuccess, resultado.Message);
        Assert.Equal(18_000, resultado.Value.SaldoRestante);
        Assert.Equal(18_000, bd.Db.ObtenerProveedorPorId(proveedorId).SaldoPendiente);

        var pago = Assert.Single(bd.Db.ObtenerPagosDeProveedor(proveedorId));
        Assert.Equal(12_000, pago.Monto);
        Assert.Equal(30_000, pago.SaldoAnterior);
        Assert.Equal(18_000, pago.SaldoNuevo);
        Assert.Equal("Dueña", pago.Usuario);
    }

    [Fact]
    public void RegistrarPago_MayorAlSaldo_NoGuardaHistorial()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var proveedorId = CrearProveedor(bd, saldoInicial: 10_000);

        var resultado = bd.Config.RegistrarPagoProveedor(proveedorId, 15_000);

        Assert.True(resultado.IsError);
        Assert.Empty(bd.Db.ObtenerPagosDeProveedor(proveedorId));
        Assert.Equal(10_000, bd.Db.ObtenerProveedorPorId(proveedorId).SaldoPendiente);
    }

    [Fact]
    public void ObtenerEstadoCuentaProveedor_SumaPagosYDevuelveSaldo()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var proveedorId = CrearProveedor(bd, saldoInicial: 50_000);

        Assert.True(bd.Config.RegistrarPagoProveedor(proveedorId, 20_000).IsSuccess);
        Assert.True(bd.Config.RegistrarPagoProveedor(proveedorId, 5_000).IsSuccess);

        var estado = bd.Config.ObtenerEstadoCuentaProveedor(proveedorId);

        Assert.True(estado.IsSuccess, estado.Message);
        Assert.Equal(25_000, estado.Value.TotalPagado);
        Assert.Equal(25_000, estado.Value.SaldoPendiente);
        Assert.Equal(2, estado.Value.Pagos.Count);
        Assert.True(estado.Value.Pagos[0].Fecha >= estado.Value.Pagos[1].Fecha);
    }

    [Fact]
    public void ObtenerEstadoCuentaProveedor_SinPagos_DevuelveCero()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var proveedorId = CrearProveedor(bd, saldoInicial: 7_500);

        var estado = bd.Config.ObtenerEstadoCuentaProveedor(proveedorId);

        Assert.True(estado.IsSuccess, estado.Message);
        Assert.Equal(0, estado.Value.TotalPagado);
        Assert.Equal(7_500, estado.Value.SaldoPendiente);
        Assert.Empty(estado.Value.Pagos);
    }

    [Fact]
    public void ObtenerEstadoCuentaProveedor_Inexistente_Falla()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var estado = bd.Config.ObtenerEstadoCuentaProveedor(9999);

        Assert.True(estado.IsError);
    }

    [Fact]
    public void PagosProveedor_NoSePuedenModificarNiBorrar()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var proveedorId = CrearProveedor(bd, saldoInicial: 10_000);
        Assert.True(bd.Config.RegistrarPagoProveedor(proveedorId, 4_000).IsSuccess);

        Assert.Throws<SqliteException>(() =>
            bd.Db.ExecuteCommand("UPDATE PagosProveedor SET Monto = 1"));
        Assert.Throws<SqliteException>(() =>
            bd.Db.ExecuteCommand("DELETE FROM PagosProveedor"));

        var pago = Assert.Single(bd.Db.ObtenerPagosDeProveedor(proveedorId));
        Assert.Equal(4_000, pago.Monto);
    }

    [Fact]
    public void EsquemaV8_CreaHistorialYDisparadoresInmutables()
    {
        using var bd = new BaseDatosPrueba();

        var tabla = bd.Db.ExecuteScalar<string>(
            "SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'PagosProveedor'");
        var disparadores = bd.Db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'trigger' AND name LIKE 'TR_PagosProveedor_%'");

        Assert.Equal("PagosProveedor", tabla);
        Assert.Equal(2, disparadores);
    }
}
