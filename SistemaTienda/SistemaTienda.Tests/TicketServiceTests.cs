using SistemaTienda.Application.Services;
using SistemaTienda.Core;

namespace SistemaTienda.Tests;

public sealed class TicketServiceTests
{
    private static ConfiguracionTienda Config(string nombre = "Abarrotes Lupita") =>
        new() { NombreTienda = nombre };

    private static Venta VentaBase(string metodo = MetodoPagoVenta.Efectivo)
    {
        return new Venta
        {
            Id = 7,
            Fecha = new DateTime(2026, 10, 25, 14, 30, 0),
            Usuario = "Cajero",
            MetodoPago = metodo,
            Total = 7_500,
            PagoEfectivo = metodo == MetodoPagoVenta.Efectivo ? 7_500 : 0
        };
    }

    private static List<VentaLinea> Lineas(int cantidad = 3, string nombre = "Refresco")
    {
        return new List<VentaLinea>
        {
            new()
            {
                ProductoId = 1,
                Nombre = nombre,
                Cantidad = cantidad,
                PrecioUnitario = 2_500,
                Importe = 2_500 * cantidad
            }
        };
    }

    [Fact]
    public void Generar_Efectivo_IncluyeProductosCambioYQr()
    {
        var venta = VentaBase();
        var ticket = new TicketService().Generar(
            venta, Lineas(), Config(), efectivoRecibido: 10_000);

        Assert.Contains("Abarrotes Lupita", ticket.Texto);
        Assert.Contains("Venta #7", ticket.Texto);
        Assert.Contains("25/10/2026 14:30", ticket.Texto);
        Assert.Contains("3 x Refresco", ticket.Texto);
        Assert.Contains("TOTAL: $75.00", ticket.Texto);
        Assert.Contains("Recibido: $100.00", ticket.Texto);
        Assert.Contains("Cambio: $25.00", ticket.Texto);
        Assert.Contains("Gracias por su compra.", ticket.Texto);

        Assert.Contains("Venta: #7", ticket.ContenidoQr);
        Assert.Contains("Fecha: 25/10/2026 14:30", ticket.ContenidoQr);
        Assert.Contains("Estado: Vigente", ticket.ContenidoQr);
        Assert.Contains("Total: $75.00", ticket.ContenidoQr);
        Assert.Contains("3 x Refresco: $75.00", ticket.ContenidoQr);
        Assert.Contains("No es un comprobante fiscal.", ticket.ContenidoQr);
    }

    [Fact]
    public void Generar_Mixto_DesglosaPagosYComision()
    {
        var venta = VentaBase(MetodoPagoVenta.Mixto);
        venta.Total = 10_000;
        venta.PagoEfectivo = 8_000;
        venta.PagoTarjeta = 2_000;
        venta.ComisionTarjeta = 58;

        var ticket = new TicketService().Generar(
            venta, new List<VentaLinea>
            {
                new() { Nombre = "Producto", Cantidad = 1, PrecioUnitario = 10_000, Importe = 10_000 }
            }, Config());

        Assert.Contains("Pago: Mixto", ticket.Texto);
        Assert.Contains("Efectivo: $80.00", ticket.Texto);
        Assert.Contains("Tarjeta: $20.00", ticket.Texto);
        Assert.Contains("Comisión tarjeta: $0.58", ticket.Texto);
        Assert.DoesNotContain("Recibido:", ticket.Texto);
        Assert.Contains("Pago: Mixto", ticket.ContenidoQr);
    }

    [Fact]
    public void Generar_Fiado_IncluyeClienteYNoIncluyeCambio()
    {
        var venta = VentaBase(MetodoPagoVenta.Fiado);
        venta.Total = 5_000;
        venta.PagoEfectivo = 0;
        venta.PagoFiado = 5_000;

        var ticket = new TicketService().Generar(
            venta, Lineas(2, "Pan"), Config(), nombreCliente: "Ana");

        Assert.Contains("Pago: Fiado", ticket.Texto);
        Assert.Contains("Fiado a: Ana", ticket.Texto);
        Assert.DoesNotContain("Cambio:", ticket.Texto);
        Assert.DoesNotContain("Ana", ticket.ContenidoQr);
    }

    [Fact]
    public void Generar_Anulada_MarcaComprobanteYNoAgradece()
    {
        var venta = VentaBase();
        venta.Anulada = true;
        var anulacion = new AnulacionVenta
        {
            VentaId = 7,
            Fecha = new DateTime(2026, 10, 25, 15, 0, 0),
            Usuario = "Dueño",
            Motivo = "Cobro duplicado"
        };

        var ticket = new TicketService().Generar(
            venta, Lineas(), Config(), anulacion: anulacion);

        Assert.Contains("*** VENTA ANULADA ***", ticket.Texto);
        Assert.Contains("Anulada por: Dueño · 25/10/2026 15:00", ticket.Texto);
        Assert.Contains("Motivo: Cobro duplicado", ticket.Texto);
        Assert.Contains("Este comprobante ya no es válido.", ticket.Texto);
        Assert.DoesNotContain("Gracias por su compra.", ticket.Texto);
        Assert.Contains("Estado: Anulada", ticket.ContenidoQr);
    }

    [Fact]
    public void Generar_QrResumeDiezProductosYLosDemasSeCuentan()
    {
        var lineas = Enumerable.Range(1, 12)
            .Select(i => new VentaLinea
            {
                Nombre = $"Producto {i}",
                Cantidad = 1,
                PrecioUnitario = 100,
                Importe = 100
            })
            .ToList();
        var venta = VentaBase();
        venta.Total = 1_200;

        var ticket = new TicketService().Generar(venta, lineas, Config());

        Assert.Contains("Producto 10: $1.00", ticket.ContenidoQr);
        Assert.DoesNotContain("Producto 11:", ticket.ContenidoQr);
        Assert.Contains("... y 2 producto(s) más", ticket.ContenidoQr);
        Assert.Contains("1 x Producto 12", ticket.Texto);
    }

    [Fact]
    public void Generar_SinNombreDeTienda_UsaNombreGenerico()
    {
        var ticket = new TicketService().Generar(
            VentaBase(), Lineas(), Config(string.Empty));

        Assert.StartsWith("Mi tienda", ticket.Texto);
        Assert.Contains("Tienda: Mi tienda", ticket.ContenidoQr);
    }
}
