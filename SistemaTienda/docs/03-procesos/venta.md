# Módulo 3: Ventas / Punto de Venta

## Estado de implementación (2026-10-23)

Base implementada en `feat/ventas-pos` (esquema v2; la anulación se agregó en v6). La venta se opera en la consola provisional y en la app MAUI (T13); la anulación sigue solo en la consola.

| Función | Estado |
|---------|--------|
| Venta con varias líneas en una transacción | ✅ Implementado (`VentaService`, `DatabaseTools.RegistrarVenta`) |
| Precio y costo congelados por línea | ✅ Implementado (`VentaLineas.PrecioUnitario`, `CostoUnitario`) |
| Descuento de stock y movimiento de inventario atómicos | ✅ Implementado; stock insuficiente revierte todo |
| Pago en efectivo (con cambio calculado en pantalla) | ✅ Implementado |
| Pago con tarjeta y comisión configurable | ✅ Implementado; la comisión se guarda en la venta |
| Pago mixto (efectivo + tarjeta) | ✅ Implementado |
| Venta a fiado (exclusiva, valida límite de crédito) | ✅ Implementado |
| Ticket interno en pantalla | ✅ Implementado (texto en consola y app) |
| Compartir o copiar el ticket | ✅ Implementado en la app MAUI (T13) |
| Descuentos | ⛔ Fase 2 (no incluidos en MVP) |
| Anulación posterior de una venta | ✅ Implementado (T11, esquema v6): venta completa, permiso dueño/encargado, devolución de stock y reverso de fiado |
| Reporte de utilidad por periodo | ✅ Implementado (T5); el detalle por venta sigue pendiente |
| Integración con corte de caja | ✅ Implementado: las ventas anuladas se excluyen del corte pendiente y no se pueden anular tras un corte cerrado |

Decisiones aplicadas: el precio de catálogo incluye impuesto y no se recalcula; el fiado no se combina con otros métodos; no hay ventas en espera (una a la vez); el sistema no se integra con la terminal bancaria.

## Punto de venta en la app MAUI (T13)

La pestaña **Vender** replica el flujo de la consola sobre los mismos servicios:

1. El cajero escanea un código (el lector envía Enter) o escribe un nombre. Un código exacto agrega el producto; un nombre muestra hasta 10 coincidencias para elegir.
2. El carrito valida el stock disponible en cada alta o incremento; se puede reducir la cantidad o quitar la línea.
3. El total se calcula con `Dinero.Multiplicar` y se muestra al instante.
4. **Cobrar** abre el panel de pago: efectivo (calcula el cambio y bloquea faltantes), tarjeta (muestra la comisión configurada), mixto (exige que la suma sea el total) o fiado (cliente y límite de crédito).
5. Al confirmar, `VentaService.RegistrarVenta` guarda la venta y descuenta el stock en una transacción. La app limpia el carrito y muestra el ticket.
6. El ticket interno (no fiscal, sin QR) se puede compartir con el sistema o copiar al portapapeles.

No incluye anulación, descuentos ni cierre de corte desde la app; esas operaciones siguen en la consola. La interfaz no tiene pruebas automatizadas: la verificación es manual y la compilación en CI. El QR del ticket (regla 7) sigue pendiente para una tarea posterior.

## Anulación de Venta (T11, esquema v6)

Reglas confirmadas con el usuario:

| Decisión | Descripción |
|----------|-------------|
| **Solo venta completa** | No hay anulación por línea ni devoluciones parciales en esta versión. |
| **Bloqueo tras corte cerrado** | Si la venta ya quedó incluida en un corte de caja cerrado, no se puede anular; la corrección se hace con movimientos nuevos. |
| **Reembolso manual** | La anulación no genera un movimiento de caja automático. La venta se excluye del corte pendiente; si el efectivo se devuelve después de ese corte, el dueño lo registra como retiro en el corte siguiente. |

Flujo:

1. Menú Ventas → [3] Anular venta (dueño o encargado).
2. Se captura el número de venta y se muestran fecha, total, método y líneas.
3. Se exige el motivo y se confirma escribiendo `ANULAR`.
4. En una sola transacción se registra la anulación en `AnulacionesVenta`, se devuelve el stock con el costo congelado de cada línea y, si hubo fiado, se descuenta del saldo del cliente.
5. La acción queda en la bitácora como `AnulacionVenta` con usuario, motivo y datos de la venta. La venta original no se edita ni se borra.

Las ventas anuladas se excluyen del corte pendiente y del reporte de utilidad, y aparecen marcadas `[ANULADA]` en las ventas del día. Si el reembolso en efectivo se paga en el mismo periodo, no se registra retiro: la exclusión de la venta ya refleja la devolución. Si se paga después de cerrar el corte, se registra como retiro en el corte donde sale el efectivo.

## Flujo Simplificado de Venta (Decisiones Confirmadas)

### Estado del Módulo: Base implementada en consola

### Decisiones Clave (Confirmadas con el usuario)

| Decisión | Descripción |
|----------|-------------|
| **Sin ventas en espera** | Una venta a la vez por cajero. El sistema no permite iniciar una nueva venta mientras haya una en progreso. |
| **Precio incluye impuesto** | El campo `Precio` en `Producto` ya considera el impuesto. Al vender, se muestra el total directamente sin cálculos adicionales: `Total = Precio × Cantidad`. |
| **No hay descuentos en MVP** | El precio final es el precio de catálogo × cantidad. Los descuentos quedan para Fase 2. |
| **QR en ticket** | El ticket digital generado incluirá un código QR con la información de la venta (enlace a detalles o pago sencillo). |

### Flujo de Venta Simplificado

```
1. ESCANEAR / BUSCAR producto → el producto aparece en el carrito temporal
2. INGRESAR cantidad (por defecto 1, el sistema valida que haya stock suficiente)
3. MOSTRAR TOTAL al instante → Total = Precio × Cantidad (el impuesto ya viene incluido en el precio de catálogo)
4. SELECCIONAR MÉTODO DE PAGO
   - EFECTIVO: el cajero ingresa el monto recibido
     * El sistema calcula el cambio: Cambio = Recibido - Total
     * Si el recibido es menor que el total, se bloquea y avisa "monto insuficiente"
   - TARJETA: se registra el método de pago
     * No se integra con terminal bancaria, solo se anota "Pagó con tarjeta"
     * La comisión configurada (en Configuración) se resta automáticamente de la utilidad neta
   - MIXTO: diseñado pero opcional en MVP (puede añadirse después)
   - FIADO: diseñado pero opcional en MVP (requiere módulo de clientes activado)
5. GENERAR TICKET DIGITAL
   - Se crea un registro con: nombre tienda, fecha, hora, lista de productos (código, nombre, cantidad, precio unitario, total por línea), subtotal, total, método de pago.
   - El ticket incluye un CÓDIGO QR que contiene la información de la venta (o un enlace a los detalles).
   - Opciones de compartir (una vez generado):
     * WhatsApp (función compartir del sistema, adjunta el ticket o texto con los datos).
     * Correo electrónico (adjunta el ticket como archivo o texto en el cuerpo).
     * Mostrar en pantalla (para que el cliente lo fotografíe).
     * Guardar en Galería (foto del ticket).
6. REDUCIR STOCK AUTOMÁTICAMENTE
   - El stock del producto se reduce de forma atómica en el momento de finalizar la venta.
   - Si el stock es insuficiente en el momento de la venta, se bloquea con mensaje: "Stock insuficiente. Stock actual: X, necesario: Y".
   - La venta nunca queda a medias: o se guarda todo (stock reducido, registro, ticket) o se guarda nada (rollback).
7. FINALIZAR VENTA
   - El cajero puede:
     * Hacer otra venta (el sistema está listo para una nueva transacción inmediata).
     * Ir al menú de corte de caja.
     * Ver el historial de ventas del día.

### Integración con Otros Módulos

- **Inventario (Módulo 2)**: Cada venta reduce automáticamente el stock del producto vendido (transacción atómica). Si el stock es insuficiente, la venta se bloquea. La anulación devuelve el stock con el costo congelado de la venta original.
- **Configuración (Módulo 1)**: El precio en catálogo ya incluye el impuesto configurado. Los métodos de pago activados y la comisión de tarjeta vienen de la configuración.
- **Respaldo (Módulo 7)**: Cada venta registrada se incluye automáticamente en la base de datos SQLite; los respaldos incluyen el historial de ventas.

### Reglas de Negocio (NO Negociables)

1. **Venta atómica**: Una venta nunca queda a medias. Si falla la validación de stock, se anula todo.
2. **Precio incluye impuesto**: El precio de catálogo es el precio final. No se calcula impuesto adicional al momento de la venta.
3. **Método de pago por venta**: Efectivo, Tarjeta, Mixto o Fiado. El sistema NO se integra con la terminal bancaria, solo registra el método.
4. **Comisión de tarjeta**: Es configurable por tienda (en Configuración) y se resta automáticamente de la utilidad neta sobre las ventas con método tarjeta.
5. **Historial inmodificable**: No se pueden editar ventas pasadas. Si hay error, se anula la venta completa (con motivo, permiso y bitácora) y se registra una nueva venta correcta.
6. **Ticket no fiscal**: El ticket digital es un comprobante interno. No se promete validez fiscal. Diseñado sin impedir la facturación CFDI futura (los campos opcionales están en la base de datos por si se necesita después).
7. **Ticket incluye QR**: **NUEVO** - El código QR en el ticket contiene los datos esenciales de la venta (total, productos, etc.) para facilitar al cliente el acceso o pago posterior.

### Casos Límite

- **Stock justo**: Si quedan exactamente las unidades necesarias, la venta se permite y el stock queda en 0.
- **Error de escaneo**: Si el código de barras no existe, el sistema ofrece buscar por nombre manualmente.
- **Corte de energía**: Si se corta la energía durante una venta, al restablecerse la energía, la aplicación vuelve al último estado consistente. La venta en progreso se marcará como "no completada" y el cajor deberá decidir: finalizarla o anularla.
- **Precio con decimales**: El cajero captura pesos con hasta dos decimales; el sistema los convierte a centavos enteros y opera siempre con `long`.

### Pantallas Clave (Simplificadas)

1. **Escaneo/Búsqueda de producto**: Campo para código de barras o búsqueda por nombre. El producto seleccionado muestra su precio y stock disponible al instante.
2. **Resumen de venta**: Una sola pantalla que muestra: producto, cantidad, precio unitario, **total** (ya incluye impuesto), método de pago. Sin pantallas intermedias de impuestos.
3. **Pago Efectivo**: Ingresar monto recibido. El sistema muestra el cambio automáticamente. Si el monto es insuficiente, bloquea y pide el correcto.
4. **Pago Tarjeta**: Un solo botón "Pagar con tarjeta". Se registra el método y listo. Se muestra un mensaje: "Comisión de tarjeta (X%) aplicará a la utilidad".
5. **Ticket generado**: Vista con el diseño de la tienda, total, productos, método de pago y **código QR**. Botones: WhatsApp, Correo, Guardar, Imprimir (opcional futuro).

### Criterios de Aceptación (Actualizados)

- [ ] El cajero puede escanear un código de barras y el producto aparece en el carrito en <200ms, mostrando precio y stock.
- [ ] La búsqueda por nombre funciona mientras se teclea, mostrando coincidencias.
- [ ] El total mostrado es exactamente: `Precio × Cantidad` (sin cálculos adicionales de impuesto en pantalla).
- [ ] El sistema bloquea la venta si el stock es insuficiente, con mensaje claro.
- [ ] Al pagar en efectivo, el cambio se calcula automáticamente: `Cambio = Recibido - Total`.
- [ ] Al pagar con tarjeta, se registra el método y se muestra el mensaje de comisión aplicada a utilidad.
- [ ] Después de finalizar la venta, se genera el ticket digital con código QR.
- [ ] Las opciones de compartir (WhatsApp, Correo, Guardar) están disponibles en el ticket.
- [ ] Si se cancela una venta (única en progreso), el stock se devuelve automáticamente.
- [ ] Las ventas a fiado (si están activadas) generan saldo en el cliente y no afectan el efectivo esperado hasta el abono.
- [ ] La comisión de tarjeta configurada se refleja en el reporte de utilidad neta.

### Preguntas Abiertas (Confirmadas)

- [x] Precio incluye impuesto: **CONFIRMADO** - No hay cálculo adicional en venta.
- [x] Sin ventas en espera: **CONFIRMADO** - Una venta a la vez.
- [x] QR en ticket: **CONFIRMADO** - Incluido en la generación.
- [ ] Descuentos en MVP: **DEFINIDO NO INCLUIR** - Para Fase 2.
- [ ] Mixto y Fiado: **DISEÑADOS SIN IMPEDIR** - Opcionales para futuras activaciones.

---

**Próximos pasos en código**: ticket con QR y compartir cuando exista interfaz gráfica; descuentos quedan para Fase 2.