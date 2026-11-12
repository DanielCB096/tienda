# Manual del Dueño / Administrador

## Acceso

- **PIN**: Ingresar el PIN de 4-6 dígitos que se configuró al instalar.
- **Recuperar PIN**: Si se olvida, el desarrollador puede ayudar mediante validación de identidad (no hay "backdoor" por seguridad y modelo de costo $0).

## App para Windows y Android (base implementada)

1. **Primer arranque**: si la base está vacía, se abre el asistente. Captura nombre de la tienda, giro, impuesto, comisión de tarjeta, fondo de caja, fiado y su límite, días entre respaldos y el PIN del dueño (se pide dos veces).
2. **Inicio de sesión**: escribe tu PIN. Después de 3 intentos incorrectos la app bloquea el acceso hasta cerrarla y abrirla de nuevo.
3. **Pestañas**:
   - **Inicio**: ventas de hoy (incluye anuladas), efectivo esperado del periodo, productos con stock bajo, fecha del último respaldo y aviso si el respaldo está vencido.
   - **Vender**: punto de venta con búsqueda o escaneo, carrito, cobro en efectivo, tarjeta, mixto o fiado, y ticket interno con QR que se puede compartir como texto o copiar.
   - **Inventario**: catálogo con stock bajo y valorizado, búsqueda o escaneo, alta/edición, entradas, mermas, ajustes e historial; el dueño controla costos, precios y ajustes.
   - **Ventas**: lista de ventas del día con folio, total, método, hora y estado; al tocar una venta se abre su detalle con líneas, pagos, comisión y datos de anulación, y se puede reimprimir el ticket o anular la venta con permiso.
   - **Caja**: periodo abierto (efectivo esperado, fondo, ventas por método, abonos, ingresos/retiros y gastos), cierre de corte con diferencia justificada, conteo por billetes y monedas, registro de ingresos y retiros, abonos de fiado e historial de los últimos 20 cortes.
   - **Ajustes**: datos de la tienda, reglas configuradas, rutas de la base y de los respaldos, último respaldo, botón para crear un respaldo y cierre de sesión.
4. **Importante**: desde la app ya se vende, se opera el inventario (alta, edición, entradas, mermas y ajustes), se opera la caja (cierre con conteo por denominaciones, ingresos, retiros, abonos e historial) y se anulan ventas desde Ventas (dueño o encargado). El desglose del conteo no se guarda en el corte: solo el total contado; el catálogo aún no filtra por categoría ni desactiva productos.
5. **Dónde están los datos**: en Windows, `%LOCALAPPDATA%\SistemaTienda\com.sistematienda.tienda\Data\tienda.db` y los respaldos en la carpeta `respaldos` junto a ese archivo. En Android están en el almacenamiento privado de la app. Las rutas exactas se muestran en Ajustes.

## Pantallas Principales

### 1. Inicio / Panel de Día

Muestra al abrir la app (después del PIN), en la pestaña **Inicio**:

- **Fecha de hoy** y nombre de la tienda.
- **Ventas de hoy**: cantidad de ventas vigentes, total cobrado y cuántas se anularon.
- **Efectivo esperado**: monto del periodo abierto e inicio del periodo con el fondo incluido.
- **Stock bajo**: cuántos productos necesitan revisión.
- **Último respaldo**: fecha, estado y aviso cuando el intervalo configurado ya venció.

### 2. Catálogo de Productos

Acceso en la app: pestaña **Inventario**.

- Listado de todos los productos con stock, mínimo, categoría y precio; resumen de stock bajo y valorizado.
- **Acciones**: buscar por nombre o escanear el código, dar de alta, editar, registrar entrada, merma o ajuste, y consultar movimientos. El borrado no está implementado.
- **Precios y ajustes**: solo el dueño puede cambiar costo/precio y ajustar el stock; el encargado puede editar los demás datos y registrar entradas o mermas.
- **Stock actual**: se muestra el stock disponible y se actualiza por movimientos; nunca se edita directamente desde el formulario.

### 3. Registrar Venta

Acceso rápido desde el panel principal o menú.

- Escanear código de barras o buscar producto.
- Seleccionar cantidad.
- Elegir método de pago (Efectivo / Tarjeta / Mixto / Fiado).
- Ver total y cambio (si es efectivo).
- Generar y mostrar/compartir ticket.

#### Ver el detalle, reimprimir o anular una venta

- En **Ventas**, toca una venta para abrir el detalle con folio, fecha, cajero, estado, líneas, pagos, comisión y cliente si fue fiado.
- **Ver ticket** reabre el ticket con su QR; si la venta está anulada, muestra el sello con usuario, fecha y motivo.
- **Anular venta** (solo dueño o encargado): escribe el motivo y confirma escribiendo `ANULAR`.
- Se anula la venta completa; el sistema devuelve el stock con el costo congelado, revierte el saldo si fue fiado y deja el registro en la bitácora. La venta original no se edita ni se borra.
- El reembolso en efectivo se entrega manualmente. La venta se excluye del corte pendiente; si el reembolso se paga después de cerrar el corte, regístralo como retiro en el corte siguiente.
- No se puede anular una venta que ya quedó incluida en un corte cerrado. La misma operación sigue disponible en la consola: Ventas → [3].

### 4. Corte de Caja

Acceso en la app: pestaña **Caja** (requiere PIN de dueño o encargado; el retiro es solo del dueño).

- **Conteo por denominaciones**: anota cuántos billetes y monedas hay de cada valor; el subtotal y el total se calculan solos y el importe pasa a **Efectivo contado** en solo lectura. También puedes capturar el total directamente. **Limpiar conteo** borra las cantidades y devuelve la captura manual.
- **Cierre**: revisa el efectivo esperado contra el contado, justifica cualquier diferencia y confirma; el periodo queda cerrado e inmutable y el siguiente arranca con lo contado.
- **Historial**: consulta los últimos 20 cortes con periodo, esperado, contado, diferencia coloreada, responsable, fecha y observaciones. El desglose de billetes y monedas no se guarda.

### 5. Reportes y Utilidad

- **Utilidad del día**: Bruta y neta.
- **Ventas por método de pago**: Cuánto se vendió en efectivo vs. tarjeta.
- **Productos más vendidos**: Top 5 o top 10.
- **Mermas del período**: Total en dinero y por motivo.
- **Gastos del período**: Por categoría.

### 6. Configuración

Acceso restringido (PIN de admin). Opciones:

- **Nombre y giro de la tienda**.
- **Impuestos**: Configurar tasas (16% IVA, 0%, exento) por producto o por defecto.
- **Métodos de pago**: Activar/desactivar Efectivo, Tarjeta, Mixto, Fiado.
- **Comisión de tarjeta**: % configurado.
- **Límites de crédito**: Si el fiado está activado, configurar límite por cliente (o "sin límite").
- **Configuración de respaldo**: Intervalo automático (ninguno, cada día, cada 5 ventas).
- **Usuarios y permisos**: Agregar nuevos usuarios y asignar PINs (Rol: Dueño, Encargado, Empleado).
- **Gastos recurrentes**: Configurar cargos periódicos (renta, sueldo, servicios).
- **Llave maestra** (opcional): Para cifrar respaldos.

### 7. Clientes (si fiado activado)

- **Ver lista de clientes** con sus saldos.
- **Registrar abono**: Recibir pago parcial o total de un cliente con fiado.
- **Enviar recordatorio WhatsApp**: Generar enlace con mensaje prellenado.
- **Nuevo cliente**: Crear cliente en el momento.

### 8. Proveedores y Cuentas por Pagar

- **Ver saldos**: Quiénes me deben pagar y cuánto.
- **Registrar compra nueva**: Entrada de inventario y creación de deuda.
- **Registrar pago**: Reducir el saldo del proveedor.
- **Historial de transacciones** con cada proveedor.

## Ticket interno y QR

### ¿Qué es?

El ticket es un texto interno generado en el momento de la venta; NO tiene validez fiscal. Incluye:

- Nombre de la tienda.
- Folio, fecha y hora, y cajero.
- Lista de productos comprados (cantidad, nombre, precio unitario e importe).
- Total y método de pago (Efectivo/Tarjeta/Mixto/Fiado); en mixto, el desglose de efectivo y tarjeta; en fiado, el cliente.
- En efectivo, el monto recibido y el cambio (solo en el ticket recién cobrado; la reimpresión no los incluye).
- Comisión de tarjeta cuando aplica.
- Un QR informativo offline con folio, fecha, estado, total, método pago y hasta diez productos, con la leyenda “No es un comprobante fiscal”.
- Si está anulada, el sello con usuario, fecha, motivo y la leyenda de que ya no es válido.

### Cómo se comparte

1. Después de finalizar la venta, la app muestra el ticket con el QR.
2. El cajero toca **Compartir** para enviar el texto por el diálogo del sistema (WhatsApp, correo u otra app instalada) o **Copiar** para el portapapeles.
3. El QR se muestra en pantalla para que el cliente lo fotografíe; todavía no se guarda ni se comparte como imagen.

### Historial de tickets

- En **Ventas** se listan las ventas del día; al tocar una se abre el detalle y **Ver ticket** reimprime el ticket con el QR.
- La consulta de fechas anteriores y la reventa todavía no están implementadas en la app.

## Respaldos y Recuperación (Guía Rápida para el Dueño)

### Ver el estado de respaldo

1. Ir a Configuración → "Respaldo y Restauración".
2. Ver "Último respaldo: hace X días".
3. Ver intervalo configurado.

### Hacer un respaldo manual

1. En la app, abrir la pestaña **Ajustes**.
2. Tocar **Crear respaldo ahora**.
3. La app crea una copia verificada en la carpeta `respaldos` y muestra la ruta del archivo.
4. En la consola, la misma acción está en Configuración → Respaldo y restauración.

La app también intenta crear un respaldo automático al iniciar y al cerrar sesión, según el intervalo configurado (0 desactiva el respaldo automático).

### ¿Cuándo restaurar?

- Si la tablet falla, se pierde o se roba.
- Si después de una actualización la app se comporta raro.
- Antes de hacer cambios mayores (nueva configuración de impuestos, agregar muchos productos).

### Cómo restaurar

1. Ir a Configuración → "Respaldo y Restauración".
2. Tocar "Restaurar desde respaldo".
3. Elegir qué fecha/archivo restaurar.
4. Confirmar. La app se reiniciará con los datos del respaldo.

### Buenas prácticas

- **Hacer respaldo después de un día intenso** (muchas ventas).
- **Hacer respaldo antes de cualquier actualización de la app**.
- **Guardar un respaldo en USB** cada semana o cada quincena.
- **Verificar el indicador "hace X días"** cada lunes por la mañana.

### Preguntas Frecuentes

- **¿Y si se me llena la memoria de la tablet?**
  - El sistema no borra automáticos los datos viejos. El dueño debe archivar o limpiar historial viejo (consultar con el desarrollador). Recomendación: cada 2-3 años, hacer un "archivo histórico" y empezar con una base limpia.
- **¿Puedo tener la base de datos en dos tablets?**
  - No recomendado para el MVP. Cada tablet tiene su propia base de datos. Si necesitas datos en dos tablets, hacer un respaldo en la original y restaurar en la nueva (o viceversa), pero los cambios hechos en una no se replican automáticamente.
- **¿Pierdo mis datos si la tablet se descarga completamente?**
  - No, si hiciste un respaldo antes. La base de datos SQLite se guarda en el almacenamiento interno y persiste aunque la tablet se apague. El riesgo es si el hardware falla (disco dañado), por eso los respaldos en USB o nube personal son importantes.