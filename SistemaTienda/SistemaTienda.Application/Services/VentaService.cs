// Servicio de ventas: valida, congela precio/costo y guarda todo de forma atómica
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Application.Services;

public sealed class LineaVentaSolicitada
{
    public int ProductoId { get; set; }
    public int Cantidad { get; set; }
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
        IReadOnlyList<LineaVentaSolicitada> lineas, PagoSolicitado pago, string usuario)
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

            lineasVenta.Add(new VentaLinea
            {
                ProductoId = producto.Id,
                CodigoBarras = producto.CodigoBarras,
                Nombre = producto.Nombre,
                Cantidad = solicitada.Cantidad,
                PrecioUnitario = producto.Precio,
                CostoUnitario = producto.Costo,
                Importe = Dinero.Multiplicar(producto.Precio, solicitada.Cantidad)
            });
        }

        var total = lineasVenta.Sum(l => l.Importe);
        if (total <= 0)
            return ProcessResult<Venta>.Error("El total de la venta debe ser mayor a cero");

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
            CostoTotal = lineasVenta.Sum(l => Dinero.Multiplicar(l.CostoUnitario, l.Cantidad)),
            PagoEfectivo = pago.Efectivo,
            PagoTarjeta = pago.Tarjeta,
            PagoFiado = pago.Fiado,
            ComisionTarjeta = Dinero.Porcentaje(pago.Tarjeta, config.ComisionTarjeta)
        };

        var guardado = _db.RegistrarVenta(venta, lineasVenta);
        if (guardado.IsError)
            return ProcessResult<Venta>.Error(guardado.Message);

        _bitacora.Registrar(AccionBitacora.Venta, "Venta", venta.Id.ToString(),
            string.Empty,
            $"Total={Dinero.Formatear(venta.Total)};Costo={Dinero.Formatear(venta.CostoTotal)};" +
            $"Metodo={venta.MetodoPago};Comision={Dinero.Formatear(venta.ComisionTarjeta)}",
            venta.ClienteId.HasValue ? $"Cliente={venta.ClienteId.Value}" : string.Empty);
        return ProcessResult<Venta>.OK(venta, guardado.Message);
    }

    public List<Venta> ObtenerVentasDelDia(DateTime fecha) => _db.ObtenerVentasDelDia(fecha);

    public List<Venta> ObtenerVentas(DateTime desde, DateTime hasta) => _db.ObtenerVentas(desde, hasta);

    public List<VentaLinea> ObtenerLineas(int ventaId) => _db.ObtenerLineasVenta(ventaId);
}
