// Formato del ticket interno y del resumen que se codifica en el QR
using System.Globalization;
using System.Text;
using SistemaTienda.Core;

namespace SistemaTienda.Application.Services;

public sealed class TicketGenerado
{
    public string Texto { get; init; } = string.Empty;
    public string ContenidoQr { get; init; } = string.Empty;
}

/// <summary>
/// Construye el ticket interno a partir de la venta guardada. El QR es informativo:
/// no sustituye un comprobante fiscal ni se valida contra un servidor.
/// </summary>
public sealed class TicketService
{
    private const int MaximoProductosQr = 10;
    private const int LargoMaximoNombreQr = 40;
    private const int LargoMaximoTiendaQr = 60;
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-MX");

    public TicketGenerado Generar(Venta venta, IReadOnlyList<VentaLinea> lineas,
        ConfiguracionTienda configuracion, string nombreCliente = null,
        long? efectivoRecibido = null, AnulacionVenta anulacion = null)
    {
        ArgumentNullException.ThrowIfNull(venta);

        var productos = lineas ?? Array.Empty<VentaLinea>();
        var tienda = string.IsNullOrWhiteSpace(configuracion?.NombreTienda)
            ? "Mi tienda"
            : configuracion.NombreTienda.Trim();

        return new TicketGenerado
        {
            Texto = ConstruirTexto(venta, productos, tienda, nombreCliente, efectivoRecibido, anulacion),
            ContenidoQr = ConstruirContenidoQr(venta, productos, tienda)
        };
    }

    private static string ConstruirTexto(Venta venta, IReadOnlyList<VentaLinea> lineas,
        string tienda, string nombreCliente, long? efectivoRecibido, AnulacionVenta anulacion)
    {
        var sb = new StringBuilder();
        sb.AppendLine(tienda);
        sb.AppendLine($"Ticket interno (no fiscal) · Venta #{venta.Id}");
        sb.AppendLine($"{venta.Fecha.ToString("dd/MM/yyyy HH:mm", Cultura)} · Atendió: {venta.Usuario}");
        sb.AppendLine(new string('-', 40));

        foreach (var linea in lineas)
        {
            sb.AppendLine($"{linea.Cantidad} x {linea.Nombre}");
            sb.AppendLine(
                $"   ${Dinero.Formatear(linea.PrecioUnitario)} c/u = ${Dinero.Formatear(linea.Importe)}");
        }

        sb.AppendLine(new string('-', 40));
        sb.AppendLine($"TOTAL: ${Dinero.Formatear(venta.Total)}");
        sb.AppendLine($"Pago: {venta.MetodoPago}");

        if (venta.PagoEfectivo > 0 && venta.PagoTarjeta > 0)
        {
            sb.AppendLine($"   Efectivo: ${Dinero.Formatear(venta.PagoEfectivo)}");
            sb.AppendLine($"   Tarjeta: ${Dinero.Formatear(venta.PagoTarjeta)}");
        }

        if (venta.PagoFiado > 0)
            sb.AppendLine($"   Fiado a: {nombreCliente ?? "cliente"}");

        if (efectivoRecibido.HasValue && venta.MetodoPago == MetodoPagoVenta.Efectivo)
        {
            sb.AppendLine($"Recibido: ${Dinero.Formatear(efectivoRecibido.Value)}");
            sb.AppendLine($"Cambio: ${Dinero.Formatear(efectivoRecibido.Value - venta.Total)}");
        }

        if (venta.ComisionTarjeta > 0)
            sb.AppendLine(
                $"Comisión tarjeta: ${Dinero.Formatear(venta.ComisionTarjeta)} (afecta la utilidad)");

        if (venta.Anulada || anulacion != null)
        {
            sb.AppendLine();
            sb.AppendLine("*** VENTA ANULADA ***");
            if (anulacion != null)
            {
                sb.AppendLine(
                    $"Anulada por: {anulacion.Usuario} · {anulacion.Fecha.ToString("dd/MM/yyyy HH:mm", Cultura)}");
                sb.AppendLine($"Motivo: {anulacion.Motivo}");
            }
            sb.AppendLine("Este comprobante ya no es válido.");
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine("Gracias por su compra.");
        }

        return sb.ToString();
    }

    private static string ConstruirContenidoQr(Venta venta, IReadOnlyList<VentaLinea> lineas,
        string tienda)
    {
        var sb = new StringBuilder();
        sb.AppendLine("SistemaTienda · Ticket interno");
        sb.AppendLine($"Tienda: {Recortar(tienda, LargoMaximoTiendaQr)}");
        sb.AppendLine($"Venta: #{venta.Id}");
        sb.AppendLine($"Fecha: {venta.Fecha.ToString("dd/MM/yyyy HH:mm", Cultura)}");
        sb.AppendLine($"Estado: {(venta.Anulada ? "Anulada" : "Vigente")}");
        sb.AppendLine($"Total: ${Dinero.Formatear(venta.Total)}");
        sb.AppendLine($"Pago: {venta.MetodoPago}");
        sb.AppendLine("Productos:");

        var incluidos = Math.Min(lineas.Count, MaximoProductosQr);
        for (var i = 0; i < incluidos; i++)
        {
            var linea = lineas[i];
            sb.AppendLine(
                $"- {linea.Cantidad} x {Recortar(linea.Nombre, LargoMaximoNombreQr)}: ${Dinero.Formatear(linea.Importe)}");
        }

        if (lineas.Count > incluidos)
            sb.AppendLine($"- ... y {lineas.Count - incluidos} producto(s) más");

        sb.Append("No es un comprobante fiscal.");
        return sb.ToString();
    }

    private static string Recortar(string texto, int largo)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        var limpio = texto.Trim();
        return limpio.Length <= largo ? limpio : $"{limpio[..largo]}…";
    }
}
