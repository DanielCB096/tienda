# AGENTS.md — Instrucciones para agentes de IA

Punto de entrada para cualquier IA o persona que trabaje en este repositorio. Léelo completo antes de tocar código. Las reglas del dueño están en [CLAUDE-tienda.md](CLAUDE-tienda.md); este archivo las resume y añade el protocolo para trabajar en paralelo sin pisarse.

## Lectura obligatoria (en orden)

1. [CLAUDE-tienda.md](CLAUDE-tienda.md) — reglas del dueño.
2. [SistemaTienda/docs/README.md](SistemaTienda/docs/README.md) — índice y estado.
3. [SistemaTienda/docs/coordinacion.md](SistemaTienda/docs/coordinacion.md) — tablero: qué está tomado y por quién.
4. [SistemaTienda/docs/roadmap.md](SistemaTienda/docs/roadmap.md) — prioridades.
5. [SistemaTienda/docs/deuda-tecnica.md](SistemaTienda/docs/deuda-tecnica.md) — atajos aceptados.
6. El documento del módulo o proceso que vas a tocar (`SistemaTienda/docs/02-modulos/`, `03-procesos/`).

## Reglas duras (resumen)

- Responde y documenta en **español (México)**. Código en inglés; textos de interfaz en español.
- **Nunca trabajes sobre `main`.** Una rama por tarea: `feat/...`, `fix/...`, `docs/...`, `test/...`.
- Commits pequeños, en español, imperativo y con prefijo: `feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `chore:`.
- No hagas commit de `bin/`, `obj/`, `*.db`, respaldos, secretos ni datos reales. `.gitignore` ya los cubre.
- **El stock es la suma de movimientos.** Nunca edites `Productos.Stock` directamente; usa las operaciones de inventario.
- Ventas, abonos, compras y ajustes son **atómicos**: todo en una transacción o nada.
- **El historial no se edita ni se borra**: se corrige con un movimiento nuevo (motivo, usuario, fecha).
- PIN siempre hasheado (PBKDF2-SHA256); nunca texto plano. Permisos validados en la lógica, no solo ocultando menús.
- **100 % offline.** Sin servicios de pago, nube ni dependencias que requieran servidor.
- Dinero: **siempre centavos enteros** (`long` en C#, `INTEGER` en SQLite). Usa `Core/Dinero.cs` para convertir, multiplicar, calcular porcentajes y formatear; los porcentajes (impuesto, comisión) sí van en `decimal`.
- Si una regla de negocio no está clara, **pregunta**. No inventes reglas ni cálculos fiscales.
- Antes de commit final: `git diff --stat`, build y pruebas en verde.
- Operaciones destructivas (`reset --hard`, `push --force`, `clean -fd`, borrar ramas): pide confirmación.

## Protocolo de coordinación (obligatorio)

Puede haber otra IA trabajando en paralelo. El tablero es [SistemaTienda/docs/coordinacion.md](SistemaTienda/docs/coordinacion.md).

### Antes de empezar

1. `git fetch origin` y `git status`. Si hay cambios sin guardar, no los descartes: avisa.
2. Abre el tablero y verifica que tu tarea no esté tomada.
3. Crea tu rama desde `feat/base-compilable` (o desde `main` cuando el PR esté integrado).
4. Reclama la tarea en el tablero: agente, rama, fecha y archivos que vas a tocar.
5. Haz el reclamo en tu primer commit, junto con el cambio.

### Mientras trabajas

- **No edites archivos reservados por otro agente.** Si los necesitas, acuerda el orden.
- `SistemaTienda.Infrastructure/Database.cs` y `SistemaTienda.Presentation/Program.cs` son puntos calientes: **un solo agente a la vez** en cada uno.
- Sincroniza con la rama base con frecuencia para reducir conflictos.
- No refactorices lo que no se te pidió ni reescribas trabajo ajeno.

### Al terminar

1. `dotnet build` y `dotnet test` en verde.
2. Actualiza el tablero (estado Hecho + hash), `changelog.md`, `roadmap.md` y `deuda-tecnica.md` si aplica.
3. Push de tu rama y abre un PR contra `main`. No hagas merge sin revisión.
4. No borres trabajo de otros ni reescribas commits ajenos.

## Cómo trabajar sin pisarse (misma máquina)

No compartas la misma carpeta de trabajo con otra IA. Usa un clon o un worktree:

```bash
# Opción A: clon independiente
git clone https://github.com/DanielCB096/tienda.git tienda-ia2
cd tienda-ia2
git checkout feat/base-compilable
git checkout -b feat/mi-tarea
```

```bash
# Opción B: worktree desde el repo existente
git worktree add ../tienda-ia2 -b feat/mi-tarea feat/base-compilable
```

Cada agente: su carpeta, su rama y su PR.

## Estado del proyecto (2026-11-12)

- **`main` ya contiene todo el trabajo** (`86cbf67`, 113 commits, 246 pruebas): el 2026-11-12 se fusionaron en orden los PR #4, #5, #7, #8, #9, #10, #11, #13, #14, #15, #16, #17 y #18 con merge commit. Los PR #1, #3 y #6 se cerraron sin fusionar porque su contenido ya estaba en la cadena (eran contra ramas intermedias o duplicaban #5). No queda ningún PR abierto.
- Las ramas `feat/*` se conservan como respaldo, sin borrar: `feat/base-compilable`, `feat/migraciones-respaldo`, `feat/ventas-pos`, `feat/corte-caja`, `feat/centavos-enteros`, `feat/integracion-t4-t9`, `feat/reportes-utilidad`, `feat/respaldo-automatico`, `feat/anulacion-ventas`, `feat/ui-maui` y las de la app MAUI hasta `feat/pago-proveedores-metodo`.
- Trabajo nuevo: rama desde `main` actualizado; ya no hay que partir de `feat/base-compilable`.
- Build: 0 errores, 0 advertencias. Pruebas: **246 en verde en local** el 2026-11-12 (el host de pruebas volvió a funcionar) y verificación por CI en cada PR. La app MAUI compila para Windows en local y para Windows y Android en CI. El CI del PR #8 quedó en verde el 2026-10-25: compilación y pruebas, y app MAUI para Windows y Android. El CI del PR #9 también quedó en verde el 2026-10-25 con 183 pruebas. El CI del PR #10 quedó en verde el 2026-10-26 con 187 pruebas. El CI del PR #11 quedó en verde el 2026-10-26 con 191 pruebas. El CI del PR #13 quedó en verde el 2026-11-06 con 206 pruebas. El CI del PR #14 quedó en verde el 2026-11-10 con 210 pruebas y el CI del PR #15 quedó en verde el 2026-11-11 con 218 pruebas (compilación, pruebas y app MAUI para Windows y Android). El CI del PR #16 quedó en verde el 2026-11-12 con 230 pruebas (compilación, pruebas y app MAUI para Windows y Android). El CI del PR #17 quedó en verde el 2026-11-12 con 234 pruebas (compilación, pruebas y app MAUI para Windows y Android). El CI del PR #18 quedó en verde el 2026-11-12 con 246 pruebas (compilación, pruebas y app MAUI para Windows y Android).
- POS verificado manualmente en Windows el 2026-10-25 con datos ficticios: los cuatro métodos de pago, cambio, límite y stock 0, ticket con copiar/cerrar y totales coherentes en Ventas/Caja. Pendientes: botón Compartir y ejecución en Android.
- Caja verificada manualmente en Windows el 2026-10-25: cierre con faltante y observación (corte #1), ingreso, retiro con confirmación, abono de fiado y cierre sin diferencia (corte #2), y en T17 conteo por denominaciones e historial de cortes (corte #3), con el efectivo esperado coincidiendo con la base. Pendientes: desglose persistido, roles distintos al dueño y Android.
- Ventas (T16) verificado manualmente en Windows el 2026-10-25: venta #10 de tres productos, ticket con QR decodificado offline, reimpresión, anulación con motivo y confirmación `ANULAR`, restauración de stock y bitácora; rechazo con motivo vacío, confirmación incorrecta y corte cerrado; encargado ve la acción y empleado no.
- Clientes (T18) implementado en la app: directorio con búsqueda por nombre o teléfono, filtro de saldo, alta, resumen por cobrar y abonos (dueño o encargado); el abono es efectivo y entra al corte. La búsqueda y el alta están cubiertas por pruebas de servicio. Verificación manual pendiente: el entorno no pudo abrir la interfaz gráfica; falta probar en Windows y Android.
- Edición y estado de cuenta de clientes (T22) implementado: botón **Estado** en cada cliente con saldo vigente, total fiado, total abonado y el historial de ventas a fiado y abonos del más reciente al más antiguo (sin ventas anuladas); **Editar datos** corrige nombre y teléfono con permiso `EditarCliente` (dueño y encargado), bitácora `EditarCliente` y sin tocar el saldo. 8 pruebas nuevas en `EstadoCuentaClientesTests`. Verificación manual pendiente por la misma limitación del entorno.
- Proveedores (T19, T23 y T25) implementado en la app: directorio con búsqueda por nombre, clave o teléfono, filtro de saldo, alta con bitácora, resumen por pagar y pago del saldo (dueño o encargado); desde T23 el pago guarda historial inmutable en `PagosProveedor` (esquema v8), el estado de cuenta muestra saldo pendiente, total pagado e historial, y **Editar datos** corrige nombre, clave, teléfono y dirección con permiso `EditarProveedor` (dueño y encargado) y bitácora. Desde T25 el pago pide método (Efectivo, Tarjeta, Transferencia u Otro); el efectivo genera un retiro de caja en la misma transacción (esquema v9) y los demás métodos no tocan la caja. 12 pruebas nuevas en `PagoProveedorMetodoTests`; total 246. Verificación manual pendiente por la misma limitación del entorno.
- Gastos (T21) implementado en la app: pestaña con resumen del total, lo pagado de la caja y lo externo, historial de los últimos 200 gastos y alta con concepto, monto, método, origen y observaciones (dueño o encargado); un gasto de caja reduce el efectivo esperado del corte y uno externo solo la utilidad. El resumen y el orden del historial están cubiertos por pruebas de servicio. Verificación manual pendiente por la misma limitación del entorno.
- Reportes (T24) implementado en la app: pestaña con filtros **Hoy**, **Este mes** y **Rango** de fechas; muestra ventas cobradas, costo de ventas, comisión de tarjeta, utilidad bruta, mermas, gastos y utilidad neta, con el número de ventas y el periodo. Reutiliza `ReporteUtilidadService` (permiso `Consultar`, ventas anuladas excluidas) y `PeriodoReporte` centraliza los límites; 4 pruebas nuevas en `PeriodoReporteTests`. Verificación manual pendiente por la misma limitación del entorno.
- Implementado: configuración inicial, usuarios/PIN/roles, gastos (consola y app), inventario por movimientos con desactivación de productos (esquema v7), ventas/POS (efectivo, tarjeta, mixto y fiado; costo y precio congelados; comisión; anulación completa con reverso), fiado (cliente/abono con historial), proveedores (saldo/pago con método e historial desde el esquema v8; el efectivo genera retiro de caja desde v9), corte de caja (efectivo esperado, diferencia justificada, ingresos/retiros e historial), conteo por denominaciones, bitácora inmutable y permisos validados en servicios, migraciones versionadas con respaldo, respaldo/restauración local y respaldo automático por intervalo al iniciar/cerrar, dinero en centavos enteros (`long`/`INTEGER`, esquema v4), reporte de utilidad por periodo (ventas − costo congelado − comisión − mermas − gastos) en consola y app, app MAUI para Windows y Android (asistente, login, tablero, POS con carrito, cobro y ticket con QR compartible, inventario con escaneo/alta/edición/entradas/mermas/ajustes/historial/desactivación, clientes con búsqueda/alta/abonos/edición y estado de cuenta, proveedores con búsqueda/alta/pagos con método/edición y estado de cuenta, gastos con resumen por origen/historial/alta, caja operativa con cierre, conteo, ingresos, retiros, abonos e historial de cortes, reportes de utilidad por periodo, anulación de ventas con reimpresión de ticket, respaldo y cierre de sesión) y consola provisional.
- No implementado: borrado de clientes y proveedores, edición/borrado y fecha retroactiva de gastos, filtro por categoría en la app, exportación o impresión de reportes, copia de respaldos a USB/nube, persistencia del desglose por denominaciones y compartir el QR como imagen.
- Deuda principal: historial de proveedores solo desde el esquema v8 (los pagos anteriores no se reconstruyen y los previos a v9 no tienen método), merma sin entidad propia, bitácora no atómica con la operación, entrada de inventario que no actualiza el costo del catálogo, clientes y proveedores sin baja ni detección de duplicados, clientes sin método de abono, gastos sin edición ni reporte por categoría, reportes sin exportación ni comparación entre periodos, conteo sin desglose persistido, QR limitado a diez productos y sin imagen compartible, app MAUI sin pruebas automatizadas y validación de Android en dispositivo.

## Tareas disponibles

El detalle vive en el tablero. Resumen y criterios de terminado:

### T1 — Migraciones versionadas + respaldo (HECHO)

- Completado en `feat/migraciones-respaldo` (8ead191, 54901bd, 50b35be).
- Runner con `PRAGMA user_version`, respaldo `antes-migrar` verificado, transacción por migración y rollback.
- `RespaldoLocal` + `RespaldoService` con crear/verificar/listar/restaurar; restauración con respaldo `antes-restaurar`.
- Pruebas en `MigracionesRespaldoTests` (9 casos).
- **T2 ya puede usar el esquema v2**: agrega `new Migracion(2, "...", ...)` en `Migraciones.ObtenerTodas()`.

### T2 — Ventas / POS (HECHO)

- Completado en `feat/ventas-pos` (040e77d, d28c223).
- Esquema v2 con `Ventas` y `VentaLineas`; venta atómica multi-línea con movimiento de salida por producto y cargo de fiado en la misma transacción.
- Precio y costo congelados por línea; `ComisionTarjeta` calculada y guardada en la cabecera.
- `VentaService` valida pagos (suma = total), duplicados, cantidades y reglas de fiado (exclusivo, cliente, límite).
- Menú de consola: carrito, cobro, cambio, ticket de texto y ventas del día.
- Pruebas en `VentasTests` (13 casos); total del proyecto: 81 al cerrar T2.
- Pendiente fuera de T2: descuentos (la anulación llegó en T11 y el ticket con QR en T16).

### T8 — Corte de caja (HECHO)

- Completado en `feat/corte-caja` (fc6436b, a172e39, ef96af7, d58f391).
- Esquema v3 con `CortesCaja`, `MovimientosCaja` y `AbonosFiado`; migración con respaldo previo.
- `RegistrarCorteCaja` calcula y guarda en una transacción; periodo desde el `Hasta` anterior (exclusivo), fondo sugerido = contado previo.
- Efectivo esperado = fondo + ventas en efectivo (incluye parte en efectivo de mixtos) + abonos en efectivo + ingresos − retiros − gastos de Caja; diferencia distinta de cero exige motivo.
- Abonos de fiado con historial en la misma transacción que descuenta el saldo.
- Menú [8] Corte de caja: resumen/cierre, ingreso, retiro e historial; Configuración pasa a [9].
- Pruebas en `CorteCajaTests` (13 casos); total: 94.
- El conteo por denominaciones se agregó en T17, solo en la app y sin persistir el desglose.

### T3 — Dinero en centavos enteros (HECHO)

- Completado en `feat/centavos-enteros`.
- `Core/Dinero.cs` centraliza conversión pesos ↔ centavos, multiplicación, porcentaje y formato invariante.
- Entidades y columnas de dinero pasan a `long`/`INTEGER`; los porcentajes siguen en `decimal`.
- Esquema v4 reconstruye las tablas con conversión `CAST(ROUND(col * 100) AS INTEGER)`, conservando Id, FKs e índices.
- Pruebas en `DineroTests` (5 casos) y migración v3 → v4 en `MigracionesRespaldoTests`; total: 114.
- Pendiente fuera de T3: cantidades fraccionarias (kg/litros), que requieren otra unidad mínima.

### T4 — Bitácora y permisos en servicios (HECHO)

- Integrado por Qoder en `feat/integracion-t4-t9` (9d1dc9f, e009af4) sobre el dinero en centavos.
- Esquema v5 con `Bitacora` inmutable (disparadores contra `UPDATE`/`DELETE`), `BitacoraService` y `AutorizacionService`.
- Permisos validados en `ConfiguracionService`, `InventoryService`, `VentaService` y `CorteCajaService`; `SesionActual` se inicia al autenticar.
- Los detalles auditados formatean el dinero con `Dinero.Formatear`.
- Pruebas en `BitacoraPermisosTests` (11 casos).

### T5 — Reportes de utilidad (HECHO)

- Completado en `feat/reportes-utilidad` sobre `feat/integracion-t4-t9`.
- Utilidad bruta = ventas − costo congelado − comisión de tarjeta; utilidad neta = bruta − mermas − gastos.
- Las mermas salen del costo congelado en `MovimientoInventario` (motivo `Merma:%`); los gastos suman ambos orígenes.
- Filtro por día, mes o rango de fechas en la consola; permiso `Consultar` validado en el servicio.
- Pruebas en `ReportesTests` (7 casos); total 157.

### T10 — Respaldo automático (HECHO)

- Completado en `feat/respaldo-automatico` sobre `feat/reportes-utilidad`.
- `RespaldoService.EjecutarAutomaticoSiCorresponde` crea un respaldo verificado cuando venció `RespaldoIntervaloDias` (0 deshabilita); cualquier respaldo previo cuenta para el intervalo.
- La consola revisa al iniciar sesión y al cerrar, avisa cuando crea la copia y advierte si está vencido y no se pudo crear.
- La creación manual y automática se registra en la bitácora (`AccionBitacora.Respaldo`) con ruta, etiqueta y usuario.
- Pruebas en `RespaldoAutomaticoTests` (5 casos); total 162.
- Pendiente fuera de T10: copia a USB/nube y respaldo durante sesiones largas.

### T11 — Anulación de ventas (HECHO)

- Completado en `feat/anulacion-ventas` sobre `feat/respaldo-automatico` (7c5ed91).
- Esquema v6 con `AnulacionesVenta` inmutable (una por venta) e índice por fecha.
- `DatabaseTools.AnularVenta` registra la anulación, devuelve el stock con el costo congelado y revierte el fiado en una transacción; se bloquea si la venta ya está en un corte cerrado.
- Permiso `AnularVenta` para dueño y encargado; motivo obligatorio y acción `AnulacionVenta` en la bitácora.
- Las ventas anuladas se excluyen del corte pendiente y de la utilidad, y se marcan `[ANULADA]` en ventas del día.
- Menú Ventas → [3] Anular venta con confirmación `ANULAR`.
- Pruebas en `AnulacionVentasTests` (8 casos); total 170.
- Limitación confirmada: el reembolso en efectivo es manual; ver `deuda-tecnica.md`.

### T6 — Importación Excel/CSV

- Implementado en `feat/importacion-catalogo-csv`: importación de productos CSV desde Inventario para dueño/encargado; se guardan las filas válidas y se omiten con reporte por línea las inválidas y duplicadas, sin reemplazar registros existentes.
- Alcance aprobado: importar productos desde CSV; guardar filas válidas y reportar/omitir por fila errores y códigos duplicados, sin reemplazar registros existentes.
- El archivo se valida completo antes de empezar a grabar; los códigos duplicados en la base o el CSV se reportan por línea. El stock inicial se conserva como movimiento de inventario.
- No agregar dependencias sin justificación y ADR; Excel `.xlsx` y plantillas por giro quedan fuera de este alcance.
- Pruebas focalizadas: 6 casos; suite total: 252. Build de la solución y MAUI Windows sin errores ni advertencias; falta verificar el selector y el reporte en Windows y Android.

### T12 — App MAUI con login y tablero (HECHO)

- Completado en `feat/ui-maui` sobre `feat/anulacion-ventas`.
- Proyecto `SistemaTienda.Maui` para `net10.0-windows10.0.19041.0` y `net10.0-android`, fuera de la solución para no frenar el job de pruebas.
- Asistente de primera configuración, login con 3 intentos, tablero con Inicio/Ventas/Caja/Ajustes, respaldo automático al iniciar y cerrar sesión, respaldo manual y cierre de sesión.
- `SesionApp` reaplica `SesionActual` en cada evento porque `AsyncLocal` no persiste entre eventos de interfaz.
- Job `app-maui` en CI compila Windows y Android.
- Verificado manualmente en Windows; Android pendiente de probar en dispositivo.
- Detalle en [SistemaTienda/docs/02-modulos/interfaz-maui.md](SistemaTienda/docs/02-modulos/interfaz-maui.md).

### T13 — POS en app MAUI (HECHO)

- Completado en `feat/maui-pos` sobre `feat/ui-maui` (commit `d7c20c3`; push y PR #4 contra `main`).
- Pestaña **Vender**: búsqueda por código o nombre, carrito con cantidades y validación de stock, cobro efectivo con recuadro de cambio/tarjeta/mixto/fiado y ticket interno compartible o copiable; el ticket de efectivo imprime lo recibido y el cambio.
- Usa `VentaService` sin lógica de negocio en la página; aplica `SesionApp.Aplicar()` antes de cada llamada.
- Verificado manualmente en Windows el 2026-10-25 con cinco ventas ficticias: los cuatro métodos de pago, cambio, límite y stock 0, ticket con copiar/cerrar y totales coherentes en Ventas/Caja. Pendientes: botón Compartir (diálogo del sistema) y Android.
- En T13 el ticket no incluía QR y no se podía anular desde la app; el QR y la anulación llegaron en T16. Sin pruebas automatizadas de interfaz.
- Detalle en [SistemaTienda/docs/02-modulos/interfaz-maui.md](SistemaTienda/docs/02-modulos/interfaz-maui.md) y [SistemaTienda/docs/03-procesos/venta.md](SistemaTienda/docs/03-procesos/venta.md).

### T14 — Caja operativa en app MAUI (HECHO)

- Completado en `feat/maui-caja` sobre `feat/maui-pos`.
- Pestaña **Caja**: periodo abierto, efectivo esperado y desglose por método; cierre con diferencia en vivo, observaciones obligatorias y confirmación; ingresos y retiros con monto y motivo; abono de fiado con cliente y saldo; tarjeta del último corte cerrado.
- Reutiliza `CorteCajaService` y `ConfiguracionService` sin lógica de negocio en la página; aplica `SesionApp.Aplicar()` antes de cada llamada.
- Verificado manualmente en Windows el 2026-10-25: corte #1 con faltante de $10.00 y observación, ingreso de $50.00, retiro de $20.00 con confirmación, abono de $10.00 y corte #2 sin diferencia por $790.00.
- En T14 no incluía conteo por denominaciones ni historial de cortes; ambos se completaron en T17. Sin pruebas automatizadas de interfaz.
- Detalle en [SistemaTienda/docs/02-modulos/interfaz-maui.md](SistemaTienda/docs/02-modulos/interfaz-maui.md) y [SistemaTienda/docs/03-procesos/corte-caja.md](SistemaTienda/docs/03-procesos/corte-caja.md).

### T15 — Inventario y catálogo en la app MAUI (HECHO)

- Completado en `feat/maui-inventario` sobre `feat/maui-caja` (commit `735766f`; PR #7 contra `main`).
- Pestaña **Inventario**: catálogo con búsqueda por nombre, escaneo por código, filtro y resumen de stock bajo/valorizado; alta y edición de productos; entradas, mermas, ajustes e historial de los últimos 200 movimientos.
- Permisos aplicados en pantalla y en `InventoryService`: empleado solo consulta; encargado edita y registra entradas/mermas (sin precios ni ajustes); dueño tiene todo.
- Verificado manualmente en Windows el 2026-10-25 con datos ficticios y los tres roles. El escaneo se simuló con teclado y Enter.
- Limitaciones: la entrada no actualiza el costo del catálogo; no hay filtro por categoría, borrado ni desactivación; sin pruebas automatizadas de interfaz.
- Detalle en [SistemaTienda/docs/02-modulos/interfaz-maui.md](SistemaTienda/docs/02-modulos/interfaz-maui.md) y [SistemaTienda/docs/02-modulos/modulo-inventario.md](SistemaTienda/docs/02-modulos/modulo-inventario.md).

### T16 — Anulación y QR en la app MAUI (HECHO)

- Completado en `feat/maui-anulacion-qr` sobre `feat/maui-inventario` (PR #8 contra `main`, CI en verde; sin merge).
- Pestaña **Ventas**: la lista abre el detalle con líneas, pagos, comisión, cliente, estado y datos de anulación; permite reimprimir el ticket y anular la venta completa con motivo obligatorio y confirmación `ANULAR`.
- Solo dueño y encargado ven la acción; `VentaService.AnularVenta` vuelve a validar el permiso `AnularVenta`, el estado de la venta y que no esté en un corte cerrado.
- `TicketService` (capa `Application`) genera el texto y el contenido del QR sin dependencias de MAUI; el QR es informativo, offline, resume hasta diez productos y no es un comprobante fiscal. Se dibuja con `QrTicketView` sobre `GraphicsView`.
- Verificado manualmente en Windows el 2026-10-25: venta #10 de tres productos, QR decodificado offline, reimpresión, rechazo por motivo vacío y confirmación incorrecta, anulación con restauración de stock y bitácora, bloqueo por corte cerrado y visibilidad por rol.
- Pruebas en `TicketServiceTests` (6 casos); total del proyecto: 177.
- Limitaciones: el QR no se comparte como imagen, la reimpresión no incluye el efectivo recibido y el QR no se ha validado en Android. Ver [ADR-qr-ticket.md](SistemaTienda/docs/05-decisiones/ADR-qr-ticket.md) y [deuda-tecnica.md](SistemaTienda/docs/deuda-tecnica.md).
- Detalle en [SistemaTienda/docs/02-modulos/interfaz-maui.md](SistemaTienda/docs/02-modulos/interfaz-maui.md) y [SistemaTienda/docs/03-procesos/venta.md](SistemaTienda/docs/03-procesos/venta.md).

### T17 — Conteo por denominaciones e historial de cortes en Caja (HECHO)

- Completado en `feat/maui-caja-denominaciones` sobre `feat/maui-anulacion-qr` (PR #9 contra `main`, sin merge).
- **Caja**: conteo opcional con los 11 billetes y monedas en pesos, subtotal por fila y total automático en **Efectivo contado** (solo lectura); cantidades inválidas bloquean el cierre; **Limpiar conteo** devuelve la captura manual; borrar la última cantidad también limpia el total derivado.
- **Historial**: últimos 20 cortes cerrados con folio, periodo, esperado, contado, diferencia coloreada, responsable, fecha y observaciones; se recarga después de cada cierre.
- `ConteoEfectivo` vive en `Core` con el catálogo y la suma verificada; 6 pruebas nuevas en `ConteoEfectivoTests`.
- El esquema no cambió: el corte guarda solo esperado, contado y diferencia, y el desglose no se persiste.
- Verificado manualmente en Windows el 2026-10-25: cantidad inválida, conteo de $790.00, limpieza, total manual, corte #3 sin diferencia, historial #3–#1 y corrección del total derivado viejo.
- Detalle en [SistemaTienda/docs/02-modulos/interfaz-maui.md](SistemaTienda/docs/02-modulos/interfaz-maui.md) y [SistemaTienda/docs/03-procesos/corte-caja.md](SistemaTienda/docs/03-procesos/corte-caja.md).

### T18 — Pantalla de Clientes en la app MAUI (HECHO)

- Completado en `feat/maui-clientes` sobre `feat/maui-caja-denominaciones` (PR #10 contra `main`, CI en verde; sin merge).
- Pestaña **Clientes** entre Ventas y Caja: resumen (clientes, con saldo y por cobrar), búsqueda por nombre o teléfono sin distinguir mayúsculas ni acentos, filtros **Todos** / **Con saldo**, alta con teléfono y saldo inicial opcionales, y abono con validación de saldo.
- `ConfiguracionService.BuscarClientes` centraliza la búsqueda; `AgregarClienteFiado` recorta nombre y teléfono y registra `AccionBitacora.Cliente`; el abono reutiliza `RegistrarAbonoCliente` con el permiso `RegistrarAbono` para dueño y encargado.
- 4 pruebas nuevas en `FiadoProveedoresTests` (búsqueda sin filtro, filtro por nombre/teléfono con acentos y mayúsculas, filtro con saldo y alta con bitácora); total del proyecto: 187.
- El abono se registra como efectivo y entra al corte; T18 no incluía edición, borrado, estado de cuenta ni método de abono (la edición y el estado de cuenta llegaron en T22; siguen fuera de alcance la baja, los duplicados y el recordatorio por WhatsApp).
- Verificación manual pendiente: la compilación y las pruebas están en verde, pero el entorno no permitió abrir la interfaz gráfica; falta probar la pantalla en Windows y Android.
- Detalle en [SistemaTienda/docs/02-modulos/interfaz-maui.md](SistemaTienda/docs/02-modulos/interfaz-maui.md) y [SistemaTienda/docs/03-procesos/cliente-fiado.md](SistemaTienda/docs/03-procesos/cliente-fiado.md).

### T19 — Pantalla de Proveedores en la app MAUI (HECHO)

- Completado en `feat/maui-proveedores` sobre `feat/maui-clientes` (PR #11 contra `main`; sin merge).
- Pestaña **Proveedores** entre Clientes y Caja: resumen (proveedores, con saldo y por pagar), búsqueda por nombre, clave o teléfono sin distinguir mayúsculas ni acentos, filtros **Todos** / **Con saldo**, alta con clave/RFC, teléfono, dirección y saldo inicial opcionales, y pago con validación de saldo.
- `ConfiguracionService.BuscarProveedores` centraliza la búsqueda; `AgregarProveedor` recorta nombre, clave, teléfono y dirección y registra `AccionBitacora.Proveedor`; el pago reutiliza `RegistrarPagoProveedor` con el permiso `PagarProveedor` para dueño y encargado.
- 4 pruebas nuevas en `FiadoProveedoresTests` (búsqueda sin filtro, filtro por nombre/clave/teléfono con acentos y mayúsculas, filtro con saldo y alta con bitácora); total del proyecto: 191.
- El pago reduce el saldo y deja bitácora, pero no entra al corte ni pregunta método; la pantalla recuerda registrarlo también como retiro si es efectivo. T19 no incluía edición, borrado ni estado de cuenta; el historial de pagos, el estado de cuenta y la edición llegaron en T23.
- Verificación manual pendiente: la compilación y las pruebas están en verde, pero el entorno no permitió abrir la interfaz gráfica; falta probar la pantalla en Windows y Android.
- Detalle en [SistemaTienda/docs/02-modulos/interfaz-maui.md](SistemaTienda/docs/02-modulos/interfaz-maui.md) y [SistemaTienda/docs/03-procesos/proveedores.md](SistemaTienda/docs/03-procesos/proveedores.md).

### T20 — Desactivación de productos (esquema v7) (HECHO)

- Completado en `feat/desactivacion-productos` sobre `feat/maui-proveedores` (PR #13 contra `main`, CI en verde).
- Migración **v7**: `Productos.Activo` (`INTEGER NOT NULL DEFAULT 1`); los productos existentes quedan activos y la desactivación es un estado operativo, no una baja del historial.
- Reglas de negocio definidas con el dueño el 2026-11-06: inactivo oculto de **Vender** (búsqueda y escaneo) y visible en **Inventario**; se puede desactivar con stock y este se conserva; reactivable en cualquier momento; mientras está inactivo se bloquean entradas, mermas y ajustes (editar datos sí); **solo el dueño** desactiva o reactiva.
- `InventoryService.DesactivarProducto`/`ReactivarProducto` con el permiso nuevo `DesactivarProducto` (dueño), bitácora `CambiarEstadoProducto` y bloqueo de movimientos para inactivos; `VentaService` rechaza vender inactivos.
- Inventario en la app: filtros **Todos/Activos/Stock bajo/Inactivos** (también en la búsqueda), distintivo **INACTIVO**, aviso en el detalle y botón desactivar/reactivar con confirmación. El POS muestra solo activos y avisa si se escanea un inactivo. El stock bajo de Inicio y la consola cuenta solo activos; el valorizado los incluye.
- 15 pruebas nuevas en `DesactivacionProductosTests` (migración v6 → v7, estados, permisos, bitácora, venta y movimientos bloqueados, búsquedas); total del proyecto: 206.
- Pendiente: verificación manual de la pantalla en Windows y Android; la consola provisional no tiene la acción (queda anotada en deuda técnica).
- Detalle en [SistemaTienda/docs/02-modulos/modulo-inventario.md](SistemaTienda/docs/02-modulos/modulo-inventario.md), [SistemaTienda/docs/02-modulos/interfaz-maui.md](SistemaTienda/docs/02-modulos/interfaz-maui.md) y [SistemaTienda/docs/03-procesos/venta.md](SistemaTienda/docs/03-procesos/venta.md).

### T21 — Pantalla de gastos en la app MAUI (HECHO)

- Completado en `feat/maui-gastos` sobre `feat/desactivacion-productos` (PR #14 contra `main`, CI en verde con 210 pruebas; sin merge).
- Pestaña **Gastos** entre Proveedores y Caja: resumen del total, lo pagado del efectivo de la caja y lo externo; historial de los últimos 200 gastos con concepto, fecha, método, origen y observaciones.
- **+ Nuevo** solo para dueño y encargado: concepto y monto obligatorios, método, interruptor **Salió del efectivo de la caja** y observaciones; `ConfiguracionService.AgregarGasto` vuelve a validar `RegistrarGasto` y deja la bitácora `Gasto`.
- `ResumenGastos` y `ConfiguracionService.ObtenerResumenGastos` separan los gastos de caja (afectan el efectivo esperado del corte) de los externos (solo la utilidad).
- 4 pruebas nuevas en `GastosTests` (campos persistidos, orden del historial y resumen con y sin gastos); total del proyecto: 210.
- Pendiente: verificación manual en Windows y Android; edición/borrado, fecha retroactiva y reporte por categoría o periodo siguen fuera de la app.
- Detalle en [SistemaTienda/docs/02-modulos/interfaz-maui.md](SistemaTienda/docs/02-modulos/interfaz-maui.md), [SistemaTienda/docs/03-procesos/gasto.md](SistemaTienda/docs/03-procesos/gasto.md) y [SistemaTienda/docs/07-pruebas/plan-de-pruebas.md](SistemaTienda/docs/07-pruebas/plan-de-pruebas.md).

### T22 — Edición y estado de cuenta de clientes (HECHO)

- Completado en `feat/maui-clientes-edicion` sobre `feat/maui-gastos` (PR #15 contra `main`, CI en verde con 218 pruebas; sin merge).
- Reglas definidas con el dueño el 2026-11-10: editar nombre y teléfono solo dueño y encargado (permiso nuevo `EditarCliente`); **no hay bajas** de clientes, solo edición; el saldo nunca se edita a mano.
- `ConfiguracionService.EditarClienteFiado` recorta nombre y teléfono, rechaza el nombre vacío y al cliente inexistente, no toca `SaldoActual` y deja bitácora `EditarCliente` con los datos anteriores y nuevos.
- `ObtenerEstadoCuentaCliente` devuelve datos, saldo vigente, total fiado, total abonado y el historial de ventas a fiado (sin anuladas) y abonos, del más reciente al más antiguo.
- Clientes en la app: botón **Estado** en cada renglón que abre el estado de cuenta; **Editar datos** dentro del estado de cuenta (dueño y encargado), **Abonar** reutiliza el flujo existente y el estado de cuenta se refresca después.
- 8 pruebas nuevas en `EstadoCuentaClientesTests` (edición con permiso y bitácora, rechazo para empleado, saldo intacto, validaciones, totales, exclusión de anuladas y cliente inexistente); total del proyecto: 218.
- Pendiente: verificación manual en Windows y Android; baja de clientes, detección de duplicados y método de abono siguen fuera de alcance.
- Detalle en [interfaz-maui.md](SistemaTienda/docs/02-modulos/interfaz-maui.md), [cliente-fiado.md](SistemaTienda/docs/03-procesos/cliente-fiado.md) y [plan-de-pruebas.md](SistemaTienda/docs/07-pruebas/plan-de-pruebas.md).

### T23 — Edición y estado de cuenta de proveedores (HECHO)

- Completado en `feat/maui-proveedores-edicion` sobre `feat/maui-clientes-edicion`.
- Reglas definidas con el dueño el 2026-11-11: editar nombre, clave/RFC, teléfono y dirección solo dueño y encargado (permiso nuevo `EditarProveedor`); **no hay bajas** de proveedores; el saldo solo cambia con pagos.
- Migración **v8** con `PagosProveedor` inmutable (monto, fecha, usuario y saldo antes/después) e índices por proveedor y fecha; los pagos anteriores a la migración no se pueden reconstruir y la pantalla lo advierte.
- `ConfiguracionService.RegistrarPagoProveedor` guarda el historial y descuenta el saldo en una sola transacción; `EditarProveedor` recorta los datos, exige nombre, no toca el saldo y deja bitácora `EditarProveedor`.
- `ObtenerEstadoCuentaProveedor` devuelve datos, saldo pendiente, total pagado e historial de pagos del más reciente al más antiguo.
- Proveedores en la app: botón **Estado** en cada renglón, **Editar datos** y **Pagar** dentro del estado de cuenta; el estado se refresca al pagar o editar.
- 12 pruebas nuevas en `EstadoCuentaProveedoresTests` (edición con permiso y bitácora, rechazo para empleado, saldo intacto, validaciones, pago atómico con historial, inmutabilidad, totales, sin pagos e inexistente); total del proyecto: 230.
- Pendiente: verificación manual en Windows y Android; baja, detección de duplicados, método de pago con entrada al corte, compras por proveedor e impresión/exportación del estado de cuenta siguen fuera de alcance.
- Detalle en [interfaz-maui.md](SistemaTienda/docs/02-modulos/interfaz-maui.md), [proveedores.md](SistemaTienda/docs/03-procesos/proveedores.md) y [plan-de-pruebas.md](SistemaTienda/docs/07-pruebas/plan-de-pruebas.md).

### T24 — Reportes de utilidad en la app MAUI (HECHO)

- Completado en `feat/maui-reportes` sobre `feat/maui-proveedores-edicion`.
- Reglas definidas con el dueño el 2026-11-12: filtros **Hoy**, **Este mes** y **Rango** de fechas; todos los roles con permiso `Consultar` pueden ver el reporte (la pantalla solo lee).
- Pestaña **Reportes** entre Caja y Ajustes: botón **Actualizar**, selector de periodo y panel de rango con `DatePicker`; muestra ventas cobradas, costo de ventas, comisión de tarjeta, utilidad bruta, mermas, gastos y utilidad neta, con número de ventas y periodo.
- Reutiliza `ReporteUtilidadService` sin tocar el cálculo; las ventas anuladas quedan excluidas y el permiso se valida en el servicio.
- `PeriodoReporte` (Core) centraliza los límites de hoy, mes en curso y rango con fechas sin hora; 4 pruebas nuevas en `PeriodoReporteTests`.
- La utilidad se pinta en verde si es positiva o cero y en rojo si es negativa, tanto en el resumen como en el desglose.
- CI del PR #17 en verde el 2026-11-12: compilación, **234 pruebas** y app MAUI para Windows y Android.
- Pendiente: verificación manual en Windows y Android; exportación/impresión, comparación entre periodos y desglose por método o categoría siguen fuera de alcance.
- Detalle en [interfaz-maui.md](SistemaTienda/docs/02-modulos/interfaz-maui.md), [reportes.md](SistemaTienda/docs/03-procesos/reportes.md) y [plan-de-pruebas.md](SistemaTienda/docs/07-pruebas/plan-de-pruebas.md).

### T25 — Método de pago y retiro automático en proveedores (HECHO)

- Completado en `feat/pago-proveedores-metodo` sobre `feat/maui-reportes`.
- Reglas definidas con el dueño el 2026-11-12: métodos **Efectivo**, **Tarjeta**, **Transferencia** y **Otro**; el efectivo genera un retiro de caja automático en la misma transacción; basta el permiso `PagarProveedor` (dueño y encargado) también para efectivo; los demás métodos no tocan la caja.
- Migración **v9** con `PagosProveedor.MetodoPago` (texto, vacío en los pagos anteriores) y `MovimientoCajaId` (nullable). El retiro se inserta antes del pago porque `PagosProveedor` es inmutable.
- `DatabaseTools.RegistrarPagoProveedor` actualiza saldo, inserta el retiro `Pago a proveedor: <nombre>` cuando aplica y guarda el pago, todo en una transacción; `ConfiguracionService.RegistrarPagoProveedor` valida el método, deja bitácora `PagoProveedor` con `Metodo=` y `RetiroCaja=` y devuelve método y movimiento.
- `MetodoPagoProveedor` (Core) centraliza el catálogo, la validación y el recorte del método; no debe confundirse con `MetodoPagoVenta`.
- Consola y app piden el método; el estado de cuenta muestra método, usuario, saldo antes/después y el retiro ligado, y advierte los pagos anteriores a v9.
- 12 pruebas nuevas en `PagoProveedorMetodoTests` (efectivo y caja, métodos sin efectivo, método inválido, recorte, encargado, empleado, migración v8 → v9); se ajustaron las pruebas de migración v1/v2/v3/v6. Total del proyecto: 246 en verde en local.
- PR #18 contra `main` abierto el 2026-11-12; CI en verde con **246 pruebas** y app MAUI para Windows y Android. Sin merge, pendiente de aprobación del dueño.
- Pendiente: verificación manual en Windows y Android; baja y detección de duplicados, impresión/exportación del estado de cuenta y compras por proveedor siguen fuera de alcance.
- Detalle en [proveedores.md](SistemaTienda/docs/03-procesos/proveedores.md), [interfaz-maui.md](SistemaTienda/docs/02-modulos/interfaz-maui.md), [modelo-de-datos.md](SistemaTienda/docs/01-arquitectura/modelo-de-datos.md) y [plan-de-pruebas.md](SistemaTienda/docs/07-pruebas/plan-de-pruebas.md).

### T7 — Pantallas de operación en MAUI

- El ADR de UI ya está decidido (MAUI). El punto de venta se entregó en T13, la caja operativa en T14, el inventario en T15, el detalle/anulación de ventas con ticket QR en T16, el conteo con historial de cortes en T17, la pantalla de clientes en T18, la de proveedores en T19, la desactivación de productos en T20, la de gastos en T21, la edición con estado de cuenta de clientes en T22, la edición con estado de cuenta de proveedores en T23 y los reportes de utilidad en T24. Las pantallas de operación del MVP quedan cubiertas.
- Trabaja una pantalla a la vez, con sus pruebas y documentación, y espera confirmación antes de la siguiente.

## No rehacer

- La reparación de la solución, referencias y capas ya está hecha y probada.
- No reintroduzcas `SistemaTienda.Core/Entities.cs` (se eliminó por duplicar entidades).
- No cambies el formato de hash (`pbkdf2-sha256$iteraciones$salt$hash`) ni bajes las iteraciones.
- No muevas las pruebas a una base compartida: cada prueba usa su archivo temporal (`BaseDatosPrueba`).
- No edites el tablero fuera de tu sección ni borres filas del registro.
- No agregues `SistemaTienda.Maui` a `SistemaTienda.sln`: el job de pruebas no debe cargar las cargas de trabajo MAUI. La app se compila en su propio job de CI.
- No pongas lógica de negocio en las páginas MAUI: usa los servicios de `Application` y reaplica la sesión con `SesionApp.Aplicar()` antes de llamarlos desde eventos.

## Comandos

```bash
# Compilar
dotnet build SistemaTienda/SistemaTienda.sln

# Probar
dotnet test SistemaTienda/SistemaTienda.sln

# Ejecutar la consola
dotnet run --project SistemaTienda/SistemaTienda.Presentation/Presentation.csproj

# Usar otra base de datos
SISTEMATIENDA_DB=/ruta/a/tienda.db dotnet run --project SistemaTienda/SistemaTienda.Presentation/Presentation.csproj

# Compilar y ejecutar la app MAUI en Windows
dotnet build SistemaTienda/SistemaTienda.Maui/SistemaTienda.Maui.csproj -f net10.0-windows10.0.19041.0
dotnet run --project SistemaTienda/SistemaTienda.Maui/SistemaTienda.Maui.csproj -f net10.0-windows10.0.19041.0

# Compilar para Android (requiere Android SDK y JDK 17)
dotnet build SistemaTienda/SistemaTienda.Maui/SistemaTienda.Maui.csproj -f net10.0-android
```

Entorno conocido: el equipo tiene runtime **.NET 10** y SDK 10; los proyectos `net8.0` usan `RollForward=Major` para ejecutarse. Si creas un nuevo ejecutable o proyecto de pruebas, agrega la misma propiedad o fallará al arrancar. La app MAUI usa los frameworks `net10.0-android` y `net10.0-windows10.0.19041.0` y requiere las cargas de trabajo `maui-windows` y `maui-android` instaladas.

**Nota local (2026-11-12)**: el bloqueo `0x800711C7` de Control de aplicaciones es intermitente. Corrió 230 pruebas en verde el 2026-11-11, pero volvió a bloquear el `SistemaTienda.Tests.dll` recién compilado el 2026-11-12 (también al compilar a una carpeta temporal). La compilación de la solución y de la app MAUI funciona. Mientras el bloqueo esté activo, la verificación de pruebas se hace con el CI de GitHub Actions, que es la referencia para cada PR; revisa Seguridad de Windows → Control de aplicaciones y navegador (o Smart App Control) si necesitas correrlas en local.

## Convención de pruebas

- xUnit, proyecto `SistemaTienda.Tests`.
- Cada prueba crea `BaseDatosPrueba` (SQLite temporal) y lo elimina en `Dispose`; no compartas estado entre pruebas.
- Nombra en español: `Metodo_Escenario_ResultadoEsperado`.
- Un bug se corrige primero con la prueba que lo reproduce.
