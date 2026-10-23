# Corte de Caja

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

### Integración con otros módulos

- **Ventas**: Cada venta registrada actualiza automáticamente los totales del corte.
- **Gastos**: Los gastos pagados de caja afectan el "Efectivo Esperado" (se restan).
- **Fiado**: Los abonos en efectivo de fiado entran al corte del día en que se cobran.
- **Utilidad**: El efectivo del corte es base para calcular la utilidad neta del período.