using Microsoft.Data.Sqlite;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Tests;

public sealed class DesgloseCajaTests
{
    private static DenominacionEfectivo Buscar(long valorCentavos) =>
        ConteoEfectivo.Denominaciones.Single(d => d.ValorCentavos == valorCentavos);

    private static List<(DenominacionEfectivo Denominacion, int Cantidad)> Conteo(
        params (long ValorCentavos, int Cantidad)[] renglones) =>
        renglones.Select(r => (Buscar(r.ValorCentavos), r.Cantidad)).ToList();

    [Fact]
    public void CerrarCorte_ConConteo_GuardaElDesgloseYCuadraConElContado()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var conteo = Conteo((20_000, 1), (5_000, 2), (1_000, 5), (50, 10));
        var resultado = new CorteCajaService(bd.Db)
            .RegistrarCorte(35_500, 20_000, "Cajero", "Sobrante revisado", conteo);

        Assert.True(resultado.IsSuccess, resultado.Message);
        var corte = resultado.Value;
        Assert.Equal(35_500, corte.EfectivoContado);
        Assert.Equal(4, corte.Desglose.Count);
        Assert.Equal(20_000, corte.Desglose[0].Subtotal);
        Assert.Equal(500, corte.Desglose[^1].Subtotal);

        var guardado = bd.Db.ObtenerDesgloseCorte(corte.Id);
        Assert.Equal(4, guardado.Count);
        Assert.Equal(35_500, guardado.Sum(d => d.Subtotal));

        var renglon = Assert.Single(guardado, d => d.ValorCentavos == 5_000);
        Assert.Equal(2, renglon.Cantidad);
        Assert.Equal(10_000, renglon.Subtotal);

        var detalleBitacora = bd.Db.ExecuteScalar<string>(
            "SELECT DatosDespues FROM Bitacora WHERE Accion = @accion ORDER BY Id DESC LIMIT 1",
            new SqliteParameter("@accion", AccionBitacora.CorteCaja));
        Assert.Contains("Desglose=$200x1", detalleBitacora);
    }

    [Fact]
    public void CerrarCorte_DescartaDenominacionesEnCero()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var conteo = Conteo((20_000, 1), (10_000, 0), (50, 0));
        var resultado = new CorteCajaService(bd.Db)
            .RegistrarCorte(20_000, 20_000, "Cajero", "", conteo);

        Assert.True(resultado.IsSuccess, resultado.Message);
        Assert.Single(resultado.Value.Desglose);
        Assert.Equal(20_000, resultado.Value.Desglose[0].ValorCentavos);
    }

    [Fact]
    public void CerrarCorte_ConConteoQueNoCoincide_SeRechazaSinGuardar()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var conteo = Conteo((20_000, 1), (50, 1));
        var resultado = new CorteCajaService(bd.Db)
            .RegistrarCorte(20_000, 20_000, "Cajero", "", conteo);

        Assert.True(resultado.IsError);
        Assert.Contains("deben coincidir", resultado.Message);
        Assert.Equal(0, bd.Db.ExecuteScalar<int>("SELECT COUNT(*) FROM CortesCaja"));
        Assert.Equal(0, bd.Db.ExecuteScalar<int>("SELECT COUNT(*) FROM CorteCajaDesglose"));
    }

    [Fact]
    public void CerrarCorte_ConDenominacionFueraDelCatalogo_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var conteo = new List<(DenominacionEfectivo, int)>
        {
            (new DenominacionEfectivo(123, "$1.23", "Monedas"), 1)
        };

        var resultado = new CorteCajaService(bd.Db)
            .RegistrarCorte(123, 20_000, "Cajero", "Diferencia revisada", conteo);

        Assert.True(resultado.IsError);
        Assert.Contains("catálogo", resultado.Message);
        Assert.Equal(0, bd.Db.ExecuteScalar<int>("SELECT COUNT(*) FROM CorteCajaDesglose"));
    }

    [Fact]
    public void CerrarCorte_ConCantidadNegativa_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var conteo = Conteo((20_000, -1));
        var resultado = new CorteCajaService(bd.Db)
            .RegistrarCorte(0, 20_000, "Cajero", "Diferencia revisada", conteo);

        Assert.True(resultado.IsError);
        Assert.Contains("entero", resultado.Message);
        Assert.Equal(0, bd.Db.ExecuteScalar<int>("SELECT COUNT(*) FROM CortesCaja"));
    }

    [Fact]
    public void CerrarCorte_ConDenominacionRepetida_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var veinte = Buscar(2_000);
        var conteo = new List<(DenominacionEfectivo, int)> { (veinte, 1), (veinte, 2) };
        var resultado = new CorteCajaService(bd.Db)
            .RegistrarCorte(6_000, 20_000, "Cajero", "Diferencia revisada", conteo);

        Assert.True(resultado.IsError);
        Assert.Contains("repetida", resultado.Message);
    }

    [Fact]
    public void CerrarCorte_SinConteo_GuardaElTotalManualSinDesglose()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var resultado = new CorteCajaService(bd.Db)
            .RegistrarCorte(21_000, 20_000, "Cajero", "Sobrante revisado");

        Assert.True(resultado.IsSuccess, resultado.Message);
        Assert.Empty(resultado.Value.Desglose);
        Assert.Empty(bd.Db.ObtenerDesgloseCorte(resultado.Value.Id));
    }

    [Fact]
    public void ObtenerCortes_CargaElDesgloseDeCadaCorte()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var servicio = new CorteCajaService(bd.Db);

        Assert.True(servicio.RegistrarCorte(20_000, 20_000, "Cajero", "",
            Conteo((20_000, 1))).IsSuccess);
        Assert.True(servicio.RegistrarCorte(20_500, 20_000, "Cajero", "Sobrante",
            Conteo((10_000, 2), (500, 1))).IsSuccess);

        var cortes = servicio.ObtenerCortes().Value;

        Assert.Equal(2, cortes.Count);
        Assert.Equal(2, cortes[0].Desglose.Count);
        Assert.Single(cortes[1].Desglose);
        Assert.Equal(20_500, cortes[0].Desglose.Sum(d => d.Subtotal));
    }

    [Fact]
    public void MigracionV11_CreaDesgloseInmutableYConservaCortesAnteriores()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var anterior = new CorteCajaService(bd.Db)
            .RegistrarCorte(20_000, 20_000, "Cajero");
        Assert.True(anterior.IsSuccess, anterior.Message);

        // Simula una base v10: sin la tabla del desglose.
        bd.Db.ExecuteCommand("DROP TABLE CorteCajaDesglose");
        bd.Db.ExecuteCommand("PRAGMA user_version = 10");

        bd.Db.InicializarBaseDeDatos();

        Assert.Equal(Migraciones.VersionMaxima(Migraciones.ObtenerTodas()),
            bd.Db.ExecuteScalar<int>("PRAGMA user_version"));
        Assert.Equal(1, bd.Db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'CorteCajaDesglose'"));
        Assert.Equal(1, bd.Db.ExecuteScalar<int>("SELECT COUNT(*) FROM CortesCaja"));
        Assert.Empty(bd.Db.ObtenerDesgloseCorte(anterior.Value.Id));
        Assert.Single(RespaldoLocal.Listar(bd.Db.DirectorioRespaldos),
            r => r.Nombre.Contains("antes-migrar"));

        var nuevo = new CorteCajaService(bd.Db)
            .RegistrarCorte(20_000, 20_000, "Cajero", "", Conteo((20_000, 1)));
        Assert.True(nuevo.IsSuccess, nuevo.Message);

        Assert.Throws<SqliteException>(() =>
            bd.Db.ExecuteCommand("UPDATE CorteCajaDesglose SET Cantidad = 99"));
        Assert.Throws<SqliteException>(() =>
            bd.Db.ExecuteCommand("DELETE FROM CorteCajaDesglose"));
    }

    [Fact]
    public void CrearDesglose_CalculaSubtotalesYTotalDelConteo()
    {
        var resultado = ConteoEfectivo.CrearDesglose(Conteo((10_000, 2), (500, 3)));

        Assert.True(resultado.IsSuccess, resultado.Message);
        Assert.Equal(2, resultado.Value.Count);
        Assert.Equal(20_000, resultado.Value[0].Subtotal);
        Assert.Equal(1_500, resultado.Value[1].Subtotal);
        Assert.Equal(21_500, resultado.Value.Sum(d => d.Subtotal));
        Assert.Equal("$100", ConteoEfectivo.Etiqueta(10_000));
    }
}
