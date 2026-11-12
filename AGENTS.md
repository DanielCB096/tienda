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

## Estado del proyecto (2026-10-25)

- Ramas publicadas con el trabajo actual: `feat/ui-maui` (sobre `feat/anulacion-ventas`, que parte de `feat/respaldo-automatico`), `feat/maui-pos` (sobre `feat/ui-maui`, PR #4 contra `main`), `feat/maui-caja` (sobre `feat/maui-pos`, PR #5 contra `main`), `feat/maui-inventario` (sobre `feat/maui-caja`, PR #7 contra `main`) y `feat/maui-anulacion-qr` (sobre `feat/maui-inventario`, PR #8 contra `main`).
- `main`: solo el commit base; los PR de `feat/base-compilable`, `feat/migraciones-respaldo`, `feat/ventas-pos`, `feat/corte-caja`, `feat/centavos-enteros`, `feat/integracion-t4-t9`, `feat/reportes-utilidad`, `feat/respaldo-automatico`, `feat/anulacion-ventas`, `feat/ui-maui`, `feat/maui-pos`, `feat/maui-caja`, `feat/maui-inventario` y `feat/maui-anulacion-qr` siguen pendientes de integración, en ese orden.
- Build: 0 errores, 0 advertencias. Pruebas: **177 en verde** (verificadas localmente el 2026-10-25 y en GitHub Actions). La app MAUI compila para Windows en local y para Windows y Android en CI. El CI del PR #8 quedó en verde el 2026-10-25: compilación y pruebas, y app MAUI para Windows y Android.
- POS verificado manualmente en Windows el 2026-10-25 con datos ficticios: los cuatro métodos de pago, cambio, límite y stock 0, ticket con copiar/cerrar y totales coherentes en Ventas/Caja. Pendientes: botón Compartir y ejecución en Android.
- Caja verificada manualmente en Windows el 2026-10-25: cierre con faltante y observación (corte #1), ingreso, retiro con confirmación, abono de fiado y cierre sin diferencia (corte #2), con el efectivo esperado coincidiendo con la base. Pendientes: denominaciones, historial de cortes en la app y roles distintos al dueño.
- Ventas (T16) verificado manualmente en Windows el 2026-10-25: venta #10 de tres productos, ticket con QR decodificado offline, reimpresión, anulación con motivo y confirmación `ANULAR`, restauración de stock y bitácora; rechazo con motivo vacío, confirmación incorrecta y corte cerrado; encargado ve la acción y empleado no.
- Implementado: configuración inicial, usuarios/PIN/roles, gastos, inventario por movimientos, ventas/POS (efectivo, tarjeta, mixto y fiado; costo y precio congelados; comisión; anulación completa con reverso), fiado (cliente/abono con historial), proveedores (saldo/pago), corte de caja (efectivo esperado, diferencia justificada, ingresos/retiros e historial), bitácora inmutable y permisos validados en servicios, migraciones versionadas con respaldo, respaldo/restauración local y respaldo automático por intervalo al iniciar/cerrar, dinero en centavos enteros (`long`/`INTEGER`, esquema v4), reporte de utilidad por periodo (ventas − costo congelado − comisión − mermas − gastos), app MAUI para Windows y Android (asistente, login, tablero, POS con carrito, cobro y ticket con QR compartible, inventario con escaneo/alta/edición/entradas/mermas/ajustes/historial, caja operativa con cierre, ingresos, retiros y abonos, anulación de ventas con reimpresión de ticket, respaldo y cierre de sesión) y consola provisional.
- No implementado: pantallas de clientes/proveedores/gastos, filtro por categoría y desactivación de productos en la app, copia de respaldos a USB/nube, conteo por denominaciones e historial de cortes en la app, y compartir el QR como imagen.
- Deuda principal: pagos a proveedores sin historial inmutable, merma sin entidad propia, bitácora no atómica con la operación, entrada de inventario que no actualiza el costo del catálogo, corte sin desglose de billetes, QR limitado a diez productos y sin imagen compartible, app MAUI sin pruebas automatizadas y validación de Android en dispositivo.

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
- Pendiente fuera de T8: conteo por denominaciones.

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

- Validar y sanear antes de grabar; códigos duplicados reportados por fila.
- Cualquier dependencia nueva requiere justificación y ADR.

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
- No incluye conteo por denominaciones ni historial de cortes; sin pruebas automatizadas de interfaz.
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

### T7 — Pantallas de operación en MAUI

- El ADR de UI ya está decidido (MAUI). El punto de venta se entregó en T13, la caja operativa en T14, el inventario en T15 y el detalle/anulación de ventas con ticket QR en T16; faltan las pantallas de clientes, proveedores y gastos.
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

**Nota local (2026-10-25)**: en este equipo las pruebas y la consola ya se ejecutan; `dotnet test` terminó con 177 pruebas en verde. La nota anterior sobre Smart App Control (error `0x800711C7` al cargar ensamblados locales) ya no aplica; si reaparece, revisa Seguridad de Windows → Control de aplicaciones y navegador.

## Convención de pruebas

- xUnit, proyecto `SistemaTienda.Tests`.
- Cada prueba crea `BaseDatosPrueba` (SQLite temporal) y lo elimina en `Dispose`; no compartas estado entre pruebas.
- Nombra en español: `Metodo_Escenario_ResultadoEsperado`.
- Un bug se corrige primero con la prueba que lo reproduce.
