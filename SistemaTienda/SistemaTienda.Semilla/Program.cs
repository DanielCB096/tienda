// Punto de entrada de la herramienta de datos de prueba.
using System.Text;
using SistemaTienda.Semilla;

Console.OutputEncoding = Encoding.UTF8;

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
