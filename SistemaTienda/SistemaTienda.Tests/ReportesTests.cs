using SistemaTienda.Application.Services;
using SistemaTienda.Core;

namespace SistemaTienda.Tests;

public sealed class ReportesTests
{
    private static Producto CrearProducto(BaseDatosPrueba bd, string codigo,
        long precio, long costo, int stock, string categoria = "")
    {
        var producto = new Producto
        {
            CodigoBarras = codigo,
            Nombre = $"Producto {codigo}",
            Costo = costo,
            Precio = precio,
            Stock = stock,
            Categoria = categoria
        };

        Assert.True(bd.Inventario.AgregarProducto(producto).IsSuccess);
        return producto;
    }

    private static ProcessResult<Venta> Vender(BaseDatosPrueba bd, PagoSolicitado pago,
        params (Producto Producto, int Cantidad)[] lineas)
    {
        var solicitud = lineas
            .Select(l => new LineaVentaSolicitada { ProductoId = l.Producto.Id, Cantidad = l.Cantidad })
            .ToList();

        return new VentaService(bd.Db).RegistrarVenta(solicitud, pago, "Cajero");
    }

    private static ReporteUtilidad DelDia(BaseDatosPrueba bd) =>
        new ReporteUtilidadService(bd.Db).Obtener(DateTime.Today, DateTime.Today).Value;

    private static void VenderEnFecha(BaseDatosPrueba bd, Producto producto,
        DateTime fecha, int cantidad)
    {
        var venta = new Venta
        {
            Fecha = fecha,
            Usuario = "Cajero",
            MetodoPago = MetodoPagoVenta.Efectivo,
            Total = producto.Precio * cantidad,
            CostoTotal = producto.Costo * cantidad,
            PagoEfectivo = producto.Precio * cantidad
        };
        var linea = new VentaLinea
        {
            ProductoId = producto.Id,
            CodigoBarras = producto.CodigoBarras,
            Nombre = producto.Nombre,
            Cantidad = cantidad,
            PrecioUnitario = producto.Precio,
            CostoUnitario = producto.Costo,
            Importe = producto.Precio * cantidad
        };

        var resultado = bd.Db.RegistrarVenta(venta, new[] { linea });
        Assert.True(resultado.IsSuccess, resultado.Message);
    }

    [Fact]
    public void Obtener_AgrupaVentasPorProductoYCategoria()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var abarrotes = CrearProducto(bd, "R1", precio: 1_000, costo: 600, stock: 10,
            categoria: "Abarrotes");
        var bebidas = CrearProducto(bd, "R2", precio: 2_000, costo: 1_200, stock: 10,
            categoria: "Bebidas");

        Assert.True(Vender(bd, new PagoSolicitado { Efectivo = 3_000 },
            (abarrotes, 1), (bebidas, 1)).IsSuccess);

        var reporte = DelDia(bd);

        Assert.Equal(2, reporte.Productos.Count);
        var producto = reporte.Productos.Single(p => p.ProductoId == abarrotes.Id);
        Assert.Equal("Abarrotes", producto.Categoria);
        Assert.Equal(1, producto.Cantidad);
        Assert.Equal(1_000, producto.Ventas);
        Assert.Equal(600, producto.Costo);
        Assert.Equal(400, producto.Utilidad);

        Assert.Equal(2, reporte.Categorias.Count);
        Assert.Equal(1_000, reporte.Categorias.Single(c => c.Categoria == "Abarrotes").Ventas);
        Assert.Equal(2_000, reporte.Categorias.Single(c => c.Categoria == "Bebidas").Ventas);
    }

    [Fact]
    public void Obtener_AgrupaVentasPorDiaYExcluyeAnuladas()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "R14", precio: 1_000, costo: 600, stock: 20);

        VenderEnFecha(bd, producto, DateTime.Today.AddDays(-1).AddHours(10), 1);
        VenderEnFecha(bd, producto, DateTime.Today.AddHours(9), 2);

        var anulada = Vender(bd, new PagoSolicitado { Efectivo = 1_000 }, (producto, 1));
        Assert.True(anulada.IsSuccess, anulada.Message);
        var anulacion = new VentaService(bd.Db).AnularVenta(anulada.Value.Id, "Prueba", "Dueño");
        Assert.True(anulacion.IsSuccess, anulacion.Message);

        var reporte = new ReporteUtilidadService(bd.Db)
            .Obtener(DateTime.Today.AddDays(-1), DateTime.Today).Value;

        Assert.Equal(2, reporte.Dias.Count);

        var ayer = reporte.Dias[0];
        Assert.Equal(DateTime.Today.AddDays(-1).Date, ayer.Fecha);
        Assert.Equal(1, ayer.NumeroVentas);
        Assert.Equal(1_000, ayer.Ventas);
        Assert.Equal(600, ayer.Costo);
        Assert.Equal(400, ayer.Utilidad);

        var hoy = reporte.Dias[1];
        Assert.Equal(DateTime.Today.Date, hoy.Fecha);
        Assert.Equal(1, hoy.NumeroVentas);
        Assert.Equal(2_000, hoy.Ventas);
        Assert.Equal(1_200, hoy.Costo);
        Assert.Equal(800, hoy.Utilidad);
    }

    [Fact]
    public void Obtener_ExcluyeVentasAnuladasDelDesglose()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "R3", precio: 1_500, costo: 1_000, stock: 10,
            categoria: "Abarrotes");

        var venta = Vender(bd, new PagoSolicitado { Efectivo = 1_500 }, (producto, 1));
        Assert.True(venta.IsSuccess, venta.Message);
        var anulacion = new VentaService(bd.Db).AnularVenta(venta.Value.Id, "Prueba", "Dueño");
        Assert.True(anulacion.IsSuccess, anulacion.Message);

        var reporte = DelDia(bd);

        Assert.Empty(reporte.Productos);
        Assert.Empty(reporte.Categorias);
    }

    [Fact]
    public void Obtener_ConVentaEnEfectivo_UsaElCostoCongelado()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 1_500, costo: 1_000, stock: 10);

        Assert.True(Vender(bd, new PagoSolicitado { Efectivo = 3_000 }, (producto, 2)).IsSuccess);

        var reporte = DelDia(bd);

        Assert.Equal(1, reporte.NumeroVentas);
        Assert.Equal(3_000, reporte.VentasTotal);
        Assert.Equal(2_000, reporte.CostoVentas);
        Assert.Equal(0, reporte.ComisionTarjeta);
        Assert.Equal(1_000, reporte.UtilidadBruta);
        Assert.Equal(1_000, reporte.UtilidadNeta);
    }

    [Fact]
    public void Obtener_TrasCambiarElCostoDelCatalogo_ConservaElCostoDeLaVenta()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 1_500, costo: 1_000, stock: 10);
        Assert.True(Vender(bd, new PagoSolicitado { Efectivo = 1_500 }, (producto, 1)).IsSuccess);

        producto.Costo = 1_200;
        Assert.True(bd.Inventario.ActualizarProducto(producto).IsSuccess);

        Assert.Equal(1_000, DelDia(bd).CostoVentas);
    }

    [Fact]
    public void Obtener_ConPagoTarjeta_RestaLaComision()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 10_000, costo: 6_000, stock: 10);

        var venta = Vender(bd, new PagoSolicitado { Tarjeta = 10_000 }, (producto, 1));
        Assert.True(venta.IsSuccess, venta.Message);

        var reporte = DelDia(bd);

        Assert.Equal(350, reporte.ComisionTarjeta);
        Assert.Equal(10_000, reporte.VentasTotal);
        Assert.Equal(3_650, reporte.UtilidadNeta);
    }

    [Fact]
    public void Obtener_ConMermaYRestaSuCosto()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 1_500, costo: 1_000, stock: 10);

        Assert.True(bd.Inventario.RegistrarMerma(producto.Id, 2, "Caducado", "Dueño").IsSuccess);

        var reporte = DelDia(bd);

        Assert.Equal(2_000, reporte.Mermas);
        Assert.Equal(-2_000, reporte.UtilidadNeta);
    }

    [Fact]
    public void Obtener_ConGastosDeCajaYExternos_RestaAmbos()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        Assert.True(bd.Config.AgregarGasto(new Gasto
        {
            Concepto = "Luz", Monto = 500, Origen = OrigenGasto.Caja
        }).IsSuccess);
        Assert.True(bd.Config.AgregarGasto(new Gasto
        {
            Concepto = "Renta", Monto = 300, Origen = OrigenGasto.Externo
        }).IsSuccess);

        var reporte = DelDia(bd);

        Assert.Equal(800, reporte.Gastos);
        Assert.Equal(-800, reporte.UtilidadNeta);
    }

    [Fact]
    public void Obtener_ConVentaFueraDelRango_NoLaCuenta()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 1_500, costo: 1_000, stock: 10);
        Assert.True(Vender(bd, new PagoSolicitado { Efectivo = 1_500 }, (producto, 1)).IsSuccess);

        var ayer = DateTime.Today.AddDays(-1);
        var reporte = new ReporteUtilidadService(bd.Db).Obtener(ayer, ayer).Value;

        Assert.Equal(0, reporte.NumeroVentas);
        Assert.Equal(0, reporte.VentasTotal);
        Assert.Equal(0, reporte.UtilidadNeta);
    }

    [Fact]
    public void Obtener_ConVentaMixta_DesglosaEfectivoYTarjeta()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 1_000, costo: 500, stock: 10);

        var venta = Vender(bd,
            new PagoSolicitado { Efectivo = 1_200, Tarjeta = 800 },
            (producto, 2));
        Assert.True(venta.IsSuccess, venta.Message);

        var reporte = DelDia(bd);
        var efectivo = Assert.Single(reporte.DesglosePagos, d => d.Metodo == MetodoPagoVenta.Efectivo);
        var tarjeta = Assert.Single(reporte.DesglosePagos, d => d.Metodo == MetodoPagoVenta.Tarjeta);

        Assert.Equal(1, efectivo.NumeroVentas);
        Assert.Equal(1_200, efectivo.Monto);
        Assert.Equal(1, tarjeta.NumeroVentas);
        Assert.Equal(800, tarjeta.Monto);
        Assert.DoesNotContain(reporte.DesglosePagos, d => d.Metodo == MetodoPagoVenta.Fiado);
    }

    [Fact]
    public void Obtener_ConVentaAnulada_NoLaIncluyeEnElDesglose()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 1_500, costo: 1_000, stock: 10);
        var venta = Vender(bd, new PagoSolicitado { Efectivo = 1_500 }, (producto, 1));
        Assert.True(venta.IsSuccess, venta.Message);

        Assert.True(new VentaService(bd.Db)
            .AnularVenta(venta.Value.Id, "Cliente devolvió", "Encargado").IsSuccess);

        var reporte = DelDia(bd);

        Assert.Empty(reporte.DesglosePagos);
        Assert.Equal(0, reporte.VentasTotal);
    }

    [Fact]
    public void ObtenerComparacion_ContraElBloqueAnterior_CalculaDiferenciasYPorcentaje()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 1_000, costo: 400, stock: 10);

        VenderEnFecha(bd, producto, DateTime.Today.AddDays(-1), 1);
        VenderEnFecha(bd, producto, DateTime.Today, 2);

        var comparacion = new ReporteUtilidadService(bd.Db)
            .ObtenerComparacion(DateTime.Today, DateTime.Today).Value;

        Assert.Equal(2_000, comparacion.Actual.VentasTotal);
        Assert.Equal(1_000, comparacion.Anterior.VentasTotal);
        Assert.Equal(1_000, comparacion.DiferenciaMontoVentas);
        Assert.Equal(100m, comparacion.VariacionVentas);
        Assert.Equal(1_200, comparacion.Actual.UtilidadNeta);
        Assert.Equal(600, comparacion.Anterior.UtilidadNeta);
        Assert.Equal(600, comparacion.DiferenciaUtilidadNeta);
        Assert.Equal(100m, comparacion.VariacionUtilidadNeta);
    }

    [Fact]
    public void ObtenerComparacion_SinPeriodoAnterior_NoCalculaPorcentaje()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var producto = CrearProducto(bd, "A1", precio: 1_500, costo: 1_000, stock: 10);
        Assert.True(Vender(bd, new PagoSolicitado { Efectivo = 1_500 }, (producto, 1)).IsSuccess);

        var comparacion = new ReporteUtilidadService(bd.Db)
            .ObtenerComparacion(DateTime.Today, DateTime.Today).Value;

        Assert.Equal(0, comparacion.Anterior.VentasTotal);
        Assert.Equal(1_500, comparacion.Actual.VentasTotal);
        Assert.Null(comparacion.VariacionVentas);
        Assert.Null(comparacion.VariacionUtilidadNeta);
    }

    [Fact]
    public void ObtenerComparacion_ConRangoInvertido_Falla()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var resultado = new ReporteUtilidadService(bd.Db)
            .ObtenerComparacion(DateTime.Today, DateTime.Today.AddDays(-1));

        Assert.True(resultado.IsError);
    }

    [Fact]
    public void Obtener_ConRangoInvertido_Falla()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();

        var resultado = new ReporteUtilidadService(bd.Db)
            .Obtener(DateTime.Today, DateTime.Today.AddDays(-1));

        Assert.True(resultado.IsError);
    }
}
