namespace SistemaTienda.Maui.Services;

/// <summary>
/// Engancha los errores no controlados del proceso, de las tareas en segundo
/// plano y de la interfaz de Windows. El objetivo es registrar el fallo y evitar
/// que un cierre inesperado borre la venta en curso.
/// </summary>
public static class ManejadorErrores
{
    private static bool _instalado;

    public static void Instalar()
    {
        if (_instalado)
            return;

        _instalado = true;

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            RegistroErrores.Registrar(
                e.ExceptionObject as Exception ?? new Exception("Error desconocido"),
                "Proceso");

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            RegistroErrores.Registrar(e.Exception, "Tarea en segundo plano");
            e.SetObserved();
        };

#if WINDOWS
        try
        {
            var aplicacion = Microsoft.UI.Xaml.Application.Current;
            if (aplicacion != null)
            {
                aplicacion.UnhandledException += (_, e) =>
                {
                    RegistroErrores.Registrar(e.Exception, "Interfaz");
                    e.Handled = true;
                };
            }
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Instalar manejador");
        }
#endif
    }
}
