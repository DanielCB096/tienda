using Microsoft.Data.Sqlite;
using SistemaTienda.Core;

namespace SistemaTienda.Tests;

public sealed class MapeoTiposTests
{
    [Theory]
    [InlineData(0, RolUsuario.Dueño)]
    [InlineData(1, RolUsuario.Encargado)]
    public void MapeoDeFilas_ConRolEntero_DevuelveEnumCorrespondiente(int valor, RolUsuario esperado)
    {
        using var bd = new BaseDatosPrueba();

        var usuario = bd.Db.ExecuteReportObject<UsuarioSistema>(
            "SELECT @rol AS Rol",
            new SqliteParameter("@rol", valor));

        Assert.Equal(esperado, usuario.Rol);
    }

    [Fact]
    public void MapeoDeFilas_ConFechaNullableNulaYCargada_LaConvierteCorrectamente()
    {
        using var bd = new BaseDatosPrueba();
        bd.Inventario.AgregarProducto(new Producto
        {
            CodigoBarras = "A1",
            Nombre = "Producto",
            Costo = 1m,
            Precio = 2m,
            Stock = 1,
            StockMinimo = 1
        });
        var producto = Assert.Single(bd.Inventario.ObtenerProductos());

        Assert.Null(producto.UltimaActualizacion);

        bd.Db.ExecuteCommand(
            "UPDATE Productos SET UltimaActualizacion = '2024-01-15 08:30:00' WHERE Id = @id",
            new SqliteParameter("@id", producto.Id));

        var recargado = bd.Inventario.BuscarPorId(producto.Id);

        Assert.NotNull(recargado.UltimaActualizacion);
        Assert.Equal(new DateTime(2024, 1, 15, 8, 30, 0), recargado.UltimaActualizacion.Value);
    }
}
