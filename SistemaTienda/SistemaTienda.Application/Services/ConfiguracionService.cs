// Servicio de configuración - lógica de negocio
using System.Security.Cryptography;
using System.Text;
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Application.Services;

public class ConfiguracionService
{
    private const int Pbkdf2Iteraciones = 100_000;
    private const int Pbkdf2LongitudHash = 32;
    private readonly DatabaseTools _db;

    public ConfiguracionService(DatabaseTools db) => _db = db;

    /// <summary>
    /// Ejecuta el asistente de primera configuración. Solo se permite una vez por instalación.
    /// </summary>
    public ProcessResult EjecutarAsistenteInicial(string nombreTienda, string giro,
        decimal impuestoPredeterminado, decimal comisionTarjeta,
        decimal fondoCajaInicial, bool fiadoActivado,
        decimal limiteCredito, int respaldoIntervaloDias, string pinDueño)
    {
        if (_db.ContarUsuarios() > 0)
            return ProcessResult.Error("El sistema ya tiene usuarios configurados");

        if (string.IsNullOrWhiteSpace(nombreTienda))
            return ProcessResult.Error("El nombre de la tienda es obligatorio");

        if (string.IsNullOrWhiteSpace(giro))
            return ProcessResult.Error("El giro es obligatorio");

        if (!PinValido(pinDueño))
            return ProcessResult.Error("El PIN debe tener entre 4 y 6 dígitos");

        if (impuestoPredeterminado < 0 || comisionTarjeta < 0 || fondoCajaInicial < 0 ||
            limiteCredito < 0 || respaldoIntervaloDias < 0)
            return ProcessResult.Error("Los importes y el intervalo no pueden ser negativos");

        var config = _db.ObtenerConfiguracion();
        if (config == null)
            return ProcessResult.Error("No se encontró la configuración base de la tienda");

        config.NombreTienda = nombreTienda.Trim();
        config.Giro = giro.Trim();
        config.ImpuestoPredeterminado = impuestoPredeterminado;
        config.ComisionTarjeta = comisionTarjeta;
        config.FondoCajaInicial = fondoCajaInicial;
        config.FiadoActivado = fiadoActivado;
        config.LimiCreditoCliente = limiteCredito;
        config.RespaldoIntervaloDias = respaldoIntervaloDias;
        config.VersionApp = "v0.1.0";

        if (!_db.ActualizarConfiguracion(config))
            return ProcessResult.Error("No se pudo guardar la configuración");

        var usuarioDueño = new UsuarioSistema
        {
            Nombre = "Dueño",
            PINHash = HashPin(pinDueño),
            Rol = RolUsuario.Dueño,
            PermisosEspeciales = "all"
        };

        try
        {
            _db.AgregarUsuario(usuarioDueño);
        }
        catch (Exception ex)
        {
            return ProcessResult.Error($"Error al crear el usuario dueño: {ex.Message}");
        }

        return ProcessResult.Success("Configuración inicial completada exitosamente");
    }

    public ProcessResult<ConfiguracionTienda> ObtenerConfiguracion()
    {
        var config = _db.ObtenerConfiguracion();
        return config == null
            ? ProcessResult<ConfiguracionTienda>.Error("No se encontró la configuración de la tienda")
            : ProcessResult<ConfiguracionTienda>.OK(config);
    }

    public ProcessResult ActualizarConfiguracion(ConfiguracionTienda nuevaConfig)
    {
        var configActual = _db.ObtenerConfiguracion();
        if (configActual == null)
            return ProcessResult.Error("No se encontró la configuración de la tienda");

        if (string.IsNullOrWhiteSpace(nuevaConfig.NombreTienda))
            return ProcessResult.Error("El nombre de la tienda es obligatorio");

        if (nuevaConfig.ImpuestoPredeterminado < 0 || nuevaConfig.ComisionTarjeta < 0 ||
            nuevaConfig.FondoCajaInicial < 0 || nuevaConfig.LimiCreditoCliente < 0 ||
            nuevaConfig.RespaldoIntervaloDias < 0)
            return ProcessResult.Error("Los importes y el intervalo no pueden ser negativos");

        nuevaConfig.Id = configActual.Id;
        nuevaConfig.VersionApp = configActual.VersionApp;
        nuevaConfig.FechaCreacion = configActual.FechaCreacion;

        return _db.ActualizarConfiguracion(nuevaConfig)
            ? ProcessResult.Success("Configuración actualizada")
            : ProcessResult.Error("No se pudo actualizar la configuración");
    }

    public ProcessResult<AgregarUsuarioResponse> AgregarUsuario(string nombre, string pin, RolUsuario rol)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return ProcessResult<AgregarUsuarioResponse>.Error("El nombre del usuario es obligatorio");

        if (!PinValido(pin))
            return ProcessResult<AgregarUsuarioResponse>.Error("El PIN debe tener entre 4 y 6 dígitos");

        try
        {
            var usuario = new UsuarioSistema
            {
                Nombre = nombre.Trim(),
                PINHash = HashPin(pin),
                Rol = rol
            };

            _db.AgregarUsuario(usuario);
            return ProcessResult<AgregarUsuarioResponse>.OK(new AgregarUsuarioResponse
            {
                UsuarioId = usuario.Id,
                Mensaje = "Usuario agregado correctamente"
            });
        }
        catch (Exception ex)
        {
            return ProcessResult<AgregarUsuarioResponse>.Error($"Error al agregar usuario: {ex.Message}");
        }
    }

    public ProcessResult<List<UsuarioSistema>> ObtenerUsuarios() =>
        ProcessResult<List<UsuarioSistema>>.OK(_db.ObtenerUsuarios());

    public ProcessResult<UsuarioSistema> VerificarAcceso(string pinIngresado)
    {
        foreach (var usuario in _db.ObtenerUsuarios())
        {
            bool coincide;
            try
            {
                coincide = VerifyPin(pinIngresado ?? string.Empty, usuario.PINHash);
            }
            catch (FormatException)
            {
                continue;
            }

            if (coincide)
                return ProcessResult<UsuarioSistema>.OK(usuario);
        }

        return ProcessResult<UsuarioSistema>.Error("PIN incorrecto");
    }

    public bool UsuarioTieneRol(int usuarioId, RolUsuario rolRequerido)
    {
        var usuario = _db.ObtenerUsuarioPorId(usuarioId);
        return usuario != null && usuario.Rol == rolRequerido;
    }

    public static bool PinValido(string pin) =>
        !string.IsNullOrEmpty(pin) && pin.Length is >= 4 and <= 6 && pin.All(char.IsAsciiDigit);

    /// <summary>
    /// Formato: pbkdf2-sha256$iteraciones$saltBase64$hashBase64
    /// </summary>
    public static string HashPin(string pin)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(pin), salt, Pbkdf2Iteraciones,
            HashAlgorithmName.SHA256, Pbkdf2LongitudHash);

        return $"pbkdf2-sha256${Pbkdf2Iteraciones}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPin(string pin, string hashAlmacenado)
    {
        if (string.IsNullOrEmpty(hashAlmacenado)) return false;

        var partes = hashAlmacenado.Split('$');
        if (partes.Length != 4 || partes[0] != "pbkdf2-sha256")
            throw new FormatException("Formato de hash de PIN no reconocido");

        var iteraciones = int.Parse(partes[1]);
        var salt = Convert.FromBase64String(partes[2]);
        var hashEsperado = Convert.FromBase64String(partes[3]);

        var hashCalculado = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(pin), salt, iteraciones,
            HashAlgorithmName.SHA256, hashEsperado.Length);

        return CryptographicOperations.FixedTimeEquals(hashCalculado, hashEsperado);
    }

    public ProcessResult<AgregarGastoResponse> AgregarGasto(Gasto nuevoGasto)
    {
        if (string.IsNullOrWhiteSpace(nuevoGasto.Concepto))
            return ProcessResult<AgregarGastoResponse>.Error("El concepto del gasto es obligatorio");

        if (nuevoGasto.Monto <= 0)
            return ProcessResult<AgregarGastoResponse>.Error("El monto debe ser mayor a cero");

        try
        {
            _db.AgregarGasto(nuevoGasto);
            return ProcessResult<AgregarGastoResponse>.OK(new AgregarGastoResponse
            {
                GastoId = nuevoGasto.Id,
                Mensaje = "Gasto registrado correctamente"
            });
        }
        catch (Exception ex)
        {
            return ProcessResult<AgregarGastoResponse>.Error($"Error al registrar gasto: {ex.Message}");
        }
    }

    public ProcessResult<decimal> ObtenerTotalGastos() =>
        ProcessResult<decimal>.OK(_db.ObtenerTotalGastos());

    public ProcessResult<List<GastoPorConcepto>> ObtenerGastosPorConcepto() =>
        ProcessResult<List<GastoPorConcepto>>.OK(_db.ObtenerGastosPorConcepto());

    public ProcessResult<List<Gasto>> ObtenerTodosLosGastos() =>
        ProcessResult<List<Gasto>>.OK(_db.ObtenerGastos());

    public ProcessResult<AgregarClienteFiadoResponse> AgregarClienteFiado(ClienteFiado nuevoCliente)
    {
        if (string.IsNullOrWhiteSpace(nuevoCliente.Nombre))
            return ProcessResult<AgregarClienteFiadoResponse>.Error("El nombre del cliente es obligatorio");

        if (nuevoCliente.SaldoActual < 0)
            return ProcessResult<AgregarClienteFiadoResponse>.Error("El saldo inicial no puede ser negativo");

        try
        {
            _db.AgregarClienteFiado(nuevoCliente);
            return ProcessResult<AgregarClienteFiadoResponse>.OK(new AgregarClienteFiadoResponse
            {
                ClienteId = nuevoCliente.Id,
                Mensaje = "Cliente agregado correctamente"
            });
        }
        catch (Exception ex)
        {
            return ProcessResult<AgregarClienteFiadoResponse>.Error($"Error al agregar cliente: {ex.Message}");
        }
    }

    public ProcessResult<List<ClienteFiado>> ObtenerTodosLosClientes() =>
        ProcessResult<List<ClienteFiado>>.OK(_db.ObtenerTodosLosClientes());

    public ProcessResult<List<ClienteFiado>> ObtenerClientesConSaldo() =>
        ProcessResult<List<ClienteFiado>>.OK(_db.ObtenerClientesConSaldo());

    public ProcessResult<RegistrarAbonoResponse> RegistrarAbonoCliente(int clienteId, decimal montoAbono,
        string usuario = "")
    {
        if (montoAbono <= 0)
            return ProcessResult<RegistrarAbonoResponse>.Error("El abono debe ser mayor a cero");

        var cliente = _db.ObtenerClienteFiadoPorId(clienteId);
        if (cliente == null)
            return ProcessResult<RegistrarAbonoResponse>.Error("Cliente no encontrado");

        if (montoAbono > cliente.SaldoActual)
            return ProcessResult<RegistrarAbonoResponse>.Error(
                $"El abono (${montoAbono:F2}) supera el saldo pendiente (${cliente.SaldoActual:F2})");

        var nuevoSaldo = cliente.SaldoActual - montoAbono;
        if (!_db.RegistrarAbonoFiado(cliente.Id, montoAbono, nuevoSaldo, usuario))
            return ProcessResult<RegistrarAbonoResponse>.Error("No se pudo registrar el abono");

        return ProcessResult<RegistrarAbonoResponse>.OK(new RegistrarAbonoResponse
        {
            ClienteId = cliente.Id,
            SaldoRestante = nuevoSaldo,
            Mensaje = "Abono registrado correctamente"
        });
    }

    public ProcessResult<AgregarProveedorResponse> AgregarProveedor(Proveedor nuevoProveedor)
    {
        if (string.IsNullOrWhiteSpace(nuevoProveedor.Nombre))
            return ProcessResult<AgregarProveedorResponse>.Error("El nombre del proveedor es obligatorio");

        if (nuevoProveedor.SaldoPendiente < 0)
            return ProcessResult<AgregarProveedorResponse>.Error("El saldo inicial no puede ser negativo");

        try
        {
            _db.AgregarProveedor(nuevoProveedor);
            return ProcessResult<AgregarProveedorResponse>.OK(new AgregarProveedorResponse
            {
                ProveedorId = nuevoProveedor.Id,
                Mensaje = "Proveedor agregado correctamente"
            });
        }
        catch (Exception ex)
        {
            return ProcessResult<AgregarProveedorResponse>.Error($"Error al agregar proveedor: {ex.Message}");
        }
    }

    public ProcessResult<List<Proveedor>> ObtenerTodosLosProveedores() =>
        ProcessResult<List<Proveedor>>.OK(_db.ObtenerTodosLosProveedores());

    public ProcessResult<RegistrarPagoProveedorResponse> RegistrarPagoProveedor(int proveedorId, decimal montoPago)
    {
        if (montoPago <= 0)
            return ProcessResult<RegistrarPagoProveedorResponse>.Error("El pago debe ser mayor a cero");

        var proveedor = _db.ObtenerProveedorPorId(proveedorId);
        if (proveedor == null)
            return ProcessResult<RegistrarPagoProveedorResponse>.Error("Proveedor no encontrado");

        if (montoPago > proveedor.SaldoPendiente)
            return ProcessResult<RegistrarPagoProveedorResponse>.Error(
                $"El pago (${montoPago:F2}) supera el saldo pendiente (${proveedor.SaldoPendiente:F2})");

        var nuevoSaldo = proveedor.SaldoPendiente - montoPago;
        if (!_db.ActualizarSaldoProveedor(proveedor.Id, nuevoSaldo))
            return ProcessResult<RegistrarPagoProveedorResponse>.Error("No se pudo registrar el pago");

        return ProcessResult<RegistrarPagoProveedorResponse>.OK(new RegistrarPagoProveedorResponse
        {
            ProveedorId = proveedor.Id,
            SaldoRestante = nuevoSaldo,
            Mensaje = "Pago registrado correctamente"
        });
    }
}

public class AgregarClienteFiadoResponse
{
    public int ClienteId { get; set; }
    public string Mensaje { get; set; } = string.Empty;
}

public class RegistrarAbonoResponse
{
    public int ClienteId { get; set; }
    public decimal SaldoRestante { get; set; }
    public string Mensaje { get; set; } = string.Empty;
}

public class AgregarProveedorResponse
{
    public int ProveedorId { get; set; }
    public string Mensaje { get; set; } = string.Empty;
}

public class RegistrarPagoProveedorResponse
{
    public int ProveedorId { get; set; }
    public decimal SaldoRestante { get; set; }
    public string Mensaje { get; set; } = string.Empty;
}

public class AgregarUsuarioResponse
{
    public int UsuarioId { get; set; }
    public string Mensaje { get; set; } = string.Empty;
}

public class AgregarGastoResponse
{
    public int GastoId { get; set; }
    public string Mensaje { get; set; } = string.Empty;
}
