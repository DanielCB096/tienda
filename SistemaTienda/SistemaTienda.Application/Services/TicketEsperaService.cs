using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Application.Services;

public sealed record LineaTicketEsperaSolicitada(int ProductoId, int Cantidad);

/// <summary>
/// Carritos guardados en el mostrador. No reservan stock ni mueven caja: el
/// stock y el precio se validan al cobrar, y la venta cierra el ticket en la
/// misma transacción para que no se pueda cobrar dos veces.
/// </summary>
public sealed class TicketEsperaService
{
    public const int MaximoTickets = 10;

    private readonly DatabaseTools _db;

    public TicketEsperaService(DatabaseTools db)
    {
        _db = db;
    }

    private static ProcessResult Autorizar()
    {
        var autorizacion = AutorizacionService.Exigir(SesionActual.Usuario, PermisoSistema.Vender);
        return autorizacion.IsError
            ? ProcessResult.Error(autorizacion.Message)
            : ProcessResult.Success(string.Empty);
    }

    public ProcessResult<TicketEspera> Dejar(IReadOnlyList<LineaTicketEsperaSolicitada> lineas,
        string nombre = null, string nota = null)
    {
        var autorizacion = Autorizar();
        if (autorizacion.IsError)
            return ProcessResult<TicketEspera>.Error(autorizacion.Message);

        if (lineas == null || lineas.Count == 0)
            return ProcessResult<TicketEspera>.Error("El carrito está vacío.");

        if (lineas.Any(linea => linea.Cantidad <= 0))
            return ProcessResult<TicketEspera>.Error("Las cantidades deben ser mayores a cero.");

        if (lineas.Select(linea => linea.ProductoId).Distinct().Count() != lineas.Count)
            return ProcessResult<TicketEspera>.Error(
                "El carrito tiene productos repetidos; une las cantidades antes de guardarlo.");

        if (_db.ContarTicketsEsperaActivos() >= MaximoTickets)
            return ProcessResult<TicketEspera>.Error(
                $"Ya hay {MaximoTickets} tickets en espera. Cobra o descarta uno antes de guardar otro.");

        var productos = _db.ObtenerProductos().ToDictionary(producto => producto.Id);
        var ticket = new TicketEspera
        {
            Nombre = Recortar(nombre, 40),
            Nota = Recortar(nota, 120),
            Usuario = SesionActual.Usuario?.Nombre ?? string.Empty
        };

        foreach (var linea in lineas)
        {
            if (!productos.TryGetValue(linea.ProductoId, out var producto))
                return ProcessResult<TicketEspera>.Error(
                    $"El producto {linea.ProductoId} ya no existe.");

            if (!producto.Activo)
                return ProcessResult<TicketEspera>.Error(
                    $"\"{producto.Nombre}\" está desactivado y no se puede guardar en un ticket.");

            ticket.Lineas.Add(new TicketEsperaLinea
            {
                ProductoId = producto.Id,
                Cantidad = linea.Cantidad,
                PrecioUnitario = producto.Precio
            });
        }

        var auditoria = new RegistroBitacora
        {
            Fecha = DateTime.Now,
            Usuario = SesionActual.Usuario?.Nombre ?? string.Empty,
            Accion = AccionBitacora.DejarTicketEspera,
            Entidad = "TicketEspera",
            Motivo = ticket.Nombre
        };

        var creado = _db.CrearTicketEspera(ticket, auditoria);
        if (creado.IsError)
            return ProcessResult<TicketEspera>.Error(creado.Message);

        ticket.Id = creado.Value;
        Enriquecer(new[] { ticket });
        return ProcessResult<TicketEspera>.OK(ticket,
            $"Ticket #{ticket.Id} guardado en espera");
    }

    public ProcessResult<List<TicketEspera>> Listar()
    {
        var autorizacion = Autorizar();
        if (autorizacion.IsError)
            return ProcessResult<List<TicketEspera>>.Error(autorizacion.Message);

        _db.LiberarTicketsEsperaVencidos();
        var tickets = _db.ObtenerTicketsEspera();
        Enriquecer(tickets);
        return ProcessResult<List<TicketEspera>>.OK(tickets);
    }

    public ProcessResult<TicketEspera> Retomar(int ticketId)
    {
        var autorizacion = Autorizar();
        if (autorizacion.IsError)
            return ProcessResult<TicketEspera>.Error(autorizacion.Message);

        _db.LiberarTicketsEsperaVencidos();
        var ticket = _db.ObtenerTicketEspera(ticketId);
        if (ticket == null)
            return ProcessResult<TicketEspera>.Error($"El ticket #{ticketId} no existe.");

        if (ticket.Estado != EstadoTicketEspera.Disponible &&
            !(ticket.Estado == EstadoTicketEspera.Retomado &&
              string.Equals(ticket.RetomadoPor, SesionActual.Usuario?.Nombre,
                  StringComparison.OrdinalIgnoreCase)))
        {
            return ProcessResult<TicketEspera>.Error(
                "El ticket ya fue cobrado, descartado o lo está usando otro cajero.");
        }

        var auditoria = new RegistroBitacora
        {
            Fecha = DateTime.Now,
            Usuario = SesionActual.Usuario?.Nombre ?? string.Empty,
            Accion = AccionBitacora.RetomarTicketEspera,
            Entidad = "TicketEspera",
            EntidadId = ticketId.ToString()
        };

        var resultado = _db.RetomarTicketEspera(ticketId,
            SesionActual.Usuario?.Nombre ?? string.Empty, auditoria);
        if (resultado.IsError)
            return ProcessResult<TicketEspera>.Error(resultado.Message);

        ticket = _db.ObtenerTicketEspera(ticketId);
        Enriquecer(new[] { ticket });
        return ProcessResult<TicketEspera>.OK(ticket,
            $"Ticket #{ticketId} retomado; revisa precios y stock antes de cobrar");
    }

    /// <summary>Devuelve a Disponible un ticket retomado por el mismo cajero.</summary>
    public ProcessResult Liberar(int ticketId)
    {
        var autorizacion = Autorizar();
        if (autorizacion.IsError)
            return autorizacion;

        _db.LiberarTicketEspera(ticketId, SesionActual.Usuario?.Nombre ?? string.Empty);
        return ProcessResult.Success(string.Empty);
    }

    public ProcessResult Descartar(int ticketId)
    {
        var autorizacion = Autorizar();
        if (autorizacion.IsError)
            return autorizacion;

        var ticket = _db.ObtenerTicketEspera(ticketId);
        if (ticket == null)
            return ProcessResult.Error($"El ticket #{ticketId} no existe.");

        if (ticket.Estado == EstadoTicketEspera.Cobrado ||
            ticket.Estado == EstadoTicketEspera.Descartado)
            return ProcessResult.Error("El ticket ya fue cobrado o descartado.");

        var usuario = SesionActual.Usuario?.Nombre ?? string.Empty;
        if (ticket.Estado == EstadoTicketEspera.Retomado &&
            !string.Equals(ticket.RetomadoPor, usuario, StringComparison.OrdinalIgnoreCase) &&
            SesionActual.Usuario?.Rol != RolUsuario.Dueño)
        {
            return ProcessResult.Error(
                "Otro cajero está usando ese ticket. Pídele que lo descarte o espera a que se libere.");
        }

        var auditoria = new RegistroBitacora
        {
            Fecha = DateTime.Now,
            Usuario = usuario,
            Accion = AccionBitacora.DescartarTicketEspera,
            Entidad = "TicketEspera",
            EntidadId = ticketId.ToString(),
            DatosAntes = $"Lineas={ticket.Lineas.Count};Piezas={ticket.Piezas};" +
                         $"Total={Dinero.Formatear(ticket.TotalEstimado)}",
            DatosDespues = $"Estado={EstadoTicketEspera.Descartado}"
        };

        return _db.DescartarTicketEspera(ticketId, usuario, auditoria);
    }

    private void Enriquecer(IEnumerable<TicketEspera> tickets)
    {
        var productos = _db.ObtenerProductos().ToDictionary(producto => producto.Id);
        foreach (var ticket in tickets)
        {
            foreach (var linea in ticket.Lineas)
            {
                if (productos.TryGetValue(linea.ProductoId, out var producto))
                {
                    linea.ProductoNombre = producto.Nombre;
                    linea.PrecioActual = producto.Precio;
                    linea.StockActual = producto.Stock;
                    linea.ProductoActivo = producto.Activo;
                }
                else
                {
                    linea.ProductoNombre = $"(producto {linea.ProductoId})";
                    linea.PrecioActual = linea.PrecioUnitario;
                    linea.StockActual = 0;
                    linea.ProductoActivo = false;
                }
            }
        }
    }

    private static string Recortar(string texto, int maximo)
    {
        var limpio = (texto ?? string.Empty).Trim();
        return limpio.Length <= maximo ? limpio : limpio[..maximo];
    }
}
