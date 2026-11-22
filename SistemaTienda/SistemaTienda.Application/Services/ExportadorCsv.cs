// Generación de CSV para reportes y estados de cuenta, sin dependencias de interfaz.
using System.Globalization;
using SistemaTienda.Core;

namespace SistemaTienda.Application.Services;

/// <summary>
/// Convierte reportes y estados de cuenta en texto CSV listo para Excel.
/// Usa coma como separador, punto decimal invariante y comillas solo cuando
/// el campo las necesita. El archivo se guarda con BOM UTF-8 desde la interfaz.
/// </summary>
public static class ExportadorCsv
{
    private const string FinLinea = "\r\n";

    public static string ReporteUtilidad(ReporteUtilidad reporte)
    {
        if (reporte == null)
            throw new ArgumentNullException(nameof(reporte));

        var lineas = new List<string>
        {
            "Concepto,Valor",
            Fila("Periodo desde", reporte.Desde.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            Fila("Periodo hasta", reporte.Hasta.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            Fila("Ventas", reporte.NumeroVentas.ToString(CultureInfo.InvariantCulture)),
            Fila("Ventas cobradas", Importe(reporte.VentasTotal)),
            Fila("Cobros en efectivo", Importe(MontoDe(reporte, MetodoPagoVenta.Efectivo))),
            Fila("Cobros con tarjeta", Importe(MontoDe(reporte, MetodoPagoVenta.Tarjeta))),
            Fila("Ventas a fiado", Importe(MontoDe(reporte, MetodoPagoVenta.Fiado))),
            Fila("Costo de ventas", Importe(reporte.CostoVentas)),
            Fila("Comision de tarjeta", Importe(reporte.ComisionTarjeta)),
            Fila("Utilidad bruta", Importe(reporte.UtilidadBruta)),
            Fila("Mermas", Importe(reporte.Mermas)),
            Fila("Gastos", Importe(reporte.Gastos)),
            Fila("Utilidad neta", Importe(reporte.UtilidadNeta))
        };

        return string.Join(FinLinea, lineas) + FinLinea;
    }

    public static string EstadoCuentaCliente(EstadoCuentaCliente estado)
    {
        if (estado == null)
            throw new ArgumentNullException(nameof(estado));

        var cliente = estado.Cliente ?? new ClienteFiado();
        var lineas = new List<string>
        {
            Fila("Cliente", cliente.Nombre),
            Fila("Telefono", cliente.Telefono),
            Fila("Saldo actual", Importe(estado.SaldoActual)),
            Fila("Total fiado", Importe(estado.TotalFiado)),
            Fila("Total abonado", Importe(estado.TotalAbonado)),
            string.Empty,
            "Fecha,Tipo,Detalle,Monto"
        };

        var movimientos = new List<(DateTime Fecha, string Tipo, string Detalle, long Monto)>();

        foreach (var venta in estado.Ventas)
        {
            movimientos.Add((venta.Fecha, $"Venta #{venta.Id} a fiado",
                string.IsNullOrWhiteSpace(venta.Usuario) ? string.Empty : $"Atendio {venta.Usuario}",
                venta.PagoFiado));
        }

        foreach (var abono in estado.Abonos)
        {
            var detalle = string.IsNullOrWhiteSpace(abono.MetodoPago)
                ? "Abono"
                : abono.MetodoPago;
            if (!string.IsNullOrWhiteSpace(abono.Usuario))
                detalle += $" · Registro {abono.Usuario}";

            movimientos.Add((abono.Fecha, "Abono", detalle, -abono.Monto));
        }

        foreach (var movimiento in movimientos
                     .OrderByDescending(m => m.Fecha)
                     .ThenByDescending(m => m.Tipo, StringComparer.Ordinal))
        {
            lineas.Add(string.Join(",",
                Campo(movimiento.Fecha.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)),
                Campo(movimiento.Tipo),
                Campo(movimiento.Detalle),
                Campo(Importe(movimiento.Monto))));
        }

        return string.Join(FinLinea, lineas) + FinLinea;
    }

    public static string EstadoCuentaProveedor(EstadoCuentaProveedor estado)
    {
        if (estado == null)
            throw new ArgumentNullException(nameof(estado));

        var proveedor = estado.Proveedor ?? new Proveedor();
        var lineas = new List<string>
        {
            Fila("Proveedor", proveedor.Nombre),
            Fila("Clave", proveedor.Clave),
            Fila("Telefono", proveedor.Telefono),
            Fila("Saldo pendiente", Importe(estado.SaldoPendiente)),
            Fila("Total pagado", Importe(estado.TotalPagado)),
            string.Empty,
            "Fecha,Metodo,Usuario,Monto,Saldo despues"
        };

        foreach (var pago in estado.Pagos.OrderByDescending(p => p.Fecha))
        {
            lineas.Add(string.Join(",",
                Campo(pago.Fecha.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)),
                Campo(string.IsNullOrWhiteSpace(pago.MetodoPago) ? "Sin metodo" : pago.MetodoPago),
                Campo(pago.Usuario),
                Campo(Importe(pago.Monto)),
                Campo(Importe(pago.SaldoNuevo))));
        }

        return string.Join(FinLinea, lineas) + FinLinea;
    }

    /// <summary>Importe en centavos a pesos con dos decimales y punto invariante.</summary>
    public static string Importe(long centavos) =>
        (centavos / 100m).ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>Escapa un campo solo cuando contiene coma, comillas o salto de línea.</summary>
    public static string Campo(string valor)
    {
        var texto = valor ?? string.Empty;
        if (texto.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0)
            return texto;

        return "\"" + texto.Replace("\"", "\"\"") + "\"";
    }

    private static string Fila(string concepto, string valor) =>
        string.Join(",", Campo(concepto), Campo(valor));

    private static long MontoDe(ReporteUtilidad reporte, string metodo) =>
        reporte.DesglosePagos.FirstOrDefault(d => d.Metodo == metodo)?.Monto ?? 0;
}
