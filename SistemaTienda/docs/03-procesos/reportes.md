# Reportes

## Estado de implementación (2026-10-23)

Base implementada en consola. Los reportes ya existentes (valorizado, stock bajo, gastos, saldos y cortes) se complementan con el reporte de utilidad por periodo.

| Reporte | Estado |
|---------|--------|
| Valorizado de inventario | ✅ Suma `Stock × Costo` actual del catálogo |
| Productos con stock bajo | ✅ `Stock` por debajo de `StockMinimo` |
| Resumen de gastos | ✅ Total y agrupación por concepto |
| Saldo con proveedores | ✅ Total por pagar |
| Ventas y utilidad | ✅ Utilidad bruta y neta por día, mes o rango |
| Ventas por día / comparativos | ⛔ Pendiente |
| Utilidad por producto | ⛔ Pendiente |

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

## Limitaciones conocidas

- Las ventas a fiado cuentan como ingreso del periodo aunque el efectivo llegue después con el abono; no hay reporte de flujo de efectivo por separado (el corte de caja sí lo cubre a nivel de caja).
- Las mermas registradas antes de que existiera el costo congelado en el movimiento (esquema anterior a v4) se convierten en la migración y conservan su importe, pero no su detalle.
- No hay exportación ni desglose por producto o categoría; ver [deuda-tecnica.md](../deuda-tecnica.md).
