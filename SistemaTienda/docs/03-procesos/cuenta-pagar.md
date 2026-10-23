# Proveedores y Cuentas por Pagar

## Proveedor

### Datos Básicos

- **Nombre**: Razón social o nombre del proveedor (obligatorio).
- **Contacto**: Nombre de la persona de contacto, teléfono, email.
- **Crédito otorgado**: Si el proveedor vende a crédito y por cuánto (opcional, para referencia).

## Compra a Crédito

### Flujo de Compra

1. **Seleccionar proveedor**: Elegir de la lista de proveedores registrados.
2. **Registrar compra**: Ingresar los productos comprados, cantidades y costos.
3. **Actualizar inventario**: Los productos entran automáticamente al stock.
4. **Generar deuda**: Se crea o actualiza el saldo por pagar al proveedor.

### Reglas de Negocio

- **El inventario se actualiza al instante**: Al registrar la compra, el stock de los productos aumenta.
- **El saldo por pagar se crea**: El sistema registra cuánto se debe al proveedor.
- **Factura opcional**: Se puede adjuntar el archivo de la factura (foto PDF) al registro de la compra.

## Pago a Proveedor

### Flujo de Pago

1. **Ver saldo actual**: El sistema muestra cuánto se debe al proveedor.
2. **Registrar pago**: Elegir monto a pagar (parcial o total) y método de pago (efectivo, transferencia, tarjeta).
3. **Reducir saldo**: El saldo del proveedor se actualiza restando el pago.
4. **Comprobante**: Se solicita guardar el comprobante de pago (foto o PDF).

### Reglas Críticas

- **Pago parcial**: Se puede pagar parte del saldo; el restante queda por pagar.
- **Pago total**: Si el pago cubre el saldo completo, el proveedor se marca como "saldo 0" y se cierra la cuenta.
- **El pago reduce la cuenta por pagar**: El efectivo sale de la caja (si es en efectivo) o se registra la transferencia, pero la cuenta por pagar se reduce inmediatamente.

## Estado de Cuenta

El sistema debe mostrar:

- **Saldo actual**: Cuánto se debe en este momento al proveedor.
- **Última compra**: Fecha de la última transacción con ese proveedor.
- **Próxima fecha de referencia**: Si el proveedor tiene términos (ej. "pago en 15 días"), la fecha límite.
- **Historial de pagos**: Lista de pagos realizados, fechas y montos.

## Integración con Otros Módulos

- **Compras**: Cada compra a crédito actualiza el inventario y la cuenta por pagar.
- **Inventario**: Los productos comprados entran al stock inmediatamente.
- **Gastos**: El pago a proveedor puede registrarse como un gasto (ej. si hay comisión de transferencia).
- **Corte de caja**: Si el pago es en efectivo, reduce el efectivo esperado del corte del día.

## Reporte de Cuentas por Pagar

- **Proveedores con saldo**: Lista de quiénes me deben pagar y cuánto.
- **En Vencimiento**: Cuáles tienen pagos próximos o atrasados (si hay fechas de referencia).
- **Envejecimiento**: Cuánto tiempo lleva cada saldo por pagar (días desde la compra).
- **Total por pagar**: Suma de todos los saldos pendientes.

## Casos Especiales

- **Proveedor nuevo**: Se registra en caliente al momento de la primera compra.
- **Compra sin proveedor registrado**: El sistema permite registrar una compra "al mercado" o sin proveedor formal; el saldo por pagar no se crea o se crea con un proveedor genérico.
- **Devolución a proveedor**: Se registra como un movimiento inverso: se reduce el inventario y se reduce el saldo por pagar, con motivo "devolución".