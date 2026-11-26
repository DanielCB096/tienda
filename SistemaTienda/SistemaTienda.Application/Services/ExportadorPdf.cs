// Documentos PDF de reportes y estados de cuenta, listos para compartir o imprimir.
using System.Globalization;
using SistemaTienda.Core;

namespace SistemaTienda.Application.Services;

public static class ExportadorPdf
{
    public static byte[] ReporteUtilidad(ReporteUtilidad reporte, string nombreTienda = null)
    {
        if (reporte == null)
            throw new ArgumentNullException(nameof(reporte));

        var pdf = new PdfDocumento();
        Encabezado(pdf, nombreTienda, "Reporte de utilidad",
            $"Periodo: {reporte.Desde:dd/MM/yyyy} al {reporte.Hasta:dd/MM/yyyy}");

        pdf.Fila("Ventas", reporte.NumeroVentas.ToString(CultureInfo.InvariantCulture));
        pdf.Fila("Ventas cobradas", Dinero.Formatear(reporte.VentasTotal));
        pdf.Fila("IVA incluido (informativo)", Dinero.Formatear(reporte.ImpuestoIncluido));

        if (reporte.DesglosePagos.Count > 0)
        {
            pdf.Espacio(4);
            pdf.Texto("Cobros por metodo", 10, true);
            foreach (var desglose in reporte.DesglosePagos)
            {
                var etiqueta = desglose.NumeroVentas == 1
                    ? $"{desglose.Metodo} (1 venta)"
                    : $"{desglose.Metodo} ({desglose.NumeroVentas} ventas)";
                pdf.Fila(etiqueta, Dinero.Formatear(desglose.Monto), 9);
            }
        }

        pdf.Espacio(4);
        pdf.Fila("Costo de ventas", Dinero.Formatear(reporte.CostoVentas));
        pdf.Fila("Comision de tarjeta", Dinero.Formatear(reporte.ComisionTarjeta));
        pdf.LineaHorizontal();
        pdf.Fila("Utilidad bruta", Dinero.Formatear(reporte.UtilidadBruta), 11, true);
        pdf.Fila("Mermas", Dinero.Formatear(reporte.Mermas));
        pdf.Fila("Gastos", Dinero.Formatear(reporte.Gastos));
        pdf.LineaHorizontal();
        pdf.Fila("Utilidad neta", Dinero.Formatear(reporte.UtilidadNeta), 12, true);

        if (reporte.Productos.Count > 0)
        {
            pdf.Espacio(4);
            pdf.Texto("Productos mas vendidos", 10, true);
            foreach (var producto in reporte.Productos.Take(15))
            {
                pdf.Fila($"{producto.Nombre} ({producto.Cantidad})",
                    $"{Dinero.Formatear(producto.Ventas)} / {Dinero.Formatear(producto.Utilidad)}", 9);
            }
        }

        if (reporte.Categorias.Count > 0)
        {
            pdf.Espacio(4);
            pdf.Texto("Ventas por categoria", 10, true);
            foreach (var categoria in reporte.Categorias)
            {
                pdf.Fila($"{categoria.Categoria} ({categoria.Cantidad})",
                    $"{Dinero.Formatear(categoria.Ventas)} / {Dinero.Formatear(categoria.Utilidad)}", 9);
            }
        }

        if (reporte.Dias.Count > 0)
        {
            pdf.Espacio(4);
            pdf.Texto("Ventas por dia", 10, true);
            foreach (var dia in reporte.Dias)
            {
                pdf.Fila($"{dia.Fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)} ({dia.NumeroVentas} ventas)",
                    $"{Dinero.Formatear(dia.Ventas)} / {Dinero.Formatear(dia.Utilidad)}", 9);
            }
        }

        pdf.Espacio(8);
        pdf.Texto("Utilidad bruta = ventas - costo - comision. Utilidad neta = bruta - mermas - gastos.", 8);
        pdf.Texto("Las ventas anuladas no se incluyen.", 8);
        Pie(pdf);
        return pdf.Generar();
    }

    public static byte[] EstadoCuentaCliente(EstadoCuentaCliente estado, string nombreTienda = null)
    {
        if (estado == null)
            throw new ArgumentNullException(nameof(estado));

        var cliente = estado.Cliente ?? new ClienteFiado();
        var pdf = new PdfDocumento();
        Encabezado(pdf, nombreTienda, "Estado de cuenta de cliente",
            $"Cliente desde {cliente.FechaRegistro:dd/MM/yyyy}");

        pdf.Texto(cliente.Nombre, 13, true);
        if (!string.IsNullOrWhiteSpace(cliente.Telefono))
            pdf.Texto($"Telefono: {cliente.Telefono}", 9);

        pdf.LineaHorizontal();
        pdf.Fila("Saldo actual", Dinero.Formatear(estado.SaldoActual), 11, true);
        pdf.Fila("Total fiado", Dinero.Formatear(estado.TotalFiado));
        pdf.Fila("Total abonado", Dinero.Formatear(estado.TotalAbonado));

        pdf.Espacio(8);
        pdf.Texto("Movimientos", 11, true);
        pdf.LineaHorizontal();

        var movimientos = new List<(DateTime Fecha, string Detalle, string Monto, bool Abono)>();
        foreach (var venta in estado.Ventas)
        {
            movimientos.Add((venta.Fecha, $"Venta #{venta.Id} a fiado · Atendio {venta.Usuario}",
                $"+{Dinero.Formatear(venta.PagoFiado)}", false));
        }

        foreach (var abono in estado.Abonos)
        {
            var metodo = string.IsNullOrWhiteSpace(abono.MetodoPago) ? "Abono" : abono.MetodoPago;
            var detalle = $"Abono · {metodo}";
            if (!string.IsNullOrWhiteSpace(abono.Usuario))
                detalle += $" · Registro {abono.Usuario}";
            movimientos.Add((abono.Fecha, detalle, $"-{Dinero.Formatear(abono.Monto)}", true));
        }

        if (movimientos.Count == 0)
        {
            pdf.Texto("Sin ventas a fiado ni abonos registrados.", 9);
        }
        else
        {
            foreach (var movimiento in movimientos.OrderByDescending(m => m.Fecha))
                pdf.Fila($"{movimiento.Fecha:dd/MM/yyyy HH:mm}  {movimiento.Detalle}", movimiento.Monto, 9);
        }

        Pie(pdf);
        return pdf.Generar();
    }

    public static byte[] EstadoCuentaProveedor(EstadoCuentaProveedor estado, string nombreTienda = null)
    {
        if (estado == null)
            throw new ArgumentNullException(nameof(estado));

        var proveedor = estado.Proveedor ?? new Proveedor();
        var pdf = new PdfDocumento();
        Encabezado(pdf, nombreTienda, "Estado de cuenta de proveedor",
            $"Proveedor desde {proveedor.FechaRegistro:dd/MM/yyyy}");

        pdf.Texto(proveedor.Nombre, 13, true);
        if (!string.IsNullOrWhiteSpace(proveedor.Clave))
            pdf.Texto($"Clave/RFC: {proveedor.Clave}", 9);
        if (!string.IsNullOrWhiteSpace(proveedor.Telefono))
            pdf.Texto($"Telefono: {proveedor.Telefono}", 9);
        if (!string.IsNullOrWhiteSpace(proveedor.Direccion))
            pdf.Texto($"Direccion: {proveedor.Direccion}", 9);

        pdf.LineaHorizontal();
        pdf.Fila("Saldo pendiente", Dinero.Formatear(estado.SaldoPendiente), 11, true);
        pdf.Fila("Total comprado", Dinero.Formatear(estado.TotalComprado));
        pdf.Fila("Total pagado", Dinero.Formatear(estado.TotalPagado));

        pdf.Espacio(8);
        pdf.Texto("Compras", 11, true);
        pdf.LineaHorizontal();

        if (estado.Compras.Count == 0)
        {
            pdf.Texto("Sin compras registradas.", 9);
        }
        else
        {
            foreach (var compra in estado.Compras.OrderByDescending(c => c.Fecha))
            {
                var detalle = compra.Lineas.Count == 1
                    ? $"{compra.Lineas[0].Nombre} x{compra.Lineas[0].Cantidad}"
                    : $"{compra.Lineas.Count} productos";
                pdf.Fila($"{compra.Fecha:dd/MM/yyyy HH:mm}  Compra #{compra.Id} · {detalle}",
                    $"+{Dinero.Formatear(compra.Total)}", 9);
            }
        }

        pdf.Espacio(8);
        pdf.Texto("Pagos", 11, true);
        pdf.LineaHorizontal();

        if (estado.Pagos.Count == 0)
        {
            pdf.Texto("Sin pagos registrados.", 9);
        }
        else
        {
            foreach (var pago in estado.Pagos.OrderByDescending(p => p.Fecha))
            {
                var metodo = string.IsNullOrWhiteSpace(pago.MetodoPago) ? "Sin metodo" : pago.MetodoPago;
                var detalle = $"{pago.Fecha:dd/MM/yyyy HH:mm}  {metodo} · {pago.Usuario}" +
                              $" · Saldo despues {Dinero.Formatear(pago.SaldoNuevo)}";
                pdf.Fila(detalle, $"-{Dinero.Formatear(pago.Monto)}", 9);
            }
        }

        pdf.Espacio(6);
        pdf.Texto("Las compras se registran desde el esquema v13 y los pagos desde el v8.", 8);
        Pie(pdf);
        return pdf.Generar();
    }

    private static void Encabezado(PdfDocumento pdf, string nombreTienda, string titulo,
        string subtitulo)
    {
        pdf.Texto(string.IsNullOrWhiteSpace(nombreTienda) ? "SistemaTienda" : nombreTienda.Trim(),
            16, true);
        pdf.Texto(titulo, 13, true);
        pdf.Texto(subtitulo, 9);
        pdf.LineaHorizontal();
    }

    private static void Pie(PdfDocumento pdf)
    {
        pdf.Espacio(10);
        pdf.Texto("Documento interno generado por SistemaTienda. No es un comprobante fiscal.", 8);
        pdf.Texto($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}", 8);
    }
}
