using Microsoft.Data.Sqlite;
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Tests;

/// <summary>
/// T25: método de pago en los pagos a proveedores y retiro de caja automático
/// cuando el pago es en efectivo.
/// </summary>
public sealed class PagoProveedorMetodoTests
{
    private static int CrearProveedor(BaseDatosPrueba bd, long saldoInicial,
        string nombre = "Distribuidora del Norte")
    {
        var resultado = bd.Config.AgregarProveedor(new Proveedor
        {
            Nombre = nombre,
            Clave = "DNO010101ABC",
            Telefono = "818-000-1111",
            Direccion = "Calle 1",
            SaldoPendiente = saldoInicial
        });

        Assert.True(resultado.IsSuccess, resultado.Message);
        return resultado.Value.ProveedorId;
    }

    [Fact]
    public void RegistrarPagoProveedor_EnEfectivo_GeneraRetiroYReduceElEfectivoEsperado()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var proveedorId = CrearProveedor(bd, saldoInicial: 30_000);
        bd.IniciarSesion(RolUsuario.Dueño, "Dueña");

        var antes = bd.Db.ObtenerResumenCajaPendiente();
        Assert.Equal(20_000, antes.EfectivoEsperado);
        Assert.Equal(0, antes.RetirosCaja);

        var pago = bd.Config.RegistrarPagoProveedor(proveedorId, 12_000,
            MetodoPagoProveedor.Efectivo);

        Assert.True(pago.IsSuccess, pago.Message);
        Assert.Equal(18_000, pago.Value.SaldoRestante);
        Assert.Equal(MetodoPagoProveedor.Efectivo, pago.Value.MetodoPago);
        Assert.NotNull(pago.Value.MovimientoCajaId);

        var despues = bd.Db.ObtenerResumenCajaPendiente();
        Assert.Equal(12_000, despues.RetirosCaja);
        Assert.Equal(8_000, despues.EfectivoEsperado);

        var movimiento = Assert.Single(bd.Db.ExecuteReportObjects<MovimientoCaja>(
            "SELECT * FROM MovimientosCaja"));
        Assert.Equal(TipoMovimientoCaja.Retiro, movimiento.Tipo);
        Assert.Equal(12_000, movimiento.Monto);
        Assert.Contains("Distribuidora del Norte", movimiento.Motivo);
        Assert.Equal("Dueña", movimiento.Usuario);
        Assert.Equal(movimiento.Id, pago.Value.MovimientoCajaId);

        var registro = Assert.Single(bd.Db.ObtenerPagosDeProveedor(proveedorId));
        Assert.Equal(MetodoPagoProveedor.Efectivo, registro.MetodoPago);
        Assert.Equal(movimiento.Id, registro.MovimientoCajaId);
    }

    [Theory]
    [InlineData(MetodoPagoProveedor.Tarjeta)]
    [InlineData(MetodoPagoProveedor.Transferencia)]
    [InlineData(MetodoPagoProveedor.Otro)]
    public void RegistrarPagoProveedor_SinEfectivo_NoTocaLaCaja(string metodo)
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var proveedorId = CrearProveedor(bd, saldoInicial: 10_000);

        var pago = bd.Config.RegistrarPagoProveedor(proveedorId, 4_000, metodo);

        Assert.True(pago.IsSuccess, pago.Message);
        Assert.Equal(metodo, pago.Value.MetodoPago);
        Assert.Null(pago.Value.MovimientoCajaId);

        var caja = bd.Db.ObtenerResumenCajaPendiente();
        Assert.Equal(0, caja.RetirosCaja);
        Assert.Equal(20_000, caja.EfectivoEsperado);
        Assert.Equal(0, bd.Db.ExecuteScalar<int>("SELECT COUNT(*) FROM MovimientosCaja"));

        var registro = Assert.Single(bd.Db.ObtenerPagosDeProveedor(proveedorId));
        Assert.Equal(metodo, registro.MetodoPago);
        Assert.Null(registro.MovimientoCajaId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Cheque")]
    [InlineData("Efectivo y tarjeta")]
    public void RegistrarPagoProveedor_ConMetodoInvalido_NoCambiaNada(string metodo)
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var proveedorId = CrearProveedor(bd, saldoInicial: 10_000);

        var pago = bd.Config.RegistrarPagoProveedor(proveedorId, 4_000, metodo);

        Assert.True(pago.IsError);
        Assert.Equal(10_000, bd.Db.ObtenerProveedorPorId(proveedorId).SaldoPendiente);
        Assert.Empty(bd.Db.ObtenerPagosDeProveedor(proveedorId));
        Assert.Equal(0, bd.Db.ExecuteScalar<int>("SELECT COUNT(*) FROM MovimientosCaja"));
        Assert.Empty(bd.Db.ObtenerBitacora(accion: AccionBitacora.PagoProveedor));
    }

    [Fact]
    public void RegistrarPagoProveedor_RecortaEspaciosDelMetodo()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var proveedorId = CrearProveedor(bd, saldoInicial: 10_000);

        var pago = bd.Config.RegistrarPagoProveedor(proveedorId, 1_000, "  Transferencia  ");

        Assert.True(pago.IsSuccess, pago.Message);
        Assert.Equal(MetodoPagoProveedor.Transferencia, pago.Value.MetodoPago);
        Assert.Equal(MetodoPagoProveedor.Transferencia,
            Assert.Single(bd.Db.ObtenerPagosDeProveedor(proveedorId)).MetodoPago);
    }

    [Fact]
    public void RegistrarPagoProveedor_EncargadoEnEfectivo_SePermiteYDejaBitacoraConRetiro()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var proveedorId = CrearProveedor(bd, saldoInicial: 10_000);
        bd.IniciarSesion(RolUsuario.Encargado, "Marta");

        var pago = bd.Config.RegistrarPagoProveedor(proveedorId, 4_000,
            MetodoPagoProveedor.Efectivo);

        Assert.True(pago.IsSuccess, pago.Message);
        Assert.NotNull(pago.Value.MovimientoCajaId);
        Assert.Equal(4_000, bd.Db.ObtenerResumenCajaPendiente().RetirosCaja);

        var registro = Assert.Single(bd.Db.ObtenerBitacora(accion: AccionBitacora.PagoProveedor));
        Assert.Equal("Marta", registro.Usuario);
        Assert.Contains("Metodo=Efectivo", registro.DatosDespues);
        Assert.Contains($"RetiroCaja={pago.Value.MovimientoCajaId.Value}", registro.DatosDespues);
    }

    [Fact]
    public void RegistrarPagoProveedor_ComoEmpleado_FallaSinCambios()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var proveedorId = CrearProveedor(bd, saldoInicial: 10_000);
        bd.IniciarSesion(RolUsuario.Empleado, "Empleado");

        var pago = bd.Config.RegistrarPagoProveedor(proveedorId, 1_000,
            MetodoPagoProveedor.Efectivo);

        Assert.True(pago.IsError);
        Assert.Equal(10_000, bd.Db.ObtenerProveedorPorId(proveedorId).SaldoPendiente);
        Assert.Empty(bd.Db.ObtenerPagosDeProveedor(proveedorId));
        Assert.Equal(0, bd.Db.ExecuteScalar<int>("SELECT COUNT(*) FROM MovimientosCaja"));
    }

    [Fact]
    public void MigracionV8aV9_ConservaPagosConMetodoVacioYSinMovimiento()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var proveedorId = CrearProveedor(bd, saldoInicial: 0);

        // Simula una base v8: quita las columnas nuevas y baja la versión.
        bd.Db.ExecuteCommand("ALTER TABLE PagosProveedor DROP COLUMN MetodoPago");
        bd.Db.ExecuteCommand("ALTER TABLE PagosProveedor DROP COLUMN MovimientoCajaId");
        bd.Db.ExecuteCommand("ALTER TABLE ClientesFiado DROP COLUMN Activo");
        bd.Db.ExecuteCommand("ALTER TABLE Proveedores DROP COLUMN Activo");
        bd.Db.ExecuteCommand("PRAGMA user_version = 8");

        bd.Db.ExecuteCommand(@"
            INSERT INTO PagosProveedor (ProveedorId, Fecha, Monto, SaldoAnterior, SaldoNuevo, Usuario)
            VALUES (@proveedor, @fecha, 5000, 15000, 10000, 'Dueña')",
            new SqliteParameter("@proveedor", proveedorId),
            new SqliteParameter("@fecha", DateTime.Now));

        bd.Db.InicializarBaseDeDatos();

        Assert.Equal(Migraciones.VersionMaxima(Migraciones.ObtenerTodas()),
            bd.Db.ExecuteScalar<int>("PRAGMA user_version"));

        var pago = Assert.Single(bd.Db.ObtenerPagosDeProveedor(proveedorId));
        Assert.Equal(5_000, pago.Monto);
        Assert.Equal(string.Empty, pago.MetodoPago);
        Assert.Null(pago.MovimientoCajaId);
    }
}
