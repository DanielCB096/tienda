# Manual del Dueño / Administrador

## Acceso

- **PIN**: Ingresar el PIN de 4-6 dígitos que se configuró al instalar.
- **Recuperar PIN**: Si se olvida, el desarrollador puede ayudar mediantevalidación de identidad (no hay "backdoor" por seguridad y modelo de costo $0).

## Pantallas Principales

### 1. Inicio / Panel de Día

Muestra al abrir la app (después del PIN):

- **Fecha de hoy**.
- **Fondo de caja inicial** configurado.
- **Ventas del día hasta el momento**: Total efectivo, total tarjeta, número de transacciones.
- **Último respaldo**: "Hace 2 días" o alerta si hace más tiempo.
- **Próximo gasto recordatorio**: Si hay gastos recurrentes configurados.

### 2. Catálogo de Productos

- Listado de todos los productos registrados.
- **Acciones**: Buscar por nombre o código de barras, nuevo producto, editar (solo dueño/gerente), eliminar (solo dueño con confirmación).
- **Stock actual**: Se muestra el stock disponible de cada producto.

### 3. Registrar Venta

Acceso rápido desde el panel principal o menú.

- Escanear código de barras o buscar producto.
- Seleccionar cantidad.
- Elegir método de pago (Efectivo / Tarjeta / Mixto / Fiado).
- Ver total y cambio (si es efectivo).
- Generar y mostrar/compartir ticket.

### 4. Corte de Caja

- Acceso: Menú → "Corte de Caja" (requiere PIN).
- Ver resultado: Efectivo esperado vs. contado, diferencia, responsable.
- Registrar el cierre y reiniciar el fondo para el siguiente día.

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

## Generando un Ticket Digital

### ¿Qué es?

El ticket es un archivo PDF o imagen generado en el momento de la venta, que NO tiene validez fiscal (es un comprobante interno). El diseño incluye:

- Nombre de la tienda.
- Fecha y hora.
- Lista de productos comprados (código, nombre, cantidad, precio unitario, total).
- Subtotal, impuesto (si aplica), total.
- Método de pago (Efectivo/Tarjeta/Mixture/Fiado).
- Saldo restante (si es fiado).
- Mensaje de retorno (ej. "Gracias por su compra").

### Cómo se comparte

1. Después de finalizar la venta, el sistema muestra el ticket.
2. El cajero toca el botón "Compartir".
3. El sistema ofrece:
   - **WhatsApp**: Abre el contacto con el archivo adjunto (o texto si el cliente solo quiere el número).
   - **Correo**: Abre el cliente de correo del dispositivo con el adjunto.
   - **Mostrar en pantalla**: El ticket queda en pantalla grande, el cliente lo fotografía.
   - **Guardar en galería**: Guardar la imagen del ticket en la foto/galería de la tablet.

### Historial deTickets

- Los tickets pasados se pueden consultar desde el menú "Historial de Ventas".
- Se puede seleccionar una fecha y revender o ver el ticket generado anteriormente.
- Útil para reimpresiones o consultas.

## Respaldos y Recuperación (Guía Rápida para el Dueño)

### Ver el estado de respaldo

1. Ir a Configuración → "Respaldo y Restauración".
2. Ver "Último respaldo: hace X días".
3. Ver intervalo configurado.

### Hacer un respaldo manual

1. En la misma pantalla, tocar "Crear respaldo ahora".
2. El sistema copia `tienda.localdb` a `tienda.localdb.copy`.
3. Mensaje: "✓ Respaldo creado exitosamente en [ruta]".

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