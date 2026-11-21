namespace SistemaTienda.Core;

/// <summary>
/// Renglón inmutable del desglose de billetes y monedas contados en un corte de caja.
/// Todos los importes están en centavos. Solo se guardan cantidades mayores a cero.
/// </summary>
public class CorteCajaDesglose
{
    public int Id { get; set; }
    public int CorteCajaId { get; set; }
    public long ValorCentavos { get; set; }
    public int Cantidad { get; set; }
    public long Subtotal { get; set; }
}
