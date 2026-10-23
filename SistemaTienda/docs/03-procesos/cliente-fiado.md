# Módulo 6: Clientes y Fiado (Crédito al Cliente)

## 6.1. Visión General
El módulo de clientes y fiado permite a la tienda ofrecer ventas a crédito a sus clientes habituales. El sistema registra el saldo pendiente por cada cliente y lleva el control de abonos realizados. Este módulo es esencial para fidelizar clientes y aumentar las ventas en el sector minorista mexicano.

## 6.2. Flujo de Trabajo

### 6.2.1. Nueva Venta a Fiado
1. El cajero selecciona la opción "[1] Nueva venta a fiado" en el menú principal
2. Ingresa el nombre del cliente (o busca en la lista existente)
3. Ingresa opcionalmente el teléfono del cliente (único dato de contacto; no se piden RFC ni cédula por privacidad)
4. Ingresa el monto total de la compra
5. Ingresa el abono inicial (puede ser 0 para venta al contado total)
6. El sistema calcula y muestra el saldo pendiente
7. La venta se registra en la base de datos local con el saldo correspondiente

### 6.2.2. Registro de Abono
1. El cajero selecciona la opción "[3] Registrar abono a cliente"
2. Ingresa el nombre del cliente
3. Si el cliente no existe, se ofrece la opción de crearlo
4. Ingresa el monto del abono
5. El sistema resta el abono del saldo pendiente
6. Se actualiza el registro del cliente y se muestra el nuevo saldo

### 6.2.3. Ver Clientes con Saldo
1. El cajero selecciona la opción "[2] Ver lista de clientes con saldo"
2. El sistema muestra todos los clientes que tienen saldo pendiente
3. Se muestra el nombre de cada cliente y el monto en pesos

## 6.3. Estructura de Datos

### Tabla: ClientesFiado
| Campo | Tipo | Descripción |
|-------|------|-------------|
| Id | INTEGER PRIMARY KEY | Identificador único |
| Nombre | TEXT | Nombre del cliente |
| Telefono | TEXT | Teléfono de contacto (opcional) |
| SaldoActual | DECIMAL | Saldo pendiente actual |
| FechaRegistro | DATETIME | Fecha en que se registró el cliente |
| UltimaActualizacion | DATETIME | Última fecha de movimiento |

### Ejemplo de registro
```
Id: 1
Nombre: "Juan Pérez"
Telefono: "555-123-4567"
SaldoActual: 150.50
FechaRegistro: 2024-01-15 10:30:00
UltimaActualizacion: 2024-03-20 14:20:00
```

## 6.4. Opciones del Menú

Opción 6 en el menú principal abre el submenú de Clientes y Fiado:

```
[1] Nueva venta a fiado
[2] Ver lista de clientes con saldo
[3] Registrar abono a cliente
[0] Regresar al menú principal
```

## 6.5. Integración con otros módulos

### 6.5.1. Inventario
- Al registrar una venta a fiado, el inventario se reduce automáticamente (igual que una venta normal)
- El sistema descuenta el stock de los productos vendidos

### 6.5.2. Reportes
- El reporte de utilidad neta incluye el impacto de las ventas a fiado
- Las mermas afectan el inventario independientemente de la condición de pago
- Los gastos operativos se restan de la utilidad neta junto con las mermas

### 6.5.3. Corte de Caja
- Las ventas a fiado NO entran inmediatamente al corte de caja
- El saldo pendiente se registra como un activo por cobrar
- Cuando el cliente realiza un abono, ese monto sí entra al corte de caja del día
- Al final del período, los saldos por cobrar pueden ser incluidos en proyecciones de caja

## 6.6. Consideraciones de Negocio

### 6.6.1. Límite de Crédito
- La configuración de la tienda puede incluir un límite de crédito por cliente
- Si el cliente excede su límite, el sistema puede bloquear nuevas ventas a fiado
- El límite se configura en el módulo de configuración (campo LimiCreditoCliente)

### 6.6.2. Condiciones de Pago
- En el MVP, no hay plazos establecidos (payment terms)
- El fiado es abierto hasta que el cliente realiza un abono
- No hay fechas de vencimiento automáticas en la versión inicial

### 6.6.3. Recuperación de Cartera
- El sistema muestra quiénes tienen saldo pendiente
- Facilita la identificación de qué clientes contactar para pago
- Los datos están disponibles para generar recordatorios manuales o WhatsApp

### 6.6.4. Riesgo y Control
- Se recomienda establecer límites de crédito por cliente
- El dueño o encargado debe revisar periódicamente la cartera
- El sistema no cobra intereses automáticos (fase para implementación futura)

## 6.7. Flujo de Datos Completo

```
Nueva Venta a Fiado
    ↓
    Productos seleccionados → Stock reducido
    ↓
    Cliente registrado → Saldo inicial = Total - Abono Inicial
    ↓
    Venta guardada en BD con estado "a crédito"
    ↓
    Cliente aparece en "Lista de clientes con saldo"
    ↓
    Cliente realiza abono → Saldo reducido
    ↓
    Si saldo = 0: Cliente retirado de la lista
    ↓
    Abono registrado en corte de caja del día
```

## 6.8. Limitaciones del MVP
- No hay límites de crédito personalizados por cliente (se usa el configurado en ajustes)
- No hay cálculo de intereses automáticos
- No hay recordatorios automáticos por WhatsApp/SMS
- No hay historial de pagos detallado por cliente (solo saldo actual)
- Los saldos son informativos y no tienen consecuencias legales dentro del sistema

## 6.9. Estado de implementación (2026-10-23)

- *Implementado*: alta de cliente con nombre, teléfono y saldo inicial; consulta de clientes con saldo; abono con validación de monto positivo y de no exceder el saldo; el menú de fiado solo aparece si `FiadoActivado` es verdadero.
- *Pendiente*: venta a fiado (depende del módulo de ventas), historial de abonos y ventas por cliente, cancelación de venta a fiado, límite de crédito por cliente.
- *Deuda*: el abono actualiza el saldo sin registrar un movimiento inmutable (ver [deuda-técnica](../deuda-tecnica.md)).
- *Pruebas*: `FiadoProveedoresTests`.