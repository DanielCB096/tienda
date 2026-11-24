// Lectura y validación de los argumentos de la herramienta de datos de prueba.
using System.Globalization;

namespace SistemaTienda.Semilla;

public sealed class OpcionesSemilla
{
    public const string EscenarioLimpio = "limpio";
    public const string EscenarioConfigurado = "configurado";
    public const string EscenarioDemo = "demo";

    public static readonly string[] Escenarios =
        { EscenarioLimpio, EscenarioConfigurado, EscenarioDemo };

    public string RutaBaseDatos { get; init; } = RutaPredeterminada();
    public string Escenario { get; init; } = EscenarioDemo;
    public bool Reiniciar { get; init; }
    public bool Forzar { get; init; }
    public bool Silencioso { get; init; }
    public bool MostrarAyuda { get; init; }
    public bool UsarRutaP3 { get; init; }

    public static string CarpetaAppP3 =>
        Path.Combine(CarpetaDatosLocales, "SistemaTienda", "com.sistematienda.p3", "Data");

    public static string RutaPredeterminada() =>
        Path.Combine(CarpetaDatosLocales, "SistemaTienda", "semilla", "tienda.db");

    private static string CarpetaDatosLocales
    {
        get
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return string.IsNullOrWhiteSpace(local) ? Directory.GetCurrentDirectory() : local;
        }
    }

    public static OpcionesSemilla Leer(string[] args)
    {
        var ruta = RutaPredeterminada();
        var escenario = EscenarioDemo;
        var reiniciar = false;
        var forzar = false;
        var silencioso = false;
        var ayuda = false;
        var p3 = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--db":
                    if (i + 1 >= args.Length)
                        throw new ArgumentException("Falta la ruta después de --db");
                    ruta = args[++i];
                    break;
                case "--p3":
                    p3 = true;
                    ruta = Path.Combine(CarpetaAppP3, "tienda.db");
                    break;
                case "--escenario":
                    if (i + 1 >= args.Length)
                        throw new ArgumentException("Falta el nombre después de --escenario");
                    escenario = args[++i].ToLower(CultureInfo.InvariantCulture);
                    break;
                case "--reset":
                    reiniciar = true;
                    break;
                case "--forzar":
                    forzar = true;
                    break;
                case "--quiet":
                    silencioso = true;
                    break;
                case "-h":
                case "--help":
                    ayuda = true;
                    break;
                default:
                    throw new ArgumentException($"Argumento no reconocido: {args[i]}");
            }
        }

        if (!Escenarios.Contains(escenario))
            throw new ArgumentException(
                $"Escenario desconocido: {escenario}. Usa: {string.Join(", ", Escenarios)}");

        if (string.IsNullOrWhiteSpace(ruta))
            throw new ArgumentException("La ruta de la base no puede quedar vacía");

        return new OpcionesSemilla
        {
            RutaBaseDatos = Path.GetFullPath(Environment.ExpandEnvironmentVariables(ruta)),
            Escenario = escenario,
            Reiniciar = reiniciar,
            Forzar = forzar,
            Silencioso = silencioso,
            MostrarAyuda = ayuda,
            UsarRutaP3 = p3
        };
    }

    public static void ImprimirAyuda()
    {
        Console.WriteLine("""
            Generador de datos de prueba para SistemaTienda.

            Uso:
              SistemaTienda.Semilla [--db <ruta> | --p3] [--escenario <nombre>] [--reset] [--forzar]

            Escenarios:
              limpio       Deja la base y los respaldos borrados. Sirve para probar el
                           asistente de primera vez, las migraciones y P3.
              configurado  Tienda, usuarios y catálogo, sin ventas. Sirve para capturar
                           una venta a mano desde cero.
              demo         Todo lo anterior más clientes, proveedores, compras, ventas
                           de los cuatro métodos, abonos, gastos, movimientos de caja,
                           un corte cerrado y un respaldo. Es el predeterminado.

            Opciones:
              --db <ruta>    Base a generar. Por defecto:
                             %LOCALAPPDATA%\SistemaTienda\semilla\tienda.db
              --p3           Atajo a la app aislada de la prueba de usabilidad:
                             %LOCALAPPDATA%\SistemaTienda\com.sistematienda.p3\Data\tienda.db
              --reset        Borra la base, sus -wal/-shm, respaldos y exportaciones.
                             Obligatorio si la base ya existe.
              --forzar       Permite escribir en la carpeta de la app real
                             (com.sistematienda.tienda). Úsalo solo con datos ficticios.
              --quiet        No imprime el resumen.
              -h, --help     Muestra esta ayuda.

            Seguridad:
              Por defecto se niega a tocar la base de la app real y se niega a
              sobrescribir una base existente sin --reset.

            Ejemplos:
              dotnet run --project SistemaTienda/SistemaTienda.Semilla -- --escenario demo --reset
              dotnet run --project SistemaTienda/SistemaTienda.Semilla -- --p3 --escenario limpio --reset
              dotnet run --project SistemaTienda/SistemaTienda.Semilla -- --p3 --escenario demo --reset
            """);
    }
}
