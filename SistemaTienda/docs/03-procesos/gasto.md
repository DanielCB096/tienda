# Gastos y Pagos

## Registro de Gastos

### Flujo

1. **Concepto**: Se ingresa qué es el gasto (renta, luz, agua, mantenimiento, nómina, salarios, bolsas, productos de limpieza, etc.).
2. **Monto**: El amount exacto pagado.
3. **Fecha**: Por defecto la fecha actual, pero se puede retroactivar.
4. **Método de pago**: Efectivo (si salió de la caja) o Tarjeta (si salió de la terminal), Transferencia, Otro.
5. **Origen**: ¿Saldó de la Caja? o ¿Saldó de otra fuente? (ej. transferencia empresarial, tarjeta corporativa).

### Reglas de Negocio

- **Los gastos afectan la utilidad neta**: Se restan de la utilidad bruta.
- **Gasto de caja vs. gasto externo**:
  - Si salió de la **caja**: Reduce el efectivo esperado en el corte de caja del día.
  - Si es **externo** (ej. transferencia bancaria): No afecta el corte de caja directo, pero sí la utilidad neta.
- **Comisión de tarjeta**: Si el gasto es una comisión de terminal bancaria, se registra y el sistema permite configurar el % para reflejarlo en la utilidad real.

### Gastos Recurrentes con Recordatorio

- El sistema permite configurar gastos periódicos (renta mensual, sueldo quincenal, servicio de agua mensual).
- Se muestra un indicador en la pantalla principal: "Próximo gasto: X días".
- Opcional: Generar aviso/alerta antes de que venzan.

### Tipos de Gastos por Categoría

- **Personal**: Sueldos, salarios, comisiones, adelantos, propinas (registrados como egresos, no nómina formal).
- **Insumos de operación**: Bolsas para empaque, papel térmico, productos de limpieza, empaques.
- **Servicios**: Luz, agua, internet, teléfono, gas.
- **Mantenimiento**: Mantenimiento de equipo, reparaciones, servicio técnico.
- **Financieras**: Comisiones de tarjeta, intereses, multas.

## Integración con Corte de Caja

- **Efectivo Esperado** (fórmula completa):
  ```
  Efectivo Esperado = Fondo Inicial
                        + Ventas Efectivo
                        + Abonos Efectivo Fiado
                        - Retiros de Caja
                        - Gastos Pagados de Caja
  ```
- Los gastos pagados con efectivo de caja **sí restan** al efectivo esperado.
- Los gastos pagados con tarjeta o transferencia **no afectan** directamente el efectivo esperado (ya que no hay efectivo saliendo de la caja), pero sí reducen la utilidad neta.

## Reporte de Gastos

El sistema debe poder reportar:

- **Gastos por categoría**: Cuánto se gastó en cada rubro (personal, insumos, servicios, etc.) por período.
- **Gastos recurrentes**: Qué gastos están configurados como periódicos y cuándo son los próximos.
- **Gasto por fecha**: Listado de todos los gastos registrados en un rango de fechas.
- **Top de gastos**: Los gastos más altos o frecuentes.

## Casos Especiales

- **Comisión de tarjeta por venta**: Es un gasto que se configura por porcentaje sobre las ventas con tarjeta. Ejemplo: si la comisión es 3.5% y se vendieron $1,000 en tarjeta, el gasto es $35. Esto afecta la utilidad neta pero no el corte de caja directo (ya que la comisión la cobra la terminal, no la tienda directamente en efectivo).
- **Pago de nómina/sueldos**: Registrados como gastos, pero el sistema deja la puerta abierta a cálculos formales de nómina, IMSS, ISR en el futuro (solo registra el egreso, no calcula retenciones).