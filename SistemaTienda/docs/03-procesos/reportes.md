# Reportes

## Estado de implementación (2026-11-21)

Base implementada en consola y en la app MAUI. Los reportes ya existentes (valorizado, stock bajo, gastos, saldos y cortes) se complementan con el reporte de utilidad por periodo.

| Reporte | Estado |
|---------|--------|
| Valorizado de inventario | ✅ Suma `Stock × Costo` actual del catálogo |
| Productos con stock bajo | ✅ `Stock` por debajo de `StockMinimo` |
| Resumen de gastos | ✅ Total y agrupación por concepto |
| Saldo con proveedores | ✅ Total por pagar |
| Ventas y utilidad | ✅ Utilidad bruta y neta por día, mes o rango, en consola y en la pestaña **Reportes** de la app |
| Ventas por día / comparativos | ⛔ Pendiente |
| Utilidad por producto | ⛔ Pendiente |
| Exportación CSV | ✅ Reporte de utilidad desde la consola y la pestaña **Reportes** de la app |
| Exportación PDF | ✅ Reporte de utilidad desde la consola y la pestaña **Reportes** de la app |
| Desglose de cobros por método | ✅ Efectivo, tarjeta y fiado en consola, app, CSV y PDF |
| Comparación con el periodo anterior | ✅ Bloque previo de la misma duración, con diferencia y porcentaje en consola y app |

## Reporte de utilidad

Fórmula implementada, con todos los importes en centavos:

```
Utilidad bruta = Ventas − Costo de ventas − Comisión de tarjeta
Utilidad neta  = Utilidad bruta − Mermas − Gastos
```

| Componente | Origen |
|------------|--------|
| Ventas | `Ventas.Total`, contando también las ventas a fiado (criterio devengado); las ventas anuladas se excluyen |
| Costo de ventas | `Ventas.CostoTotal`, congelado al momento de vender |
| Comisión de tarjeta | `Ventas.ComisionTarjeta`, calculada al cobrar |
| Mermas | `MovimientoInventario` con tipo `Salida` y motivo `Merma:%`, usando su `CostoUnitario` congelado |
| Gastos | `Gastos.Monto` de ambos orígenes (`Caja` y `Externo`) |

- El periodo se filtra por `date(Fecha)` entre la fecha inicial y la final, ambas incluidas.
- El servicio valida el permiso `Consultar` en la capa de aplicación; el dueño, el encargado y el empleado pueden consultarlo.
- La fecha final no puede ser anterior a la inicial.
- Un cambio posterior del costo en el catálogo no altera la utilidad histórica: la venta conserva su costo congelado.

### Cobros por método

`ReporteUtilidad.DesglosePagos` agrupa los cobros del periodo en **Efectivo**, **Tarjeta** y **Fiado**, cada uno con su número de ventas y su importe. Se calcula con `Ventas.PagoEfectivo`, `PagoTarjeta` y `PagoFiado`, sin contar anuladas:

- Una venta mixta suma su parte en efectivo y su parte con tarjeta, así que aparece en ambos renglones.
- El fiado se muestra aparte porque es una cuenta por cobrar, no efectivo disponible en caja.
- La comisión de tarjeta se resta en la utilidad y no se descuenta del monto cobrado con tarjeta.

### Comparación con el periodo anterior

`PeriodoReporte.Anterior` toma el bloque inmediatamente anterior de la misma duración: un día contra el día previo y un rango de 7 días contra los 7 anteriores. `ReporteUtilidadService.ObtenerComparacion` devuelve el reporte actual, el anterior y las diferencias de ventas, utilidad neta y gastos.

- La diferencia se muestra en dinero; el porcentaje solo aparece cuando el periodo anterior es distinto de cero, porque dividir entre cero no está definido.
- La comparación valida el mismo permiso `Consultar` y el mismo orden de fechas que el reporte normal.
- No cambia el periodo mostrado: es una lectura adicional para decidir, no un ajuste contable.

## Consulta en la consola

Menú `[7] Reportes` → `[5] Ventas y utilidad`:

1. Hoy.
2. Este mes.
3. Rango de fechas (`dd/mm/aaaa`).

## Consulta en la app MAUI

Pestaña **Reportes**, entre Caja y Ajustes:

1. **Hoy**, **Este mes** o **Rango**; el rango usa dos `DatePicker` y el botón **Ver**.
2. **Actualizar** vuelve a leer el periodo seleccionado.
3. El resumen muestra la utilidad neta, el número de ventas y el periodo; el desglose lista ventas cobradas, costo de ventas, comisión de tarjeta, utilidad bruta, mermas, gastos y utilidad neta.
4. La utilidad bruta y neta se colorean en verde cuando son ≥ 0 y en rojo cuando son negativas.
5. `PeriodoReporte` (Core) calcula los límites de hoy y del mes en curso con fechas sin hora; el rango normaliza ambas fechas. La pantalla llama a `ReporteUtilidadService.Obtener` y el permiso `Consultar` se valida en el servicio.
6. La pestaña está disponible para dueño, encargado y empleado porque los tres tienen `Consultar`; la pantalla solo lee.
7. **Comparar con periodo anterior** muestra una tabla con ventas, utilidad neta y gastos del periodo actual y del bloque previo de la misma duración, con diferencia en dinero y porcentaje cuando el anterior no es cero.

## Exportación CSV y PDF

`ExportadorCsv` (capa `Application`) convierte el reporte en texto CSV con encabezado `Concepto,Valor`, coma como separador, punto decimal invariante y comillas solo cuando el campo las necesita. Incluye periodo, número de ventas, ventas cobradas, costo, comisión, utilidad bruta, mermas, gastos y utilidad neta.

`ExportadorPdf` usa `PdfDocumento`, un generador PDF mínimo sin dependencias externas, para producir un documento tamaño carta con el mismo desglose. Usa las fuentes base Helvetica y Helvetica-Bold, codifica el texto en Latin-1 y agrega saltos de página automáticos; el archivo se puede abrir con cualquier lector e imprimir desde él.

- **Consola**: al terminar el reporte de utilidad pregunta si se exporta a CSV y/o PDF; los archivos se guardan en `exportaciones/` junto a la base y se muestra la ruta.
- **App MAUI**: los botones **Exportar CSV** y **Exportar PDF** escriben el archivo en la caché y abren el diálogo del sistema para compartirlo, guardarlo o imprimirlo. Es local y no requiere red.

## Limitaciones conocidas

- Las ventas a fiado cuentan como ingreso del periodo aunque el efectivo llegue después con el abono; no hay reporte de flujo de efectivo por separado (el corte de caja sí lo cubre a nivel de caja).
- Las mermas registradas antes de que existiera el costo congelado en el movimiento (esquema anterior a v4) se convierten en la migración y conservan su importe, pero no su detalle.
- No hay ventas por día ni desglose por producto o categoría; ver [deuda-tecnica.md](../deuda-tecnica.md). La exportación CSV y PDF está cubierta por pruebas y se verificó en Windows y en Android 16 el 2026-11-21; el desglose por método y la comparación entre periodos se verificaron en Windows el 2026-11-22 y quedan pendientes en Android.
