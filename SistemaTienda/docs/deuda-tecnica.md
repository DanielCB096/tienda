# Deuda Técnica

Atajos y pendientes aceptados conscientemente. Cada entrada indica impacto y qué la cerraría.

## Alta prioridad

### Bitácora no atómica con la operación

- **Dónde**: `BitacoraService.Registrar` se llama después de que la operación de negocio ya se confirmó.
- **Por qué**: los métodos de `DatabaseTools` abren su propia transacción y no exponen un punto de extensión; unificarlo exigía refactorizar cada operación sensible.
- **Impacto**: si falla la escritura de la bitácora (por ejemplo, disco lleno), la operación ya quedó aplicada y falta su registro. La tabla sigue siendo inmutable y la operación normal sí queda auditada.
- **Cierre**: pasar la transacción o un `UnitOfWork` a los servicios para insertar acción y bitácora en la misma transacción.

### Pagos a proveedores sin historial inmutable

- **Dónde**: `ActualizarSaldoProveedor`.
- **Impacto**: se actualiza el saldo, pero no se guarda el movimiento de pago. No se puede auditar ni reconstruir el saldo. Contradice "el historial no se edita". Los abonos de fiado ya tienen historial (`AbonosFiado`), los pagos a proveedores no.
- **Cierre**: tabla `PagosProveedor` inmutable; el saldo se actualiza dentro de la misma transacción.

### Ventas sin descuentos

- **Dónde**: `VentaService` y `DatabaseTools.RegistrarVenta`.
- **Impacto**: el total es siempre precio de catálogo × cantidad. La anulación ya existe (solo venta completa, bloqueada tras un corte cerrado), pero no hay descuentos por línea ni por venta.
- **Cierre**: descuentos como fase 2 con autorización y registro en bitácora.

### Ventas sin ticket QR

- **Dónde**: `TicketPage` en la app MAUI y `Program.ImprimirTicket` en la consola.
- **Impacto**: el ticket es texto interno y ya se puede compartir o copiar desde la app, pero no se genera PDF/imagen ni lleva QR, y no se puede guardar en galería. WhatsApp y correo dependen del menú de compartir del sistema.
- **Cierre**: generación de ticket con QR en la app MAUI.

### Corte de caja sin conteo por denominaciones

- **Dónde**: `CajaPage` y `Program.CerrarCorte`; registro de abonos.
- **Impacto**: el retiro ya requiere permiso de dueño y queda en bitácora; el conteo se captura como un solo importe, sin desglose de billetes y monedas, y el abono de fiado siempre se registra como efectivo (no se pregunta el método). La app tampoco muestra el historial de cortes, solo el último cerrado.
- **Cierre**: captura por denominaciones, consulta del historial en Caja y método de pago en abonos.

## Media prioridad

### Entrada de inventario no actualiza el costo del catálogo

- **Dónde**: `InventoryService.RegistrarEntrada` y la pestaña Inventario de la app MAUI.
- **Por qué**: el servicio registra el costo unitario del movimiento, pero no toca `Productos.Costo`; el encargado puede registrar entradas sin tener permiso para cambiar precios.
- **Impacto**: después de una compra a otro costo, el valorizado y el costo congelado de las siguientes ventas siguen usando el costo anterior hasta que el dueño lo edite a mano. La interfaz lo advierte, pero es un paso fácil de olvidar.
- **Cierre**: decidir si la entrada actualiza el costo del catálogo (con bitácora) o si se captura un costo de reposición separado, y reflejarlo en la interfaz.

### Reembolso de anulación sin movimiento de caja

- **Dónde**: `DatabaseTools.AnularVenta` y `Program.AnularVenta`.
- **Por qué**: se confirmó con el usuario que el reembolso sea manual y documentado en esta versión.
- **Impacto**: la anulación devuelve stock y revierte el fiado, pero no registra un movimiento de caja. La venta se excluye del corte pendiente, así que un reembolso pagado en el mismo periodo no necesita retiro; si se paga después de cerrar el corte, el dueño debe registrar un retiro en el corte siguiente. El sistema no conoce el método ni el momento del reembolso.
- **Cierre**: si se requiere, generar automáticamente un `MovimientoCaja` de retiro ligado a la anulación, con permiso y bitácora.

### Respaldo automático solo se evalúa al iniciar o cerrar

- **Dónde**: `Program.Main` y `RespaldoService.EjecutarAutomaticoSiCorresponde`.
- **Impacto**: si la aplicación queda abierta varios días, no hay copia nueva hasta el cierre o el siguiente inicio; una sesión larga puede dejar el último respaldo viejo aunque el intervalo ya se cumplió.
- **Cierre**: temporizador o verificación después de cada N ventas, sin bloquear la caja.

### Restauración sin registro en bitácora

- **Dónde**: `RespaldoService.Restaurar`.
- **Impacto**: la restauración reemplaza toda la base y no deja registro de quién la ejecutó ni de qué archivo se usó. El respaldo `antes-restaurar` evita la pérdida de datos, pero la acción no queda auditada.
- **Cierre**: registrar `AccionBitacora.Respaldo` con motivo `Restauracion` y la ruta usada inmediatamente después de restaurar.

### La app MAUI no cubre todas las operaciones

- **Dónde**: `SistemaTienda.Maui/Pages`.
- **Por qué**: el punto de venta se entregó en T13, la caja operativa en T14 y el inventario en T15; el resto de las operaciones de escritura se hará en tareas posteriores.
- **Impacto**: desde la app se vende, se opera el inventario, se cierra el corte, se registran ingresos, retiros y abonos de fiado, se crea respaldo y se cierra sesión. Anular ventas, gestionar clientes, proveedores y gastos siguen en la consola, que no es usable por el cajero.
- **Cierre**: pantallas de anulación, clientes, proveedores y gastos en MAUI, módulo por módulo y con sus pruebas.

### App MAUI sin pruebas automatizadas

- **Dónde**: `SistemaTienda.Maui`.
- **Impacto**: CI solo compila para Windows y Android. Una regresión de navegación, sesión, POS, inventario, caja o formato no se detecta hasta la verificación manual. El cierre de corte y los movimientos de inventario desde la app dependen de esa verificación.
- **Cierre**: extraer la lógica de cada página a modelos de vista probables y agregar pruebas; valorar pruebas de interfaz más adelante.

### Android sin validación en dispositivo

- **Dónde**: job `app-maui` de GitHub Actions.
- **Impacto**: la app compila para Android, pero no se ha instalado ni probado en la tablet objetivo. Puede haber diferencias de diseño, rendimiento o rutas de almacenamiento.
- **Cierre**: instalar el APK en la tablet real y ejecutar el checklist de primera configuración, login, tablero, respaldo y cierre de sesión.

### App MAUI en .NET 10 con bibliotecas en .NET 8

- **Dónde**: `SistemaTienda.Maui.csproj` frente a `Core`, `Infrastructure` y `Application`.
- **Por qué**: las cargas de trabajo MAUI disponibles en este equipo son para el SDK 10; las bibliotecas se mantienen en `net8.0` para no tocar el resto del stack.
- **Impacto**: compilar la app exige SDK 10 y sus cargas de trabajo. Una máquina con solo SDK 8 puede compilar la solución y las pruebas, pero no la app.
- **Cierre**: unificar todos los proyectos en el mismo framework cuando se decida actualizar el stack completo.

### Mermas sin entidad propia

- **Dónde**: `InventoryService.RegistrarMerma` registra una salida con motivo `Merma: ...`.
- **Impacto**: el costo perdido se calcula al momento y se muestra en el mensaje; no queda guardado como merma con su costo. Los reportes no pueden sumar pérdidas por merma sin recalcular.
- **Cierre**: tabla `Mermas` o columnas de costo en el movimiento que permitan reportes directos.

### Validaciones de borde sin corregir (hallazgos de T9)

- **Dónde**: `InventoryService.AjustarStock`, `ConfiguracionService.AgregarUsuario` y `VerificarAcceso`, `ConfiguracionService.AgregarGasto`, `InventoryService.BuscarPorNombre`.
- **Impacto**: `AjustarStock` no valida que el producto exista (con stock mayor a cero falla por llave foránea; con stock cero reporta éxito sin haber ajustado). Se permiten PIN duplicados y `VerificarAcceso` devuelve el primer usuario por nombre, lo que puede asignar el rol equivocado. `AgregarGasto` no recorta el concepto, así que agrupa distinguiendo mayúsculas y espacios. La búsqueda por nombre no ignora acentos.
- **Cierre**: validar existencia antes de ajustar, exigir PIN único (o verificar por usuario y PIN), recortar el concepto al guardar y normalizar la búsqueda (mayúsculas/acentos).

### Reportes incompletos

- **Dónde**: `MenuReportes` y `ReporteUtilidadService`.
- **Impacto**: ya existe la utilidad por periodo (ventas, costo congelado, comisión, mermas y gastos), pero faltan ventas por día, comparativos entre periodos, utilidad por producto y exportación.
- **Cierre**: consultas agregadas por producto y periodo con índices por fecha (ya existen en `Ventas(Fecha)`).

## Baja prioridad

- `Merma` comparte historial con ajustes y ventas; falta filtrar por tipo en la interfaz.
- `ConfiguracionTienda.UsuarioDueñoPINHash` existe en la tabla pero no se usa (el hash vive en `UsuariosSistema`). Eliminar en la próxima migración.
- `Rol` se documentó como texto en algunos documentos; en la base es `INTEGER` con enum. Unificar documentación.
- Sin `Activo` en productos: no se puede retirar un producto del catálogo sin borrarlo.
- El catálogo de la app MAUI no filtra por categoría, aunque la búsqueda por nombre ya existe.
- Inicio y Ventas muestran el importe de las ventas vigentes como “cobrados”, pero incluyen las ventas a fiado, que no entran a caja (Caja sí las separa). Revisar la etiqueta o excluir el fiado del resumen.
- Sin pruebas de rendimiento con volúmenes realistas (10 000 productos / 500 ventas por día).
- Comentarios XML heredados de la versión anterior en `InventoryService`; no afectan.
