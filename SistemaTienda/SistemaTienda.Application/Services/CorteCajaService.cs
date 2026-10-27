// Servicio de corte de caja: concilia el efectivo del periodo con lo registrado
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Application.Services;

public class CorteCajaService
{
    private readonly DatabaseTools _db;
    private readonly BitacoraService _bitacora;

    public CorteCajaService(DatabaseTools db)
    {
        _db = db;
        _bitacora = new BitacoraService(db);
    }

    private static ProcessResult Autorizar(PermisoSistema permiso) =>
        AutorizacionService.Exigir(SesionActual.Usuario, permiso);

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
    public ProcessResult<CorteCaja> RegistrarCorte(long efectivoContado, long fondoInicial,
        string usuario, string observaciones = "")
    {
        var autorizacion = Autorizar(PermisoSistema.CorteCaja);
        if (autorizacion.IsError)
            return ProcessResult<CorteCaja>.Error(autorizacion.Message);

        if (efectivoContado < 0)
            return ProcessResult<CorteCaja>.Error("El efectivo contado no puede ser negativo");

        if (fondoInicial < 0)
            return ProcessResult<CorteCaja>.Error("El fondo inicial no puede ser negativo");

        if (string.IsNullOrWhiteSpace(usuario))
            return ProcessResult<CorteCaja>.Error("El corte requiere el usuario responsable");

        var resultado = _db.RegistrarCorteCaja(efectivoContado, fondoInicial, usuario.Trim(), observaciones);
        if (resultado.IsSuccess)
        {
            _bitacora.Registrar(AccionBitacora.CorteCaja, "CorteCaja", resultado.Value.Id.ToString(),
                string.Empty,
                $"Esperado={Dinero.Formatear(resultado.Value.EfectivoEsperado)};" +
                $"Contado={Dinero.Formatear(resultado.Value.EfectivoContado)};" +
                $"Diferencia={Dinero.Formatear(resultado.Value.Diferencia)}",
                observaciones);
        }

        return resultado;
    }

    public ProcessResult<MovimientoCaja> RegistrarMovimiento(string tipo, long monto,
        string motivo, string usuario)
    {
        if (tipo != TipoMovimientoCaja.Ingreso && tipo != TipoMovimientoCaja.Retiro)
            return ProcessResult<MovimientoCaja>.Error("El tipo de movimiento debe ser Ingreso o Retiro");

        var autorizacion = Autorizar(tipo == TipoMovimientoCaja.Retiro
            ? PermisoSistema.RetiroCaja
            : PermisoSistema.IngresoCaja);
        if (autorizacion.IsError)
            return ProcessResult<MovimientoCaja>.Error(autorizacion.Message);

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
            Monto = monto,
            Motivo = motivo.Trim(),
            Usuario = usuario.Trim()
        };

        _db.RegistrarMovimientoCaja(movimiento);
        _bitacora.Registrar(AccionBitacora.MovimientoCaja, "MovimientoCaja", movimiento.Id.ToString(),
            string.Empty,
            $"Tipo={movimiento.Tipo};Monto={Dinero.Formatear(movimiento.Monto)}", movimiento.Motivo);
        return ProcessResult<MovimientoCaja>.OK(movimiento,
            $"{tipo} de ${Dinero.Formatear(movimiento.Monto)} registrado");
    }
}
