// Capa de acceso a datos local con SQLite
using System.Text;
using SistemaTienda.Core;

public class DatabaseTools
{
    private readonly string _connectionString;

    public DatabaseTools(string databasePath)
    {
        _connectionString = $"Data Source={databasePath}";
    }

    public System.Data.Common.DbConnection CreateConnection()
    {
        var conn = new System.Data.SQLite.SQLiteConnection(_connectionString);
        return conn;
    }

    public List<T> ExecuteReportObjects<T>(string sql, params System.Data.SQLite.SQLiteParameter[] parameters) where T : new()
    {
        var list = new List<T>();
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        if (parameters != null)
            foreach (var p in parameters)
                cmd.Parameters.Add(p);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var item = new T();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var prop = typeof(T).GetProperty(reader.GetName(i));
                if (prop != null && !prop.GetIsNullable() && reader.IsDBNull(i))
                    continue;
                if (prop != null) prop.SetValue(item, reader.GetValue(i));
            }
            list.Add(item);
        }
        return list;
    }

    public T? ExecuteReportObject<T>(string sql, params System.Data.SQLite.SQLiteParameter[] parameters) where T : new()
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        if (parameters != null)
            foreach (var p in parameters)
                cmd.Parameters.Add(p);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var item = new T();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var prop = typeof(T).GetProperty(reader.GetName(i));
                if (prop != null) prop.SetValue(item, reader.GetValue(i));
            }
            return item;
        }
        return default;
    }

    public int ExecuteCommand(string sql, params System.Data.SQLite.SQLiteParameter[] parameters)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        if (parameters != null)
            foreach (var p in parameters)
                cmd.Parameters.Add(p);
        return cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Crea las tablas de configuración e inventario si no existen
    /// </summary>
    public void InicializarTablasConfiguracionYGastos()
    {
        // Tabla de configuración global de la tienda
        ExecuteCommand(@"
            CREATE TABLE IF NOT EXISTS ConfiguracionTienda (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                NombreTienda TEXT NOT NULL DEFAULT '',
                Giro TEXT NOT NULL DEFAULT '',
                ImpuestoPredeterminado DECIMAL NOT NULL DEFAULT 16.00,
                ComisionTarjeta DECIMAL NOT NULL DEFAULT 3.50,
                FondoCajaInicial DECIMAL NOT NULL DEFAULT 200.00,
                FiadoActivado INTEGER NOT NULL DEFAULT 0,
                LimiCreditoCliente DECIMAL NOT NULL DEFAULT 0,
                RespaldoIntervaloDias INTEGER NOT NULL DEFAULT 1,
                UsuarioDueñoPINHash TEXT NOT NULL DEFAULT '',
                FechaCreacion DATETIME NOT NULL DEFAULT (datetime('now')),
                VersionApp TEXT NOT NULL DEFAULT ''
            )");

        // Tabla de usuarios del sistema
        ExecuteCommand(@"
            CREATE TABLE IF NOT EXISTS UsuariosSistema (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Nombre TEXT NOT NULL DEFAULT '',
                PINHash TEXT NOT NULL DEFAULT '',
                Rol INTEGER NOT NULL DEFAULT 0, -- 0=Dueño, 1=Encargado, 2=Empleado
                PermisosEspeciales TEXT NOT NULL DEFAULT '',
                FechaCreacion DATETIME NOT NULL DEFAULT (datetime('now'))
            )");

        // Tabla de productos
        ExecuteCommand(@"
            CREATE TABLE IF NOT EXISTS Productos (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CodigoBarras TEXT NOT NULL UNIQUE,
                Nombre TEXT NOT NULL,
                Costo DECIMAL NOT NULL DEFAULT 0,
                Precio DECIMAL NOT NULL DEFAULT 0,
                Stock INTEGER NOT NULL DEFAULT 0,
                StockMinimo INTEGER NOT NULL DEFAULT 5,
                Categoria TEXT NOT NULL DEFAULT '',
                Impuesto DECIMAL NOT NULL DEFAULT 16.00,
                FechaCreacion DATETIME NOT NULL DEFAULT (datetime('now')),
                UltimaActualizacion DATETIME
            )");

        // Tabla de movimientos de inventario
        ExecuteCommand(@"
            CREATE TABLE IF NOT EXISTS MovimientoInventario (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProductoId INTEGER NOT NULL,
                Tipo TEXT NOT NULL, -- 'ENTRADA', 'SALIDA', 'AJUSTE'
                Cantidad INTEGER NOT NULL,
                CostoUnitario DECIMAL NOT NULL DEFAULT 0,
                Motivo TEXT NOT NULL DEFAULT '',
                Usuario TEXT NOT NULL DEFAULT '',
                Fecha DATETIME NOT NULL DEFAULT (datetime('now')),
                FOREIGN KEY (ProductoId) REFERENCES Productos(Id)
            )");

        // **NUEVA TABLA: Gastos**
        ExecuteCommand(@"
            CREATE TABLE IF NOT EXISTS Gastos (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Concepto TEXT NOT NULL DEFAULT '',
                Monto DECIMAL NOT NULL DEFAULT 0,
                Fecha DATETIME NOT NULL DEFAULT (datetime('now')),
                MetodoPago TEXT NOT NULL DEFAULT '',
                Origen TEXT NOT NULL DEFAULT 'Caja', -- 'Caja' o 'Externo'
                Observaciones TEXT
            )");

        // **NUEVA TABLA: Clientes Fiado**
        ExecuteCommand(@"
            CREATE TABLE IF NOT EXISTS ClientesFiado (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Nombre TEXT NOT NULL DEFAULT '',
                Clave TEXT NOT NULL DEFAULT '', -- Cédula o RFC
                SaldoActual DECIMAL NOT NULL DEFAULT 0,
                FechaRegistro DATETIME NOT NULL DEFAULT (datetime('now')),
                UltimaActualizacion DATETIME
            )");

// **NUEVA TABLA: Proveedores
        ExecuteCommand(@"
            CREATE TABLE IF NOT EXISTS Proveedores (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Nombre TEXT NOT NULL DEFAULT '',
                Clave TEXT NOT NULL DEFAULT '', -- RFC o nombre comercial
                Telefono TEXT NOT NULL DEFAULT '',
                Direccion TEXT NOT NULL DEFAULT '',
                SaldoPendiente DECIMAL NOT NULL DEFAULT 0,
                FechaRegistro DATETIME NOT NULL DEFAULT (datetime('now')),
                UltimaActualizacion DATETIME
            )");

        // Índices para rendimiento
        ExecuteCommand("CREATE INDEX IF NOT EXISTS IX_Productos_CodigoBarras ON Productos(CodigoBarras)");
        ExecuteCommand("CREATE INDEX IF NOT EXISTS IX_Productos_Nombre ON Productos(Nombre)");
        ExecuteCommand("CREATE INDEX IF NOT EXISTS IX_Productos_Stock ON Productos(Stock)");
        ExecuteCommand("CREATE INDEX IF NOT EXISTS IX_MovimientoInventario_ProductoId ON MovimientoInventario(ProductoId)");
        ExecuteCommand("CREATE INDEX IF NOT EXISTS IX_Gastos_Fecha ON Gastos(Fecha)");
        ExecuteCommand("CREATE INDEX IF NOT EXISTS IX_ClientesFiado_Nombre ON ClientesFiado(Nombre)");
        ExecuteCommand("CREATE INDEX IF NOT EXISTS IX_Proveedores_Nombre ON Proveedores(Nombre)");

        // Insertar configuración por defecto si la tabla está vacía
        var existente = ExecuteReportObject<ConfiguracionTienda>("SELECT Id FROM ConfiguracionTienda LIMIT 1");
        if (existente == null)
        {
            ExecuteCommand(@"
                INSERT INTO ConfiguracionTienda (NombreTienda, Giro, ImpuestoPredeterminado, ComisionTarjeta, FondoCajaInicial, FiadoActivado, LimiCreditoCliente, RespaldoIntervaloDias, UsuarioDueñoPINHash, VersionApp)
                VALUES ('Mi Tienda', 'Abarrotes', 16.00, 3.50, 200.00, 0, 0, 1, '', 'v1.0.0')");
        }
    }

    /// <summary>
    /// Obtiene la configuración actual de la tienda
    /// </summary>
    public ConfiguracionTienda ObtenerConfiguracion() => ExecuteReportObject<ConfiguracionTienda>("SELECT * FROM ConfiguracionTienda WHERE Id = 1");

    /// <summary>
    /// Actualiza la configuración de la tienda
    /// </summary>
    public bool ActualizarConfiguracion(ConfiguracionTienda config)
    {
        try
        {
            ExecuteCommand(@"
                UPDATE ConfiguracionTienda 
                SET NombreTienda = @nom, Giro = @giro, ImpuestoPredeterminado = @imp, 
                    ComisionTarjeta = @comis, FondoCajaInicial = @fondo, FiadoActivado = @fiado,
                    LimiCreditoCliente = @limite, RespaldoIntervaloDias = @intervalo, 
                    UsuarioDueñoPINHash = @pinHash, VersionApp = @ver
                WHERE Id = 1", 
                new System.Data.SQLite.SQLiteParameter("@nom", config.NombreTienda),
                new System.Data.SQLite.SQLiteParameter("@giro", config.Giro),
                new System.Data.SQLite.SQLiteParameter("@imp", (object)config.ImpuestoPredeterminado ?? DBNull.Value),
                new System.Data.SQLite.SQLiteParameter("@comis", (object)config.ComisionTarjeta ?? DBNull.Value),
                new System.Data.SQLite.SQLiteParameter("@fondo", (object)config.FondoCajaInicial ?? DBNull.Value),
                new System.Data.SQLite.SQLiteParameter("@fiado", config.FiadoActivado ? 1 : 0),
                new System.Data.SQLite.SQLiteParameter("@limite", (object)config.LimiCreditoCliente ?? DBNull.Value),
                new System.Data.SQLite.SQLiteParameter("@intervalo", config.RespaldoIntervaloDias),
                new System.Data.SQLite.SQLiteParameter("@pinHash", config.UsuarioDueñoPINHash),
                new System.Data.SQLite.SQLiteParameter("@ver", config.VersionApp));
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Agrega un nuevo gasto
    /// </summary>
    public bool AgregarGasto(Gasto gasto)
    {
        try
        {
            ExecuteCommand(@"
                INSERT INTO Gastos (Concepto, Monto, Fecha, MetodoPago, Origen, Observaciones)
                VALUES (@con, @mon, @fecha, @met, @ori, @obs)", 
                new System.Data.SQLite.SQLiteParameter("@con", gasto.Concepto),
                new System.Data.SQLite.SQLiteParameter("@mon", (object)gasto.Monto ?? DBNull.Value),
                new System.Data.SQLite.SQLiteParameter("@fecha", gasto.Fecha ?? DateTime.Now),
                new System.Data.SQLite.SQLiteParameter("@met", gasto.MetodoPago ?? ""),
                new System.Data.SQLite.SQLiteParameter("@ori", gasto.Origen ?? "Caja"),
                new System.Data.SQLite.SQLiteParameter("@obs", (object)gasto.Observaciones ?? DBNull.Value));
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Obtiene todos los gastos registrados
    /// </summary>
    public List<Gasto> ObtenerGastos() => ExecuteReportObjects<Gasto>("SELECT * FROM Gastos ORDER BY Fecha DESC");

    /// <summary>
    /// Obtiene gastos por fecha o método de pago
    /// </summary>
    public List<Gasto> ObtenerGastosPorFiltro(string metodoPago = null, string origen = null)
    {
        string sql = "SELECT * FROM Gastos WHERE 1=1";
        var parameters = new List<System.Data.SQLite.SQLiteParameter>();
        
        if (!string.IsNullOrEmpty(metodoPago))
        {
            sql += " AND MetodoPago = @met";
            parameters.Add(new System.Data.SQLite.SQLiteParameter("@met", metodoPago));
        }
        
        if (!string.IsNullOrEmpty(origen))
        {
            sql += " AND Origen = @ori";
            parameters.Add(new System.Data.SQLite.SQLiteParameter("@ori", origen));
        }

        return ExecuteReportObjects<Gasto>(sql, parameters.ToArray().ToArray());
    }

    /// <summary>
    /// Obtiene el total de gastos en un período
    /// </summary>
    public decimal ObtenerTotalGastos() => ExecuteReportObject<decimal>("SELECT ISNULL(SUM(Monto), 0) FROM Gastos");

    /// <summary>
    /// Obtiene gastos por categoría (concepto)
    /// </summary>
    public List<(string Concepto, decimal Total)> ObtenerGastosPorConcepto()
    {
        return ExecuteReportObjects<(string Concepto, decimal Total)>(
            "SELECT Concepto, ISNULL(SUM(Monto), 0) as Total FROM Gastos GROUP BY Concepto ORDER BY Total DESC");
    }

    /// <summary>
    /// Agrega un nuevo cliente fiado
    /// </summary>
    public bool AgregarClienteFiado(ClienteFiado cliente)
    {
        try
        {
            ExecuteCommand(@"
                INSERT INTO ClientesFiado (Nombre, Clave, SaldoActual, FechaRegistro, UltimaActualizacion)
                VALUES (@nom, @clv, @sal, @fec, @ult)", 
                new System.Data.SQLite.SQLiteParameter("@nom", cliente.Nombre),
                new System.Data.SQLite.SQLiteParameter("@clv", cliente.Clave),
                new System.Data.SQLite.SQLiteParameter("@sal", (object)cliente.SaldoActual ?? DBNull.Value),
                new System.Data.SQLite.SQLiteParameter("@fec", cliente.FechaRegistro),
                new System.Data.SQLite.SQLiteParameter("@ult", cliente.UltimaActualizacion));
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Obtiene todos los clientes fiado
    /// </summary>
    public List<ClienteFiado> ObtenerTodosLosClientes() => ExecuteReportObjects<ClienteFiado>("SELECT * FROM ClientesFiado ORDER BY Nombre");

    /// <summary>
    /// Obtiene clientes con saldo pendiente
    /// </summary>
    public List<ClienteFiado> ObtenerClientesConSaldo() => ExecuteReportObjects<ClienteFiado>("SELECT * FROM ClientesFiado WHERE SaldoActual > 0 ORDER BY Nombre");

    /// <summary>
    /// Obtiene un cliente fiado por ID
    /// </summary>
    public ClienteFiado? ObtenerClienteFiadoPorId(int clienteId) => ExecuteReportObject<ClienteFiado>("SELECT * FROM ClientesFiado WHERE Id = @id", new System.Data.SQLite.SQLiteParameter("@id", clienteId));

    /// <summary>
    /// Actualiza el saldo de un cliente después de un abono
    /// </summary>
    public bool ActualizarSaldoCliente(ClienteFiado cliente)
    {
        try
        {
            ExecuteCommand(@"
                UPDATE ClientesFiado 
                SET SaldoActual = @sal, UltimaActualizacion = datetime('now')
                WHERE Id = @id", 
                new System.Data.SQLite.SQLiteParameter("@id", cliente.Id),
                new System.Data.SQLite.SQLiteParameter("@sal", (object)cliente.SaldoActual ?? DBNull.Value));
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Verifica si un cliente existe
    /// </summary>
    public bool ClienteExiste(string nombre) => ExecuteReportObject<int?>("SELECT COUNT(*) FROM ClientesFiado WHERE Nombre = @nom", new System.Data.SQLite.SQLiteParameter("@nom", nombre)) > 0;

    /// <summary>
    /// Agrega un nuevo proveedor
    /// </summary>
    public bool AgregarProveedor(Proveedor proveedor)
    {
        try
        {
            ExecuteCommand(@"
                INSERT INTO Proveedores (Nombre, Clave, Telefono, Direccion, SaldoPendiente, FechaRegistro, UltimaActualizacion)
                VALUES (@nom, @clv, @tel, @dir, @sal, @fec, @ult)", 
                new System.Data.SQLite.SQLiteParameter("@nom", proveedor.Nombre),
                new System.Data.SQLite.SQLiteParameter("@clv", proveedor.Clave),
                new System.Data.SQLite.SQLiteParameter("@tel", proveedor.Telefono),
                new System.Data.SQLite.SQLiteParameter("@dir", proveedor.Direccion),
                new System.Data.SQLite.SQLiteParameter("@sal", (object)proveedor.SaldoPendiente ?? DBNull.Value),
                new System.Data.SQLite.SQLiteParameter("@fec", proveedor.FechaRegistro),
                new System.Data.SQLite.SQLiteParameter("@ult", proveedor.UltimaActualizacion));
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Obtiene todos los proveedores
    /// </summary>
    public List<Proveedor> ObtenerTodosLosProveedores() => ExecuteReportObjects<Proveedor>("SELECT * FROM Proveedores ORDER BY Nombre");

    /// <summary>
    /// Obtiene el saldo pendiente de un proveedor
    /// </summary>
    public decimal ObtenerSaldoProveedor(int proveedorId) => ExecuteReportObject<decimal>("SELECT ISNULL(SaldoPendiente, 0) FROM Proveedores WHERE Id = @id", new System.Data.SQLite.SQLiteParameter("@id", proveedorId));

    /// <summary>
    /// Actualiza el saldo de un proveedor después de un pago
    /// </summary>
    public bool ActualizarSaldoProveedor(Proveedor proveedor)
    {
        try
        {
            ExecuteCommand(@"
                UPDATE Proveedores 
                SET SaldoPendiente = @sal, UltimaActualizacion = datetime('now')
                WHERE Id = @id", 
                new System.Data.SQLite.SQLiteParameter("@id", proveedor.Id),
                new System.Data.SQLite.SQLiteParameter("@sal", (object)proveedor.SaldoPendiente ?? DBNull.Value));
            return true;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Entidad para cliente fiado
/// </summary>
public class ClienteFiado
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Clave { get; set; } = string.Empty; // Cédula o RFC
    public decimal SaldoActual { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.Now;
    public DateTime? UltimaActualizacion { get; set; }
}

/// <summary>
/// Entidad para proveedor
/// </summary>
public class Proveedor
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Clave { get; set; } = string.Empty; // RFC o nombre comercial
    public string Telefono { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public decimal SaldoPendiente { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.Now;
    public DateTime? UltimaActualizacion { get; set; }
}