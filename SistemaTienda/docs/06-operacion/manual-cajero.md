# Manual del Cajero / Encargado

## Acceso

- **PIN rápido**: Cada usuario tiene su propio PIN (4-6 dígitos).
- **Roles y permisos**:
  - **Dueño/Administrador**: Ve todo, configura impuestos, usuarios, cortes, reportes.
  - **Encargado/Cajero**: Vende, cobra, registra compras, mermas, gastos permitidos, hace corte de caja.
  - **Empleado básico**: Solo vende, consulta precios y ventas, y reimprime tickets (no puede acceder a configuración, reportes, cortes, mermas, proveedores ni anular ventas).

## Pantallas Principales

### 1. Inicio de Sesión

1. Al abrir la app, se solicita el PIN.
2. El usuario ingresa su PIN.
3. El sistema valida y muestra el tablero (Inicio, Vender, Inventario, Ventas, Caja y Ajustes).
4. Si el PIN está mal tres veces seguidas, la app bloquea el acceso hasta cerrarla y abrirla de nuevo.

> **Estado de la app (2026-10-25)**: el tablero muestra ventas del día, efectivo esperado del corte, stock bajo y último respaldo. **Vender** registra ventas con carrito, cobros y ticket interno con QR compartible como texto. **Inventario** permite consultar, escanear, dar de alta o editar productos, y registrar entradas, mermas y ajustes según el rol. **Ventas** muestra el detalle de cada venta, permite reimprimir el ticket con su QR y anular la venta completa (solo dueño o encargado). **Caja** ya permite cerrar el corte, registrar ingresos y retiros, y abonar al fiado. El conteo de caja se captura como un total, sin desglose de billetes y monedas. La anulación también sigue disponible en la consola provisional. El manual describe el flujo completo planeado; revisa en cada versión qué partes ya están en la app.

### 2. Catálogo / Búsqueda de Producto

Desde esta pantalla el cajero busca qué vender.

- **Buscar por código de barras**: Escanear el código (el lector USB/Bluetooth lo envía como teclado, automáticamente enfoca el campo de búsqueda).
- **Buscar por nombre**: Teclado aparece, se escriben las primeras letras y el sistema sugiere coincidencias.
- **Favoritos**: Los productos más vendidos pueden aparecer en una sección "Rápido".
- **Información mostrada**: Nombre, precio, stock actual, código de barras.

### 3. Registrar Venta

Flujo paso a paso:

1. **Añadir producto**: Buscar y tocar el producto. Este se adiciona al carrito.
2. **Cantidad**: Si se vende más de 1, ingresar la cantidad. El sistema multiplica por precio unitario.
3. **Repetir**: Agregar más productos según sea necesario.
4. **Ver carrito**: Tocar el ícono del carrito para ver el resumen:
   - Lista de productos con subtotal cada uno.
   - Subtotal general.
   - Descuentos (si los hay, requieren permiso).
   - Total a pagar.
5. **Método de pago**:
   - **Efectivo**: El sistema calcula el cambio automáticamente. Se registra la venta y se muestra el total en efectivo.
   - **Tarjeta**: Se registra el método. No se procesa el cobro (no hay terminal integrada), solo se anota "Pagó con tarjeta". Se suma la comisión configurada como gasto.
   - **Mixto (parte Efectivo + parte Tarjeta)**: El cajero divide la venta. Ej: $80 en efectivo + $20 en tarjeta. El sistema lleva el control de ambas partes.
   - **Fiado** (solo si está activado y el cliente tiene registro):
     - Seleccionar cliente de la lista o crear uno nuevo.
     - El total se suma al saldo del cliente.
     - El ticket se genera indicando "Venta a Fiado".
6. **Pagar y generar ticket**: Tocar "Finalizar Venta".
   - El sistema registra la venta, actualiza inventario (si es que corresponde) y genera el ticket digital.
   - El ticket muestra un QR informativo (no fiscal) y se puede compartir como texto con el sistema o copiar. El QR no se guarda como imagen.

**Ver el detalle, reimprimir o anular una venta**

En la pestaña **Ventas**, toca una venta para abrir su detalle con líneas, pagos, comisión y estado:

- **Ver ticket** reabre el ticket con el QR. Si la venta está anulada, muestra el sello, el usuario, la fecha y el motivo.
- **Anular venta** (solo dueño o encargado): escribe el motivo y confirma con la palabra `ANULAR`. El sistema devuelve el stock con el costo original, revierte el saldo si fue fiado y deja el registro en la bitácora.
- El reembolso en efectivo se entrega manualmente. La venta anulada se excluye del corte; si el reembolso se paga después de cerrar el corte, el dueño lo registra como retiro en el corte siguiente.
- No se puede anular una venta que ya quedó incluida en un corte cerrado.

### 4. Inventario (pestaña Inventario)

Muestra el número de productos, los que están en stock bajo y el valorizado. La búsqueda acepta nombre o el código del lector; Enter abre el producto si el código existe. Un código desconocido de al menos ocho dígitos ofrece darlo de alta.

- **Consultar**: Toca **Ver** para abrir el detalle; **Ver movimientos** muestra los últimos 200 con usuario, motivo y cantidad firmada.
- **Alta**: **+ Nuevo** pide código, nombre, categoría, costo, precio, stock inicial, stock mínimo e impuesto. El código ya no se puede cambiar después.
- **Editar**: Cambia nombre, categoría, stock mínimo e impuesto. Solo el dueño puede cambiar costo y precio; el stock no se edita aquí.
- **Entrada**: Recibe unidades con costo unitario y motivo. El costo del movimiento queda en el historial, pero no cambia el costo del catálogo.
- **Merma**: Descuenta unidades por daño, caducidad o robo, con motivo obligatorio.
- **Ajustar stock**: Solo el dueño; fija el stock al conteo físico con motivo. La diferencia queda en el historial.

El encargado puede editar y registrar entradas o mermas, pero no ajustar stock ni cambiar precios. El empleado solo consulta.

### 5. Registrar Merma

Acceso: pestaña **Inventario** → **Ver** → **Registrar merma** (requiere rol Encargado o Dueño).

1. Abrir el producto y tocar **Registrar merma**.
2. Ingresar la cantidad y el motivo (por ejemplo, dañado, caducado o robo).
3. El sistema muestra el costo perdido estimado con el costo actual del producto.
4. Tocar **Registrar merma**.
5. El sistema reduce el stock y registra el movimiento con el usuario de la sesión; no se puede editar ni borrar.
6. Se muestra el mensaje de confirmación y el detalle actualizado.

### 6. Registrar Gasto

Acceso: Menú → "Gastos" (rol Encargado o Dueño).

1. Tocar "Nuevo Gasto".
2. Capturar:
   - **Concepto** (ej. "Renta", "Luz", "Sueldo", "Bolsas").
   - **Monto**.
   - **Fecha** (por defecto hoy, pero se puede retroactivar).
   - **Método de pago** (Efectivo / Tarjeta / Transferencia).
   - **Origen**: ¿Saldó de Caja? o ¿Externo? (importante para el corte de caja).
3. Tocar "Guardar".
4. El gasto queda registrado y afecta la utilidad neta y, si es de caja, el corte siguiente.

### 7. Ver Reporte Rápido

Acceso: Menú → "Reportes" (ver resumen).

- **Ventas del día**: Total, cantidad, efectivo/tarjeta.
- **Utilidad neta** del día (ventas - comisión tarjeta - gastos de caja).
- **Stock bajo**: Quiénes productos tienen poco stock (configurable umbral).
- **Mermas recientes**.

### 8. Corte de Caja (Parcial o Diario)

Acceso en la app: pestaña **Caja** (requiere sesión de dueño o encargado para operar; el retiro es solo del dueño).

1. El sistema muestra:
   - Fondo inicial del periodo.
   - Ventas en efectivo acumuladas.
   - Ventas en tarjeta acumuladas.
   - Abonos, ingresos, retiros y gastos de caja.
   - **Efectivo esperado** (fórmula: fondo + ventas efectivo + abonos efectivo + ingresos − retiros − gastos de caja).
   - **Efectivo contado**: Cuánto cuenta el cajero al cerrar.
   - **Diferencia**: Contado − Esperado (positiva es sobrante y negativa faltante).

2. **Acciones**:
   - **Cerrar corte**: Registrar el corte con el resultado y el responsable. Si hay diferencia, hay que explicar el motivo. El siguiente periodo inicia con lo contado.
   - **Salir sin cerrar**: Volver al menú sin guardar el corte (los datos del periodo se mantienen, pero no se considera "oficial" hasta que se cierra).

## Flujo de Cierre del Día (Resumen para el Cajero)

1. **Ultimar ventas**: Asegurarse de que no queden ventas pendientes o en espera.
2. **Ver reporte rápido**: Revisar ventas del día, mermas, gastos.
3. **Hacer corte de caja**: Menú → Corte de Caja, revisar esperado vs. contado, anotar diferencia.
4. **Reiniciar fondo**: El sistema reinicia el fondo inicial para el siguiente día automáticamente.
5. **Hacer respaldo**: Menú → "Crear respaldo ahora" (recomendado antes de apagar la tablet).
6. **Apagar o dejar encendida**: Decidir si apagar la tablet o dejarla lista para el siguiente día.

## Casos Comunes y Cómo Manejarlos

### "Me equivoqué al poner la cantidad"

- Si la venta aún no se ha finalizado (aún está en el carrito), se puede quitar el producto del carrito y volver a agregar con la cantidad correcta.
- Si la venta ya se registró, no se puede editar. Solicita al dueño o encargado que la **anule** desde Ventas → detalle de la venta → **Anular venta** (o en la consola, Ventas → [3]): se anula la venta completa, se devuelve el stock y, si fue fiado, se revierte el saldo del cliente.
- El reembolso en efectivo se entrega manualmente. La venta anulada ya se excluye del corte; si el reembolso se paga después de cerrar el corte, el dueño lo registra como retiro en el corte siguiente.

### "El cliente no tiene código de barras"

- El cajero puede buscar por nombre del cliente (si está registrado) o simplemente seleccionar "Venta a Fiado" e ingresar el nombre/manual.
- Para productos sin código, se pueden agregar como "Producto varios" o de venta rápida, seleccionando el precio manualmente.

### "Se fue la luz/corte de energía"

- **Si la tablet tiene batería**: Sigue funcionando con la batería. Los datos se guardan automáticamente en SQLite mientras haya energía mínima.
- **Si se apaga de golpe**: Al volver a encender, la app inicia con el último estado guardado. No debería haber ventas a medias si el sistema usó transacciones atómicas.
- **Recomendación**: Mantener la tablet cargada o conectada a un mini UPS barato para tiendas con cortes de energía frecuentes.

### "El lector de código de barras no funciona"

- Verificar que esté conectado (USB) o emparejado (Bluetooth).
- Reiniciar la app (a veces el foco se pierde).
- En la búsqueda manual, teclear el código alfabéticamente.

## Buenas Prácticas del Cajero

1. **Siempre cobrar antes de quitar un producto del carrito** (si ya se avanzó en el proceso).
2. **Verificar el total antes de pedir el pago** (el sistema muestra un resumen final).
3. **Contar el vuelto dos veces** antes de entregarlo al cliente (specially en efectivo).
4. **Registrar mermas al momento** de identificar la pérdida, no al final del día (para tener registro preciso).
5. **Hacer respaldo al menos una vez por día**, preferiblemente al cierre.
6. **Si hay dudas sobre un precio o promoción**, consultar al dueño antes de registrar, no adivinar.
7. **Usar el PIN de cada cajero**, no compartir el PIN del dueño para cada transacción.