using Microsoft.Data.Sqlite;
using SistemaTienda.Core;

namespace SistemaTienda.Tests;

/// <summary>
/// Historial de ventas por rango: la consulta incluye los extremos del día y
/// conserva las ventas anuladas marcadas para que el historial sea completo.
/// </summary>
public sealed class HistorialVentasTests
{
    private static void InsertarVenta(BaseDatosPrueba bd, string fecha, long total,
        string usuario = "Cajera")
    {
        bd.Db.ExecuteCommand(@"
            INSERT INTO Ventas (Fecha, Usuario, MetodoPago, Total, CostoTotal)
            VALUES (@fecha, @usuario, 'Efectivo', @total, 0)",
            new SqliteParameter("@fecha", fecha),
            new SqliteParameter("@usuario", usuario),
            new SqliteParameter("@total", total));
    }

    [Fact]
    public void Rango_IncluyeLosDiasCompletosYExcluyeLosBordes()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        InsertarVenta(bd, "2026-11-24 00:00:00", 1_000);
        InsertarVenta(bd, "2026-11-24 23:59:59", 2_000);
        InsertarVenta(bd, "2026-11-25 12:00:00", 3_000);
        InsertarVenta(bd, "2026-11-26 00:00:00", 4_000);

        var ventas = bd.Db.ObtenerVentas(
            new DateTime(2026, 11, 24), new DateTime(2026, 11, 25));

        Assert.Equal(3, ventas.Count);
        Assert.DoesNotContain(ventas, venta => venta.Total == 4_000);
        Assert.Equal(6_000, ventas.Sum(venta => venta.Total));
    }

    [Fact]
    public void Historial_ConservaLasVentasAnuladasMarcadas()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        InsertarVenta(bd, "2026-11-25 09:00:00", 5_000);
        var ventaId = bd.Db.ExecuteScalar<int>(
            "SELECT MAX(Id) FROM Ventas");
        bd.Db.ExecuteCommand(@"
            INSERT INTO AnulacionesVenta (VentaId, Fecha, Usuario, Motivo)
            VALUES (@id, @fecha, 'Dueño', 'Prueba de anulación')",
            new SqliteParameter("@id", ventaId),
            new SqliteParameter("@fecha", "2026-11-25 10:00:00"));

        var venta = Assert.Single(bd.Db.ObtenerVentas(
            new DateTime(2026, 11, 25), new DateTime(2026, 11, 25)));

        Assert.True(venta.Anulada);
        Assert.Equal(ventaId, venta.Id);
    }

    [Fact]
    public void Rango_Invertido_NoDevuelveVentas()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        InsertarVenta(bd, "2026-11-25 12:00:00", 1_000);

        var ventas = bd.Db.ObtenerVentas(
            new DateTime(2026, 11, 26), new DateTime(2026, 11, 25));

        Assert.Empty(ventas);
    }
}
