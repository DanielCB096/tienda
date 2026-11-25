using SistemaTienda.Application.Services;
using SistemaTienda.Core;

namespace SistemaTienda.Tests;

public sealed class ExportadorCsvTests
{
    [Fact]
    public void ReporteUtilidad_IncluyePeriodoYDesglose()
    {
        var reporte = new ReporteUtilidad
        {
            Desde = new DateTime(2026, 11, 1),
            Hasta = new DateTime(2026, 11, 21),
            NumeroVentas = 3,
            VentasTotal = 12_345,
            CostoVentas = 5_000,
            ComisionTarjeta = 100,
            Mermas = 200,
            Gastos = 1_000
        };

        var csv = ExportadorCsv.ReporteUtilidad(reporte);

        Assert.StartsWith("Concepto,Valor", csv);
        Assert.Contains("Periodo desde,2026-11-01", csv);
        Assert.Contains("Periodo hasta,2026-11-21", csv);
        Assert.Contains("Ventas,3", csv);
        Assert.Contains("Ventas cobradas,123.45", csv);
        Assert.Contains("Costo de ventas,50.00", csv);
        Assert.Contains("Utilidad bruta,72.45", csv);
        Assert.Contains("Utilidad neta,60.45", csv);
        Assert.EndsWith("\r\n", csv);
    }

    [Theory]
    [InlineData("simple", "simple")]
    [InlineData("con,coma", "\"con,coma\"")]
    [InlineData("con \"comillas\"", "\"con \"\"comillas\"\"\"")]
    [InlineData("linea\nnueva", "\"linea\nnueva\"")]
    public void Campo_EscapaSoloCuandoHaceFalta(string valor, string esperado)
    {
        Assert.Equal(esperado, ExportadorCsv.Campo(valor));
    }

    [Fact]
    public void Campo_Nulo_DevuelveVacio()
    {
        Assert.Equal(string.Empty, ExportadorCsv.Campo(null));
    }

    [Theory]
    [InlineData("=SUM(A1)", "'=SUM(A1)")]
    [InlineData("+cmd|' /C calc'!A0", "'+cmd|' /C calc'!A0")]
    [InlineData("-2+3", "'-2+3")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("texto normal", "texto normal")]
    public void CampoTexto_NeutralizaFormulas(string valor, string esperado)
    {
        Assert.Equal(esperado, ExportadorCsv.CampoTexto(valor));
    }

    [Fact]
    public void CampoTexto_ConComa_NeutralizaYEntrecomilla()
    {
        Assert.Equal("\"'=SUM(A1,B1)\"", ExportadorCsv.CampoTexto("=SUM(A1,B1)"));
    }

    [Fact]
    public void ImporteNegativo_NoSeNeutralizaComoFormula()
    {
        Assert.Equal("-40.00", ExportadorCsv.Importe(-4_000));
    }

    [Fact]
    public void EstadoCuentaCliente_NeutralizaNombreConFormula()
    {
        var estado = new EstadoCuentaCliente
        {
            Cliente = new ClienteFiado
            {
                Nombre = "=HYPERLINK(\"http://malo\")",
                Telefono = "+5550000000"
            }
        };

        var csv = ExportadorCsv.EstadoCuentaCliente(estado);

        Assert.Contains("\"'=HYPERLINK(\"\"http://malo\"\")\"", csv);
        Assert.Contains("'+5550000000", csv);
    }

    [Fact]
    public void Importe_UsaPuntoDecimalInvariante()
    {
        Assert.Equal("1234.50", ExportadorCsv.Importe(123_450));
        Assert.Equal("-40.00", ExportadorCsv.Importe(-4_000));
        Assert.Equal("0.00", ExportadorCsv.Importe(0));
    }

    [Fact]
    public void EstadoCuentaCliente_IncluyeResumenYMovimientos()
    {
        var estado = new EstadoCuentaCliente
        {
            Cliente = new ClienteFiado
            {
                Nombre = "Ana, \"la jefa\"",
                Telefono = "5550000000"
            },
            SaldoActual = 6_000,
            TotalFiado = 10_000,
            TotalAbonado = 4_000,
            Ventas = new List<VentaFiadaCliente>
            {
                new()
                {
                    Id = 7,
                    Fecha = new DateTime(2026, 11, 20, 10, 30, 0),
                    PagoFiado = 10_000,
                    Usuario = "Cajero"
                }
            },
            Abonos = new List<AbonoFiadoCliente>
            {
                new()
                {
                    Id = 2,
                    Fecha = new DateTime(2026, 11, 21, 12, 0, 0),
                    Monto = 4_000,
                    MetodoPago = MetodoPagoAbono.Efectivo,
                    Usuario = "Cajera"
                }
            }
        };

        var csv = ExportadorCsv.EstadoCuentaCliente(estado);

        Assert.Contains("Cliente,\"Ana, \"\"la jefa\"\"\"", csv);
        Assert.Contains("Saldo actual,60.00", csv);
        Assert.Contains("Total fiado,100.00", csv);
        Assert.Contains("Total abonado,40.00", csv);
        Assert.Contains("Fecha,Tipo,Detalle,Monto", csv);
        Assert.Contains("2026-11-20 10:30,Venta #7 a fiado,Atendio Cajero,100.00", csv);
        Assert.Contains("2026-11-21 12:00,Abono,Efectivo · Registro Cajera,-40.00", csv);

        var posicionAbono = csv.IndexOf("2026-11-21 12:00", StringComparison.Ordinal);
        var posicionVenta = csv.IndexOf("2026-11-20 10:30", StringComparison.Ordinal);
        Assert.True(posicionAbono < posicionVenta, "Los movimientos deben ir del más reciente al más antiguo");
    }

    [Fact]
    public void EstadoCuentaProveedor_IncluyeMetodoSaldoDespuesYMetodoVacio()
    {
        var estado = new EstadoCuentaProveedor
        {
            Proveedor = new Proveedor
            {
                Nombre = "Distribuidora del Norte",
                Clave = "DIN-01",
                Telefono = "5551112222"
            },
            SaldoPendiente = 15_000,
            TotalPagado = 5_000,
            Pagos = new List<PagoProveedor>
            {
                new()
                {
                    Id = 1,
                    Fecha = new DateTime(2026, 11, 21, 9, 15, 0),
                    Monto = 5_000,
                    SaldoNuevo = 15_000,
                    MetodoPago = MetodoPagoProveedor.Efectivo,
                    Usuario = "Dueño"
                },
                new()
                {
                    Id = 2,
                    Fecha = new DateTime(2026, 11, 19, 8, 0, 0),
                    Monto = 2_000,
                    SaldoNuevo = 20_000,
                    MetodoPago = string.Empty,
                    Usuario = "Sistema"
                }
            }
        };

        var csv = ExportadorCsv.EstadoCuentaProveedor(estado);

        Assert.Contains("Proveedor,Distribuidora del Norte", csv);
        Assert.Contains("Clave,DIN-01", csv);
        Assert.Contains("Saldo pendiente,150.00", csv);
        Assert.Contains("Total pagado,50.00", csv);
        Assert.Contains("2026-11-21 09:15,Efectivo,Dueño,50.00,150.00", csv);
        Assert.Contains("2026-11-19 08:00,Sin metodo,Sistema,20.00,200.00", csv);
    }

    [Fact]
    public void Metodos_Nulos_LanzanArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ExportadorCsv.ReporteUtilidad(null));
        Assert.Throws<ArgumentNullException>(() => ExportadorCsv.EstadoCuentaCliente(null));
        Assert.Throws<ArgumentNullException>(() => ExportadorCsv.EstadoCuentaProveedor(null));
    }
}
