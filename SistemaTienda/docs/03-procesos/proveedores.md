# Módulo 7: Proveedores y Cuentas por Pagar

## 7.1. Visión General
El módulo de proveedores y cuentas por pagar permite a la tienda gestionar sus relaciones con los proveedores, llevar el control de lo que se debe pagar y registrar los pagos realizados. Este módulo es esencial para la operación eficiente de la cadena de suministro en tiendas pequeñas mexicanas.

## 7.2. Flujo de Trabajo

### 7.2.1. Nuevo Proveedor
1. El cajero o dueño selecciona la opción "[1] Nuevo proveedor" en el menú de Proveedores
2. Ingresa el nombre del proveedor (razón social o nombre comercial)
3. Ingresa el RFC o clave del proveedor (opcional, pero recomendado)
4. Ingresa el número de teléfono de contacto (opcional)
5. Ingresa la dirección del proveedor (opcional)
6. El sistema registra al proveedor con saldo inicial de $0
7. El proveedor está disponible para futuras transacciones

### 7.2.2. Ver Lista de Proveedores
1. El usuario selecciona la opción "[2] Ver lista de proveedores" en el menú de Proveedores
2. El sistema muestra todos los proveedores registrados
3. Se muestra el nombre, RFC y saldo pendiente de cada proveedor
4. Los proveedores con saldo > 0 se destacan para atención prioritaria

### 7.2.3. Registrar Pago a Proveedor
1. El usuario selecciona la opción "[3] Registrar pago a proveedor"
2. Ingresa el nombre del proveedor o busca en la lista
3. Ingresa el monto del pago
4. El sistema resta el monto del saldo pendiente
5. Si el saldo llega a $0, el proveedor se considera "al día"
6. El monto del pago se registra en el corte de caja del día

## 7.3. Estructura de Datos

### Tabla: Proveedores
| Campo | Tipo | Descripción |
|-------|------|-------------|
| Id | INTEGER PRIMARY KEY | Identificador único |
| Nombre | TEXT | Nombre del proveedor (razón social) |
| Clave | TEXT | RFC o nombre comercial |
| Telefono | TEXT | Teléfono de contacto |
| Direccion | TEXT | Dirección del proveedor |
| SaldoPendiente | INTEGER | Monto total pendiente de pago en centavos |
| FechaRegistro | DATETIME | Fecha en que se registró el proveedor |
| UltimaActualizacion | DATETIME | Última fecha de movimiento (pago/crédito) |

### Ejemplo de registro
```
Id: 1
Nombre: "Distribuidor Azul S.A. de C.V."
Clave: "BLA980501XYZR"
Telefono: "+52 55 5123 4567"
Dirección: "Calzada de Tlalpan #1456, México D.F."
SaldoPendiente: 350.50
FechaRegistro: 2024-01-10 09:15:00
UltimaActualizacion: 2024-03-25 14:30:00
```

## 7.4. Integración con el Sistema

### 7.4.1. Inventario y Compras
- Al registrar un nuevo proveedor, se puede asociar futuras compras de productos
- El saldo pendiente permite llevar el control de créditos con cada proveedor
- Los proveedores con saldo alto pueden negociarse mejores términos

### 7.4.2. Corte de Caja
- **Pago a proveedor**: Sí entra al corte de caja del día (es un egreso)
- **Crédito con proveedor**: El saldo pendiente NO entra al corte de caja hasta que se paga
- El sistema lleva el control de quiénes proveedores tienen saldo y cuánto

### 7.4.3. Reportes Financieros
- El reporte de "Total saldo pendiente a proveedores" muestra el total que la tienda debe
- Este monto se considera un pasivo a corto plazo en la posición financiera
- Se incluye en el reporte de utilidad neta como compromiso de pago futuro

### 7.4.4. Proceso de Compras Flujo Completo
```
Nueva compra a crédito
    ↓
Producto(s) registrados en inventario
    ↓
Proveedor seleccionado o creado
    ↓
Saldo pendiente actualizado automáticamente
    ↓
Pago registrado cuando se realiza
    ↓
Saldo reducido, entrada al corte de caja
    ↓
Si saldo = 0: Proveedor al día
```

## 7.5. Opciones del Menú

Opción 7 en el menú principal abre el submenú de Proveedores y Cuentas por Pagar:

```
[1] Nuevo proveedor
[2] Ver lista de proveedores
[3] Registrar pago a proveedor
[0] Regresar al menú principal
```

## 7.6. Consideraciones de Negocio

### 7.5.1. Gestión de Crédito con Proveedores
- En el MVP, no hay plazos de pago establecidos (fechas de vencimiento)
- El fiado con proveedores es abierto hasta que se realiza un pago
- No hay cálculo de intereses automáticos (fase para implementación futura)
- Se recomienda revisar periódicamente la cartera de proveedores

### 7.5.2. Priorización de Pagos
- Los proveedores con saldo pendiente alto deben ser prioritarios
- El sistema muestra el saldo de cada proveedor en la lista
- Facilita la toma de decisiones sobre qué pagar primero

### 7.5.3. Relación Proveedor - Tienda
- Se recomienda tener al menos un proveedor principal por categoría de productos
- El RFC/Clave ayuda a identificar y evitar proveedores duplicados
- El teléfono y dirección facilitan el contacto para seguimiento

### 7.5.4. Limitaciones del MVP
- No hay catálogo de productos por proveedor (relación Many-to-Many pendiente)
- No hay historial detallado de compras por proveedor (solo saldo actual)
- No hay generación automática de órdenes de compra
- No hay términos de pago automáticos (fechas de vencimiento)
- Los saldos son informativos y no tienen consecuencias legales dentro del sistema

## 7.6. Flujo de Datos Completo

```
Nuevo Proveedor
    ↓
Registrar en BD: Nombre, Clave, Teléfono, Dirección, Saldo=0
    ↓
Proveedor aparece en lista disponible
    ↓
Seleccionar proveedor al registrar compra/entrada
    ↓
Actualizar saldo pendiente al registrar pago
    ↓
Saldo reducido: NuevoSaldo = SaldoActual - MontoPago
    ↓
Si nuevoSaldo = 0: Proveedor "al día", quitado o marcado
    ↓
Monto del pago entra al corte de caja del día
    ↓
Reporte de proveedores muestra saldos actualizados
```

## 7.7. Limitaciones del MVP
- No hay límites de crédito personalizados por proveedor
- No hay cálculo de intereses automáticos
- No hay recordatorios automáticos por correo/SMS
- No hay historial detallado de compras por proveedor (solo saldo actual)
- Los saldos son informativos y no tienen consecuencias legales dentro del sistema
- No hay clasificación de proveedores (por categoría, confianza, etc.)