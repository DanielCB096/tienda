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

### QR del ticket sin imagen ni validación

- **Dónde**: `TicketService`, `QrTicketView`, `TicketPage` y `VentasPage` de la app MAUI; `Program.ImprimirTicket` en la consola.
- **Por qué**: el alcance de T16 era mostrar el QR en pantalla con una dependencia mínima (`Net.Codecrete.QrCodeGenerator`), sin generar archivos ni integrar la galería o el menú de compartir con imágenes.
- **Impacto**: el QR es informativo y no fiscal; resume hasta diez productos, no se guarda ni se comparte como imagen, no permite validar la venta contra nada y no se ha probado en Android. La reimpresión reconstruye el ticket, pero no incluye el efectivo recibido ni el cambio porque la venta solo guarda el total. WhatsApp y correo dependen del diálogo de compartir del sistema, que sigue sin verificarse. Ver [ADR-qr-ticket.md](05-decisiones/ADR-qr-ticket.md).
- **Cierre**: renderizar el QR a PNG y compartirlo como archivo, decidir si se sube el límite de productos, guardar el efectivo recibido si se quiere reimprimir el cambio y validar el QR en un dispositivo Android.

### Conteo por denominaciones sin persistencia

- **Dónde**: `ConteoEfectivo` (Core), `CajaPage` y `Program.CerrarCorte`; registro de abonos.
- **Por qué**: T17 agregó el conteo en la app sin cambiar el esquema; el corte sigue guardando solo esperado, contado y diferencia.
- **Impacto**: el desglose de billetes y monedas se usa para calcular el contado, pero no se guarda, así que no se puede auditar después ni reconstruir un faltante por denominación. La consola todavía captura un total directo y el abono de fiado siempre se registra como efectivo (no se pregunta el método ni en Caja ni en Clientes).
- **Cierre**: tabla de desglose por corte si se requiere auditoría, captura por denominaciones en la consola y método de pago en abonos.

## Media prioridad

### Entrada de inventario no actualiza el costo del catálogo

- **Dónde**: `InventoryService.RegistrarEntrada` y la pestaña Inventario de la app MAUI.
- **Por qué**: el servicio registra el costo unitario del movimiento, pero no toca `Productos.Costo`; el encargado puede registrar entradas sin tener permiso para cambiar precios.
- **Impacto**: después de una compra a otro costo, el valorizado y el costo congelado de las siguientes ventas siguen usando el costo anterior hasta que el dueño lo edite a mano. La interfaz lo advierte, pero es un paso fácil de olvidar.
- **Cierre**: decidir si la entrada actualiza el costo del catálogo (con bitácora) o si se captura un costo de reposición separado, y reflejarlo en la interfaz.

### Reembolso de anulación sin movimiento de caja

- **Dónde**: `DatabaseTools.AnularVenta`, `Program.AnularVenta` y `VentasPage` de la app MAUI.
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

### Clientes sin edición, estado de cuenta ni método de abono

- **Dónde**: `ConfiguracionService` y la pestaña Clientes de la app MAUI.
- **Por qué**: T18 cubrió el directorio, la búsqueda, el alta y el abono; la edición, el borrado lógico y el estado de cuenta quedaron fuera del alcance.
- **Impacto**: no se puede corregir el nombre o teléfono de un cliente ni retirarlo del directorio, y el alta no detecta duplicados. El abono se registra siempre como efectivo (no se pregunta el método) y no hay estado de cuenta con ventas fiadas y abonos; la consola conserva las mismas limitaciones. La pantalla aún no se ha verificado a mano en Windows ni en Android.
- **Cierre**: edición con permiso y bitácora, detección de duplicados, estado de cuenta de solo lectura y método de pago en el abono.

### La app MAUI no cubre todas las operaciones

- **Dónde**: `SistemaTienda.Maui/Pages`.
- **Por qué**: el punto de venta se entregó en T13, la caja operativa en T14, el inventario en T15, el detalle/anulación de ventas en T16, el conteo con historial de cortes en T17 y la pantalla de clientes en T18; el resto de las operaciones de escritura se hará en tareas posteriores.
- **Impacto**: desde la app se vende, se opera el inventario, se cierra el corte con conteo, se registran ingresos, retiros y abonos de fiado, se anulan ventas, se reimprime el ticket, se consulta el historial de cortes, se gestiona el directorio de clientes con sus abonos y se crea respaldo. Gestionar proveedores y gastos sigue en la consola, que no es usable por el cajero; la edición y el estado de cuenta de clientes y la consulta de ventas de fechas anteriores tampoco están en la app.
- **Cierre**: pantallas de proveedores y gastos en MAUI, módulo por módulo y con sus pruebas; después, edición/estado de cuenta de clientes y consulta histórica de ventas.

### App MAUI sin pruebas automatizadas

- **Dónde**: `SistemaTienda.Maui`.
- **Impacto**: CI solo compila para Windows y Android. Una regresión de navegación, sesión, POS, Ventas, anulación, inventario, clientes, caja, historial o formato no se detecta hasta la verificación manual. El cierre de corte, el conteo, los movimientos de inventario, la anulación y los abonos desde la app dependen de esa verificación. `TicketServiceTests`, `ConteoEfectivoTests` y las pruebas de búsqueda y alta de clientes cubren cálculos, textos y servicios, no la interfaz.
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
