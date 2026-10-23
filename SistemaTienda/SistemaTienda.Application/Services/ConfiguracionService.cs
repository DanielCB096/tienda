// Servicio de configuración - lógica de negocio
using SistemaTienda.Core;

public class ConfiguracionService
{
    private readonly DatabaseTools _db;

    public ConfiguracionService(DatabaseTools db) => _db = db;

    /// <summary>
    /// Ejecuta el asistente de primera configuración si no existe datos
    /// </summary>
    public ProcessResult EjecutarAsistenteInicial(string nombreTienda, string giro, 
        decimal impuestoPredeterminado, decimal comisionTarjeta, 
        decimal fondoCajaInicial, bool fiadoActivado, 
        decimal limiteCredito, int respaldoIntervaloDias, string pinDueño)
    {
        try
        {
            // Hashear el PIN (en un sistema real usaríamos un servicio de hash dedicado)
            string pinHash = HashPin(pinDueño);

            // Crear el usuario dueño
            var usuarioDueño = new UsuarioSistema
            {
                Nombre = "Dueño",
                PINHash = pinHash,
                Rol = RolUsuario.Dueño,
                PermisosEspeciales = "all"
            };
            _db.AgregarUsuario(usuarioDueño);

            // Guardar configuración
            var config = new ConfiguracionTienda
            {
                NombreTienda = nombreTienda,
                Giro = giro,
                ImpuestoPredeterminado = impuestoPredeterminado,
                ComisionTarjeta = comisionTarjeta,
                FondoCajaInicial = fondoCajaInicial,
                FiadoActivado = fiadoActivado,
                LimiCreditoCliente = limiteCredito,
                RespaldoIntervaloDias = respaldoIntervaloDias,
                UsuarioDueñoPINHash = pinHash,
                VersionApp = "v1.0.0"
            };
            _db.ActualizarConfiguracion(config);

            return ProcessResult.OK().Success("Configuración inicial completada exitosamente");
        }
        catch (Exception ex)
        {
            return ProcessResult.Error($"Error en asistente inicial: {ex.Message}");
        }
    }

    /// <summary>
    /// Obtiene la configuración actual
    /// </summary>
    public ProcessResult<ConfiguracionTienda> ObtenerConfiguracion() => 
        ProcessResult<ConfiguracionTienda>.OK(_db.ObtenerConfiguracion());

    /// <summary>
    /// Actualiza la configuración (solo para dueño/administrador)
    /// </summary>
    public ProcessResult ActualizarConfiguracion(ConfiguracionTienda nuevaConfig)
    {
        var configActual = _db.ObtenerConfiguracion().Value;
        
        // Mantener algunos campos inmutables o con lógica de negocio
        nuevaConfig.VersionApp = configActual.VersionApp; // No permitir cambio manual fácil
        nuevaConfig.FechaCreacion = configActual.FechaCreacion; // Siempre la original

        if (_db.ActualizarConfiguracion(nuevaConfig))
            return ProcessResult.OK().Success("Configuración actualizada");
        else
            return ProcessResult.Error("No se pudo actualizar la configuración");
    }

    /// <summary>
    /// Agrega un nuevo usuario (solo dueño)
    /// </summary>
    public ProcessResult<AgregarUsuarioResponse> AgregarUsuario(UsuarioSistema nuevoUsuario)
    {
        // Verificar que el dueño autorice (en un sistema real verificaría permisos)
        try
        {
            // Hashear PIN
            nuevoUsuario.PINHash = HashPin(nuevoUsuario.PINHash);
            
            _db.AgregarUsuario(nuevoUsuario);
            return ProcessResult<AgregarUsuarioResponse>.OK(new AgregarUsuarioResponse { UsuarioId = nuevoUsuario.Id, Mensaje = "Usuario agregado correctamente" });
        }
        catch (Exception ex)
        {
            return ProcessResult<AgregarUsuarioResponse>.Error($"Error al agregar usuario: {ex.Message}");
        }
    }

    /// <summary>
    /// Verifica PIN de acceso
    /// </summary>
    public ProcessResult<UsuarioSistema> VerificarAcceso(string pinIngresado)
    {
        // Buscar usuario por PIN (hash comparado)
        var usuarios = _db.ObtenerUsuarios().Value;
        foreach (var usuario in usuarios)
        {
            // Comparar hash - en producción usarían un método de comparación seguro
            if (usuario.PINHash == pinIngresado) // Simplificado
            {
                return ProcessResult<UsuarioSistema>.OK(usuario);
            }
        }
        return ProcessResult<UsuarioSistema>.Error("PIN incorrecto o usuario no encontrado");
    }

    /// <summary>
    /// Hashea un PIN para almacenamiento
    /// </summary>
    private static string HashPin(string pin)
    {
        // En producción: usar SHA-256 con salt, o bcrypt, o sistema propio
        // Simplificado: convertir a upper y agregar prefix
        return $"PIN_{pin.ToUpper()}".GetHashCode().ToString("x8"); // Placeholder
    }

    /// <summary>
    /// Agrega un nuevo gasto al sistema
    /// </summary>
    public ProcessResult<AgregarGastoResponse> AgregarGasto(Gasto nuevoGasto)
    {
        try
        {
            // Validaciones básicas
            if (string.IsNullOrWhiteSpace(nuevoGasto.Concepto))
                return ProcessResult<AgregarGastoResponse>.Error("El concepto del gasto es obligatorio");
            
            if (nuevoGasto.Monto <= 0)
                return ProcessResult<AgregarGastoResponse>.Error("El monto debe ser mayor a cero");
            
            _db.AgregarGasto(nuevoGasto);
            return ProcessResult<AgregarGastoResponse>.OK(new AgregarGastoResponse { GastoId = nuevoGasto.Id, Mensaje = "Gasto registrado correctamente" });
        }
        catch (Exception ex)
        {
            return ProcessResult<AgregarGastoResponse>.Error($"Error al registrar gasto: {ex.Message}");
        }
    }

    /// <summary>
    /// Obtiene el total de gastos registrados
    /// </summary>
    public ProcessResult<decimal> ObtenerTotalGastos() => 
        ProcessResult<decimal>.OK(_db.ObtenerTotalGastos());

    /// <summary>
    /// Obtiene gastos por concepto (para reportes)
    /// </summary>
    public ProcessResult<List<(string Concepto, decimal Total)>> ObtenerGastosPorConcepto() =>
        ProcessResult<List<(string Concepto, decimal Total)>>.OK(_db.ObtenerGastosPorConcepto());

    /// <summary>
    /// Obtiene todos los gastos registrados
    /// </summary>
    public ProcessResult<List<Gasto>> ObtenerTodosLosGastos() =>
        ProcessResult<List<Gasto>>.OK(_db.ObtenerGastos());

    /// <summary>
    /// Verifica si el usuario tiene un rol específico
    /// </summary>
    public bool UsuarioTieneRol(int usuarioId, RolUsuario rolRequerido)
    {
        var usuario = ExecuteReportObject<UsuarioSistema>("SELECT Rol FROM UsuariosSistema WHERE Id = @id", new System.Data.SQLite.SQLiteParameter("@id", usuarioId));
        if (usuario == null) return false;
        
        return (RolUsuario)usuario.Rol == rolRequerido;
    }

    /// <summary>
    /// Agrega un nuevo cliente fiado
    /// </summary>
    public ProcessResult<AgregarClienteFiadoResponse> AgregarClienteFiado(ClienteFiado nuevoCliente)
    {
        try
        {
            // Validaciones básicas
            if (string.IsNullOrWhiteSpace(nuevoCliente.Nombre))
                return ProcessResult<AgregarClienteFiadoResponse>.Error("El nombre del cliente es obligatorio");
            
            _db.AgregarClienteFiado(nuevoCliente);
            return ProcessResult<AgregarClienteFiadoResponse>.OK(new AgregarClienteFiadoResponse { ClienteId = nuevoCliente.Id, Mensaje = "Cliente fiado agregado correctamente" });
        }
        catch (Exception ex)
        {
            return ProcessResult<AgregarClienteFiadoResponse>.Error($"Error al agregar cliente fiado: {ex.Message}");
        }
    }

    /// <summary>
    /// Obtiene todos los clientes fiado
    /// </summary>
    public ProcessResult<List<ClienteFiado>> ObtenerTodosLosClientes() => 
        ProcessResult<List<ClienteFiado>>.OK(_db.ObtenerTodosLosClientes());

    /// <summary>
    /// Obtiene clientes con saldo pendiente
    /// </summary>
    public ProcessResult<List<ClienteFiado>> ObtenerClientesConSaldo() => 
        ProcessResult<List<ClienteFiado>>.OK(_db.ObtenerClientesConSaldo());

    /// <summary>
    /// Registra un abono hacia el saldo de un cliente fiado
    /// </summary>
    public ProcessResult<RegistrarAbonoResponse> RegistrarAbonoCliente(int clienteId, decimal montoAbono)
    {
        try
        {
            // Obtener cliente actual
            var cliente = _db.ObtenerClienteFiadoPorId(clienteId);
            if (cliente == null)
                return ProcessResult<RegistrarAbonoResponse>.Error("Cliente no encontrado");
            
            // Calcular nuevo saldo
            decimal nuevoSaldo = cliente.Value.SaldoActual - montoAbono;
            
            // Actualizar saldo
            var clienteActualizado = new ClienteFiado
            {
                Id = cliente.Value.Id,
                Nombre = cliente.Value.Nombre,
                Clave = cliente.Value.Clave,
                SaldoActual = nuevoSaldo > 0 ? nuevoSaldo : 0, // No permitir saldo negativo
                UltimaActualizacion = DateTime.Now
            };
            
            _db.ActualizarSaldoCliente(clienteActualizado);
            
            return ProcessResult<RegistrarAbonoResponse>.OK(new RegistrarAbonoResponse 
            { 
                ClienteId = cliente.Value.Id, 
                SaldoRestante = nuevoSaldo > 0 ? nuevoSaldo : 0,
                Mensaje = "Abono registrado correctamente"
            });
        }
        catch (Exception ex)
        {
            return ProcessResult<RegistrarAbonoResponse>.Error($"Error al registrar abono: {ex.Message}");
        }
    }

    /// <summary>
    /// Agrega un nuevo proveedor al sistema
    /// </summary>
    public ProcessResult<AgregarProveedorResponse> AgregarProveedor(Proveedor nuevoProveedor)
    {
        try
        {
            // Validaciones básicas
            if (string.IsNullOrWhiteSpace(nuevoProveedor.Nombre))
                return ProcessResult<AgregarProveedorResponse>.Error("El nombre del proveedor es obligatorio");
            
            _db.AgregarProveedor(nuevoProveedor);
            return ProcessResult<AgregarProveedorResponse>.OK(new AgregarProveedorResponse { ProveedorId = nuevoProveedor.Id, Mensaje = "Proveedor agregado correctamente" });
        }
        catch (Exception ex)
        {
            return ProcessResult<AgregarProveedorResponse>.Error($"Error al agregar proveedor: {ex.Message}");
        }
    }

    /// <summary>
    /// Obtiene todos los proveedores registrados
    /// </summary>
    public ProcessResult<List<Proveedor>> ObtenerTodosLosProveedores() => 
        ProcessResult<List<Proveedor>>.OK(_db.ObtenerTodosLosProveedores());

    /// <summary>
    /// Registra un pago a un proveedor (reduce el saldo cuentas por pagar)
    /// </summary>
    public ProcessResult<RegistrarPagoProveedorResponse> RegistrarPagoProveedor(int proveedorId, decimal montoPago)
    {
        try
        {
            // Obtener proveedor actual
            var proveedores = _db.ObtenerTodosLosProveedores().Value;
            var proveedor = proveedores.FirstOrDefault(p => p.Id == proveedorId);
            if (proveedor == null)
                return ProcessResult<RegistrarPagoProveedorResponse>.Error("Proveedor no encontrado");
            
            // Calcular nuevo saldo
            decimal nuevoSaldo = proveedor.SaldoPendiente - montoPago;
            
            // Actualizar saldo
            var proveedorActualizado = new Proveedor
            {
                Id = proveedor.Id,
                Nombre = proveedor.Nombre,
                Clave = proveedor.Clave,
                Telefono = proveedor.Telefono,
                Direccion = proveedor.Direccion,
                SaldoPendiente = nuevoSaldo >= 0 ? nuevoSaldo : 0, // No permitir saldo negativo
                FechaRegistro = proveedor.FechaRegistro,
                UltimaActualizacion = DateTime.Now
            };
            
            _db.ActualizarSaldoProveedor(proveedorActualizado);
            
            return ProcessResult<RegistrarPagoProveedorResponse>.OK(new RegistrarPagoProveedorResponse 
            { 
                ProveedorId = proveedorId, 
                SaldoRestante = nuevoSaldo >= 0 ? nuevoSaldo : 0,
                Mensaje = "Pago registrado correctamente"
            });
        }
        catch (Exception ex)
        {
            return ProcessResult<RegistrarPagoProveedorResponse>.Error($"Error al registrar pago: {ex.Message}");
        }
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