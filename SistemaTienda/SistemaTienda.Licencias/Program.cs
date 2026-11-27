// Herramienta del proveedor para generar llaves y emitir licencias POSale.
// La llave privada nunca se guarda en el repositorio ni se compila en la app.
using System.Globalization;
using System.Security.Cryptography;
using SistemaTienda.Core;

return Ejecutar(args);

static int Ejecutar(string[] args)
{
    if (args.Length == 0 || args[0] is "-h" or "--help" or "ayuda")
    {
        Ayuda();
        return args.Length == 0 ? 1 : 0;
    }

    try
    {
        return args[0].ToLowerInvariant() switch
        {
            "generar-claves" => GenerarClaves(args),
            "emitir" => Emitir(args),
            "verificar" => Verificar(args),
            _ => Error($"Comando desconocido: {args[0]}")
        };
    }
    catch (Exception ex)
    {
        return Error(ex.Message);
    }
}

static int GenerarClaves(string[] args)
{
    var privada = Opcion(args, "--privada")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".posale", "private.pem");
    var publica = Opcion(args, "--publica");

    using var rsa = RSA.Create(3072);
    var pemPrivada = rsa.ExportRSAPrivateKeyPem();
    var pemPublica = rsa.ExportSubjectPublicKeyInfoPem();

    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(privada))!);
    File.WriteAllText(privada, pemPrivada);

    Console.WriteLine($"Llave privada: {Path.GetFullPath(privada)}");
    Console.WriteLine("Guárdala fuera del repositorio y no la compartas.");
    Console.WriteLine();
    Console.WriteLine("Llave pública (va en licencia-publica.pem):");
    Console.WriteLine(pemPublica);

    if (!string.IsNullOrWhiteSpace(publica))
    {
        File.WriteAllText(publica, pemPublica);
        Console.WriteLine($"Llave pública escrita en {Path.GetFullPath(publica)}");
    }

    return 0;
}

static int Emitir(string[] args)
{
    var privada = Opcion(args, "--privada")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".posale", "private.pem");
    var cliente = Opcion(args, "--cliente");
    var folio = Opcion(args, "--folio") ?? $"POS-{DateTime.Now:yyyyMMdd-HHmmss}";
    var cajas = int.TryParse(Opcion(args, "--cajas"), out var c) ? c : 1;
    var salida = Opcion(args, "--salida") ?? $"licencia-{folio}.poslic";
    var notas = Opcion(args, "--notas") ?? string.Empty;
    var soporte = Opcion(args, "--soporte-hasta");

    if (string.IsNullOrWhiteSpace(cliente))
        return Error("Falta --cliente");

    if (!File.Exists(privada))
        return Error($"No existe la llave privada: {privada}");

    DateTime? soporteHasta = null;
    if (!string.IsNullOrWhiteSpace(soporte))
    {
        if (!DateTime.TryParseExact(soporte, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
            return Error("--soporte-hasta debe ser yyyy-MM-dd");

        soporteHasta = fecha;
    }

    var licencia = new Licencia
    {
        Cliente = cliente,
        LicenciaId = folio,
        Emitida = DateTime.Today,
        Cajas = cajas,
        SoporteHasta = soporteHasta,
        Notas = notas
    };

    var contenido = LicenciaFirma.Crear(licencia, File.ReadAllText(privada));
    File.WriteAllText(salida, contenido);

    Console.WriteLine($"Licencia emitida: {Path.GetFullPath(salida)}");
    Console.WriteLine($"Cliente: {licencia.Cliente}");
    Console.WriteLine($"Folio: {licencia.LicenciaId}");
    Console.WriteLine($"Cajas: {licencia.Cajas}");
    Console.WriteLine($"Soporte hasta: {(soporteHasta?.ToString("yyyy-MM-dd") ?? "sin fecha")}");
    return 0;
}

static int Verificar(string[] args)
{
    var publica = Opcion(args, "--publica");
    var archivo = Opcion(args, "--archivo");

    if (string.IsNullOrWhiteSpace(publica) || !File.Exists(publica))
        return Error("Falta --publica o el archivo no existe");

    if (string.IsNullOrWhiteSpace(archivo) || !File.Exists(archivo))
        return Error("Falta --archivo o el archivo no existe");

    var ok = LicenciaFirma.LeerYVerificar(File.ReadAllText(archivo),
        File.ReadAllText(publica), out var licencia, out var error);
    if (!ok)
        return Error(error);

    Console.WriteLine($"Válida: {licencia.Cliente} ({licencia.LicenciaId})");
    Console.WriteLine($"Cajas: {licencia.Cajas}");
    Console.WriteLine($"Emitida: {licencia.Emitida:yyyy-MM-dd}");
    Console.WriteLine($"Soporte hasta: {(licencia.SoporteHasta?.ToString("yyyy-MM-dd") ?? "sin fecha")}");
    return 0;
}

static string? Opcion(string[] args, string nombre)
{
    for (var i = 1; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], nombre, StringComparison.OrdinalIgnoreCase))
            return args[i + 1];
    }

    return null;
}

static int Error(string mensaje)
{
    Console.Error.WriteLine("Error: " + mensaje);
    return 1;
}

static void Ayuda()
{
    Console.WriteLine("""
        POSale — herramienta de licencias

          generar-claves [--privada ruta] [--publica ruta]
              Crea una llave RSA 3072. La privada queda fuera del repo.

          emitir --cliente "Nombre" [--folio POS-001] [--cajas 1]
                 [--soporte-hasta 2027-11-27] [--notas "..."]
                 [--privada ruta] [--salida archivo.poslic]

          verificar --publica llave.pem --archivo licencia.poslic

        Ejemplo:
          dotnet run --project SistemaTienda.Licencias -- generar-claves
          dotnet run --project SistemaTienda.Licencias -- emitir ^
            --cliente "Abarrotes La Esquina" --folio POS-001 ^
            --soporte-hasta 2027-11-27 --salida licencia-POS-001.poslic
        """);
}
