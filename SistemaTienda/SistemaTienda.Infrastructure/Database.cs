// Capa de acceso a datos local con SQLite
using System.Globalization;
using Microsoft.Data.Sqlite;
using SistemaTienda.Core;

namespace SistemaTienda.Infrastructure;

public class DatabaseTools
{
    private readonly string _connectionString;

    public string RutaBaseDatos { get; }
    public string DirectorioRespaldos { get; }
    public string UltimoRespaldoCreado { get; private set; } = string.Empty;

    public DatabaseTools(string databasePath)
    {
        RutaBaseDatos = Path.GetFullPath(databasePath);
        var directory = Path.GetDirectoryName(RutaBaseDatos);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        DirectorioRespaldos = RespaldoLocal.DirectorioPorDefecto(RutaBaseDatos);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = RutaBaseDatos,
            ForeignKeys = true,
            DefaultTimeout = 30
        }.ToString();
    }

    public SqliteConnection CreateConnection() => new(_connectionString);

    /// <summary>
    /// Crea el esquema base si no existe y aplica las migraciones pendientes.
    /// Antes de migrar se crea un respaldo; si algo falla, la base queda en su versión anterior.
    /// </summary>
    public void InicializarBaseDeDatos()
    {
        var esquemaCreado = false;

        using (var conn = CreateConnection())
        {
            conn.Open();

            if (!ExisteTabla(conn, "ConfiguracionTienda"))
            {
                CrearEsquemaBase(conn);
                esquemaCreado = true;
            }
            else if (LeerVersion(conn) == 0)
            {
                // Bases anteriores al versionado: ya tienen todas las tablas de la v1.
                EscribirVersion(conn, null, Migraciones.VersionEsquemaBase);
            }
        }

        var migrador = new MigradorEsquema(RutaBaseDatos, Migraciones.ObtenerTodas(), DirectorioRespaldos);
        var resultado = migrador.Migrar(crearRespaldoPrevio: !esquemaCreado);
        if (resultado.IsError)
            throw new InvalidOperationException(resultado.Message);

        UltimoRespaldoCreado = resultado.Value?.RutaRespaldo ?? string.Empty;

        if ((esquemaCreado || resultado.Value?.HuboCambios == true) && !IntegridadOk())
            throw new InvalidOperationException("La base de datos no pasó la verificación de integridad.");
    }

    private static void CrearEsquemaBase(SqliteConnection conn)
    {
        using var tx = conn.BeginTransaction();

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
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
            );

            CREATE TABLE IF NOT EXISTS UsuariosSistema (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Nombre TEXT NOT NULL DEFAULT '',
                PINHash TEXT NOT NULL DEFAULT '',
                Rol INTEGER NOT NULL DEFAULT 2,
                PermisosEspeciales TEXT NOT NULL DEFAULT '',
                FechaCreacion DATETIME NOT NULL DEFAULT (datetime('now'))
            );

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
            );

            CREATE TABLE IF NOT EXISTS MovimientoInventario (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProductoId INTEGER NOT NULL,
                Tipo TEXT NOT NULL,
                Cantidad INTEGER NOT NULL,
                CostoUnitario DECIMAL NOT NULL DEFAULT 0,
                Motivo TEXT NOT NULL DEFAULT '',
                Usuario TEXT NOT NULL DEFAULT '',
                Fecha DATETIME NOT NULL DEFAULT (datetime('now')),
                FOREIGN KEY (ProductoId) REFERENCES Productos(Id)
            );

            CREATE TABLE IF NOT EXISTS Gastos (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Concepto TEXT NOT NULL DEFAULT '',
                Monto DECIMAL NOT NULL DEFAULT 0,
                Fecha DATETIME NOT NULL DEFAULT (datetime('now')),
                MetodoPago TEXT NOT NULL DEFAULT '',
                Origen TEXT NOT NULL DEFAULT 'Caja',
                Observaciones TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS ClientesFiado (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Nombre TEXT NOT NULL DEFAULT '',
                Telefono TEXT NOT NULL DEFAULT '',
                SaldoActual DECIMAL NOT NULL DEFAULT 0,
                FechaRegistro DATETIME NOT NULL DEFAULT (datetime('now')),
                UltimaActualizacion DATETIME
            );

            CREATE TABLE IF NOT EXISTS Proveedores (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Nombre TEXT NOT NULL DEFAULT '',
                Clave TEXT NOT NULL DEFAULT '',
                Telefono TEXT NOT NULL DEFAULT '',
                Direccion TEXT NOT NULL DEFAULT '',
                SaldoPendiente DECIMAL NOT NULL DEFAULT 0,
                FechaRegistro DATETIME NOT NULL DEFAULT (datetime('now')),
                UltimaActualizacion DATETIME
            );

            CREATE INDEX IF NOT EXISTS IX_Productos_CodigoBarras ON Productos(CodigoBarras);
            CREATE INDEX IF NOT EXISTS IX_Productos_Nombre ON Productos(Nombre);
            CREATE INDEX IF NOT EXISTS IX_Productos_Stock ON Productos(Stock);
            CREATE INDEX IF NOT EXISTS IX_MovimientoInventario_ProductoId ON MovimientoInventario(ProductoId);
            CREATE INDEX IF NOT EXISTS IX_MovimientoInventario_Fecha ON MovimientoInventario(Fecha);
            CREATE INDEX IF NOT EXISTS IX_Gastos_Fecha ON Gastos(Fecha);
            CREATE INDEX IF NOT EXISTS IX_ClientesFiado_Nombre ON ClientesFiado(Nombre);
            CREATE INDEX IF NOT EXISTS IX_Proveedores_Nombre ON Proveedores(Nombre);
        ";
            cmd.ExecuteNonQuery();
        }

        using (var insert = conn.CreateCommand())
        {
            insert.Transaction = tx;
            insert.CommandText = @"
                INSERT INTO ConfiguracionTienda
                    (NombreTienda, Giro, ImpuestoPredeterminado, ComisionTarjeta, FondoCajaInicial,
                     FiadoActivado, LimiCreditoCliente, RespaldoIntervaloDias, VersionApp)
                VALUES ('Mi Tienda', @giro, @impuesto, @comision, @fondo, 0, 0, @intervalo, 'v0.1.0')";
            insert.Parameters.AddWithValue("@giro", ConfiguracionPorDefecto.GiroPredeterminado);
            insert.Parameters.AddWithValue("@impuesto", ConfiguracionPorDefecto.ImpuestoPredeterminado);
            insert.Parameters.AddWithValue("@comision", ConfiguracionPorDefecto.ComisionTarjetaPredeterminada);
            insert.Parameters.AddWithValue("@fondo",
                Dinero.APesos(ConfiguracionPorDefecto.FondoCajaInicialPredeterminado));
            insert.Parameters.AddWithValue("@intervalo", ConfiguracionPorDefecto.RespaldoIntervaloDiasPredeterminado);
            insert.ExecuteNonQuery();
        }

        EscribirVersion(conn, tx, Migraciones.VersionEsquemaBase);
        tx.Commit();
    }

    private static bool ExisteTabla(SqliteConnection conn, string nombre)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @nombre";
        cmd.Parameters.AddWithValue("@nombre", nombre);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static int LeerVersion(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static void EscribirVersion(SqliteConnection conn, SqliteTransaction tx, int version)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"PRAGMA user_version = {version}";
        cmd.ExecuteNonQuery();
    }

    private bool IntegridadOk()
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA integrity_check";
        return string.Equals(Convert.ToString(cmd.ExecuteScalar()), "ok", StringComparison.OrdinalIgnoreCase);
    }

    // ---------- Consultas genéricas ----------

    public List<T> ExecuteReportObjects<T>(string sql, params SqliteParameter[] parameters) where T : new()
    {
        using var conn = CreateConnection();
        conn.Open();
        return ExecuteReportObjects<T>(conn, sql, parameters);
    }

    private static List<T> ExecuteReportObjects<T>(SqliteConnection conn, string sql, params SqliteParameter[] parameters) where T : new()
    {
        var list = new List<T>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        if (parameters != null)
            cmd.Parameters.AddRange(parameters);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var item = new T();
            var props = typeof(T).GetProperties();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var prop = props.FirstOrDefault(p => p.Name.Equals(reader.GetName(i), StringComparison.OrdinalIgnoreCase));
                if (prop == null || reader.IsDBNull(i)) continue;
                prop.SetValue(item, ConvertValue(reader.GetValue(i), prop.PropertyType));
            }
            list.Add(item);
        }
        return list;
    }

    public T ExecuteReportObject<T>(string sql, params SqliteParameter[] parameters) where T : new()
    {
        using var conn = CreateConnection();
        conn.Open();
        return ExecuteReportObject<T>(conn, sql, parameters);
    }

    private static T ExecuteReportObject<T>(SqliteConnection conn, string sql, params SqliteParameter[] parameters) where T : new()
    {
        var list = ExecuteReportObjects<T>(conn, sql, parameters);
        return list.Count > 0 ? list[0] : default;
    }

    public T ExecuteScalar<T>(string sql, params SqliteParameter[] parameters)
    {
        using var conn = CreateConnection();
        conn.Open();
        return ExecuteScalar<T>(conn, sql, parameters);
    }

    private static T ExecuteScalar<T>(SqliteConnection conn, string sql, params SqliteParameter[] parameters)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        if (parameters != null)
            cmd.Parameters.AddRange(parameters);

        var result = cmd.ExecuteScalar();
        if (result == null || result == DBNull.Value)
            return default;

        return (T)ConvertValue(result, typeof(T));
    }

    public int ExecuteCommand(string sql, params SqliteParameter[] parameters)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        if (parameters != null)
            cmd.Parameters.AddRange(parameters);
        return cmd.ExecuteNonQuery();
    }

    private static object ConvertValue(object value, Type targetType)
    {
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlying == typeof(string))
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

        if (underlying == typeof(bool))
        {
            if (value is string s)
                return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
            return Convert.ToInt64(value, CultureInfo.InvariantCulture) != 0;
        }

        if (underlying == typeof(long))
            return Convert.ToInt64(value, CultureInfo.InvariantCulture);

        if (underlying == typeof(decimal))
            return Convert.ToDecimal(value, CultureInfo.InvariantCulture);

        if (underlying == typeof(DateTime))
        {
            if (value is DateTime dt) return dt;
            return DateTime.Parse(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
                CultureInfo.InvariantCulture, DateTimeStyles.None);
        }

        if (underlying.IsEnum)
            return Enum.ToObject(underlying, Convert.ToInt64(value, CultureInfo.InvariantCulture));

        return Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
    }

    // ---------- Configuración ----------

    public ConfiguracionTienda ObtenerConfiguracion() =>
        ExecuteReportObject<ConfiguracionTienda>("SELECT * FROM ConfiguracionTienda ORDER BY Id LIMIT 1");

    public bool ActualizarConfiguracion(ConfiguracionTienda config)
    {
        var filas = ExecuteCommand(@"
            UPDATE ConfiguracionTienda
            SET NombreTienda = @nom, Giro = @giro, ImpuestoPredeterminado = @imp,
                ComisionTarjeta = @comis, FondoCajaInicial = @fondo, FiadoActivado = @fiado,
                LimiCreditoCliente = @limite, RespaldoIntervaloDias = @intervalo,
                VersionApp = @ver
            WHERE Id = @id",
            new SqliteParameter("@id", config.Id),
            new SqliteParameter("@nom", config.NombreTienda),
            new SqliteParameter("@giro", config.Giro),
            new SqliteParameter("@imp", config.ImpuestoPredeterminado),
            new SqliteParameter("@comis", config.ComisionTarjeta),
            new SqliteParameter("@fondo", config.FondoCajaInicial),
            new SqliteParameter("@fiado", config.FiadoActivado ? 1 : 0),
            new SqliteParameter("@limite", config.LimiCreditoCliente),
            new SqliteParameter("@intervalo", config.RespaldoIntervaloDias),
            new SqliteParameter("@ver", config.VersionApp));

        return filas > 0;
    }

    // ---------- Usuarios ----------

    public int AgregarUsuario(UsuarioSistema usuario)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO UsuariosSistema (Nombre, PINHash, Rol, PermisosEspeciales)
            VALUES (@nom, @pin, @rol, @perm);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@nom", usuario.Nombre);
        cmd.Parameters.AddWithValue("@pin", usuario.PINHash);
        cmd.Parameters.AddWithValue("@rol", (int)usuario.Rol);
        cmd.Parameters.AddWithValue("@perm", usuario.PermisosEspeciales ?? string.Empty);

        usuario.Id = Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        return usuario.Id;
    }

    public List<UsuarioSistema> ObtenerUsuarios() =>
        ExecuteReportObjects<UsuarioSistema>("SELECT * FROM UsuariosSistema ORDER BY Nombre");

    public UsuarioSistema ObtenerUsuarioPorId(int usuarioId) =>
        ExecuteReportObject<UsuarioSistema>("SELECT * FROM UsuariosSistema WHERE Id = @id",
            new SqliteParameter("@id", usuarioId));

    public int ContarUsuarios() =>
        ExecuteScalar<int>("SELECT COUNT(*) FROM UsuariosSistema");

    // ---------- Productos ----------

    /// <summary>
    /// Inserta el producto y registra su stock inicial como movimiento de entrada.
    /// Devuelve el Id creado o 0 si el código de barras ya existe.
    /// </summary>
    public int AgregarProducto(Producto producto)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        try
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                INSERT INTO Productos
                    (CodigoBarras, Nombre, Costo, Precio, Stock, StockMinimo, Categoria, Impuesto)
                VALUES (@codigo, @nombre, @costo, @precio, 0, @minimo, @categoria, @impuesto);
                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@codigo", producto.CodigoBarras);
            cmd.Parameters.AddWithValue("@nombre", producto.Nombre);
            cmd.Parameters.AddWithValue("@costo", producto.Costo);
            cmd.Parameters.AddWithValue("@precio", producto.Precio);
            cmd.Parameters.AddWithValue("@minimo", producto.StockMinimo);
            cmd.Parameters.AddWithValue("@categoria", producto.Categoria ?? string.Empty);
            cmd.Parameters.AddWithValue("@impuesto", producto.Impuesto);

            var id = Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);

            if (producto.Stock > 0)
                InsertarMovimiento(conn, tx, id, MovimientoTipo.Entrada, producto.Stock,
                    producto.Costo, "Stock inicial", "Sistema");

            tx.Commit();
            producto.Id = id;
            return id;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            tx.Rollback();
            return 0;
        }
    }

    public bool ActualizarProducto(Producto producto)
    {
        var filas = ExecuteCommand(@"
            UPDATE Productos
            SET Nombre = @nombre, Costo = @costo, Precio = @precio,
                StockMinimo = @minimo, Categoria = @categoria, Impuesto = @impuesto,
                UltimaActualizacion = datetime('now')
            WHERE Id = @id",
            new SqliteParameter("@id", producto.Id),
            new SqliteParameter("@nombre", producto.Nombre),
            new SqliteParameter("@costo", producto.Costo),
            new SqliteParameter("@precio", producto.Precio),
            new SqliteParameter("@minimo", producto.StockMinimo),
            new SqliteParameter("@categoria", producto.Categoria ?? string.Empty),
            new SqliteParameter("@impuesto", producto.Impuesto));

        return filas > 0;
    }

    public List<Producto> ObtenerProductos() =>
        ExecuteReportObjects<Producto>("SELECT * FROM Productos ORDER BY Nombre");

    public List<Producto> BuscarProductosPorNombre(string texto)
    {
        var patron = $"%{texto.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
        return ExecuteReportObjects<Producto>(
            "SELECT * FROM Productos WHERE Nombre LIKE @patron ESCAPE '\\' ORDER BY Nombre LIMIT 50",
            new SqliteParameter("@patron", patron));
    }

    public Producto ObtenerProductoPorCodigo(string codigoBarras) =>
        ExecuteReportObject<Producto>("SELECT * FROM Productos WHERE CodigoBarras = @codigo",
            new SqliteParameter("@codigo", codigoBarras));

    public Producto ObtenerProductoPorId(int productoId) =>
        ExecuteReportObject<Producto>("SELECT * FROM Productos WHERE Id = @id",
            new SqliteParameter("@id", productoId));

    public List<Producto> ObtenerProductosStockBajo() =>
        ExecuteReportObjects<Producto>(
            "SELECT * FROM Productos WHERE Stock <= StockMinimo ORDER BY Stock, Nombre");

    public long ObtenerValorizadoInventario() =>
        ExecuteScalar<long>("SELECT COALESCE(SUM(Stock * Costo), 0) FROM Productos");

    public int ObtenerStock(int productoId) =>
        ExecuteScalar<int>("SELECT COALESCE(SUM(Cantidad), 0) FROM MovimientoInventario WHERE ProductoId = @id",
            new SqliteParameter("@id", productoId));

    // ---------- Movimientos de inventario ----------

    public bool RegistrarEntrada(int productoId, int cantidad, long costoUnitario, string motivo, string usuario)
    {
        if (cantidad <= 0) return false;
        return RegistrarMovimientoSeguro(productoId, MovimientoTipo.Entrada, cantidad, costoUnitario, motivo, usuario);
    }

    /// <summary>
    /// Registra una salida (venta o merma) validando stock dentro de la transacción.
    /// </summary>
    public bool RegistrarSalida(int productoId, int cantidad, string motivo, string usuario)
    {
        if (cantidad <= 0) return false;

        using var conn = CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var stock = ObtenerStockEnTransaccion(conn, tx, productoId);
        if (stock < cantidad)
        {
            tx.Rollback();
            return false;
        }

        var producto = ObtenerProductoEnTransaccion(conn, tx, productoId);
        InsertarMovimiento(conn, tx, productoId, MovimientoTipo.Salida, -cantidad,
            producto?.Costo ?? 0, motivo, usuario);
        tx.Commit();
        return true;
    }

    /// <summary>
    /// Ajusta el stock a un valor absoluto registrando la diferencia como movimiento.
    /// </summary>
    public bool AjustarStock(int productoId, int nuevoStock, string motivo, string usuario)
    {
        if (nuevoStock < 0) return false;

        using var conn = CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var stockActual = ObtenerStockEnTransaccion(conn, tx, productoId);
        var diferencia = nuevoStock - stockActual;
        if (diferencia == 0)
        {
            tx.Rollback();
            return true;
        }

        InsertarMovimiento(conn, tx, productoId, MovimientoTipo.Ajuste,
            diferencia, 0, motivo, usuario);
        tx.Commit();
        return true;
    }

    public List<MovimientoInventario> ObtenerHistorialMovimientos(int productoId) =>
        ExecuteReportObjects<MovimientoInventario>(
            "SELECT * FROM MovimientoInventario WHERE ProductoId = @id ORDER BY Fecha DESC, Id DESC LIMIT 200",
            new SqliteParameter("@id", productoId));

    private bool RegistrarMovimientoSeguro(int productoId, string tipo, int cantidad,
        long costoUnitario, string motivo, string usuario)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        if (ObtenerProductoEnTransaccion(conn, tx, productoId) == null)
        {
            tx.Rollback();
            return false;
        }

        InsertarMovimiento(conn, tx, productoId, tipo, cantidad, costoUnitario, motivo, usuario);
        tx.Commit();
        return true;
    }

    private static void InsertarMovimiento(SqliteConnection conn, SqliteTransaction tx, int productoId,
        string tipo, int cantidadFirmada, long costoUnitario, string motivo, string usuario)
    {
        using var insert = conn.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = @"
            INSERT INTO MovimientoInventario (ProductoId, Tipo, Cantidad, CostoUnitario, Motivo, Usuario)
            VALUES (@producto, @tipo, @cantidad, @costo, @motivo, @usuario)";
        insert.Parameters.AddWithValue("@producto", productoId);
        insert.Parameters.AddWithValue("@tipo", tipo);
        insert.Parameters.AddWithValue("@cantidad", cantidadFirmada);
        insert.Parameters.AddWithValue("@costo", costoUnitario);
        insert.Parameters.AddWithValue("@motivo", motivo ?? string.Empty);
        insert.Parameters.AddWithValue("@usuario", usuario ?? string.Empty);
        insert.ExecuteNonQuery();

        using var update = conn.CreateCommand();
        update.Transaction = tx;
        update.CommandText = @"
            UPDATE Productos
            SET Stock = Stock + @cantidad, UltimaActualizacion = datetime('now')
            WHERE Id = @producto";
        update.Parameters.AddWithValue("@cantidad", cantidadFirmada);
        update.Parameters.AddWithValue("@producto", productoId);
        update.ExecuteNonQuery();
    }

    private static int ObtenerStockEnTransaccion(SqliteConnection conn, SqliteTransaction tx, int productoId)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT COALESCE(SUM(Cantidad), 0) FROM MovimientoInventario WHERE ProductoId = @id";
        cmd.Parameters.AddWithValue("@id", productoId);
        return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static Producto ObtenerProductoEnTransaccion(SqliteConnection conn, SqliteTransaction tx, int productoId)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT * FROM Productos WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", productoId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        var producto = new Producto();
        var props = typeof(Producto).GetProperties();
        for (int i = 0; i < reader.FieldCount; i++)
        {
            var prop = props.FirstOrDefault(p => p.Name.Equals(reader.GetName(i), StringComparison.OrdinalIgnoreCase));
            if (prop == null || reader.IsDBNull(i)) continue;
            prop.SetValue(producto, ConvertValue(reader.GetValue(i), prop.PropertyType));
        }
        return producto;
    }

    // ---------- Bitácora ----------

    public int AgregarBitacora(RegistroBitacora registro)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Bitacora
                (Fecha, Usuario, Accion, Entidad, EntidadId, DatosAntes, DatosDespues, Motivo)
            VALUES (@fecha, @usuario, @accion, @entidad, @entidadId, @antes, @despues, @motivo);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@fecha", registro.Fecha);
        cmd.Parameters.AddWithValue("@usuario", registro.Usuario);
        cmd.Parameters.AddWithValue("@accion", registro.Accion);
        cmd.Parameters.AddWithValue("@entidad", registro.Entidad ?? string.Empty);
        cmd.Parameters.AddWithValue("@entidadId", registro.EntidadId ?? string.Empty);
        cmd.Parameters.AddWithValue("@antes", registro.DatosAntes ?? string.Empty);
        cmd.Parameters.AddWithValue("@despues", registro.DatosDespues ?? string.Empty);
        cmd.Parameters.AddWithValue("@motivo", registro.Motivo ?? string.Empty);

        registro.Id = Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        return registro.Id;
    }

    public List<RegistroBitacora> ObtenerBitacora(int limite = 100,
        string usuario = null, string accion = null)
    {
        var sql = "SELECT * FROM Bitacora WHERE 1 = 1";
        var parametros = new List<SqliteParameter>();

        if (!string.IsNullOrWhiteSpace(usuario))
        {
            sql += " AND Usuario = @usuario";
            parametros.Add(new SqliteParameter("@usuario", usuario.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(accion))
        {
            sql += " AND Accion = @accion";
            parametros.Add(new SqliteParameter("@accion", accion.Trim()));
        }

        sql += " ORDER BY Fecha DESC, Id DESC LIMIT @limite";
        parametros.Add(new SqliteParameter("@limite", Math.Clamp(limite, 1, 500)));

        return ExecuteReportObjects<RegistroBitacora>(sql, parametros.ToArray());
    }

    // ---------- Gastos ----------

    public int AgregarGasto(Gasto gasto)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Gastos (Concepto, Monto, Fecha, MetodoPago, Origen, Observaciones)
            VALUES (@concepto, @monto, @fecha, @metodo, @origen, @obs);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@concepto", gasto.Concepto);
        cmd.Parameters.AddWithValue("@monto", gasto.Monto);
        cmd.Parameters.AddWithValue("@fecha", gasto.Fecha);
        cmd.Parameters.AddWithValue("@metodo", gasto.MetodoPago ?? string.Empty);
        cmd.Parameters.AddWithValue("@origen", gasto.Origen ?? OrigenGasto.Caja);
        cmd.Parameters.AddWithValue("@obs", gasto.Observaciones ?? string.Empty);

        gasto.Id = Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        return gasto.Id;
    }

    public List<Gasto> ObtenerGastos() =>
        ExecuteReportObjects<Gasto>("SELECT * FROM Gastos ORDER BY Fecha DESC, Id DESC");

    public long ObtenerTotalGastos() =>
        ExecuteScalar<long>("SELECT COALESCE(SUM(Monto), 0) FROM Gastos");

    public List<GastoPorConcepto> ObtenerGastosPorConcepto()
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT Concepto, COALESCE(SUM(Monto), 0) AS Total
            FROM Gastos
            GROUP BY Concepto
            ORDER BY Total DESC";

        var lista = new List<GastoPorConcepto>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            lista.Add(new GastoPorConcepto
            {
                Concepto = reader.GetString(0),
                Total = Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture)
            });
        }
        return lista;
    }

    // ---------- Clientes fiado ----------

    public int AgregarClienteFiado(ClienteFiado cliente)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO ClientesFiado (Nombre, Telefono, SaldoActual)
            VALUES (@nombre, @telefono, @saldo);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@nombre", cliente.Nombre);
        cmd.Parameters.AddWithValue("@telefono", cliente.Telefono ?? string.Empty);
        cmd.Parameters.AddWithValue("@saldo", cliente.SaldoActual);

        cliente.Id = Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        return cliente.Id;
    }

    public List<ClienteFiado> ObtenerTodosLosClientes() =>
        ExecuteReportObjects<ClienteFiado>("SELECT * FROM ClientesFiado ORDER BY Nombre");

    public List<ClienteFiado> ObtenerClientesConSaldo() =>
        ExecuteReportObjects<ClienteFiado>("SELECT * FROM ClientesFiado WHERE SaldoActual > 0 ORDER BY Nombre");

    public ClienteFiado ObtenerClienteFiadoPorId(int clienteId) =>
        ExecuteReportObject<ClienteFiado>("SELECT * FROM ClientesFiado WHERE Id = @id",
            new SqliteParameter("@id", clienteId));

    public bool ActualizarSaldoCliente(int clienteId, long nuevoSaldo)
    {
        var filas = ExecuteCommand(@"
            UPDATE ClientesFiado
            SET SaldoActual = @saldo, UltimaActualizacion = datetime('now')
            WHERE Id = @id",
            new SqliteParameter("@id", clienteId),
            new SqliteParameter("@saldo", nuevoSaldo));

        return filas > 0;
    }

    /// <summary>
    /// Guarda el abono en el historial y actualiza el saldo en la misma transacción.
    /// El historial permite reconstruir el efectivo recibido por periodo.
    /// </summary>
    public bool RegistrarAbonoFiado(int clienteId, long monto, long nuevoSaldo,
        string usuario, string metodoPago = MetodoPagoVenta.Efectivo)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        using (var update = conn.CreateCommand())
        {
            update.Transaction = tx;
            update.CommandText = @"
                UPDATE ClientesFiado
                SET SaldoActual = @saldo, UltimaActualizacion = datetime('now')
                WHERE Id = @id";
            update.Parameters.AddWithValue("@saldo", nuevoSaldo);
            update.Parameters.AddWithValue("@id", clienteId);

            if (update.ExecuteNonQuery() == 0)
            {
                tx.Rollback();
                return false;
            }
        }

        using (var insert = conn.CreateCommand())
        {
            insert.Transaction = tx;
            insert.CommandText = @"
                INSERT INTO AbonosFiado (ClienteId, Fecha, Monto, MetodoPago, Usuario)
                VALUES (@cliente, @fecha, @monto, @metodo, @usuario)";
            insert.Parameters.AddWithValue("@cliente", clienteId);
            insert.Parameters.AddWithValue("@fecha", DateTime.Now);
            insert.Parameters.AddWithValue("@monto", monto);
            insert.Parameters.AddWithValue("@metodo", metodoPago ?? MetodoPagoVenta.Efectivo);
            insert.Parameters.AddWithValue("@usuario", usuario ?? string.Empty);
            insert.ExecuteNonQuery();
        }

        tx.Commit();
        return true;
    }

    // ---------- Proveedores ----------

    public int AgregarProveedor(Proveedor proveedor)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Proveedores (Nombre, Clave, Telefono, Direccion, SaldoPendiente)
            VALUES (@nombre, @clave, @telefono, @direccion, @saldo);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@nombre", proveedor.Nombre);
        cmd.Parameters.AddWithValue("@clave", proveedor.Clave ?? string.Empty);
        cmd.Parameters.AddWithValue("@telefono", proveedor.Telefono ?? string.Empty);
        cmd.Parameters.AddWithValue("@direccion", proveedor.Direccion ?? string.Empty);
        cmd.Parameters.AddWithValue("@saldo", proveedor.SaldoPendiente);

        proveedor.Id = Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        return proveedor.Id;
    }

    public List<Proveedor> ObtenerTodosLosProveedores() =>
        ExecuteReportObjects<Proveedor>("SELECT * FROM Proveedores ORDER BY Nombre");

    public Proveedor ObtenerProveedorPorId(int proveedorId) =>
        ExecuteReportObject<Proveedor>("SELECT * FROM Proveedores WHERE Id = @id",
            new SqliteParameter("@id", proveedorId));

    public bool ActualizarSaldoProveedor(int proveedorId, long nuevoSaldo)
    {
        var filas = ExecuteCommand(@"
            UPDATE Proveedores
            SET SaldoPendiente = @saldo, UltimaActualizacion = datetime('now')
            WHERE Id = @id",
            new SqliteParameter("@id", proveedorId),
            new SqliteParameter("@saldo", nuevoSaldo));

        return filas > 0;
    }

    // ---------- Ventas ----------

    /// <summary>
    /// Guarda la venta, sus líneas, los movimientos de inventario y el saldo de fiado
    /// en una sola transacción. Si falta stock o se excede el crédito, no se guarda nada.
    /// </summary>
    public ProcessResult RegistrarVenta(Venta venta, IReadOnlyList<VentaLinea> lineas)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        try
        {
            foreach (var linea in lineas)
            {
                var stock = ObtenerStockEnTransaccion(conn, tx, linea.ProductoId);
                if (stock < linea.Cantidad)
                {
                    tx.Rollback();
                    return ProcessResult.Error(
                        $"Stock insuficiente de \"{linea.Nombre}\". Disponible: {stock}, solicitado: {linea.Cantidad}");
                }
            }

            int ventaId;
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT INTO Ventas
                        (Fecha, Usuario, ClienteId, MetodoPago, Total, CostoTotal,
                         PagoEfectivo, PagoTarjeta, PagoFiado, ComisionTarjeta)
                    VALUES
                        (@fecha, @usuario, @cliente, @metodo, @total, @costo,
                         @efectivo, @tarjeta, @fiado, @comision);
                    SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("@fecha", venta.Fecha);
                cmd.Parameters.AddWithValue("@usuario", venta.Usuario ?? string.Empty);
                cmd.Parameters.AddWithValue("@cliente", (object)venta.ClienteId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@metodo", venta.MetodoPago);
                cmd.Parameters.AddWithValue("@total", venta.Total);
                cmd.Parameters.AddWithValue("@costo", venta.CostoTotal);
                cmd.Parameters.AddWithValue("@efectivo", venta.PagoEfectivo);
                cmd.Parameters.AddWithValue("@tarjeta", venta.PagoTarjeta);
                cmd.Parameters.AddWithValue("@fiado", venta.PagoFiado);
                cmd.Parameters.AddWithValue("@comision", venta.ComisionTarjeta);
                ventaId = Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
            }

            foreach (var linea in lineas)
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT INTO VentaLineas
                        (VentaId, ProductoId, CodigoBarras, Nombre, Cantidad,
                         PrecioUnitario, CostoUnitario, Importe)
                    VALUES
                        (@venta, @producto, @codigo, @nombre, @cantidad,
                         @precio, @costo, @importe)";
                cmd.Parameters.AddWithValue("@venta", ventaId);
                cmd.Parameters.AddWithValue("@producto", linea.ProductoId);
                cmd.Parameters.AddWithValue("@codigo", linea.CodigoBarras);
                cmd.Parameters.AddWithValue("@nombre", linea.Nombre);
                cmd.Parameters.AddWithValue("@cantidad", linea.Cantidad);
                cmd.Parameters.AddWithValue("@precio", linea.PrecioUnitario);
                cmd.Parameters.AddWithValue("@costo", linea.CostoUnitario);
                cmd.Parameters.AddWithValue("@importe", linea.Importe);
                cmd.ExecuteNonQuery();

                linea.VentaId = ventaId;
                InsertarMovimiento(conn, tx, linea.ProductoId, MovimientoTipo.Salida,
                    -linea.Cantidad, linea.CostoUnitario, $"Venta #{ventaId}", venta.Usuario);
            }

            if (venta.PagoFiado > 0)
            {
                var errorFiado = CobrarFiadoEnTransaccion(conn, tx, venta);
                if (errorFiado != null)
                {
                    tx.Rollback();
                    return ProcessResult.Error(errorFiado);
                }
            }

            tx.Commit();
            venta.Id = ventaId;
            return ProcessResult.Success($"Venta #{ventaId} registrada");
        }
        catch (SqliteException ex)
        {
            try { tx.Rollback(); } catch (SqliteException) { }
            return ProcessResult.Error($"No se pudo registrar la venta: {ex.Message}");
        }
    }

    private static string CobrarFiadoEnTransaccion(SqliteConnection conn, SqliteTransaction tx, Venta venta)
    {
        if (venta.ClienteId == null)
            return "La venta a fiado requiere un cliente";

        bool fiadoActivado;
        long limite;
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT FiadoActivado, LimiCreditoCliente FROM ConfiguracionTienda ORDER BY Id LIMIT 1";
            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return "No hay configuración de la tienda";
            fiadoActivado = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture) != 0;
            limite = Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture);
        }

        if (!fiadoActivado)
            return "El fiado está desactivado";

        long saldoActual;
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT SaldoActual FROM ClientesFiado WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", venta.ClienteId.Value);
            var valor = cmd.ExecuteScalar();
            if (valor == null || valor == DBNull.Value)
                return "Cliente no encontrado";
            saldoActual = Convert.ToInt64(valor, CultureInfo.InvariantCulture);
        }

        var nuevoSaldo = saldoActual + venta.PagoFiado;
        if (limite > 0 && nuevoSaldo > limite)
            return $"El cliente excede su límite de crédito (${Dinero.Formatear(limite)}). " +
                   $"Saldo actual: ${Dinero.Formatear(saldoActual)}, con la venta: ${Dinero.Formatear(nuevoSaldo)}";

        using var update = conn.CreateCommand();
        update.Transaction = tx;
        update.CommandText = @"
            UPDATE ClientesFiado
            SET SaldoActual = @saldo, UltimaActualizacion = datetime('now')
            WHERE Id = @id";
        update.Parameters.AddWithValue("@saldo", nuevoSaldo);
        update.Parameters.AddWithValue("@id", venta.ClienteId.Value);
        update.ExecuteNonQuery();
        return null;
    }

    public Venta ObtenerVentaPorId(int ventaId) =>
        ExecuteReportObject<Venta>("SELECT * FROM Ventas WHERE Id = @id",
            new SqliteParameter("@id", ventaId));

    public List<Venta> ObtenerVentasDelDia(DateTime fecha) =>
        ExecuteReportObjects<Venta>(
            "SELECT * FROM Ventas WHERE date(Fecha) = date(@fecha) ORDER BY Fecha DESC, Id DESC",
            new SqliteParameter("@fecha", fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));

    public List<Venta> ObtenerVentas(DateTime desde, DateTime hasta) =>
        ExecuteReportObjects<Venta>(
            "SELECT * FROM Ventas WHERE date(Fecha) BETWEEN date(@desde) AND date(@hasta) ORDER BY Fecha DESC, Id DESC",
            new SqliteParameter("@desde", desde.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            new SqliteParameter("@hasta", hasta.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));

    public List<VentaLinea> ObtenerLineasVenta(int ventaId) =>
        ExecuteReportObjects<VentaLinea>(
            "SELECT * FROM VentaLineas WHERE VentaId = @id ORDER BY Id",
            new SqliteParameter("@id", ventaId));

    // ---------- Reporte de utilidad ----------

    public ReporteUtilidad ObtenerReporteUtilidad(DateTime desde, DateTime hasta)
    {
        var reporte = new ReporteUtilidad { Desde = desde.Date, Hasta = hasta.Date };

        using var conn = CreateConnection();
        conn.Open();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT COUNT(*), COALESCE(SUM(Total), 0),
                       COALESCE(SUM(CostoTotal), 0), COALESCE(SUM(ComisionTarjeta), 0)
                FROM Ventas
                WHERE date(Fecha) BETWEEN date(@desde) AND date(@hasta)";
            cmd.Parameters.AddWithValue("@desde", desde.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("@hasta", hasta.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                reporte.NumeroVentas = Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture);
                reporte.VentasTotal = Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture);
                reporte.CostoVentas = Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture);
                reporte.ComisionTarjeta = Convert.ToInt64(reader.GetValue(3), CultureInfo.InvariantCulture);
            }
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT COALESCE(SUM(-Cantidad * CostoUnitario), 0)
                FROM MovimientoInventario
                WHERE Tipo = @tipo AND Motivo LIKE 'Merma:%'
                  AND date(Fecha) BETWEEN date(@desde) AND date(@hasta)";
            cmd.Parameters.AddWithValue("@tipo", MovimientoTipo.Salida);
            cmd.Parameters.AddWithValue("@desde", desde.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("@hasta", hasta.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

            reporte.Mermas = Convert.ToInt64(cmd.ExecuteScalar() ?? 0L, CultureInfo.InvariantCulture);
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT COALESCE(SUM(Monto), 0) FROM Gastos
                WHERE date(Fecha) BETWEEN date(@desde) AND date(@hasta)";
            cmd.Parameters.AddWithValue("@desde", desde.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("@hasta", hasta.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

            reporte.Gastos = Convert.ToInt64(cmd.ExecuteScalar() ?? 0L, CultureInfo.InvariantCulture);
        }

        return reporte;
    }

    // ---------- Corte de caja ----------

    /// <summary>
    /// Vista previa del periodo abierto: desde el último corte hasta ahora.
    /// El fondo sugerido es el efectivo contado en el corte anterior o el configurado.
    /// </summary>
    public CorteCaja ObtenerResumenCajaPendiente()
    {
        using var conn = CreateConnection();
        conn.Open();

        var corte = CalcularTotalesCaja(conn, null);
        var ultimo = ObtenerUltimoCorteCaja();
        var config = ObtenerConfiguracion();

        corte.FondoInicial = ultimo != null
            ? ultimo.EfectivoContado
            : config?.FondoCajaInicial ?? 0L;
        corte.EfectivoEsperado = CalcularEfectivoEsperado(corte);
        return corte;
    }

    /// <summary>
    /// Calcula y guarda el corte en una sola transacción para que los totales
    /// correspondan exactamente a lo que queda registrado.
    /// </summary>
    public ProcessResult<CorteCaja> RegistrarCorteCaja(long efectivoContado, long fondoInicial,
        string usuario, string observaciones)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var corte = CalcularTotalesCaja(conn, tx);
        corte.FondoInicial = fondoInicial;
        corte.EfectivoContado = efectivoContado;
        corte.EfectivoEsperado = CalcularEfectivoEsperado(corte);
        corte.Diferencia = corte.EfectivoContado - corte.EfectivoEsperado;
        corte.Usuario = (usuario ?? string.Empty).Trim();
        corte.Observaciones = (observaciones ?? string.Empty).Trim();
        corte.FechaRegistro = corte.Hasta;

        if (corte.Diferencia != 0 && string.IsNullOrWhiteSpace(corte.Observaciones))
        {
            tx.Rollback();
            return ProcessResult<CorteCaja>.Error(
                $"El corte tiene una diferencia de ${Dinero.Formatear(corte.Diferencia)}; explique el motivo antes de guardarlo");
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
                INSERT INTO CortesCaja
                    (Desde, Hasta, FondoInicial, VentasEfectivo, VentasTarjeta, VentasFiado,
                     TotalVentas, NumeroVentas, NumeroVentasEfectivo, NumeroVentasTarjeta,
                     NumeroVentasMixto, NumeroVentasFiado, AbonosEfectivo, IngresosCaja,
                     RetirosCaja, GastosCaja, EfectivoEsperado, EfectivoContado, Diferencia,
                     Usuario, Observaciones, FechaRegistro)
                VALUES
                    (@desde, @hasta, @fondo, @ventasEfectivo, @ventasTarjeta, @ventasFiado,
                     @totalVentas, @numeroVentas, @numeroEfectivo, @numeroTarjeta,
                     @numeroMixto, @numeroFiado, @abonos, @ingresos,
                     @retiros, @gastos, @esperado, @contado, @diferencia,
                     @usuario, @observaciones, @registro);
                SELECT last_insert_rowid();";

            cmd.Parameters.AddWithValue("@desde", corte.Desde);
            cmd.Parameters.AddWithValue("@hasta", corte.Hasta);
            cmd.Parameters.AddWithValue("@fondo", corte.FondoInicial);
            cmd.Parameters.AddWithValue("@ventasEfectivo", corte.VentasEfectivo);
            cmd.Parameters.AddWithValue("@ventasTarjeta", corte.VentasTarjeta);
            cmd.Parameters.AddWithValue("@ventasFiado", corte.VentasFiado);
            cmd.Parameters.AddWithValue("@totalVentas", corte.TotalVentas);
            cmd.Parameters.AddWithValue("@numeroVentas", corte.NumeroVentas);
            cmd.Parameters.AddWithValue("@numeroEfectivo", corte.NumeroVentasEfectivo);
            cmd.Parameters.AddWithValue("@numeroTarjeta", corte.NumeroVentasTarjeta);
            cmd.Parameters.AddWithValue("@numeroMixto", corte.NumeroVentasMixto);
            cmd.Parameters.AddWithValue("@numeroFiado", corte.NumeroVentasFiado);
            cmd.Parameters.AddWithValue("@abonos", corte.AbonosEfectivo);
            cmd.Parameters.AddWithValue("@ingresos", corte.IngresosCaja);
            cmd.Parameters.AddWithValue("@retiros", corte.RetirosCaja);
            cmd.Parameters.AddWithValue("@gastos", corte.GastosCaja);
            cmd.Parameters.AddWithValue("@esperado", corte.EfectivoEsperado);
            cmd.Parameters.AddWithValue("@contado", corte.EfectivoContado);
            cmd.Parameters.AddWithValue("@diferencia", corte.Diferencia);
            cmd.Parameters.AddWithValue("@usuario", corte.Usuario);
            cmd.Parameters.AddWithValue("@observaciones", corte.Observaciones);
            cmd.Parameters.AddWithValue("@registro", corte.FechaRegistro);

            corte.Id = Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        tx.Commit();
        return ProcessResult<CorteCaja>.OK(corte, $"Corte #{corte.Id} registrado");
    }

    public CorteCaja ObtenerUltimoCorteCaja() =>
        ExecuteReportObject<CorteCaja>(
            "SELECT * FROM CortesCaja ORDER BY Hasta DESC, Id DESC LIMIT 1");

    public List<CorteCaja> ObtenerCortesCaja(int limite = 20) =>
        ExecuteReportObjects<CorteCaja>(
            "SELECT * FROM CortesCaja ORDER BY Hasta DESC, Id DESC LIMIT @limite",
            new SqliteParameter("@limite", limite));

    public int RegistrarMovimientoCaja(MovimientoCaja movimiento)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO MovimientosCaja (Fecha, Tipo, Monto, Motivo, Usuario)
            VALUES (@fecha, @tipo, @monto, @motivo, @usuario);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@fecha", movimiento.Fecha);
        cmd.Parameters.AddWithValue("@tipo", movimiento.Tipo);
        cmd.Parameters.AddWithValue("@monto", movimiento.Monto);
        cmd.Parameters.AddWithValue("@motivo", movimiento.Motivo ?? string.Empty);
        cmd.Parameters.AddWithValue("@usuario", movimiento.Usuario ?? string.Empty);

        movimiento.Id = Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        return movimiento.Id;
    }

    /// <summary>
    /// Periodo abierto y totales del flujo de efectivo. El primer corte arranca
    /// en el primer movimiento registrado; los siguientes, en el corte anterior.
    /// </summary>
    private static CorteCaja CalcularTotalesCaja(SqliteConnection conn, SqliteTransaction tx)
    {
        var hasta = DateTime.Now;
        var desde = CalcularInicioPeriodo(conn, tx);
        var corte = new CorteCaja { Desde = desde, Hasta = hasta };

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
                SELECT
                    COALESCE(SUM(PagoEfectivo), 0),
                    COALESCE(SUM(PagoTarjeta), 0),
                    COALESCE(SUM(PagoFiado), 0),
                    COALESCE(SUM(Total), 0),
                    COUNT(*),
                    COALESCE(SUM(CASE WHEN MetodoPago = @efectivo THEN 1 ELSE 0 END), 0),
                    COALESCE(SUM(CASE WHEN MetodoPago = @tarjeta THEN 1 ELSE 0 END), 0),
                    COALESCE(SUM(CASE WHEN MetodoPago = @mixto THEN 1 ELSE 0 END), 0),
                    COALESCE(SUM(CASE WHEN MetodoPago = @fiado THEN 1 ELSE 0 END), 0)
                FROM Ventas
                WHERE Fecha > @desde AND Fecha <= @hasta";
            cmd.Parameters.AddWithValue("@desde", desde);
            cmd.Parameters.AddWithValue("@hasta", hasta);
            cmd.Parameters.AddWithValue("@efectivo", MetodoPagoVenta.Efectivo);
            cmd.Parameters.AddWithValue("@tarjeta", MetodoPagoVenta.Tarjeta);
            cmd.Parameters.AddWithValue("@mixto", MetodoPagoVenta.Mixto);
            cmd.Parameters.AddWithValue("@fiado", MetodoPagoVenta.Fiado);

            using var reader = cmd.ExecuteReader();
            reader.Read();
            corte.VentasEfectivo = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
            corte.VentasTarjeta = Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture);
            corte.VentasFiado = Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture);
            corte.TotalVentas = Convert.ToInt64(reader.GetValue(3), CultureInfo.InvariantCulture);
            corte.NumeroVentas = Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture);
            corte.NumeroVentasEfectivo = Convert.ToInt32(reader.GetValue(5), CultureInfo.InvariantCulture);
            corte.NumeroVentasTarjeta = Convert.ToInt32(reader.GetValue(6), CultureInfo.InvariantCulture);
            corte.NumeroVentasMixto = Convert.ToInt32(reader.GetValue(7), CultureInfo.InvariantCulture);
            corte.NumeroVentasFiado = Convert.ToInt32(reader.GetValue(8), CultureInfo.InvariantCulture);
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
                SELECT COALESCE(SUM(Monto), 0) FROM AbonosFiado
                WHERE MetodoPago = @efectivo AND Fecha > @desde AND Fecha <= @hasta";
            cmd.Parameters.AddWithValue("@efectivo", MetodoPagoVenta.Efectivo);
            cmd.Parameters.AddWithValue("@desde", desde);
            cmd.Parameters.AddWithValue("@hasta", hasta);
            corte.AbonosEfectivo = Convert.ToInt64(cmd.ExecuteScalar() ?? 0L, CultureInfo.InvariantCulture);
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
                SELECT
                    COALESCE(SUM(CASE WHEN Tipo = @ingreso THEN Monto ELSE 0 END), 0),
                    COALESCE(SUM(CASE WHEN Tipo = @retiro THEN Monto ELSE 0 END), 0)
                FROM MovimientosCaja
                WHERE Fecha > @desde AND Fecha <= @hasta";
            cmd.Parameters.AddWithValue("@ingreso", TipoMovimientoCaja.Ingreso);
            cmd.Parameters.AddWithValue("@retiro", TipoMovimientoCaja.Retiro);
            cmd.Parameters.AddWithValue("@desde", desde);
            cmd.Parameters.AddWithValue("@hasta", hasta);

            using var reader = cmd.ExecuteReader();
            reader.Read();
            corte.IngresosCaja = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
            corte.RetirosCaja = Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture);
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
                SELECT COALESCE(SUM(Monto), 0) FROM Gastos
                WHERE Origen = @origen AND Fecha > @desde AND Fecha <= @hasta";
            cmd.Parameters.AddWithValue("@origen", OrigenGasto.Caja);
            cmd.Parameters.AddWithValue("@desde", desde);
            cmd.Parameters.AddWithValue("@hasta", hasta);
            corte.GastosCaja = Convert.ToInt64(cmd.ExecuteScalar() ?? 0L, CultureInfo.InvariantCulture);
        }

        return corte;
    }

    private static DateTime CalcularInicioPeriodo(SqliteConnection conn, SqliteTransaction tx)
    {
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT Hasta FROM CortesCaja ORDER BY Hasta DESC, Id DESC LIMIT 1";
            var ultimo = cmd.ExecuteScalar();
            if (ultimo != null && ultimo != DBNull.Value)
                return Convert.ToDateTime(ultimo, CultureInfo.InvariantCulture);
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
                SELECT MIN(f) FROM (
                    SELECT MIN(Fecha) AS f FROM Ventas
                    UNION ALL SELECT MIN(Fecha) FROM Gastos
                    UNION ALL SELECT MIN(Fecha) FROM AbonosFiado
                    UNION ALL SELECT MIN(Fecha) FROM MovimientosCaja
                )";
            var primera = cmd.ExecuteScalar();
            if (primera != null && primera != DBNull.Value)
                return Convert.ToDateTime(primera, CultureInfo.InvariantCulture).AddSeconds(-1);
        }

        return DateTime.Today;
    }

    private static long CalcularEfectivoEsperado(CorteCaja corte) =>
        corte.FondoInicial + corte.VentasEfectivo + corte.AbonosEfectivo +
        corte.IngresosCaja - corte.RetirosCaja - corte.GastosCaja;
}
