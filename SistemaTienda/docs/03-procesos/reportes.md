# Reportes

## Estado de implementación (2026-11-12)

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
| Exportación o impresión | ⛔ Pendiente |

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

## Limitaciones conocidas

- Las ventas a fiado cuentan como ingreso del periodo aunque el efectivo llegue después con el abono; no hay reporte de flujo de efectivo por separado (el corte de caja sí lo cubre a nivel de caja).
- Las mermas registradas antes de que existiera el costo congelado en el movimiento (esquema anterior a v4) se convierten en la migración y conservan su importe, pero no su detalle.
- No hay exportación ni impresión del reporte, comparación entre periodos, ni desglose por producto, método o categoría; ver [deuda-tecnica.md](../deuda-tecnica.md). La pantalla de la app aún no se verifica manualmente en Windows ni Android.
