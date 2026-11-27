using SistemaTienda.Application.Services;
using SistemaTienda.Core;

namespace SistemaTienda.Tests;

/// <summary>
/// Simula un día completo con ventas de todos los tipos, descuentos,
/// anulaciones, reembolsos, abono, gasto, merma y compra. Comprueba que los
/// totales de ventas, reportes y caja cuadren entre sí.
/// </summary>
public sealed class DiaCompletoTests
{
    private static Producto CrearProducto(BaseDatosPrueba bd, string codigo, string nombre,
        string categoria, long precio, long costo, int stock = 20)
    {
        var producto = new Producto
        {
            CodigoBarras = codigo,
            Nombre = nombre,
            Categoria = categoria,
            Costo = costo,
            Precio = precio,
            Stock = stock,
            Impuesto = 16m
        };

        Assert.True(bd.Inventario.AgregarProducto(producto).IsSuccess);
        return producto;
    }

    private static Venta Vender(BaseDatosPrueba bd, PagoSolicitado pago,
        params (Producto Producto, int Cantidad, decimal Porcentaje, long Monto)[] lineas)
    {
        var solicitud = lineas
            .Select(l => new LineaVentaSolicitada
            {
                ProductoId = l.Producto.Id,
                Cantidad = l.Cantidad,
                DescuentoPorcentaje = l.Porcentaje,
                DescuentoMonto = l.Monto
            })
            .ToList();

        var resultado = new VentaService(bd.Db).RegistrarVenta(solicitud, pago, "Cajero");
        Assert.True(resultado.IsSuccess, resultado.Message);
        return resultado.Value;
    }

    [Fact]
    public void DiaCompleto_CuadraVentasReportesYCaja()
    {
        using var bd = new BaseDatosPrueba();
        bd.ConfigurarTienda();
        var ventas = new VentaService(bd.Db);

        var arroz = CrearProducto(bd, "DIA1", "Arroz", "Abarrotes", precio: 1_000, costo: 600);
        var refresco = CrearProducto(bd, "DIA2", "Refresco", "Bebidas", precio: 2_000, costo: 1_200);
        var pan = CrearProducto(bd, "DIA3", "Pan", "Abarrotes", precio: 500, costo: 300);

        // 1. Efectivo con cambio.
        Vender(bd, new PagoSolicitado { Efectivo = 4_000, EfectivoRecibido = 5_000 },
            (arroz, 2, 0m, 0L), (refresco, 1, 0m, 0L));

        // 2. Tarjeta.
        Vender(bd, new PagoSolicitado { Tarjeta = 1_000 },
            (arroz, 1, 0m, 0L));

        // 3. Mixta.
        Vender(bd, new PagoSolicitado { Efectivo = 1_500, Tarjeta = 1_500 },
            (refresco, 1, 0m, 0L), (pan, 2, 0m, 0L));

        // 4. Fiado.
        var cliente = new ClienteFiado { Nombre = "Cliente del día" };
        Assert.True(bd.Config.AgregarClienteFiado(cliente).IsSuccess);
        var clienteGuardado = bd.Config.ObtenerTodosLosClientes().Value.Single();
        Vender(bd, new PagoSolicitado { Fiado = 2_000, ClienteId = clienteGuardado.Id },
            (pan, 4, 0m, 0L));

        // 5. Descuentos distintos por producto: 10% en arroz y $3.00 en refresco.
        Vender(bd, new PagoSolicitado { Efectivo = 900, Tarjeta = 1_700 },
            (arroz, 1, 10m, 0L), (refresco, 1, 0m, 300L));

        // 6. Venta en efectivo que se anula y se reembolsa completa.
        var aReembolsar = Vender(bd, new PagoSolicitado { Efectivo = 1_000 },
            (arroz, 1, 0m, 0L));
        Assert.True(ventas.AnularVenta(aReembolsar.Id, "Cliente devolvió el producto", "Dueño").IsSuccess);
        Assert.True(ventas.RegistrarReembolso(aReembolsar.Id,
            MetodoPagoAbono.Efectivo, 1_000, "Dueño").IsSuccess);

        // 7. Venta con tarjeta que se anula y se reembolsa a la mitad.
        var reembolsoParcial = Vender(bd, new PagoSolicitado { Tarjeta = 2_000 },
            (refresco, 1, 0m, 0L));
        Assert.True(ventas.AnularVenta(reembolsoParcial.Id, "Cargo duplicado", "Dueño").IsSuccess);
        Assert.True(ventas.RegistrarReembolso(reembolsoParcial.Id,
            MetodoPagoAbono.Tarjeta, 1_000, "Dueño").IsSuccess);

        // Abonos: uno en efectivo y otro por transferencia.
        Assert.True(bd.Config.RegistrarAbonoCliente(clienteGuardado.Id, 500, "Dueño",
            MetodoPagoAbono.Efectivo).IsSuccess);
        Assert.True(bd.Config.RegistrarAbonoCliente(clienteGuardado.Id, 300, "Dueño",
            MetodoPagoAbono.Transferencia).IsSuccess);

        // Gasto de caja y gasto externo.
        Assert.True(bd.Config.AgregarGasto(new Gasto
        {
            Concepto = "Bolsas",
            Monto = 450,
            MetodoPago = MetodoPagoAbono.Efectivo,
            Origen = OrigenGasto.Caja
        }).IsSuccess);
        Assert.True(bd.Config.AgregarGasto(new Gasto
        {
            Concepto = "Renta",
            Monto = 1_200,
            MetodoPago = MetodoPagoAbono.Transferencia,
            Origen = OrigenGasto.Externo
        }).IsSuccess);

        // Merma de dos panes.
        Assert.True(bd.Inventario.RegistrarMerma(pan.Id, 2, "Caducado", "Dueño").IsSuccess);

        // Compra que actualiza el costo del arroz al último costo.
        var proveedor = new Proveedor { Nombre = "Proveedor del día" };
        Assert.True(bd.Config.AgregarProveedor(proveedor).IsSuccess);
        var proveedorGuardado = bd.Config.ObtenerTodosLosProveedores().Value.Single();
        Assert.True(bd.Config.RegistrarCompraProveedor(proveedorGuardado.Id, new[]
        {
            new LineaCompraSolicitada { ProductoId = arroz.Id, Cantidad = 10, CostoUnitario = 700 }
        }, "Reposición").IsSuccess);

        // ---- Ventas del día ----
        var ventasDelDia = bd.Db.ObtenerVentasDelDia(DateTime.Today);
        Assert.Equal(7, ventasDelDia.Count);
        var vigentes = ventasDelDia.Where(v => !v.Anulada).ToList();
        Assert.Equal(5, vigentes.Count);
        Assert.Equal(12_600, vigentes.Sum(v => v.Total));
        Assert.Equal(2, ventasDelDia.Count(v => v.Anulada));

        // ---- Reporte de utilidad ----
        var reporte = new ReporteUtilidadService(bd.Db)
            .Obtener(DateTime.Today, DateTime.Today).Value;

        Assert.Equal(5, reporte.NumeroVentas);
        Assert.Equal(12_600, reporte.VentasTotal);
        Assert.Equal(7_800, reporte.CostoVentas);
        Assert.Equal(148, reporte.ComisionTarjeta);
        Assert.Equal(4_652, reporte.UtilidadBruta);
        Assert.Equal(600, reporte.Mermas);
        Assert.Equal(1_650, reporte.Gastos);
        Assert.Equal(2_402, reporte.UtilidadNeta);
        Assert.True(reporte.ImpuestoIncluido > 0);

        var efectivo = reporte.DesglosePagos.Single(d => d.Metodo == MetodoPagoVenta.Efectivo);
        var tarjeta = reporte.DesglosePagos.Single(d => d.Metodo == MetodoPagoVenta.Tarjeta);
        var fiado = reporte.DesglosePagos.Single(d => d.Metodo == MetodoPagoVenta.Fiado);
        Assert.Equal(6_400, efectivo.Monto);
        Assert.Equal(3, efectivo.NumeroVentas);
        Assert.Equal(4_200, tarjeta.Monto);
        Assert.Equal(3, tarjeta.NumeroVentas);
        Assert.Equal(2_000, fiado.Monto);
        Assert.Equal(1, fiado.NumeroVentas);

        var arrozReporte = reporte.Productos.Single(p => p.ProductoId == arroz.Id);
        Assert.Equal(3_900, arrozReporte.Ventas);
        Assert.Equal(4, arrozReporte.Cantidad);
        Assert.Equal(1_500, arrozReporte.Utilidad);
        var refrescoReporte = reporte.Productos.Single(p => p.ProductoId == refresco.Id);
        Assert.Equal(5_700, refrescoReporte.Ventas);
        Assert.Equal(3, refrescoReporte.Cantidad);
        Assert.Equal(2_100, refrescoReporte.Utilidad);
        var panReporte = reporte.Productos.Single(p => p.ProductoId == pan.Id);
        Assert.Equal(3_000, panReporte.Ventas);
        Assert.Equal(6, panReporte.Cantidad);
        Assert.Equal(1_200, panReporte.Utilidad);

        Assert.Equal(6_900, reporte.Categorias.Single(c => c.Categoria == "Abarrotes").Ventas);
        Assert.Equal(5_700, reporte.Categorias.Single(c => c.Categoria == "Bebidas").Ventas);

        var dia = Assert.Single(reporte.Dias);
        Assert.Equal(5, dia.NumeroVentas);
        Assert.Equal(12_600, dia.Ventas);
        Assert.Equal(4_652, dia.Utilidad);

        // ---- Reembolsos ----
        var estadoCompleto = ventas.ObtenerEstadoReembolso(aReembolsar.Id).Value;
        Assert.Equal(1_000, estadoCompleto.Reembolsado);
        Assert.Equal(0, estadoCompleto.Pendiente);

        var estadoParcial = ventas.ObtenerEstadoReembolso(reembolsoParcial.Id).Value;
        Assert.Equal(1_000, estadoParcial.Reembolsado);
        Assert.Equal(1_000, estadoParcial.Pendiente);

        // ---- Caja ----
        var pendiente = bd.Db.ObtenerResumenCajaPendiente();
        Assert.Equal(6_400, pendiente.VentasEfectivo);
        Assert.Equal(500, pendiente.AbonosEfectivo);
        Assert.Equal(1_000, pendiente.RetirosCaja);
        Assert.Equal(450, pendiente.GastosCaja);
        Assert.Equal(25_450, pendiente.EfectivoEsperado);

        // ---- Stock y costo ----
        Assert.Empty(bd.Db.ProductosDescuadrados());
        Assert.Equal(700, bd.Inventario.BuscarPorId(arroz.Id).Costo);
        Assert.Equal(26, bd.Inventario.BuscarPorId(arroz.Id).Stock);
        Assert.Equal(17, bd.Inventario.BuscarPorId(refresco.Id).Stock);
        Assert.Equal(12, bd.Inventario.BuscarPorId(pan.Id).Stock);

        // ---- Corte ciego que debe cerrar sin diferencia ----
        var corte = new CorteCajaService(bd.Db).RegistrarCorte(
            pendiente.EfectivoEsperado, 20_000, "Dueño", "Cierre del día completo");
        Assert.True(corte.IsSuccess, corte.Message);
        Assert.Equal(0, corte.Value.Diferencia);
        Assert.Equal(25_450, corte.Value.EfectivoContado);
    }
}
