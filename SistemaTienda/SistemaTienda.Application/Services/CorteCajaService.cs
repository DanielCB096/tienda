// Servicio de corte de caja: concilia el efectivo del periodo con lo registrado
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Application.Services;

public class CorteCajaService
{
    private readonly DatabaseTools _db;

    public CorteCajaService(DatabaseTools db) => _db = db;

    public ProcessResult<CorteCaja> ObtenerResumenPendiente() =>
        ProcessResult<CorteCaja>.OK(_db.ObtenerResumenCajaPendiente());

    public ProcessResult<CorteCaja> ObtenerUltimoCorte() =>
        ProcessResult<CorteCaja>.OK(_db.ObtenerUltimoCorteCaja());

    public ProcessResult<List<CorteCaja>> ObtenerCortes(int limite = 20) =>
        ProcessResult<List<CorteCaja>>.OK(_db.ObtenerCortesCaja(limite));

    /// <summary>
    /// Cierra el periodo abierto. Si el contado no coincide con el esperado,
    /// el motivo es obligatorio: el corte no se guarda sin explicación.
    /// </summary>
    public ProcessResult<CorteCaja> RegistrarCorte(decimal efectivoContado, decimal fondoInicial,
        string usuario, string observaciones = "")
    {
        if (efectivoContado < 0)
            return ProcessResult<CorteCaja>.Error("El efectivo contado no puede ser negativo");

        if (fondoInicial < 0)
            return ProcessResult<CorteCaja>.Error("El fondo inicial no puede ser negativo");

        if (string.IsNullOrWhiteSpace(usuario))
            return ProcessResult<CorteCaja>.Error("El corte requiere el usuario responsable");

        return _db.RegistrarCorteCaja(efectivoContado, fondoInicial, usuario.Trim(), observaciones);
    }

    public ProcessResult<MovimientoCaja> RegistrarMovimiento(string tipo, decimal monto,
        string motivo, string usuario)
    {
        if (tipo != TipoMovimientoCaja.Ingreso && tipo != TipoMovimientoCaja.Retiro)
            return ProcessResult<MovimientoCaja>.Error("El tipo de movimiento debe ser Ingreso o Retiro");

        if (monto <= 0)
            return ProcessResult<MovimientoCaja>.Error("El monto debe ser mayor a cero");

        if (string.IsNullOrWhiteSpace(motivo))
            return ProcessResult<MovimientoCaja>.Error("El motivo es obligatorio");

        if (string.IsNullOrWhiteSpace(usuario))
            return ProcessResult<MovimientoCaja>.Error("El movimiento requiere un usuario");

        var movimiento = new MovimientoCaja
        {
            Fecha = DateTime.Now,
            Tipo = tipo,
            Monto = Math.Round(monto, 2),
            Motivo = motivo.Trim(),
            Usuario = usuario.Trim()
        };

        _db.RegistrarMovimientoCaja(movimiento);
        return ProcessResult<MovimientoCaja>.OK(movimiento,
            $"{tipo} de ${movimiento.Monto:F2} registrado");
    }
}
