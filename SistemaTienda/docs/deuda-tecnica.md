# Deuda Técnica

Atajos y pendientes aceptados conscientemente. Cada entrada indica impacto y qué la cerraría.

## Alta prioridad

### Bitácora no atómica con la operación

- **Dónde**: `BitacoraService.Registrar` se llama después de que la operación de negocio ya se confirmó.
- **Por qué**: los métodos de `DatabaseTools` abren su propia transacción y no exponen un punto de extensión; unificarlo exigía refactorizar cada operación sensible.
- **Impacto**: si falla la escritura de la bitácora (por ejemplo, disco lleno), la operación ya quedó aplicada y falta su registro. La tabla sigue siendo inmutable y la operación normal sí queda auditada.
- **Cierre**: pasar la transacción o un `UnitOfWork` a los servicios para insertar acción y bitácora en la misma transacción.

### Ventas sin descuentos

- **Dónde**: `VentaService` y `DatabaseTools.RegistrarVenta`.
- **Impacto**: el total es siempre precio de catálogo × cantidad. La anulación ya existe (solo venta completa, bloqueada tras un corte cerrado), pero no hay descuentos por línea ni por venta.
- **Cierre**: descuentos como fase 2 con autorización y registro en bitácora.

### QR del ticket: imagen resuelta, validación pendiente

- **Dónde**: `TicketService`, `QrImagenService`, `QrTicketView`, `TicketPage` y `VentasPage` de la app MAUI; `Program.ImprimirTicket` en la consola.
- **Resuelto (T32)**: el QR se genera como PNG en escala de grises con `QrImagenService` (codificador propio sobre `ZLibStream`, sin bibliotecas de dibujo) y se comparte con el diálogo del sistema desde `TicketPage`. `QrImagenServiceTests` cubre firma, dimensiones, chunks y píxeles.
- **Impacto restante**: el QR es informativo y no fiscal; resume hasta diez productos, no permite validar la venta contra nada y no se ha probado en Android. WhatsApp y correo dependen del diálogo de compartir del sistema, que sigue sin verificarse en dispositivo.
- **Cierre**: validar la imagen y el diálogo en Windows y Android y decidir si se sube el límite de productos. Ver [ADR-qr-ticket.md](05-decisiones/ADR-qr-ticket.md).

### Conteo por denominaciones (persistencia resuelta el 2026-11-21)

- **Dónde**: `ConteoEfectivo` (Core), `CajaPage`, `Program.CerrarCorte` y `CorteCajaDesglose`.
- **Resuelto**: el esquema v11 guarda el desglose por corte y su suma debe coincidir con el efectivo contado; la app y la consola lo capturan y el historial lo muestra. Los renglones son inmutables y los cortes anteriores a la v11 no se reconstruyen.

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

- **Dónde**: `RespaldoService.Restaurar` y **Ajustes → Ver y restaurar respaldos**.
- **Impacto**: la restauración ya se puede hacer desde la app, exige el permiso `RestaurarRespaldo` (solo dueño) y crea un respaldo `antes-restaurar`, pero no deja registro de quién la ejecutó ni qué archivo usó: la base se reemplaza y el registro se perdería con ella.
- **Cierre**: guardar la auditoría en un archivo aparte o en el respaldo de seguridad antes de reemplazar la base.

### Clientes sin estado de cuenta firmado ni envío

- **Dónde**: `ConfiguracionService` y la pestaña Clientes de la app MAUI.
- **Por qué**: T27 agregó baja lógica solo para el dueño y bloqueó el nuevo fiado para clientes inactivos; la cuenta conserva saldo e historial y permite liquidarlos. T30 agregó el método del abono, T31 la exportación CSV y T36 el PDF.
- **Impacto**: el estado de cuenta se exporta a CSV y PDF, pero el PDF no se firma ni se envía automáticamente. La pantalla se verificó en vivo en Windows el 2026-11-19 y en Android 16 el 2026-11-21.
- **Cierre**: firma o envío del estado de cuenta y recordatorios de pago.

### Proveedores sin catálogo propio

- **Dónde**: `ConfiguracionService` y la pestaña Proveedores de la app MAUI.
- **Por qué**: T27 agregó baja lógica exclusiva del dueño; T31/T36 la exportación del estado de cuenta; T39 las compras con detalle, stock, último costo y saldo.
- **Impacto**: las compras se registran con productos y cantidades, pero no existe un catálogo de productos por proveedor ni órdenes de compra. Los pagos y compras anteriores a v8/v13 no se reconstruyen.
- **Cierre**: catálogo por proveedor, órdenes de compra y términos de pago.

### La app MAUI ya cubre la consulta histórica

- **Dónde**: `SistemaTienda.Maui/Pages`.
- **Resuelto (T44)**: la pestaña **Ventas** elige **Desde** y **Hasta** y consulta cualquier periodo, con detalle, reimpresión y anulación. Ya no depende de la consola para ver ventas anteriores.
- **Impacto restante**: la app no tiene pruebas automatizadas de interfaz; la consulta se apoya en `VentaService.ObtenerVentas` y se verifica manualmente.

### Gastos sin edición, fecha retroactiva ni reporte en la app

- **Dónde**: `SistemaTienda.Maui/Pages/GastosPage.xaml(.cs)` y `ConfiguracionService.AgregarGasto`.
- **Por qué**: T21 cubrió el resumen por origen, el historial y el alta; la edición, el borrado y los reportes quedaron fuera del alcance.
- **Impacto**: un gasto mal capturado no se corrige desde la app (solo se compensa con otro gasto) y siempre se registra con la fecha del momento, aunque el proceso admita retroactividad. El resumen por categoría o periodo sigue en la consola. La pantalla recorta concepto y observaciones, pero `AgregarGasto` no lo hace para otros clientes del servicio.
- **Cierre**: edición o anulación con bitácora, captura de fecha y reporte por categoría o periodo en la app.

### App MAUI sin pruebas automatizadas

- **Dónde**: `SistemaTienda.Maui`.
- **Impacto**: CI solo compila para Windows y Android. Una regresión de navegación, sesión, POS, Ventas, anulación, inventario, clientes, proveedores, gastos, caja, reportes, historial o formato no se detecta hasta la verificación manual. El cierre de corte, el conteo, los movimientos de inventario, la anulación, los abonos, los pagos, los gastos y los periodos de reporte desde la app dependen de esa verificación. `TicketServiceTests`, `ConteoEfectivoTests`, `PeriodoReporteTests` y las pruebas de búsqueda, alta, permisos y resumen de clientes, proveedores y gastos cubren cálculos, textos y servicios, no la interfaz.
- **Cierre**: extraer la lógica de cada página a modelos de vista probables y agregar pruebas; valorar pruebas de interfaz más adelante.

### Android verificado, faltan otros fabricantes

- **Dónde**: job `app-maui` de GitHub Actions.
- **Resuelto (2026-11-21)**: la app se instaló y probó en un Redmi Note 13 con Android 16 (API 36): asistente, login, inventario, POS, ventas, clientes, proveedores, gastos, caja, reportes, importación CSV/XLSX, configuración, usuarios/PIN, exportación CSV y PDF, QR como imagen y reimpresión. La verificación encontró que los CSV con MIME genérico no eran seleccionables y se corrigió el filtro de `InventarioPage`.
- **Impacto restante**: solo se ha probado en un fabricante y una versión; pueden aparecer diferencias de diseño o rutas de almacenamiento en otros dispositivos y tabletas.
- **Cierre**: repetir el checklist en tabletas y en al menos otro fabricante.

### App MAUI en .NET 10 con bibliotecas en .NET 8

- **Dónde**: `SistemaTienda.Maui.csproj` frente a `Core`, `Infrastructure` y `Application`.
- **Por qué**: las cargas de trabajo MAUI disponibles en este equipo son para el SDK 10; las bibliotecas se mantienen en `net8.0` para no tocar el resto del stack.
- **Impacto**: compilar la app exige SDK 10 y sus cargas de trabajo. Una máquina con solo SDK 8 puede compilar la solución y las pruebas, pero no la app.
- **Cierre**: unificar todos los proyectos en el mismo framework cuando se decida actualizar el stack completo.

### Mermas sin entidad propia

- **Dónde**: `InventoryService.RegistrarMerma` registra una salida con motivo `Merma: ...`.
- **Impacto**: el costo perdido se calcula al momento y se muestra en el mensaje; no queda guardado como merma con su costo. Los reportes no pueden sumar pérdidas por merma sin recalcular.
- **Cierre**: tabla `Mermas` o columnas de costo en el movimiento que permitan reportes directos.

### Validaciones de borde (resueltas en T40)

- **Dónde**: `InventoryService.AjustarStock`, `ConfiguracionService.AgregarGasto`, `InventoryService.BuscarPorNombre`.
- **Resuelto (T40)**: `AjustarStock` rechaza un producto inexistente antes de abrir la transacción. `AgregarGasto` recorta concepto y observaciones y rechaza un gasto nulo. La búsqueda de productos ignora acentos mediante `Texto.SinAcentos`, compartido con clientes y proveedores.
- **Resuelto (T35)**: el alta y la edición de usuarios exigen nombre y PIN únicos, y el PIN se valida en el servicio. Los usuarios creados antes de T35 podrían conservar un PIN duplicado; en ese caso `VerificarAcceso` devuelve el primero que coincide y conviene cambiar el PIN desde Ajustes.

### Reportes sin desglose por producto

- **Dónde**: `ReporteUtilidadService`, `PeriodoReporte`, `ComparacionReporte`, `ExportadorCsv`, `ExportadorPdf` y la pestaña Reportes de la app MAUI.
- **Impacto**: ya existe la utilidad por periodo (ventas, costo congelado, comisión, mermas y gastos), se consulta en la app con filtros hoy/mes/rango, se desglosa por método de cobro (Efectivo, Tarjeta y Fiado), se compara con el bloque anterior y se exporta a CSV y PDF desde T31/T36/T37/T38. Faltan ventas por día y utilidad por producto o categoría. La consulta de ventas de fechas anteriores sigue solo en la consola.
- **Cierre**: consultas agregadas por producto y periodo con índices por fecha (ya existen en `Ventas(Fecha)`) y ventas por día.

## Baja prioridad

- `Merma` comparte historial con ajustes y ventas; falta filtrar por tipo en la interfaz.
- `ConfiguracionTienda.UsuarioDueñoPINHash` existe en la tabla pero no se usa (el hash vive en `UsuariosSistema`). Eliminar en la próxima migración.
- `Rol` se documentó como texto en algunos documentos; en la base es `INTEGER` con enum. Unificar documentación.
- Sin `Activo` en productos: no se puede retirar un producto del catálogo sin borrarlo. *(Resuelto el 2026-11-06: esquema v7 con desactivación; ver [modulo-inventario.md](02-modulos/modulo-inventario.md).)*
- El catálogo de la app MAUI no filtra por categoría, aunque la búsqueda por nombre ya existe.
- Inicio y Ventas muestran el importe de las ventas vigentes como “cobrados”, pero incluyen las ventas a fiado, que no entran a caja (Caja sí las separa). Revisar la etiqueta o excluir el fiado del resumen.
- Sin pruebas de rendimiento con volúmenes realistas (10 000 productos / 500 ventas por día).
- Los clientes y proveedores creados antes del 2026-11-19 tienen `FechaRegistro` en UTC y se muestran con la hora desplazada; no se reescriben para no alterar datos históricos. Las altas nuevas ya usan hora local.
- El selector de archivos de Android filtra por MIME; se añadieron los tipos habituales de CSV y Excel, pero un archivo con un MIME poco común podría seguir sin ser seleccionable. Validar la extensión después de elegir el archivo sigue protegiendo la importación.
- Comentarios XML heredados de la versión anterior en `InventoryService`; no afectan.
