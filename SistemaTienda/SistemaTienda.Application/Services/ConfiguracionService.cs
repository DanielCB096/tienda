// Servicio de configuración - lógica de negocio
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SistemaTienda.Core;
using SistemaTienda.Infrastructure;

namespace SistemaTienda.Application.Services;

public class ConfiguracionService
{
    private const int Pbkdf2Iteraciones = 100_000;
    private const int Pbkdf2LongitudHash = 32;
    private const int IntentosMaximos = 5;
    private const int MinutosBloqueo = 3;
    private readonly DatabaseTools _db;
    private readonly BitacoraService _bitacora;

    public ConfiguracionService(DatabaseTools db)
    {
        _db = db;
        _bitacora = new BitacoraService(db);
    }

    private static ProcessResult Autorizar(PermisoSistema permiso) =>
        AutorizacionService.Exigir(SesionActual.Usuario, permiso);

    /// <summary>
    /// Ejecuta el asistente de primera configuración. Solo se permite una vez por instalación.
    /// </summary>
    public ProcessResult EjecutarAsistenteInicial(string nombreTienda, string giro,
        decimal impuestoPredeterminado, decimal comisionTarjeta,
        long fondoCajaInicial, bool fiadoActivado,
        long limiteCredito, int respaldoIntervaloDias, string pinDueño)
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
        var autorizacion = Autorizar(PermisoSistema.Configurar);
        if (autorizacion.IsError)
            return autorizacion;

        var configActual = _db.ObtenerConfiguracion();
        if (configActual == null)
            return ProcessResult.Error("No se encontró la configuración de la tienda");

        if (string.IsNullOrWhiteSpace(nuevaConfig.NombreTienda))
            return ProcessResult.Error("El nombre de la tienda es obligatorio");

        if (nuevaConfig.ImpuestoPredeterminado < 0 || nuevaConfig.ComisionTarjeta < 0 ||
            nuevaConfig.FondoCajaInicial < 0 || nuevaConfig.LimiCreditoCliente < 0 ||
            nuevaConfig.RespaldoIntervaloDias < 0)
            return ProcessResult.Error("Los importes y el intervalo no pueden ser negativos");

        if (nuevaConfig.MinutosInactividad < 2 || nuevaConfig.MinutosInactividad > 15)
            return ProcessResult.Error("El bloqueo por inactividad debe estar entre 2 y 15 minutos");

        nuevaConfig.Id = configActual.Id;
        nuevaConfig.VersionApp = configActual.VersionApp;
        nuevaConfig.FechaCreacion = configActual.FechaCreacion;

        var auditoria = new RegistroBitacora
        {
            Fecha = DateTime.Now,
            Usuario = SesionActual.Usuario?.Nombre ?? string.Empty,
            Accion = AccionBitacora.Configuracion,
            Entidad = "Configuracion",
            EntidadId = nuevaConfig.Id.ToString(),
            DatosAntes = $"Nombre={configActual.NombreTienda};Comision={configActual.ComisionTarjeta:F2}",
            DatosDespues = $"Nombre={nuevaConfig.NombreTienda};Comision={nuevaConfig.ComisionTarjeta:F2}"
        };

        if (!_db.ActualizarConfiguracion(nuevaConfig, auditoria))
            return ProcessResult.Error("No se pudo actualizar la configuración");

        return ProcessResult.Success("Configuración actualizada");
    }

    public ProcessResult<AgregarUsuarioResponse> AgregarUsuario(string nombre, string pin, RolUsuario rol)
    {
        var autorizacion = Autorizar(PermisoSistema.GestionarUsuarios);
        if (autorizacion.IsError)
            return ProcessResult<AgregarUsuarioResponse>.Error(autorizacion.Message);

        if (string.IsNullOrWhiteSpace(nombre))
            return ProcessResult<AgregarUsuarioResponse>.Error("El nombre del usuario es obligatorio");

        if (!PinValido(pin))
            return ProcessResult<AgregarUsuarioResponse>.Error("El PIN debe tener entre 4 y 6 dígitos");

        var nombreLimpio = nombre.Trim();
        var usuarios = _db.ObtenerUsuarios();
        if (NombreUsuarioUsado(nombreLimpio, usuarios))
            return ProcessResult<AgregarUsuarioResponse>.Error(
                $"Ya existe un usuario con el nombre \"{nombreLimpio}\"");

        if (PinYaUsado(pin, usuarios))
            return ProcessResult<AgregarUsuarioResponse>.Error(
                "Ese PIN ya está en uso por otro usuario; elige uno distinto");

        try
        {
            var usuario = new UsuarioSistema
            {
                Nombre = nombreLimpio,
                PINHash = HashPin(pin),
                Rol = rol
            };

            var auditoria = new RegistroBitacora
            {
                Fecha = DateTime.Now,
                Usuario = SesionActual.Usuario?.Nombre ?? string.Empty,
                Accion = AccionBitacora.Usuario,
                Entidad = "Usuario",
                DatosDespues = $"Alta: {usuario.Nombre};Rol={usuario.Rol}"
            };

            _db.AgregarUsuario(usuario, auditoria);
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

    public ProcessResult ActualizarUsuario(int usuarioId, string nombre, RolUsuario rol)
    {
        var autorizacion = Autorizar(PermisoSistema.GestionarUsuarios);
        if (autorizacion.IsError)
            return autorizacion;

        if (string.IsNullOrWhiteSpace(nombre))
            return ProcessResult.Error("El nombre del usuario es obligatorio");

        var usuario = _db.ObtenerUsuarioPorId(usuarioId);
        if (usuario == null)
            return ProcessResult.Error("Usuario no encontrado");

        var nombreLimpio = nombre.Trim();
        var usuarios = _db.ObtenerUsuarios();
        if (NombreUsuarioUsado(nombreLimpio, usuarios, usuarioId))
            return ProcessResult.Error($"Ya existe un usuario con el nombre \"{nombreLimpio}\"");

        if (usuario.Rol == RolUsuario.Dueño && rol != RolUsuario.Dueño &&
            !usuarios.Any(u => u.Id != usuarioId && u.Rol == RolUsuario.Dueño))
            return ProcessResult.Error("Debe quedar al menos un usuario con rol Dueño");

        var auditoria = new RegistroBitacora
        {
            Fecha = DateTime.Now,
            Usuario = SesionActual.Usuario?.Nombre ?? string.Empty,
            Accion = AccionBitacora.EditarUsuario,
            Entidad = "Usuario",
            EntidadId = usuarioId.ToString(),
            DatosAntes = $"Nombre={usuario.Nombre};Rol={usuario.Rol}",
            DatosDespues = $"Nombre={nombreLimpio};Rol={rol}"
        };

        if (!_db.ActualizarUsuario(usuarioId, nombreLimpio, rol, auditoria))
            return ProcessResult.Error("No se pudo actualizar el usuario");

        // Un permiso especial no sobrevive a un cambio de rol que ya no lo admite.
        if (rol != RolUsuario.Encargado && !string.IsNullOrWhiteSpace(usuario.PermisosEspeciales))
            _db.ActualizarPermisosUsuario(usuarioId, string.Empty);

        return ProcessResult.Success("Usuario actualizado");
    }

    public ProcessResult CambiarPin(int usuarioId, string nuevoPin)
    {
        var autorizacion = Autorizar(PermisoSistema.GestionarUsuarios);
        if (autorizacion.IsError)
            return autorizacion;

        if (!PinValido(nuevoPin))
            return ProcessResult.Error("El PIN debe tener entre 4 y 6 dígitos");

        var usuario = _db.ObtenerUsuarioPorId(usuarioId);
        if (usuario == null)
            return ProcessResult.Error("Usuario no encontrado");

        var usuarios = _db.ObtenerUsuarios();
        if (PinYaUsado(nuevoPin, usuarios, usuarioId))
            return ProcessResult.Error("Ese PIN ya está en uso por otro usuario; elige uno distinto");

        var auditoria = new RegistroBitacora
        {
            Fecha = DateTime.Now,
            Usuario = SesionActual.Usuario?.Nombre ?? string.Empty,
            Accion = AccionBitacora.CambiarPin,
            Entidad = "Usuario",
            EntidadId = usuarioId.ToString(),
            DatosDespues = $"PIN actualizado para {usuario.Nombre}"
        };

        if (!_db.ActualizarPinUsuario(usuarioId, HashPin(nuevoPin), auditoria))
            return ProcessResult.Error("No se pudo cambiar el PIN");

        return ProcessResult.Success("PIN actualizado");
    }

    /// <summary>
    /// Concede o revoca un permiso especial a un encargado. El dueño ya tiene
    /// todos los permisos y un empleado no puede recibirlos.
    /// </summary>
    public ProcessResult ActualizarPermisoEspecial(int usuarioId, string permiso, bool activo)
    {
        var autorizacion = Autorizar(PermisoSistema.GestionarUsuarios);
        if (autorizacion.IsError)
            return autorizacion;

        if (!string.Equals(permiso, PermisoEspecial.VerCostos, StringComparison.OrdinalIgnoreCase))
            return ProcessResult.Error("El permiso indicado no existe");

        var usuario = _db.ObtenerUsuarioPorId(usuarioId);
        if (usuario == null)
            return ProcessResult.Error("Usuario no encontrado");

        if (usuario.Rol == RolUsuario.Dueño)
            return ProcessResult.Error("El dueño ya tiene todos los permisos");

        if (usuario.Rol != RolUsuario.Encargado)
            return ProcessResult.Error("Solo se puede conceder a un encargado");

        var nuevos = PermisoEspecial.Establecer(
            usuario.PermisosEspeciales, PermisoEspecial.VerCostos, activo);

        if (string.Equals(nuevos, usuario.PermisosEspeciales ?? string.Empty, StringComparison.Ordinal))
            return ProcessResult.Success("Sin cambios");

        var auditoria = new RegistroBitacora
        {
            Fecha = DateTime.Now,
            Usuario = SesionActual.Usuario?.Nombre ?? string.Empty,
            Accion = AccionBitacora.EditarUsuario,
            Entidad = "Usuario",
            EntidadId = usuarioId.ToString(),
            DatosAntes = $"Permisos={usuario.PermisosEspeciales}",
            DatosDespues = $"Permisos={nuevos}",
            Motivo = activo ? "Concede ver costos" : "Revoca ver costos"
        };

        if (!_db.ActualizarPermisosUsuario(usuarioId, nuevos, auditoria))
            return ProcessResult.Error("No se pudo actualizar el permiso");

        return ProcessResult.Success(activo
            ? "Ahora puede ver costos y utilidad"
            : "Ya no puede ver costos y utilidad");
    }

    public ProcessResult<List<UsuarioSistema>> ObtenerUsuarios() =>
        ProcessResult<List<UsuarioSistema>>.OK(_db.ObtenerUsuarios());

    private static bool NombreUsuarioUsado(string nombre, List<UsuarioSistema> usuarios,
        int? excluirId = null) =>
        usuarios.Any(u => (excluirId == null || u.Id != excluirId.Value) &&
                          string.Equals(u.Nombre.Trim(), nombre.Trim(),
                              StringComparison.CurrentCultureIgnoreCase));

    private static bool PinYaUsado(string pin, List<UsuarioSistema> usuarios,
        int? excluirId = null)
    {
        foreach (var usuario in usuarios)
        {
            if (excluirId.HasValue && usuario.Id == excluirId.Value)
                continue;

            try
            {
                if (VerifyPin(pin, usuario.PINHash))
                    return true;
            }
            catch (FormatException)
            {
                // Un hash heredado mal formado no debe impedir elegir un PIN nuevo.
            }
        }

        return false;
    }

    public ProcessResult<UsuarioSistema> VerificarAcceso(string pinIngresado)
    {
        var (fallos, bloqueadoHasta) = _db.ObtenerEstadoAcceso();
        var ahora = DateTime.Now;

        if (bloqueadoHasta.HasValue && bloqueadoHasta.Value > ahora)
        {
            var restante = Math.Max(1,
                (int)Math.Ceiling((bloqueadoHasta.Value - ahora).TotalMinutes));
            return ProcessResult<UsuarioSistema>.Error(
                $"Acceso bloqueado por intentos fallidos. Espera {restante} minuto(s).");
        }

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
            {
                _db.LimpiarEstadoAcceso();
                return ProcessResult<UsuarioSistema>.OK(usuario);
            }
        }

        var intentos = _db.RegistrarFalloAcceso(IntentosMaximos,
            ahora.AddMinutes(MinutosBloqueo));

        _bitacora.Registrar(AccionBitacora.AccesoFallido, "Acceso", string.Empty,
            string.Empty, $"Intento={intentos}",
            $"PIN incorrecto; intentos máximos={IntentosMaximos}");

        return intentos >= IntentosMaximos
            ? ProcessResult<UsuarioSistema>.Error(
                $"Demasiados intentos. El acceso queda bloqueado {MinutosBloqueo} minutos.")
            : ProcessResult<UsuarioSistema>.Error(
                $"PIN incorrecto. Intento {intentos} de {IntentosMaximos}.");
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

    /// <summary>
    /// Corrige concepto, monto, método, origen y observaciones. No cambia la fecha
    /// ni permite tocar gastos incluidos en un corte cerrado.
    /// </summary>
    public ProcessResult EditarGasto(int gastoId, string concepto, long monto,
        string metodoPago, string origen, string observaciones)
    {
        var autorizacion = Autorizar(PermisoSistema.RegistrarGasto);
        if (autorizacion.IsError)
            return autorizacion;

        concepto = (concepto ?? string.Empty).Trim();
        metodoPago = string.IsNullOrWhiteSpace(metodoPago)
            ? MetodoPagoAbono.Efectivo
            : metodoPago.Trim();
        origen = string.IsNullOrWhiteSpace(origen) ? OrigenGasto.Caja : origen.Trim();
        observaciones = (observaciones ?? string.Empty).Trim();

        if (concepto.Length == 0)
            return ProcessResult.Error("El concepto del gasto es obligatorio");

        if (monto <= 0)
            return ProcessResult.Error("El monto debe ser mayor a cero");

        if (origen == OrigenGasto.Caja &&
            !string.Equals(metodoPago, MetodoPagoAbono.Efectivo, StringComparison.OrdinalIgnoreCase))
            return ProcessResult.Error(
                "Un gasto de caja debe pagarse en efectivo. Si lo pagaste con tarjeta o " +
                "transferencia, regístralo como gasto externo.");

        var actual = _db.ObtenerGastoPorId(gastoId);
        if (actual == null)
            return ProcessResult.Error($"El gasto #{gastoId} no existe");

        var auditoria = new RegistroBitacora
        {
            Fecha = DateTime.Now,
            Usuario = SesionActual.Usuario?.Nombre ?? string.Empty,
            Accion = AccionBitacora.EditarGasto,
            Entidad = "Gasto",
            EntidadId = gastoId.ToString(),
            DatosAntes = $"Concepto={actual.Concepto};Monto={Dinero.Formatear(actual.Monto)};" +
                         $"Metodo={actual.MetodoPago};Origen={actual.Origen}",
            DatosDespues = $"Concepto={concepto};Monto={Dinero.Formatear(monto)};" +
                           $"Metodo={metodoPago};Origen={origen}"
        };

        return _db.ActualizarGasto(new Gasto
        {
            Id = gastoId,
            Concepto = concepto,
            Monto = monto,
            Fecha = actual.Fecha,
            MetodoPago = metodoPago,
            Origen = origen,
            Observaciones = observaciones
        }, auditoria);
    }

    public ProcessResult<AgregarGastoResponse> AgregarGasto(Gasto nuevoGasto)
    {
        var autorizacion = Autorizar(PermisoSistema.RegistrarGasto);
        if (autorizacion.IsError)
            return ProcessResult<AgregarGastoResponse>.Error(autorizacion.Message);

        if (nuevoGasto == null)
            return ProcessResult<AgregarGastoResponse>.Error("El gasto es obligatorio");

        nuevoGasto.Concepto = (nuevoGasto.Concepto ?? string.Empty).Trim();
        nuevoGasto.Observaciones = (nuevoGasto.Observaciones ?? string.Empty).Trim();

        if (nuevoGasto.Concepto.Length == 0)
            return ProcessResult<AgregarGastoResponse>.Error("El concepto del gasto es obligatorio");

        if (nuevoGasto.Monto <= 0)
            return ProcessResult<AgregarGastoResponse>.Error("El monto debe ser mayor a cero");

        // El gasto de caja nace en efectivo; si el método viene vacío se asume efectivo.
        if (string.IsNullOrWhiteSpace(nuevoGasto.MetodoPago))
            nuevoGasto.MetodoPago = MetodoPagoAbono.Efectivo;

        if (nuevoGasto.Origen == OrigenGasto.Caja &&
            !string.Equals(nuevoGasto.MetodoPago, MetodoPagoAbono.Efectivo,
                StringComparison.OrdinalIgnoreCase))
            return ProcessResult<AgregarGastoResponse>.Error(
                "Un gasto de caja debe pagarse en efectivo. Si lo pagaste con tarjeta o " +
                "transferencia, regístralo como gasto externo.");

        try
        {
            var auditoria = new RegistroBitacora
            {
                Fecha = DateTime.Now,
                Usuario = SesionActual.Usuario?.Nombre ?? string.Empty,
                Accion = AccionBitacora.Gasto,
                Entidad = "Gasto",
                DatosDespues =
                    $"Concepto={nuevoGasto.Concepto};Monto={Dinero.Formatear(nuevoGasto.Monto)};Origen={nuevoGasto.Origen}"
            };

            _db.AgregarGasto(nuevoGasto, auditoria);
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

    public ProcessResult<long> ObtenerTotalGastos() =>
        ProcessResult<long>.OK(_db.ObtenerTotalGastos());

    public ProcessResult<List<GastoPorConcepto>> ObtenerGastosPorConcepto() =>
        ProcessResult<List<GastoPorConcepto>>.OK(_db.ObtenerGastosPorConcepto());

    public ProcessResult<List<Gasto>> ObtenerTodosLosGastos() =>
        ProcessResult<List<Gasto>>.OK(_db.ObtenerGastos());

    public ProcessResult<ResumenGastos> ObtenerResumenGastos()
    {
        var gastos = _db.ObtenerGastos();
        var total = gastos.Sum(g => g.Monto);
        var deCaja = gastos.Where(g => g.Origen == OrigenGasto.Caja).Sum(g => g.Monto);

        return ProcessResult<ResumenGastos>.OK(new ResumenGastos
        {
            Cantidad = gastos.Count,
            Total = total,
            DeCaja = deCaja,
            Externos = total - deCaja
        });
    }

    public ProcessResult<AgregarClienteFiadoResponse> AgregarClienteFiado(ClienteFiado nuevoCliente)
    {
        var autorizacion = Autorizar(PermisoSistema.CrearCliente);
        if (autorizacion.IsError)
            return ProcessResult<AgregarClienteFiadoResponse>.Error(autorizacion.Message);

        if (string.IsNullOrWhiteSpace(nuevoCliente.Nombre))
            return ProcessResult<AgregarClienteFiadoResponse>.Error("El nombre del cliente es obligatorio");

        if (nuevoCliente.SaldoActual < 0)
            return ProcessResult<AgregarClienteFiadoResponse>.Error("El saldo inicial no puede ser negativo");

        nuevoCliente.Nombre = nuevoCliente.Nombre.Trim();
        nuevoCliente.Telefono = (nuevoCliente.Telefono ?? string.Empty).Trim();

        try
        {
            var auditoria = new RegistroBitacora
            {
                Fecha = DateTime.Now,
                Usuario = SesionActual.Usuario?.Nombre ?? string.Empty,
                Accion = AccionBitacora.Cliente,
                Entidad = "ClienteFiado",
                DatosDespues = $"Alta: {nuevoCliente.Nombre};Saldo={Dinero.Formatear(nuevoCliente.SaldoActual)}"
            };

            _db.AgregarClienteFiado(nuevoCliente, auditoria);
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

    public ProcessResult DesactivarCliente(int clienteId) =>
        CambiarEstadoCliente(clienteId, activo: false);

    public ProcessResult ReactivarCliente(int clienteId) =>
        CambiarEstadoCliente(clienteId, activo: true);

    private ProcessResult CambiarEstadoCliente(int clienteId, bool activo)
    {
        var autorizacion = Autorizar(PermisoSistema.CambiarEstadoCliente);
        if (autorizacion.IsError)
            return autorizacion;

        var cliente = _db.ObtenerClienteFiadoPorId(clienteId);
        if (cliente == null)
            return ProcessResult.Error("Cliente no encontrado");

        if (cliente.Activo == activo)
            return ProcessResult.Error(activo
                ? "El cliente ya está activo"
                : "El cliente ya está inactivo");

        var auditoria = new RegistroBitacora
        {
            Fecha = DateTime.Now,
            Usuario = SesionActual.Usuario?.Nombre ?? string.Empty,
            Accion = AccionBitacora.CambiarEstadoCliente,
            Entidad = "ClienteFiado",
            EntidadId = clienteId.ToString(),
            DatosAntes = $"Nombre={cliente.Nombre};Activo={(cliente.Activo ? 1 : 0)};Saldo={Dinero.Formatear(cliente.SaldoActual)}",
            DatosDespues = $"Nombre={cliente.Nombre};Activo={(activo ? 1 : 0)};Saldo={Dinero.Formatear(cliente.SaldoActual)}"
        };

        if (!_db.CambiarEstadoCliente(clienteId, activo, auditoria))
            return ProcessResult.Error("No se pudo cambiar el estado del cliente");

        return ProcessResult.Success(activo
            ? "Cliente reactivado; podrá recibir nuevas ventas a fiado"
            : "Cliente dado de baja. Se conserva su historial y se pueden registrar abonos, pero no nuevas ventas a fiado");
    }

    /// <summary>
    /// Filtra por nombre o teléfono sin distinguir mayúsculas ni acentos.
    /// </summary>
    public ProcessResult<List<ClienteFiado>> BuscarClientes(string texto = "", bool soloConSaldo = false)
    {
        var clientes = soloConSaldo
            ? _db.ObtenerClientesConSaldo()
            : _db.ObtenerTodosLosClientes();

        var filtro = (texto ?? string.Empty).Trim();
        if (filtro.Length == 0)
            return ProcessResult<List<ClienteFiado>>.OK(clientes);

        var filtroNormalizado = SinAcentos(filtro);
        var lista = clientes.Where(c =>
            SinAcentos(c.Nombre).Contains(filtroNormalizado, StringComparison.OrdinalIgnoreCase) ||
            SinAcentos(c.Telefono ?? string.Empty).Contains(filtroNormalizado, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return ProcessResult<List<ClienteFiado>>.OK(lista);
    }

    public ProcessResult<List<ClienteFiado>> DetectarDuplicadosCliente(
        string nombre, string telefono, int? excluirClienteId = null)
    {
        var nombreNormalizado = NormalizarTextoDuplicado(nombre);
        var telefonoNormalizado = NormalizarTelefono(telefono);
        var coincidencias = _db.ObtenerTodosLosClientes()
            .Where(cliente =>
                (!excluirClienteId.HasValue || cliente.Id != excluirClienteId.Value) &&
                ((nombreNormalizado.Length > 0 &&
                  StringComparer.OrdinalIgnoreCase.Equals(
                      NormalizarTextoDuplicado(cliente.Nombre), nombreNormalizado)) ||
                 (telefonoNormalizado.Length > 0 &&
                  NormalizarTelefono(cliente.Telefono) == telefonoNormalizado)))
            .ToList();

        return ProcessResult<List<ClienteFiado>>.OK(coincidencias);
    }

    private static string SinAcentos(string texto) => Texto.SinAcentos(texto);

    private static string NormalizarTextoDuplicado(string texto)
    {
        var partes = SinAcentos((texto ?? string.Empty).Trim())
            .Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", partes);
    }

    private static string NormalizarTelefono(string telefono) =>
        new((telefono ?? string.Empty).Where(char.IsDigit).ToArray());

    /// <summary>
    /// Corrige nombre y teléfono de un cliente existente. No toca el saldo:
    /// ese solo cambia con ventas a fiado y abonos.
    /// </summary>
    public ProcessResult EditarClienteFiado(int clienteId, string nombre, string telefono)
    {
        var autorizacion = Autorizar(PermisoSistema.EditarCliente);
        if (autorizacion.IsError)
            return autorizacion;

        var nombreRecortado = (nombre ?? string.Empty).Trim();
        if (nombreRecortado.Length == 0)
            return ProcessResult.Error("El nombre del cliente es obligatorio");

        var cliente = _db.ObtenerClienteFiadoPorId(clienteId);
        if (cliente == null)
            return ProcessResult.Error("Cliente no encontrado");

        var telefonoRecortado = (telefono ?? string.Empty).Trim();

        try
        {
            var auditoria = new RegistroBitacora
            {
                Fecha = DateTime.Now,
                Usuario = SesionActual.Usuario?.Nombre ?? string.Empty,
                Accion = AccionBitacora.EditarCliente,
                Entidad = "ClienteFiado",
                EntidadId = clienteId.ToString(),
                DatosAntes = $"Nombre={cliente.Nombre};Telefono={cliente.Telefono}",
                DatosDespues = $"Nombre={nombreRecortado};Telefono={telefonoRecortado}"
            };

            if (!_db.ActualizarDatosCliente(clienteId, nombreRecortado, telefonoRecortado, auditoria))
                return ProcessResult.Error("No se pudo actualizar el cliente");

            return ProcessResult.Success("Cliente actualizado correctamente");
        }
        catch (Exception ex)
        {
            return ProcessResult.Error($"Error al actualizar cliente: {ex.Message}");
        }
    }

    /// <summary>
    /// Estado de cuenta del cliente: datos, saldo vigente, ventas a fiado
    /// vigentes y abonos con historial. Excluye las ventas anuladas.
    /// </summary>
    public ProcessResult<EstadoCuentaCliente> ObtenerEstadoCuentaCliente(int clienteId)
    {
        var cliente = _db.ObtenerClienteFiadoPorId(clienteId);
        if (cliente == null)
            return ProcessResult<EstadoCuentaCliente>.Error("Cliente no encontrado");

        var ventas = _db.ObtenerVentasFiadasDeCliente(clienteId);
        var abonos = _db.ObtenerAbonosDeCliente(clienteId);

        return ProcessResult<EstadoCuentaCliente>.OK(new EstadoCuentaCliente
        {
            Cliente = cliente,
            Ventas = ventas,
            Abonos = abonos,
            TotalFiado = ventas.Sum(v => v.PagoFiado),
            TotalAbonado = abonos.Sum(a => a.Monto),
            SaldoActual = cliente.SaldoActual
        });
    }

    public ProcessResult<RegistrarAbonoResponse> RegistrarAbonoCliente(int clienteId, long montoAbono,
        string usuario = "", string metodoPago = MetodoPagoAbono.Efectivo)
    {
        var autorizacion = Autorizar(PermisoSistema.RegistrarAbono);
        if (autorizacion.IsError)
            return ProcessResult<RegistrarAbonoResponse>.Error(autorizacion.Message);

        if (montoAbono <= 0)
            return ProcessResult<RegistrarAbonoResponse>.Error("El abono debe ser mayor a cero");

        var metodo = MetodoPagoAbono.Normalizar(metodoPago);
        if (!MetodoPagoAbono.EsValido(metodo))
            return ProcessResult<RegistrarAbonoResponse>.Error(
                $"El método del abono debe ser uno de: {string.Join(", ", MetodoPagoAbono.Todos)}");

        var cliente = _db.ObtenerClienteFiadoPorId(clienteId);
        if (cliente == null)
            return ProcessResult<RegistrarAbonoResponse>.Error("Cliente no encontrado");

        if (montoAbono > cliente.SaldoActual)
            return ProcessResult<RegistrarAbonoResponse>.Error(
                $"El abono (${Dinero.Formatear(montoAbono)}) supera el saldo pendiente (${Dinero.Formatear(cliente.SaldoActual)})");

        var nuevoSaldo = cliente.SaldoActual - montoAbono;
        var auditoria = new RegistroBitacora
        {
            Fecha = DateTime.Now,
            Usuario = usuario,
            Accion = AccionBitacora.Abono,
            Entidad = "ClienteFiado",
            DatosAntes = $"Saldo={Dinero.Formatear(cliente.SaldoActual)}",
            DatosDespues =
                $"Saldo={Dinero.Formatear(nuevoSaldo)};Abono={Dinero.Formatear(montoAbono)};Metodo={metodo}"
        };

        if (!_db.RegistrarAbonoFiado(cliente.Id, montoAbono, nuevoSaldo, usuario, metodo, auditoria))
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
        var autorizacion = Autorizar(PermisoSistema.CrearProveedor);
        if (autorizacion.IsError)
            return ProcessResult<AgregarProveedorResponse>.Error(autorizacion.Message);

        if (string.IsNullOrWhiteSpace(nuevoProveedor.Nombre))
            return ProcessResult<AgregarProveedorResponse>.Error("El nombre del proveedor es obligatorio");

        if (nuevoProveedor.SaldoPendiente < 0)
            return ProcessResult<AgregarProveedorResponse>.Error("El saldo inicial no puede ser negativo");

        nuevoProveedor.Nombre = nuevoProveedor.Nombre.Trim();
        nuevoProveedor.Clave = (nuevoProveedor.Clave ?? string.Empty).Trim();
        nuevoProveedor.Telefono = (nuevoProveedor.Telefono ?? string.Empty).Trim();
        nuevoProveedor.Direccion = (nuevoProveedor.Direccion ?? string.Empty).Trim();

        try
        {
            var auditoria = new RegistroBitacora
            {
                Fecha = DateTime.Now,
                Usuario = SesionActual.Usuario?.Nombre ?? string.Empty,
                Accion = AccionBitacora.Proveedor,
                Entidad = "Proveedor",
                DatosDespues = $"Alta: {nuevoProveedor.Nombre};Saldo={Dinero.Formatear(nuevoProveedor.SaldoPendiente)}"
            };

            _db.AgregarProveedor(nuevoProveedor, auditoria);
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

    public ProcessResult DesactivarProveedor(int proveedorId) =>
        CambiarEstadoProveedor(proveedorId, activo: false);

    public ProcessResult ReactivarProveedor(int proveedorId) =>
        CambiarEstadoProveedor(proveedorId, activo: true);

    private ProcessResult CambiarEstadoProveedor(int proveedorId, bool activo)
    {
        var autorizacion = Autorizar(PermisoSistema.CambiarEstadoProveedor);
        if (autorizacion.IsError)
            return autorizacion;

        var proveedor = _db.ObtenerProveedorPorId(proveedorId);
        if (proveedor == null)
            return ProcessResult.Error("Proveedor no encontrado");

        if (proveedor.Activo == activo)
            return ProcessResult.Error(activo
                ? "El proveedor ya está activo"
                : "El proveedor ya está inactivo");

        var auditoria = new RegistroBitacora
        {
            Fecha = DateTime.Now,
            Usuario = SesionActual.Usuario?.Nombre ?? string.Empty,
            Accion = AccionBitacora.CambiarEstadoProveedor,
            Entidad = "Proveedor",
            EntidadId = proveedorId.ToString(),
            DatosAntes = $"Nombre={proveedor.Nombre};Activo={(proveedor.Activo ? 1 : 0)};Saldo={Dinero.Formatear(proveedor.SaldoPendiente)}",
            DatosDespues = $"Nombre={proveedor.Nombre};Activo={(activo ? 1 : 0)};Saldo={Dinero.Formatear(proveedor.SaldoPendiente)}"
        };

        if (!_db.CambiarEstadoProveedor(proveedorId, activo, auditoria))
            return ProcessResult.Error("No se pudo cambiar el estado del proveedor");

        return ProcessResult.Success(activo
            ? "Proveedor reactivado y disponible en el directorio"
            : "Proveedor dado de baja. Se conserva su historial y se pueden registrar pagos para liquidar el saldo");
    }

    /// <summary>
    /// Filtra por nombre, clave o teléfono sin distinguir mayúsculas ni acentos.
    /// </summary>
    public ProcessResult<List<Proveedor>> BuscarProveedores(string texto = "", bool soloConSaldo = false)
    {
        var proveedores = _db.ObtenerTodosLosProveedores();
        if (soloConSaldo)
            proveedores = proveedores.Where(p => p.SaldoPendiente > 0).ToList();

        var filtro = (texto ?? string.Empty).Trim();
        if (filtro.Length == 0)
            return ProcessResult<List<Proveedor>>.OK(proveedores);

        var filtroNormalizado = SinAcentos(filtro);
        var lista = proveedores.Where(p =>
            SinAcentos(p.Nombre).Contains(filtroNormalizado, StringComparison.OrdinalIgnoreCase) ||
            SinAcentos(p.Clave ?? string.Empty).Contains(filtroNormalizado, StringComparison.OrdinalIgnoreCase) ||
            SinAcentos(p.Telefono ?? string.Empty).Contains(filtroNormalizado, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return ProcessResult<List<Proveedor>>.OK(lista);
    }

    public ProcessResult<List<Proveedor>> DetectarDuplicadosProveedor(
        string nombre, string clave, int? excluirProveedorId = null)
    {
        var nombreNormalizado = NormalizarTextoDuplicado(nombre);
        var claveNormalizada = NormalizarTextoDuplicado(clave);
        var coincidencias = _db.ObtenerTodosLosProveedores()
            .Where(proveedor =>
                (!excluirProveedorId.HasValue || proveedor.Id != excluirProveedorId.Value) &&
                ((nombreNormalizado.Length > 0 &&
                  StringComparer.OrdinalIgnoreCase.Equals(
                      NormalizarTextoDuplicado(proveedor.Nombre), nombreNormalizado)) ||
                 (claveNormalizada.Length > 0 &&
                  StringComparer.OrdinalIgnoreCase.Equals(
                      NormalizarTextoDuplicado(proveedor.Clave), claveNormalizada))))
            .ToList();

        return ProcessResult<List<Proveedor>>.OK(coincidencias);
    }

    /// <summary>
    /// Corrige nombre, clave, teléfono y dirección de un proveedor existente.
    /// No toca el saldo: solo cambia con pagos. No hay baja de proveedores.
    /// </summary>
    public ProcessResult EditarProveedor(int proveedorId, string nombre, string clave,
        string telefono, string direccion)
    {
        var autorizacion = Autorizar(PermisoSistema.EditarProveedor);
        if (autorizacion.IsError)
            return ProcessResult.Error(autorizacion.Message);

        if (string.IsNullOrWhiteSpace(nombre))
            return ProcessResult.Error("El nombre del proveedor es obligatorio");

        var proveedor = _db.ObtenerProveedorPorId(proveedorId);
        if (proveedor == null)
            return ProcessResult.Error("Proveedor no encontrado");

        var nombreRecortado = nombre.Trim();
        var claveRecortada = (clave ?? string.Empty).Trim();
        var telefonoRecortado = (telefono ?? string.Empty).Trim();
        var direccionRecortada = (direccion ?? string.Empty).Trim();

        try
        {
            var auditoria = new RegistroBitacora
            {
                Fecha = DateTime.Now,
                Usuario = SesionActual.Usuario?.Nombre ?? string.Empty,
                Accion = AccionBitacora.EditarProveedor,
                Entidad = "Proveedor",
                EntidadId = proveedorId.ToString(),
                DatosAntes = $"Nombre={proveedor.Nombre};Clave={proveedor.Clave};Telefono={proveedor.Telefono};Direccion={proveedor.Direccion}",
                DatosDespues = $"Nombre={nombreRecortado};Clave={claveRecortada};Telefono={telefonoRecortado};Direccion={direccionRecortada}",
                Motivo = "Corrección de datos del proveedor"
            };

            if (!_db.ActualizarDatosProveedor(proveedorId, nombreRecortado, claveRecortada,
                    telefonoRecortado, direccionRecortada, auditoria))
                return ProcessResult.Error("No se pudo actualizar el proveedor");

            return ProcessResult.Success("Proveedor actualizado correctamente");
        }
        catch (Exception ex)
        {
            return ProcessResult.Error($"Error al actualizar proveedor: {ex.Message}");
        }
    }

    /// <summary>
    /// Estado de cuenta del proveedor: datos, saldo pendiente e historial de pagos.
    /// Los pagos anteriores a la migración v8 no se pueden reconstruir.
    /// </summary>
    public ProcessResult<EstadoCuentaProveedor> ObtenerEstadoCuentaProveedor(int proveedorId)
    {
        var proveedor = _db.ObtenerProveedorPorId(proveedorId);
        if (proveedor == null)
            return ProcessResult<EstadoCuentaProveedor>.Error("Proveedor no encontrado");

        var pagos = _db.ObtenerPagosDeProveedor(proveedorId);
        var compras = _db.ObtenerComprasDeProveedor(proveedorId);

        return ProcessResult<EstadoCuentaProveedor>.OK(new EstadoCuentaProveedor
        {
            Proveedor = proveedor,
            Pagos = pagos,
            Compras = compras,
            TotalPagado = pagos.Sum(p => p.Monto),
            TotalComprado = compras.Sum(c => c.Total),
            SaldoPendiente = proveedor.SaldoPendiente
        });
    }

    /// <summary>
    /// Registra una compra a crédito con detalle de productos: aumenta el stock,
    /// actualiza el costo del catálogo al último costo y suma el total al saldo del
    /// proveedor. El pago se hace después con <see cref="RegistrarPagoProveedor"/>.
    /// </summary>
    public ProcessResult<CompraProveedor> RegistrarCompraProveedor(int proveedorId,
        IReadOnlyList<LineaCompraSolicitada> lineas, string observaciones = null)
    {
        var autorizacion = Autorizar(PermisoSistema.RegistrarCompra);
        if (autorizacion.IsError)
            return ProcessResult<CompraProveedor>.Error(autorizacion.Message);

        if (lineas == null || lineas.Count == 0)
            return ProcessResult<CompraProveedor>.Error("La compra debe tener al menos un producto");

        var proveedor = _db.ObtenerProveedorPorId(proveedorId);
        if (proveedor == null)
            return ProcessResult<CompraProveedor>.Error("Proveedor no encontrado");

        if (!proveedor.Activo)
            return ProcessResult<CompraProveedor>.Error(
                "El proveedor está inactivo; reactívalo para registrar compras");

        var renglones = new List<CompraProveedorLinea>();
        var vistos = new HashSet<int>();
        foreach (var solicitada in lineas)
        {
            if (solicitada == null || solicitada.ProductoId <= 0)
                return ProcessResult<CompraProveedor>.Error("Hay un producto inválido en la compra");

            if (solicitada.Cantidad <= 0)
                return ProcessResult<CompraProveedor>.Error(
                    "La cantidad de cada producto debe ser mayor a cero");

            if (solicitada.CostoUnitario < 0)
                return ProcessResult<CompraProveedor>.Error(
                    "El costo unitario no puede ser negativo");

            if (!vistos.Add(solicitada.ProductoId))
                return ProcessResult<CompraProveedor>.Error(
                    "La compra tiene el mismo producto repetido; únelo en un solo renglón");

            var producto = _db.ObtenerProductoPorId(solicitada.ProductoId);
            if (producto == null)
                return ProcessResult<CompraProveedor>.Error(
                    $"No se encontró el producto #{solicitada.ProductoId}");

            if (!producto.Activo)
                return ProcessResult<CompraProveedor>.Error(
                    $"El producto \"{producto.Nombre}\" está desactivado; reactívalo para comprarlo");

            renglones.Add(new CompraProveedorLinea
            {
                ProductoId = producto.Id,
                CodigoBarras = producto.CodigoBarras,
                Nombre = producto.Nombre,
                Cantidad = solicitada.Cantidad,
                CostoUnitario = solicitada.CostoUnitario,
                Importe = solicitada.Cantidad * solicitada.CostoUnitario
            });
        }

        var auditoria = new RegistroBitacora
        {
            Fecha = DateTime.Now,
            Usuario = SesionActual.Usuario?.Nombre ?? string.Empty,
            Accion = AccionBitacora.CompraProveedor,
            Entidad = "Proveedor",
            DatosAntes = $"Saldo={Dinero.Formatear(proveedor.SaldoPendiente)}",
            Motivo = string.IsNullOrWhiteSpace(observaciones) ? "Compra a proveedor" : observaciones
        };

        return _db.RegistrarCompraProveedor(proveedor.Id, renglones,
            SesionActual.Usuario?.Nombre ?? string.Empty, observaciones, auditoria);
    }

    public ProcessResult<RegistrarPagoProveedorResponse> RegistrarPagoProveedor(int proveedorId,
        long montoPago, string metodoPago)
    {
        var autorizacion = Autorizar(PermisoSistema.PagarProveedor);
        if (autorizacion.IsError)
            return ProcessResult<RegistrarPagoProveedorResponse>.Error(autorizacion.Message);

        if (montoPago <= 0)
            return ProcessResult<RegistrarPagoProveedorResponse>.Error("El pago debe ser mayor a cero");

        var metodo = MetodoPagoProveedor.Normalizar(metodoPago);
        if (!MetodoPagoProveedor.EsValido(metodo))
            return ProcessResult<RegistrarPagoProveedorResponse>.Error(
                "Selecciona un método de pago válido");

        var proveedor = _db.ObtenerProveedorPorId(proveedorId);
        if (proveedor == null)
            return ProcessResult<RegistrarPagoProveedorResponse>.Error("Proveedor no encontrado");

        if (montoPago > proveedor.SaldoPendiente)
            return ProcessResult<RegistrarPagoProveedorResponse>.Error(
                $"El pago (${Dinero.Formatear(montoPago)}) supera el saldo pendiente (${Dinero.Formatear(proveedor.SaldoPendiente)})");

        var nuevoSaldo = proveedor.SaldoPendiente - montoPago;
        var usuario = SesionActual.Usuario?.Nombre ?? string.Empty;
        var esEfectivo = metodo == MetodoPagoProveedor.Efectivo;
        var auditoria = new RegistroBitacora
        {
            Fecha = DateTime.Now,
            Usuario = usuario,
            Accion = AccionBitacora.PagoProveedor,
            Entidad = "Proveedor",
            DatosAntes = $"Saldo={Dinero.Formatear(proveedor.SaldoPendiente)}"
        };

        var registro = _db.RegistrarPagoProveedor(proveedor.Id, proveedor.Nombre, montoPago,
            proveedor.SaldoPendiente, nuevoSaldo, usuario, metodo, esEfectivo, auditoria);

        if (!registro.Exito)
            return ProcessResult<RegistrarPagoProveedorResponse>.Error("No se pudo registrar el pago");

        return ProcessResult<RegistrarPagoProveedorResponse>.OK(new RegistrarPagoProveedorResponse
        {
            ProveedorId = proveedor.Id,
            SaldoRestante = nuevoSaldo,
            MetodoPago = metodo,
            MovimientoCajaId = registro.MovimientoCajaId,
            Mensaje = esEfectivo
                ? "Pago registrado y retiro de caja generado"
                : "Pago registrado correctamente"
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
    public long SaldoRestante { get; set; }
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
    public long SaldoRestante { get; set; }
    public string MetodoPago { get; set; } = string.Empty;
    public int? MovimientoCajaId { get; set; }
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
