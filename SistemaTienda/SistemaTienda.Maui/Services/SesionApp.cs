using SistemaTienda.Application.Services;
using SistemaTienda.Core;

namespace SistemaTienda.Maui.Services;

/// <summary>
/// Mantiene la sesión de la interfaz. Los manejadores de eventos de MAUI no
/// conservan el contexto de <see cref="SesionActual"/> entre uno y otro, así que
/// se vuelve a aplicar antes de cada operación que valida permisos o audita.
/// </summary>
public sealed class SesionApp
{
    public UsuarioSistema? Usuario { get; private set; }

    public void Iniciar(UsuarioSistema usuario)
    {
        Usuario = usuario;
        SesionActual.Iniciar(usuario);
    }

    public void Aplicar()
    {
        if (Usuario != null)
            SesionActual.Iniciar(Usuario);
    }

    public void Cerrar()
    {
        Usuario = null;
        SesionActual.Cerrar();
    }
}
