// Servicio de ventas: valida, congela precio/costo y guarda todo de forma atómica
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Application.Services;

public sealed class LineaVentaSolicitada
{
    public int ProductoId { get; set; }
    public int Cantidad { get; set; }

    /// <summary>
    /// Descuento de este renglón: porcentaje o monto en centavos, nunca ambos.
    /// El total de la venta se calcula con el importe neto de cada línea.
    /// </summary>
    public decimal DescuentoPorcentaje { get; set; }

    public long DescuentoMonto { get; set; }
}

/// <summary>
/// Montos aplicados a la venta (no el efectivo recibido). El cambio se calcula en la interfaz.
/// El fiado es un método exclusivo: no se combina con efectivo ni tarjeta.
/// </summary>
public sealed class PagoSolicitado
{
    /// <summary>Importes en centavos.</summary>
    public long Efectivo { get; set; }
    public long Tarjeta { get; set; }
    public long Fiado { get; set; }
    public int? ClienteId { get; set; }

    /// <summary>
    /// Efectivo entregado por el cliente cuando paga todo en efectivo. No forma
    /// parte de la suma de pagos: solo sirve para guardar el cambio.
    /// </summary>
    public long? EfectivoRecibido { get; set; }
}

public class VentaService
{
    private readonly DatabaseTools _db;
    private readonly BitacoraService _bitacora;

    public VentaService(DatabaseTools db)
    {
        _db = db;
        _bitacora = new BitacoraService(db);
    }

    private static ProcessResult Autorizar(PermisoSistema permiso) =>
        AutorizacionService.Exigir(SesionActual.Usuario, permiso);

    public ProcessResult<Venta> RegistrarVenta(
        IReadOnlyList<LineaVentaSolicitada> lineas, PagoSolicitado pago, string usuario,
        int? ticketEsperaId = null)
    {
        var autorizacion = Autorizar(PermisoSistema.Vender);
        if (autorizacion.IsError)
            return ProcessResult<Venta>.Error(autorizacion.Message);

        if (lineas == null || lineas.Count == 0)
            return ProcessResult<Venta>.Error("La venta debe tener al menos un producto");

        if (string.IsNullOrWhiteSpace(usuario))
            return ProcessResult<Venta>.Error("La venta requiere un usuario");

        if (pago == null)
            return ProcessResult<Venta>.Error("Indique la forma de pago");

        if (pago.Efectivo < 0 || pago.Tarjeta < 0 || pago.Fiado < 0)
            return ProcessResult<Venta>.Error("Los montos de pago no pueden ser negativos");

        var config = _db.ObtenerConfiguracion();
        if (config == null)
            return ProcessResult<Venta>.Error("No hay configuración de la tienda");

        var productos = new HashSet<int>();
        var lineasVenta = new List<VentaLinea>();
        foreach (var solicitada in lineas)
        {
            if (solicitada == null || solicitada.Cantidad <= 0)
                return ProcessResult<Venta>.Error("Las cantidades deben ser mayores a cero");

            if (!productos.Add(solicitada.ProductoId))
                return ProcessResult<Venta>.Error("Hay un producto repetido en la venta");

            var producto = _db.ObtenerProductoPorId(solicitada.ProductoId);
            if (producto == null)
                return ProcessResult<Venta>.Error($"Producto {solicitada.ProductoId} no encontrado");

            if (!producto.Activo)
                return ProcessResult<Venta>.Error(
                    $"El producto {producto.Nombre} está desactivado y no se puede vender");

            var importe = Dinero.Multiplicar(producto.Precio, solicitada.Cantidad);
            var tasaImpuesto = producto.Impuesto > 0 ? producto.Impuesto : 0m;
            lineasVenta.Add(new VentaLinea
            {
                ProductoId = producto.Id,
                CodigoBarras = producto.CodigoBarras,
                Nombre = producto.Nombre,
                Cantidad = solicitada.Cantidad,
                PrecioUnitario = producto.Precio,
                CostoUnitario = producto.Costo,
                Importe = importe,
                ImpuestoPorcentaje = tasaImpuesto,
                ImpuestoImporte = Dinero.ImpuestoIncluido(importe, tasaImpuesto)
            });
        }

        var hayDescuento = lineas.Any(l =>
            l != null && (l.DescuentoPorcentaje > 0 || l.DescuentoMonto > 0));

        if (hayDescuento)
        {
            var autorizacionDescuento = Autorizar(PermisoSistema.AplicarDescuento);
            if (autorizacionDescuento.IsError)
                return ProcessResult<Venta>.Error(autorizacionDescuento.Message);
        }

        // El descuento vive en cada renglón: puede ser un porcentaje o un monto
        // distinto por producto. El importe de la línea queda neto desde aquí,
        // así que el IVA y los reportes por producto también quedan netos.
        for (var i = 0; i < lineasVenta.Count; i++)
        {
            var solicitada = lineas[i];
            var linea = lineasVenta[i];

            if (solicitada.DescuentoPorcentaje < 0 || solicitada.DescuentoMonto < 0)
                return ProcessResult<Venta>.Error("El descuento no puede ser negativo");

            if (solicitada.DescuentoPorcentaje > 0 && solicitada.DescuentoMonto > 0)
                return ProcessResult<Venta>.Error(
                    $"En \"{linea.Nombre}\" indique el descuento como porcentaje o como monto, no ambos");

            if (solicitada.DescuentoPorcentaje > 0)
            {
                if (!DescuentoVenta.EsPorcentajeValido(solicitada.DescuentoPorcentaje))
                    return ProcessResult<Venta>.Error(
                        $"El descuento de \"{linea.Nombre}\" debe ser mayor a 0% y menor a 100%");

                var montoDescuento = DescuentoVenta.DesdePorcentaje(
                    linea.Importe, solicitada.DescuentoPorcentaje);
                if (montoDescuento <= 0)
                    return ProcessResult<Venta>.Error(
                        $"El descuento de \"{linea.Nombre}\" es cero; ajusta el porcentaje o usa un monto");

                linea.Descuento = montoDescuento;
                linea.DescuentoPorcentaje = solicitada.DescuentoPorcentaje;
            }
            else if (solicitada.DescuentoMonto > 0)
            {
                if (!DescuentoVenta.EsMontoValido(solicitada.DescuentoMonto, linea.Importe))
                    return ProcessResult<Venta>.Error(
                        $"El descuento de \"{linea.Nombre}\" debe ser mayor a cero y menor a su importe");

                linea.Descuento = solicitada.DescuentoMonto;
            }

            if (linea.Descuento > 0)
            {
                linea.Importe -= linea.Descuento;
                linea.ImpuestoImporte = Dinero.ImpuestoIncluido(
                    linea.Importe, linea.ImpuestoPorcentaje);
            }
        }

        var subtotal = lineasVenta.Sum(l => l.Subtotal);
        var descuento = lineasVenta.Sum(l => l.Descuento);
        var total = subtotal - descuento;
        if (total <= 0)
            return ProcessResult<Venta>.Error("El total de la venta debe ser mayor a cero");

        var porcentajes = lineasVenta
            .Where(l => l.Descuento > 0 && l.DescuentoPorcentaje > 0)
            .Select(l => l.DescuentoPorcentaje)
            .Distinct()
            .ToList();
        var descuentoPorcentaje = descuento > 0 && porcentajes.Count == 1
            ? porcentajes[0]
            : 0m;

        var pagado = pago.Efectivo + pago.Tarjeta + pago.Fiado;
        if (pagado != total)
            return ProcessResult<Venta>.Error(
                $"Los pagos (${Dinero.Formatear(pagado)}) no coinciden con el total de la venta (${Dinero.Formatear(total)})");

        if (pago.Fiado > 0)
        {
            if (!config.FiadoActivado)
                return ProcessResult<Venta>.Error("El fiado está desactivado en la configuración");

            if (pago.Fiado != total)
                return ProcessResult<Venta>.Error("El fiado no se puede combinar con otros métodos de pago");

            if (pago.ClienteId == null)
                return ProcessResult<Venta>.Error("Seleccione el cliente para la venta a fiado");

            var cliente = _db.ObtenerClienteFiadoPorId(pago.ClienteId.Value);
            if (cliente == null)
                return ProcessResult<Venta>.Error("Cliente no encontrado");

            if (!cliente.Activo)
                return ProcessResult<Venta>.Error(
                    "El cliente está inactivo y no puede recibir nuevas ventas a fiado");

            var nuevoSaldo = cliente.SaldoActual + pago.Fiado;
            if (config.LimiCreditoCliente > 0 && nuevoSaldo > config.LimiCreditoCliente)
                return ProcessResult<Venta>.Error(
                    $"La venta excede el límite de crédito (${Dinero.Formatear(config.LimiCreditoCliente)}). " +
                    $"Saldo actual: ${Dinero.Formatear(cliente.SaldoActual)}, con la venta: ${Dinero.Formatear(nuevoSaldo)}");
        }
        else if (pago.ClienteId != null)
        {
            return ProcessResult<Venta>.Error("El cliente solo se asocia en ventas a fiado");
        }

        string metodoPago;
        if (pago.Fiado > 0)
            metodoPago = MetodoPagoVenta.Fiado;
        else if (pago.Tarjeta == total)
            metodoPago = MetodoPagoVenta.Tarjeta;
        else if (pago.Efectivo == total)
            metodoPago = MetodoPagoVenta.Efectivo;
        else if (pago.Efectivo > 0 && pago.Tarjeta > 0)
            metodoPago = MetodoPagoVenta.Mixto;
        else
            return ProcessResult<Venta>.Error("Forma de pago no válida");

        var venta = new Venta
        {
            Fecha = DateTime.Now,
            Usuario = usuario.Trim(),
            ClienteId = pago.ClienteId,
            MetodoPago = metodoPago,
            Total = total,
            Descuento = descuento,
            DescuentoPorcentaje = descuentoPorcentaje,
            CostoTotal = lineasVenta.Sum(l => Dinero.Multiplicar(l.CostoUnitario, l.Cantidad)),
            PagoEfectivo = pago.Efectivo,
            PagoTarjeta = pago.Tarjeta,
            PagoFiado = pago.Fiado,
            ComisionTarjeta = Dinero.Porcentaje(pago.Tarjeta, config.ComisionTarjeta)
        };

        if (metodoPago == MetodoPagoVenta.Efectivo && pago.EfectivoRecibido.HasValue)
        {
            if (pago.EfectivoRecibido.Value < total)
                return ProcessResult<Venta>.Error(
                    "El efectivo recibido no alcanza para cubrir el total de la venta");

            venta.EfectivoRecibido = pago.EfectivoRecibido.Value;
        }

        var auditoria = new RegistroBitacora
        {
            Fecha = DateTime.Now,
            Usuario = usuario,
            Accion = AccionBitacora.Venta,
            Entidad = "Venta",
            DatosDespues =
                $"Total={Dinero.Formatear(venta.Total)};Costo={Dinero.Formatear(venta.CostoTotal)};" +
                $"Metodo={venta.MetodoPago};Comision={Dinero.Formatear(venta.ComisionTarjeta)}" +
                (venta.Descuento > 0
                    ? $";Descuento={Dinero.Formatear(venta.Descuento)}" +
                      (venta.DescuentoPorcentaje > 0
                          ? $";DescuentoPorcentaje={venta.DescuentoPorcentaje}"
                          : string.Empty)
                    : string.Empty),
            Motivo = venta.ClienteId.HasValue ? $"Cliente={venta.ClienteId.Value}" : string.Empty
        };

        if (ticketEsperaId.HasValue)
            auditoria.DatosDespues += $";TicketEspera={ticketEsperaId.Value}";

        var guardado = _db.RegistrarVenta(venta, lineasVenta, auditoria, ticketEsperaId);
        if (guardado.IsError)
            return ProcessResult<Venta>.Error(guardado.Message);

        return ProcessResult<Venta>.OK(venta, guardado.Message);
    }

    public List<Venta> ObtenerVentasDelDia(DateTime fecha) => _db.ObtenerVentasDelDia(fecha);

    public List<Venta> ObtenerVentas(DateTime desde, DateTime hasta) => _db.ObtenerVentas(desde, hasta);

    public List<VentaLinea> ObtenerLineas(int ventaId) => _db.ObtenerLineasVenta(ventaId);

    public Venta ObtenerVentaPorId(int ventaId) => _db.ObtenerVentaPorId(ventaId);

    public AnulacionVenta ObtenerAnulacion(int ventaId) => _db.ObtenerAnulacionVenta(ventaId);

    /// <summary>
    /// Anula una venta completa con motivo obligatorio. El historial no se edita:
    /// se registra la anulación, se devuelve el stock y se revierte el fiado.
    /// </summary>
    public ProcessResult AnularVenta(int ventaId, string motivo, string usuario)
    {
        var autorizacion = Autorizar(PermisoSistema.AnularVenta);
        if (autorizacion.IsError)
            return autorizacion;

        if (ventaId <= 0)
            return ProcessResult.Error("Indique la venta a anular");

        if (string.IsNullOrWhiteSpace(motivo))
            return ProcessResult.Error("Indique el motivo de la anulación");

        if (string.IsNullOrWhiteSpace(usuario))
            return ProcessResult.Error("La anulación requiere un usuario");

        var venta = _db.ObtenerVentaPorId(ventaId);
        if (venta == null)
            return ProcessResult.Error($"La venta #{ventaId} no existe");

        if (venta.Anulada)
            return ProcessResult.Error($"La venta #{ventaId} ya está anulada");

        var auditoria = new RegistroBitacora
        {
            Fecha = DateTime.Now,
            Usuario = usuario.Trim(),
            Accion = AccionBitacora.AnulacionVenta,
            Entidad = "Venta",
            DatosAntes = $"Total={Dinero.Formatear(venta.Total)};Metodo={venta.MetodoPago};" +
                         $"PagoFiado={Dinero.Formatear(venta.PagoFiado)}" +
                         (venta.ClienteId.HasValue ? $";Cliente={venta.ClienteId.Value}" : string.Empty),
            DatosDespues = "Anulada",
            Motivo = motivo.Trim()
        };

        return _db.AnularVenta(ventaId, usuario.Trim(), motivo.Trim(), auditoria);
    }
}
