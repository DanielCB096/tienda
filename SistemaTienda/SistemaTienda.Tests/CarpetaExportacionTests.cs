using SistemaTienda.Application.Services;

namespace SistemaTienda.Tests;

public class CarpetaExportacionTests
{
    [Fact]
    public void Elegir_UsaLaPrimeraCarpetaEscribible()
    {
        var raiz = CrearRaiz();
        try
        {
            var primera = Path.Combine(raiz, "documentos");
            var segunda = Path.Combine(raiz, "escritorio");

            var elegida = CarpetaExportacion.Elegir(new[] { primera, segunda });

            Assert.Equal(primera, elegida);
            Assert.True(Directory.Exists(primera));
            Assert.False(Directory.Exists(segunda));
            Assert.Empty(Directory.GetFileSystemEntries(primera));
        }
        finally
        {
            Limpiar(raiz);
        }
    }

    [Fact]
    public void Elegir_SaltaUnaRutaQueNoEsCarpeta()
    {
        var raiz = CrearRaiz();
        try
        {
            var archivo = Path.Combine(raiz, "documentos-bloqueado");
            File.WriteAllText(archivo, "no es una carpeta");
            var segunda = Path.Combine(raiz, "escritorio");

            var elegida = CarpetaExportacion.Elegir(new[] { archivo, segunda });

            Assert.Equal(segunda, elegida);
            Assert.True(Directory.Exists(segunda));
        }
        finally
        {
            Limpiar(raiz);
        }
    }

    [Fact]
    public void Elegir_FallaSiNingunaRutaSirve()
    {
        var raiz = CrearRaiz();
        try
        {
            var archivo = Path.Combine(raiz, "archivo");
            File.WriteAllText(archivo, "no es una carpeta");

            var error = Assert.Throws<IOException>(() =>
                CarpetaExportacion.Elegir(new[] { string.Empty, archivo }));

            Assert.Contains("No se encontró una carpeta escribible", error.Message);
        }
        finally
        {
            Limpiar(raiz);
        }
    }

    private static string CrearRaiz()
    {
        var raiz = Path.Combine(Path.GetTempPath(),
            "sistematienda-carpeta-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(raiz);
        return raiz;
    }

    private static void Limpiar(string raiz)
    {
        try
        {
            if (Directory.Exists(raiz))
                Directory.Delete(raiz, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
