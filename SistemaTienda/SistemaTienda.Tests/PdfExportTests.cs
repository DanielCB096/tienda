using System.Text;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;

namespace SistemaTienda.Tests;

public sealed class PdfExportTests
{
    private static string Texto(byte[] pdf) => Encoding.Latin1.GetString(pdf);

    private static ReporteUtilidad Reporte() => new()
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

    [Fact]
    public void Reporte_GeneraPdfValidoConResumenYDesglose()
    {
        var pdf = ExportadorPdf.ReporteUtilidad(Reporte(), "Tienda Prueba");
        var texto = Texto(pdf);

        Assert.StartsWith("%PDF-1.4", texto);
        Assert.Contains("%%EOF", texto);
        Assert.Contains("/Type /Catalog", texto);
        Assert.Contains("/Type /Page", texto);
        Assert.Contains("/BaseFont /Helvetica", texto);
        Assert.Contains("Tienda Prueba", texto);
        Assert.Contains("Reporte de utilidad", texto);
        Assert.Contains("Utilidad neta", texto);
        Assert.Contains("123.45", texto);
        Assert.Contains("60.45", texto);
        Assert.Contains("Comision de tarjeta", texto);
    }

    [Fact]
    public void EstadoCuentaCliente_IncluyeDatosYMovimientos()
    {
        var estado = new EstadoCuentaCliente
        {
            Cliente = new ClienteFiado
            {
                Id = 4,
                Nombre = "José Pérez",
                Telefono = "5550000000",
                FechaRegistro = new DateTime(2026, 1, 15)
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
                    MetodoPago = MetodoPagoAbono.Transferencia,
                    Usuario = "Cajera"
                }
            }
        };

        var texto = Texto(ExportadorPdf.EstadoCuentaCliente(estado, "Tienda Prueba"));

        Assert.Contains("Estado de cuenta de cliente", texto);
        Assert.Contains("José Pérez", texto);
        Assert.Contains("5550000000", texto);
        Assert.Contains("Saldo actual", texto);
        Assert.Contains("60.00", texto);
        Assert.Contains("Venta #7 a fiado", texto);
        Assert.Contains("Abono", texto);
        Assert.Contains("Transferencia", texto);
        Assert.Contains("-40.00", texto);
        Assert.Contains("100.00", texto);
    }

    [Fact]
    public void EstadoCuentaProveedor_IncluyePagosMetodoYSaldoDespues()
    {
        var estado = new EstadoCuentaProveedor
        {
            Proveedor = new Proveedor
            {
                Id = 3,
                Nombre = "Distribuidora del Norte",
                Clave = "DIN-01",
                Telefono = "5551112222",
                FechaRegistro = new DateTime(2026, 2, 1)
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
                }
            }
        };

        var texto = Texto(ExportadorPdf.EstadoCuentaProveedor(estado, "Tienda Prueba"));

        Assert.Contains("Estado de cuenta de proveedor", texto);
        Assert.Contains("Distribuidora del Norte", texto);
        Assert.Contains("DIN-01", texto);
        Assert.Contains("Saldo pendiente", texto);
        Assert.Contains("Efectivo", texto);
        Assert.Contains("Dueño", texto);
        Assert.Contains("Saldo despues", texto);
        Assert.Contains("-50.00", texto);
        Assert.Contains("150.00", texto);
    }

    [Fact]
    public void EstadoCuentaCliente_SinMovimientos_LoIndica()
    {
        var estado = new EstadoCuentaCliente
        {
            Cliente = new ClienteFiado { Nombre = "Sin saldo" },
            SaldoActual = 0
        };

        var texto = Texto(ExportadorPdf.EstadoCuentaCliente(estado));

        Assert.Contains("Sin ventas a fiado ni abonos registrados.", texto);
    }

    [Fact]
    public void EstadoCuentaProveedor_SinPagos_LoIndica()
    {
        var estado = new EstadoCuentaProveedor
        {
            Proveedor = new Proveedor { Nombre = "Sin pagos" },
            SaldoPendiente = 0
        };

        var texto = Texto(ExportadorPdf.EstadoCuentaProveedor(estado));

        Assert.Contains("Sin pagos registrados.", texto);
    }

    [Fact]
    public void Documento_Largo_GeneraVariasPaginas()
    {
        var pdf = new PdfDocumento();
        for (var i = 0; i < 200; i++)
            pdf.Texto($"Linea de prueba {i} con texto suficiente para ocupar espacio en la pagina.");

        var texto = Texto(pdf.Generar());
        var paginas = System.Text.RegularExpressions.Regex.Matches(texto, "/Type /Page ").Count;

        Assert.True(paginas >= 2, $"Se esperaban varias páginas y se encontraron {paginas}");
        Assert.Contains("Linea de prueba 199", texto);
    }

    [Fact]
    public void Texto_EscapaParentesisYBarraInvertida()
    {
        var pdf = new PdfDocumento();
        pdf.Texto("Cliente (A) \\ B");
        var texto = Texto(pdf.Generar());

        Assert.Contains(@"Cliente \(A\) \\ B", texto);
    }

    [Fact]
    public void Texto_ConservaAcentosEnLatín1()
    {
        var pdf = new PdfDocumento();
        pdf.Texto("Comisión · José");
        var bytes = pdf.Generar();

        Assert.Contains("Comisi", Texto(bytes));
        Assert.Contains((byte)0xF3, bytes); // ó en Latin-1
        Assert.Contains((byte)0xE9, bytes); // é en Latin-1
    }

    [Fact]
    public void Reporte_Nulo_Lanza()
    {
        Assert.Throws<ArgumentNullException>(() => ExportadorPdf.ReporteUtilidad(null));
        Assert.Throws<ArgumentNullException>(() => ExportadorPdf.EstadoCuentaCliente(null));
        Assert.Throws<ArgumentNullException>(() => ExportadorPdf.EstadoCuentaProveedor(null));
    }
}
