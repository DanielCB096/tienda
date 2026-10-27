using SistemaTienda.Core;

namespace SistemaTienda.Maui.Models;

public sealed class ClienteOpcion
{
    public ClienteFiado Cliente { get; init; } = null!;
    public string NombreConSaldo => $"{Cliente.Nombre} · saldo {Formato.Moneda(Cliente.SaldoActual)}";
}
