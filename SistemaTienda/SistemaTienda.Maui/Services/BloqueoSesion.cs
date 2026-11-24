using SistemaTienda.Application.Services;

namespace SistemaTienda.Maui.Services;

/// <summary>
/// Bloquea la pantalla tras un periodo de inactividad sin cerrar la app, para
/// que la caja no quede abierta con el usuario anterior. Cualquier toque, cambio
/// de pestaña o PIN válido reinicia el conteo.
/// </summary>
public sealed class BloqueoSesion
{
    private const int MinimoMinutos = 2;
    private const int MaximoMinutos = 15;
    private const int MinutosPredeterminados = 5;

    private readonly ConfiguracionService _config;
    private readonly NavegacionApp _navegacion;

    private IDispatcherTimer? _timer;
    private DateTime _ultimaActividad = DateTime.Now;
    private bool _bloqueado;

    public BloqueoSesion(ConfiguracionService config, NavegacionApp navegacion)
    {
        _config = config;
        _navegacion = navegacion;
    }

    public bool EstaBloqueado => _bloqueado;

    public void Iniciar()
    {
        Reiniciar();

        if (_timer != null)
        {
            _timer.Start();
            return;
        }

        var dispatcher = Microsoft.Maui.Controls.Application.Current?.Dispatcher;
        if (dispatcher == null)
            return;

        _timer = dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(10);
        _timer.Tick += (_, _) => Verificar();
        _timer.Start();
    }

    public void Detener()
    {
        _timer?.Stop();
        _timer = null;
        _bloqueado = false;
    }

    public void Reiniciar()
    {
        _ultimaActividad = DateTime.Now;
    }

    public void Verificar()
    {
        if (_bloqueado)
            return;

        var configuracion = _config.ObtenerConfiguracion().Value;
        var minutos = configuracion?.MinutosInactividad ?? MinutosPredeterminados;
        minutos = Math.Clamp(minutos, MinimoMinutos, MaximoMinutos);

        if ((DateTime.Now - _ultimaActividad).TotalMinutes < minutos)
            return;

        _bloqueado = true;
        Reiniciar();
        _navegacion.MostrarBloqueo();
    }

    public void Desbloquear()
    {
        _bloqueado = false;
        Reiniciar();
    }
}
