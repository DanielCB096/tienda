// Punto de entrada de la herramienta de datos de prueba.
using System.Text;
using SistemaTienda.Infrastructure;
using SistemaTienda.Semilla;

Console.OutputEncoding = Encoding.UTF8;

// El empaquetador de H5 pide el manifiesto para no duplicar la versión de esquema.
if (args.Any(a => string.Equals(a, "--manifiesto", StringComparison.OrdinalIgnoreCase)))
{
    Console.WriteLine(ManifiestoSistema.Actual().ToJson());
    return 0;
}

try
{
    var opciones = OpcionesSemilla.Leer(args);
    return GeneradorSemilla.Ejecutar(opciones);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine($"Argumento inválido: {ex.Message}");
    Console.Error.WriteLine("Usa --help para ver las opciones.");
    return 2;
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine($"No se generó la base: {ex.Message}");
    return 3;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error inesperado: {ex.GetType().Name}: {ex.Message}");
    return 1;
}
