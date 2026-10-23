// Entidades principales del sistema

public class Producto
{
    public int Id { get; set; }
    public string CodigoBarras { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public decimal Costo { get; set; }
    public decimal Precio { get; set; }
    public int Stock { get; set; }
    public string Categoria { get; set; } = string.Empty;
}

public class Venta
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    public decimal Total { get; set; }
    public string MetodoPago { get; set; } = string.Empty;
    public string Estado { get; set; } = "Completada";
}

public class Merma
{
    public int Id { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public decimal CostoPerdido { get; set; }
    public int ProductoId { get; set; }
    public string Usuario { get; set; } = string.Empty;
}

public class Gasto
{
    public int Id { get; set; }
    public string Concepto { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    public string MetodoPago { get; set; } = string.Empty;
}

public class Cliente
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public decimal SaldoFiado { get; set; }
}

public class Proveedor
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Contacto { get; set; } = string.Empty;
    public decimal SaldoPagar { get; set; }
}