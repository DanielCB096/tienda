# Manual del Cajero / Encargado

## Acceso

- **PIN rápido**: Cada usuario tiene su propio PIN (4-6 dígitos).
- **Roles y permisos**:
  - **Dueño/Administrador**: Ve todo, configura impuestos, usuarios, cortes, reportes.
  - **Encargado/Cajero**: Vende, cobra, registra compras, mermas, gastos permitidos, hace corte de caja.
  - **Empleado básico**: Solo vende y consulta precios (no puede acceder a configuración, reportes, cortes, mermas, proveedores).

## Pantallas Principales

### 1. Inicio de Sesión

1. Al abrir la app, se solicita el PIN.
2. El usuario ingresa su PIN.
3. El sistema valida y muestra la interfaz según su rol.
4. Si el PIN está mal tres veces seguidas, la app se cierra temporalmente (o requiere esperar 30 segundos) para evitar intentos brutos.

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
   - Se ofrece compartir el ticket (WhastApp, correo, mostrar en pantalla).

### 5. Registrar Merma

Acceso: Menú → "Mermas" (requiere rol Encargado o Dueño).

1. Tocar "Nueva Merma".
2. Seleccionar producto (o buscar).
3. Ingresar:
   - **Motivo** (lista desplegable: Dañado, Caducado, Robado, Consumo propio, Regalo, Otra).
   - **Cantidad** (unidades o peso).
   - **Costo perdido** (automático: costo unitario × cantidad, o manual).
   - **Usuario** (seleccionado de la lista o propio PIN).
4. Tocar "Registrar".
5. El sistema reduce el stock y registra la merma en la bitácora.
6. Se muestra mensaje: "✓ Merma registrada, stock reducido".

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

Acceso: Menú → "Corte de Caja" (requiere PIN de cajero o debido).

1. El sistema muestra:
   - Fondo inicial del día.
   - Ventas en efectivo acumuladas del día.
   - Ventas en tarjeta acumuladas.
   - Retiros de caja (si hubo durante el día).
   - Gastos pagados de caja.
   - **Efectivo esperado** (fórmula: fondo + ventas efectivo + abonos efectivo - retiros - gastos de caja).
   - **Efectivo contado**: Cuánto cuenta el cajero al cerrar.
   - **Diferencia**: Esperado - Contado.

2. **Acciones**:
   - **Cerrar corte**: Registrar el corte con el resultado y el responsable. Reinicia el fondo para el próximo día.
   - **Salir sin cerrar**: Volver al menú sin guardar el corte (los datos del día se mantienen, pero no se considera "oficial" hasta que se cierra).

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
- Si la venta ya se registró, no se puede editar. Se debe registrar una **devolución** o **contramovimiento** (nueva venta inversa con motivo "Error en captura").

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