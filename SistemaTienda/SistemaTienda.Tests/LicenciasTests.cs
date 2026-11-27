using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;

namespace SistemaTienda.Tests;

public sealed class LicenciasTests
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static (RSA Llave, string Publica) CrearLlave()
    {
        var rsa = RSA.Create(2048);
        return (rsa, rsa.ExportSubjectPublicKeyInfoPem());
    }

    private static string Emitir(RSA llave, Licencia licencia) =>
        LicenciaFirma.Crear(licencia, llave.ExportRSAPrivateKeyPem());

    private static LicenciaService Servicio(BaseDatosPrueba bd, string publica) =>
        new(bd.Db, publica);

    [Fact]
    public void LicenciaPublica_EsUnaLlavePemValida()
    {
        Assert.Contains("BEGIN PUBLIC KEY", LicenciaPublica.Pem);

        using var rsa = RSA.Create();
        rsa.ImportFromPem(LicenciaPublica.Pem);
        Assert.True(rsa.KeySize >= 2048);
    }

    [Fact]
    public void LicenciaFirma_FirmaValida_SeAcepta()
    {
        var (llave, publica) = CrearLlave();
        var contenido = Emitir(llave, new Licencia
        {
            Cliente = "Abarrotes La Esquina",
            LicenciaId = "POS-001",
            SoporteHasta = new DateTime(2027, 11, 27)
        });

        Assert.True(LicenciaFirma.LeerYVerificar(contenido, publica,
            out var licencia, out var error), error);
        Assert.Equal("Abarrotes La Esquina", licencia.Cliente);
        Assert.Equal("POS-001", licencia.LicenciaId);
        Assert.Equal(1, licencia.Cajas);
    }

    [Fact]
    public void LicenciaFirma_ContenidoAlterado_SeRechaza()
    {
        var (llave, publica) = CrearLlave();
        var contenido = Emitir(llave, new Licencia
        {
            Cliente = "Cliente original",
            LicenciaId = "POS-002"
        });

        var archivo = JsonSerializer.Deserialize<LicenciaArchivo>(contenido, Opciones)!;
        var payload = Convert.FromBase64String(archivo.Payload);
        payload[10] ^= 0xFF;
        archivo.Payload = Convert.ToBase64String(payload);
        var alterado = JsonSerializer.Serialize(archivo, Opciones);

        Assert.False(LicenciaFirma.LeerYVerificar(alterado, publica,
            out _, out var error));
        Assert.Contains("firma", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Estado_SinLicencia_EmpiezaEnPrueba()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var (llave, publica) = CrearLlave();

        var servicio = Servicio(bd, publica);
        servicio.Iniciar();

        var estado = servicio.ObtenerEstado();
        Assert.Equal(NivelLicencia.Prueba, estado.Nivel);
        Assert.Equal(30, estado.DiasRestantesPrueba);
        Assert.False(estado.TieneLicencia);
    }

    [Fact]
    public void Estado_PruebaVencida_AvisaSinBloquear()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var (llave, publica) = CrearLlave();

        var servicio = Servicio(bd, publica);
        File.WriteAllText(servicio.RutaEstado, JsonSerializer.Serialize(new
        {
            inicioPrueba = DateTime.Today.AddDays(-45),
            ultimaApertura = DateTime.Today
        }));

        var estado = servicio.ObtenerEstado();
        Assert.Equal(NivelLicencia.Aviso, estado.Nivel);
        Assert.Equal(0, estado.DiasRestantesPrueba);
        Assert.True(estado.MostrarAviso);
        Assert.Contains("terminada", estado.Titulo);
    }

    [Fact]
    public void Importar_LicenciaValida_GuardaYRegistraBitacora()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var (llave, publica) = CrearLlave();
        var contenido = Emitir(llave, new Licencia
        {
            Cliente = "Abarrotes La Esquina",
            LicenciaId = "POS-003",
            SoporteHasta = DateTime.Today.AddYears(1)
        });

        var servicio = Servicio(bd, publica);
        var resultado = servicio.ImportarContenido(contenido);

        Assert.True(resultado.IsSuccess, resultado.Message);
        Assert.True(File.Exists(servicio.RutaLicencia));
        Assert.Equal(NivelLicencia.Licencia, resultado.Value.Nivel);
        Assert.True(resultado.Value.TieneLicencia);
        Assert.Equal("POS-003", resultado.Value.LicenciaId);
        Assert.Equal(1, bd.Db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Bitacora WHERE Accion = @accion",
            new SqliteParameter("@accion", AccionBitacora.Licencia)));
    }

    [Fact]
    public void Importar_FirmaInvalida_NoGuardaNada()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var (llave, publica) = CrearLlave();
        var original = Emitir(llave, new Licencia
        {
            Cliente = "Cliente",
            LicenciaId = "POS-004"
        });
        var archivo = JsonSerializer.Deserialize<LicenciaArchivo>(original, Opciones)!;
        archivo.Firma = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 });
        var contenido = JsonSerializer.Serialize(archivo, Opciones);

        var servicio = Servicio(bd, publica);
        var resultado = servicio.ImportarContenido(contenido);

        Assert.True(resultado.IsError);
        Assert.False(File.Exists(servicio.RutaLicencia));
    }

    [Fact]
    public void Importar_EmpleadoSinPermiso_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var (llave, publica) = CrearLlave();
        var contenido = Emitir(llave, new Licencia
        {
            Cliente = "Cliente",
            LicenciaId = "POS-005"
        });
        bd.IniciarSesion(RolUsuario.Empleado, "Empleado");

        var servicio = Servicio(bd, publica);
        var resultado = servicio.ImportarContenido(contenido);

        Assert.True(resultado.IsError);
        Assert.False(File.Exists(servicio.RutaLicencia));
    }

    [Fact]
    public void Estado_SoporteVencido_MuestraAvisoSinBloquear()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var (llave, publica) = CrearLlave();
        var contenido = Emitir(llave, new Licencia
        {
            Cliente = "Cliente con soporte vencido",
            LicenciaId = "POS-006",
            SoporteHasta = DateTime.Today.AddDays(-1)
        });

        var servicio = Servicio(bd, publica);
        Assert.True(servicio.ImportarContenido(contenido).IsSuccess);

        var estado = servicio.ObtenerEstado();
        Assert.Equal(NivelLicencia.Licencia, estado.Nivel);
        Assert.True(estado.SoporteVencido);
        Assert.True(estado.MostrarAviso);
        Assert.Contains("Soporte vencido", estado.Detalle);
    }

    [Fact]
    public void Importar_OtroProducto_SeRechaza()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var (llave, publica) = CrearLlave();

        var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            version = 1,
            producto = "OtroProducto",
            cliente = "Cliente",
            licenciaId = "POS-007",
            emitida = DateTime.Today,
            tipo = "Perpetua",
            cajas = 1,
            notas = ""
        }));
        var firma = llave.SignData(payload, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pss);
        var contenido = JsonSerializer.Serialize(new LicenciaArchivo
        {
            Payload = Convert.ToBase64String(payload),
            Firma = Convert.ToBase64String(firma)
        });

        var servicio = Servicio(bd, publica);
        var resultado = servicio.ImportarContenido(contenido);

        Assert.True(resultado.IsError);
        Assert.Contains("POSale", resultado.Message);
    }
}
