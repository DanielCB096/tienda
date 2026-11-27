namespace SistemaTienda.Application.Services;

/// <summary>
/// Llave pública que valida las licencias emitidas. Es pública a propósito:
/// solo sirve para verificar, nunca para firmar. La llave privada se guarda
/// fuera del repositorio, en la máquina del proveedor.
/// </summary>
public static class LicenciaPublica
{
    public const string Pem =
@"-----BEGIN PUBLIC KEY-----
MIIBojANBgkqhkiG9w0BAQEFAAOCAY8AMIIBigKCAYEA2Xc8QbxRikbBb12XWyU9
XIMx1nTqW/agMj/zEkMWhrp1cLq5oIhSTL8Le55p5n0xpg4UJkYGKktha2dPACVc
CG1JuaXEl+e8Ac5x9ZH3Pvz/lqoouBzaMeND7eMhHOKHyph24CXVIyiHDdopw30D
AwWHEUm04+lXGx94C6r5W93smzBEFX1ldQMcn8xJFjDbvh4TitjMk3G0/U5l5Csh
hafvMgvFZvpVqFYGxBI8huLJvc9S0QQxrvmpt91IzvDDu/S9EpXh9dX8cMivG25m
/UM841Jmw1OUpoo+NgYZ+RId8OQ6zKJ8bfVNC9MjETuam1fEecZc9U+ubqaKMXs7
9TRMXqlriv7uSsKChQVn4iWC4JfHRUyGd4xLWvf3pz17FUrV565L8xzbcjtxjZRv
x0zUgmOG0U+jCGD9XN+4Z68HoA7KmK48woAnEIEZO6fqlKh/Yhu6vgoVAjxj+BMJ
v8f5Iyjs8uJNnrpj0N5Sbluz72QVjJJwy7oiP6Nv1MNBAgMBAAE=
-----END PUBLIC KEY-----";
}
