using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Application.Services;

/// <summary>
/// Registra y consulta la bitácora. Solo permite agregar registros: la
/// inmutabilidad también se refuerza con disparadores en SQLite.
/// </summary>
public class BitacoraService
{
    private readonly DatabaseTools _db;

    public BitacoraService(DatabaseTools db) => _db = db;

    public ProcessResult<RegistroBitacora> Registrar(string accion, string entidad, string entidadId,
        string datosAntes = "", string datosDespues = "", string motivo = "",
        UsuarioSistema usuario = null)
    {
        if (string.IsNullOrWhiteSpace(accion))
            return ProcessResult<RegistroBitacora>.Error("La acción de la bitácora es obligatoria");

        usuario ??= SesionActual.Usuario;
        if (usuario == null || string.IsNullOrWhiteSpace(usuario.Nombre))
            return ProcessResult<RegistroBitacora>.Error("La bitácora requiere un usuario");

        var registro = new RegistroBitacora
        {
            Fecha = DateTime.Now,
            Usuario = usuario.Nombre.Trim(),
            Accion = accion.Trim(),
            Entidad = entidad?.Trim() ?? string.Empty,
            EntidadId = entidadId ?? string.Empty,
            DatosAntes = datosAntes ?? string.Empty,
            DatosDespues = datosDespues ?? string.Empty,
            Motivo = motivo?.Trim() ?? string.Empty
        };

        try
        {
            _db.AgregarBitacora(registro);
            return ProcessResult<RegistroBitacora>.OK(registro, "Bitácora registrada");
        }
        catch (Exception ex)
        {
            return ProcessResult<RegistroBitacora>.Error($"No se pudo registrar la bitácora: {ex.Message}");
        }
    }

    public ProcessResult<List<RegistroBitacora>> Consultar(int limite = 100,
        string usuario = null, string accion = null) =>
        ProcessResult<List<RegistroBitacora>>.OK(
            _db.ObtenerBitacora(limite, usuario, accion));
}
