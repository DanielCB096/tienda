using System.Reflection;
using SistemaTienda.Application.Services;

namespace SistemaTienda.Tests;

/// <summary>
/// Detector de cambios: si aparece un método público nuevo en un servicio, esta
/// prueba falla hasta que alguien revise si necesita permiso y actualice la lista.
/// No sustituye las pruebas de autorización, pero evita que se cuele un hueco por
/// omisión como el de AgregarClienteFiado o AgregarProveedor.
/// </summary>
public sealed class ManifiestoServiciosTests
{
    private static readonly Dictionary<string, string[]> Esperado = new()
    {
        ["ConfiguracionService"] = new[]
        {
            "ActualizarConfiguracion", "ActualizarPermisoEspecial", "ActualizarUsuario",
            "AgregarClienteFiado", "AgregarGasto", "AgregarProveedor", "AgregarUsuario",
            "BuscarClientes", "BuscarProveedores", "CambiarPin", "DesactivarCliente",
            "DesactivarProveedor", "DetectarDuplicadosCliente", "DetectarDuplicadosProveedor",
            "EditarClienteFiado", "EditarProveedor", "EjecutarAsistenteInicial",
            "ObtenerClientesConSaldo", "ObtenerConfiguracion", "ObtenerEstadoCuentaCliente",
            "ObtenerEstadoCuentaProveedor", "ObtenerGastosPorConcepto", "ObtenerResumenGastos",
            "ObtenerTodosLosClientes", "ObtenerTodosLosGastos", "ObtenerTodosLosProveedores",
            "ObtenerTotalGastos", "ObtenerUsuarios", "ReactivarCliente", "ReactivarProveedor",
            "RegistrarAbonoCliente", "RegistrarCompraProveedor", "RegistrarPagoProveedor",
            "UsuarioTieneRol", "VerificarAcceso"
        },
        ["InventoryService"] = new[]
        {
            "ActualizarProducto", "AgregarProducto", "AjustarStock", "BuscarPorCodigo",
            "BuscarPorId", "BuscarPorNombre", "DesactivarProducto", "HistorialMovimientos",
            "ImportarProductosCsv", "ImportarProductosXlsx", "ObtenerImportacion",
            "ObtenerImportaciones", "ObtenerProductos", "ObtenerValorizadoInventario",
            "PrevisualizarProductosCsv", "PrevisualizarProductosXlsx", "ProductosStockBajo",
            "ReactivarProducto", "ReducirStockVenta", "RegistrarEntrada", "RegistrarMerma",
            "RevertirImportacion", "SugerirProductos"
        },
        ["VentaService"] = new[]
        {
            "AnularVenta", "ObtenerAnulacion", "ObtenerLineas", "ObtenerVentaPorId",
            "ObtenerVentas", "ObtenerVentasDelDia", "RegistrarVenta"
        },
        ["CorteCajaService"] = new[]
        {
            "ObtenerCortes", "ObtenerDesglose", "ObtenerResumenPendiente",
            "ObtenerUltimoCorte", "RegistrarCorte", "RegistrarMovimiento"
        },
        ["ReporteUtilidadService"] = new[] { "Obtener", "ObtenerComparacion" },
        ["BitacoraService"] = new[] { "Consultar", "Registrar" },
        ["RespaldoService"] = new[]
        {
            "CrearRespaldo", "CrearRespaldoManual", "EjecutarAutomaticoSiCorresponde",
            "ListarRespaldos", "ObtenerUltimoRespaldo", "Restaurar"
        },
        ["PaqueteSoporteService"] = new[] { "Crear" }
    };

    [Fact]
    public void Servicios_Publicos_CoincidenConElManifiestoRevisado()
    {
        var ensamblado = typeof(ConfiguracionService).Assembly;

        foreach (var (nombre, esperados) in Esperado)
        {
            var tipo = ensamblado.GetType($"SistemaTienda.Application.Services.{nombre}");
            Assert.NotNull(tipo);

            var reales = tipo!
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName)
                .Select(m => m.Name)
                .Distinct()
                .OrderBy(n => n)
                .ToArray();

            var ordenados = esperados.OrderBy(n => n).ToArray();
            var nuevos = reales.Except(ordenados).ToArray();
            var faltantes = ordenados.Except(reales).ToArray();

            Assert.True(nuevos.Length == 0 && faltantes.Length == 0,
                $"{nombre} cambió. Revisa si los métodos nuevos exigen permiso y actualiza " +
                $"el manifiesto. Nuevos: [{string.Join(", ", nuevos)}]. " +
                $"Faltantes: [{string.Join(", ", faltantes)}].");
        }
    }
}
