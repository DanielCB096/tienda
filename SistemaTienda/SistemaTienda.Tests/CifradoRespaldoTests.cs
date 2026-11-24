using System.Security.Cryptography;
using System.Text;
using SistemaTienda.Application.Services;

namespace SistemaTienda.Tests;

public sealed class CifradoRespaldoTests
{
    private const string Clave = "0123456789ABCDEFGHJKMNPQ";
    private const int IteracionesRapidas = 10_000;

    [Fact]
    public void GenerarClave_Devuelve24CaracteresValidosYUnicos()
    {
        var primera = CifradoRespaldo.GenerarClave();
        var segunda = CifradoRespaldo.GenerarClave();

        Assert.True(CifradoRespaldo.ClaveValida(primera), primera);
        Assert.True(CifradoRespaldo.ClaveValida(segunda), segunda);
        Assert.NotEqual(primera, segunda);
        Assert.Equal(CifradoRespaldo.LargoClave, primera.Length);
    }

    [Fact]
    public void CifrarYDescifrar_DevuelveElMismoContenido()
    {
        var datos = Encoding.UTF8.GetBytes("respaldo de prueba con acentos: ñ á é");

        var cifrado = CifradoRespaldo.Cifrar(datos, Clave, IteracionesRapidas);
        var descifrado = CifradoRespaldo.Descifrar(cifrado, Clave);

        Assert.Equal(datos, descifrado);
        Assert.True(CifradoRespaldo.EsCifrado(cifrado));
        Assert.Equal(IteracionesRapidas, CifradoRespaldo.LeerIteraciones(cifrado));
    }

    [Fact]
    public void CifrarDosVeces_UsaSalYNonceDistintos()
    {
        var datos = Encoding.UTF8.GetBytes("mismo contenido");

        var uno = CifradoRespaldo.Cifrar(datos, Clave, IteracionesRapidas);
        var dos = CifradoRespaldo.Cifrar(datos, Clave, IteracionesRapidas);

        Assert.NotEqual(uno, dos);
    }

    [Fact]
    public void DescifrarConOtraClave_Falla()
    {
        var datos = Encoding.UTF8.GetBytes("contenido");
        var cifrado = CifradoRespaldo.Cifrar(datos, Clave, IteracionesRapidas);

        var otra = CifradoRespaldo.GenerarClave();
        Assert.ThrowsAny<CryptographicException>(
            () => CifradoRespaldo.Descifrar(cifrado, otra));
    }

    [Fact]
    public void DescifrarArchivoAlterado_Falla()
    {
        var datos = Encoding.UTF8.GetBytes("contenido que no se debe alterar");
        var cifrado = CifradoRespaldo.Cifrar(datos, Clave, IteracionesRapidas);

        cifrado[^1] ^= 0xFF;
        Assert.ThrowsAny<CryptographicException>(
            () => CifradoRespaldo.Descifrar(cifrado, Clave));
    }

    [Fact]
    public void ClaveNormalizada_AceptaMinusculasGuionesYConfusiones()
    {
        var normalizada = CifradoRespaldo.NormalizarClave("0123-4567 89ab cdef ghjk mnpq");
        Assert.Equal("0123456789ABCDEFGHJKMNPQ", normalizada);

        Assert.Equal("01", CifradoRespaldo.NormalizarClave("OI"));
        Assert.Equal("01", CifradoRespaldo.NormalizarClave("ol"));
    }

    [Fact]
    public void ClaveInvalida_SeRechaza()
    {
        var datos = Encoding.UTF8.GetBytes("x");

        Assert.False(CifradoRespaldo.ClaveValida("corta"));
        Assert.False(CifradoRespaldo.ClaveValida("0123456789ABCDEFGHJKMNPU"));
        Assert.Throws<ArgumentException>(
            () => CifradoRespaldo.Cifrar(datos, "corta", IteracionesRapidas));
    }

    [Fact]
    public void DescifrarArchivoSinFormato_Falla()
    {
        var plano = Encoding.UTF8.GetBytes("SQLite format 3");

        Assert.False(CifradoRespaldo.EsCifrado(plano));
        Assert.Throws<InvalidDataException>(
            () => CifradoRespaldo.Descifrar(plano, Clave));
    }

    [Fact]
    public void ArchivoGrande_SeCifraYDescifra()
    {
        var datos = new byte[1_000_000];
        Random.Shared.NextBytes(datos);

        var cifrado = CifradoRespaldo.Cifrar(datos, Clave, IteracionesRapidas);
        var descifrado = CifradoRespaldo.Descifrar(cifrado, Clave);

        Assert.Equal(datos, descifrado);
    }
}
