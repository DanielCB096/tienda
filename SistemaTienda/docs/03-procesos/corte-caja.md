# Corte de Caja

## Estado de implementación (2026-10-25)

Base implementada en consola y en la app MAUI sobre el esquema vigente (v6, importes en centavos, bitácora de cortes y movimientos y anulaciones de venta). El corte cierra un periodo, concilia el efectivo y queda inmutable.

| Elemento | Estado |
|----------|--------|
| Periodo | ✅ Desde el `Hasta` del corte anterior (exclusivo). El primero arranca en el primer movimiento registrado |
| Fondo inicial | ✅ Se sugiere el efectivo contado del corte anterior o el configurado; el cajero puede ajustarlo |
| Ventas por método | ✅ Totales y número de transacciones (efectivo, tarjeta, mixto, fiado) |
| Abonos en efectivo | ✅ Nuevo historial `AbonosFiado`; los abonos previos a la v3 no tienen registro |
| Ingresos y retiros | ✅ `MovimientosCaja` con monto, motivo y usuario |
| Gastos de caja | ✅ Solo los gastos con origen `Caja` restan; los `Externo` no |
| Diferencia | ✅ Si no es cero, el motivo es obligatorio y el corte no se guarda sin él |
| Inmutabilidad | ✅ No hay edición ni borrado: se corrige con un movimiento o corte nuevo |
| Ventas anuladas | ✅ Se excluyen de los totales; una venta incluida en un corte cerrado ya no se puede anular |
| Permisos y aprobación de retiros | ✅ Corte e ingreso para dueño/encargado; retiro solo dueño; validado en `CorteCajaService` |
| Operación desde la app MAUI | ✅ Cierre, ingresos, retiros y abonos de fiado en la pestaña Caja; verificado en Windows el 2026-10-25 |
| Conteo por denominaciones e historial en la app | ⛔ Pendiente; la app captura el total contado y solo muestra el último corte cerrado |

## Operación desde la app MAUI

La pestaña **Caja** reutiliza `CorteCajaService` y `ConfiguracionService`; no reimplementa la fórmula ni los permisos. Muestra el periodo abierto con el desglose por método, el efectivo esperado y el último corte cerrado.

- **Cierre**: el cajero confirma el fondo inicial y captura el efectivo contado. La diferencia se calcula en vivo; si no es cero, las observaciones son obligatorias y el servicio rechaza el cierre si faltan. Antes de guardar se pide confirmación porque el periodo queda inmutable. El siguiente periodo arranca con lo contado.
- **Ingresos y retiros**: piden monto y motivo. El retiro confirma antes de guardar y solo lo autoriza el dueño; un rechazo del servicio se muestra en la propia pantalla.
- **Abonos de fiado**: se muestran solo si el fiado está activado; el cliente se elige de la lista con saldo y el abono se registra como efectivo del periodo.

Las validaciones de negocio siguen en `Application`: la app solo captura y muestra errores. El detalle de la pantalla está en [interfaz-maui.md](../02-modulos/interfaz-maui.md).

## Fórmula implementada

```
Efectivo Esperado = Fondo Inicial
                      + Ventas cobradas en efectivo   (incluye la parte en efectivo de pagos mixtos)
                      + Abonos de fiado en efectivo
                      + Ingresos de caja
                      - Retiros de caja
                      - Gastos con origen Caja

Diferencia = Efectivo Contado - Efectivo Esperado
```

Los totales y el registro del corte se calculan y guardan dentro de una sola transacción, de modo que el corte corresponde exactamente a los datos guardados. Todos los importes se guardan en centavos enteros y se suman sin redondeos intermedios.

### Reglas de negocio implementadas

1. **Periodos contiguos**: el nuevo periodo empieza después del `Hasta` del corte anterior, así que ninguna venta, abono o movimiento se cuenta dos veces.
2. **Primer corte**: abarca todo el historial registrado; el cajero debe indicar el fondo que había al inicio de ese periodo.
3. **Motivo obligatorio**: una diferencia distinta de cero exige observaciones; sin ellas el corte se rechaza y no se guarda.
4. **Gastos**: solo restan los marcados como pagados del efectivo de la caja (`Origen = Caja`).
5. **Abonos**: se registran como efectivo; ni la consola ni la app preguntan el método del abono.
6. **Responsable**: se guarda el usuario que cierra el corte junto con la fecha y hora.
7. **Sin borrado**: no existe operación para editar o eliminar un corte o un movimiento.

## Apertura

1. **Fondo inicial**: Se registra el monto de efectivo que se inicia en la caja (ej. $200.00).
2. **Ventas en efectivo vs. tarjeta**: Desde el inicio, las ventas se clasifican por método de pago.
3. **Retiros e ingresos**: Durante el día, si se retira efectivo o se deposita ingresos extra, se registran con motivo y usuario.

## Cierre (al final del día o turno)

### Pasos

1. **Ventas del día**: El sistema muestra:
   - Total de ventas en efectivo
   - Total de ventas con tarjeta
   - Total de ventas a fiado (si está activado)
   - Número de transacciones por método

2. **Efectivo esperado** (fórmula crítica):
   ```
   Efectivo Esperado = Fondo Inicial
                         + Ventas en Efectivo del Día
                         + Abonos en Efectivo recibidos hoy
                         + Ingresos Extra en Caja (si los hubo)
                         - Retiros de Caja (si los hubo)
                         - Gastos pagados de Caja
   ```

3. **Efectivo Contado**: El cajero cuenta el dinero físico que queda en la caja.

4. **Diferencia**:
   ```
   Diferencia = Efectivo Contado - Efectivo Esperado
   ```

5. **Registro de responsable**: Se anota quién fue el cajero responsable del corte.

### Posibles resultados

- **Sin diferencia**: Corte perfecto, se registra y se reinicia con nuevo fondo.
- **Diferencia positiva**: Hay más dinero del esperado. Se investiga si:
  - Hay billetes/o monedas no registrados.
  - Se realizó un abono de fiado en efectivo que no se contabilizó bien.
- **Diferencia negativa**: Faltante de efectivo. Se investiga si:
  - Error en el registro de ventas.
  - Retiro no autorizado.
  - Fallo en el cálculo de vuelto.

### Bitácora requerida

- **Quién**: Nombre del cajero.
- **Cuándo**: Fecha y hora del corte.
- **Qué**: Resultado (sin diferencia, positiva, negativa).
- **Motivo** (si hay diferencia): Descripción de las causas investigadas.

Implementado en `CortesCaja`: `Usuario`, `FechaRegistro`, `Diferencia` y `Observaciones`. Los cortes y movimientos de caja también se registran en la bitácora general (`Bitacora`) con antes/después.

### Integración con otros módulos

- **Ventas**: Cada venta registrada actualiza automáticamente los totales del corte.
- **Gastos**: Los gastos pagados de caja afectan el "Efectivo Esperado" (se restan).
- **Fiado**: Los abonos en efectivo de fiado entran al corte del día en que se cobran.
- **Utilidad**: El efectivo del corte es base para calcular la utilidad neta del período.