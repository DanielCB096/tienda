# Coordinación entre agentes

Actualizado: 2026-10-25. Tablero único: cualquier IA o persona que trabaje en este repositorio debe reclamar su tarea aquí antes de empezar.

Instrucciones generales: [AGENTS.md](../../AGENTS.md).

## Convenciones

- Estados: `Libre`, `Tomada`, `En revisión`, `Hecho`, `Bloqueada`.
- Una tarea = una rama = un PR. No se trabaja directo en `main`.
- Actualiza tu fila al empezar y al terminar; incluye rama y hash del commit.
- Si dos tareas tocan los mismos archivos, se serializan.
- No borres filas del registro de sesiones; agrega una nueva.
- No edites la fila de otro agente sin acordarlo.

## Archivos calientes (un solo agente a la vez)

| Archivo | Motivo |
|---------|--------|
| `SistemaTienda/SistemaTienda.Infrastructure/Database.cs` | Esquema, migraciones y acceso; casi todo lo toca |
| `SistemaTienda/SistemaTienda.Presentation/Program.cs` | Menús de consola en un solo archivo |
| `SistemaTienda/SistemaTienda.Maui/MauiProgram.cs` | Registro de dependencias y ruta de la base de la app |
| `SistemaTienda/SistemaTienda.sln` | Altas/bajas de proyectos |

Para cambios de esquema, T1 (migraciones) va primero. T2 (ventas) y T3 (centavos) **no** pueden ejecutarse en paralelo.

## En curso

Sin tareas tomadas al cierre de esta sesión.

## Hecho

| Tarea | Agente | Rama | Resultado | Commit |
|-------|--------|------|-----------|--------|
| T1 | Qoder | feat/migraciones-respaldo | Runner de migraciones con respaldo previo, respaldo/restauración local, menú de consola, 9 pruebas y CI | 8ead191, 54901bd, 50b35be |
| T2 | Qoder | feat/ventas-pos | Esquema v2 (Ventas/VentaLineas), venta atómica multi-línea, pagos efectivo/tarjeta/mixto/fiado, comisión, menú POS y 13 pruebas | 040e77d, d28c223 |
| T8 | Qoder | feat/corte-caja | Esquema v3 (CortesCaja, MovimientosCaja, AbonosFiado), corte atómico con efectivo esperado y diferencia justificada, ingresos/retiros, historial, menú [8] y 13 pruebas | fc6436b, a172e39, ef96af7, d58f391 |
| T3 | Qoder | feat/centavos-enteros | Esquema v4 con importes `long`/`INTEGER`, utilidades `Dinero`, consola sin `Math.Round`, migración v3 → v4 con integridad de FKs y 114 pruebas | d26debc..20894d6 |
| T4 | DeepSeek → Qoder | feat/integracion-t4-t9 | Integración sobre centavos: bitácora pasa a esquema v5, detalles con `Dinero.Formatear`, permisos y bitácora en todos los servicios; 125 pruebas | 9d1dc9f, e009af4 |
| T9 | DeepSeek → Qoder | feat/integracion-t4-t9 | 25 pruebas de borde integradas y convertidas a centavos: gastos, saldos, inventario, usuarios y mapeo de tipos; 150 pruebas | fd879cc |
| T5 | Qoder | feat/reportes-utilidad | Reporte de utilidad bruta y neta por periodo (costo congelado, comisión, mermas y gastos), filtro por fecha, permiso `Consultar` y 7 pruebas; 157 pruebas | 1f3152b |
| T10 | Qoder | feat/respaldo-automatico | Respaldo automático por intervalo al iniciar/cerrar con aviso, bitácora de respaldos manuales y automáticos y 5 pruebas; 162 pruebas | bbc5257 |
| T11 | Qoder | feat/anulacion-ventas | Esquema v6 (`AnulacionesVenta` inmutable), anulación de venta completa con devolución de stock y reverso de fiado, bloqueo tras corte cerrado, permiso `AnularVenta`, menú [3], exclusión de utilidad/corte y 8 pruebas; 170 pruebas | 7c5ed91 |
| T12 | Qoder | feat/ui-maui | App MAUI para Windows y Android con asistente, login por PIN, tablero Inicio/Ventas/Caja/Ajustes, respaldo automático y manual, cierre de sesión y job CI de compilación; 170 pruebas sin cambios | 20bcccd, 221fab0 |
| — | Qoder | feat/maui-pos | Corrección de fecha local en movimientos de inventario: `RegistrarSalida` usa la fecha del equipo y se agregó la prueba de regresión; 171 pruebas | 09413fe |
| T13 | Qoder | feat/maui-pos | Pestaña Vender con carrito, búsqueda/escaneo, cobro efectivo/tarjeta/mixto/fiado y ticket compartible o copiable; 171 pruebas sin cambios | d7c20c3 |
| T14 | Qoder | feat/maui-caja | Pestaña Caja operativa: cierre con diferencia justificada y observaciones obligatorias, ingresos, retiros con confirmación, abonos de fiado y último corte; 171 pruebas sin cambios; PR #5 contra `main` | b8dfad6, 7db70da |
| T15 | Qoder | feat/maui-inventario | Pestaña Inventario: catálogo, escaneo/búsqueda, alta/edición, entradas, mermas, ajustes, historial y filtro de stock bajo; permisos de dueño/encargado/empleado; 171 pruebas sin cambios; PR #7 contra `main` | 735766f |
| T16 | Qoder | feat/maui-anulacion-qr | Detalle de venta en Ventas, reimpresión con QR informativo offline y anulación con motivo/`ANULAR`; permiso validado en `VentaService`; 6 pruebas nuevas; 177 pruebas; PR #8 contra `main` con CI en verde | e474bcb, dc23434, 0eefeba, d6f9cf0 |
| T17 | Qoder | feat/maui-caja-denominaciones | Conteo por denominaciones en Caja con total automático, validación y limpieza; historial de los últimos 20 cortes; 6 pruebas nuevas; 183 pruebas | dab055f, 0496869 |

## Tareas

| ID | Tarea | Estado | Depende de | Notas |
|----|-------|--------|------------|-------|
| T1 | Migraciones versionadas + respaldo | Hecho | — | En `feat/migraciones-respaldo`. `Database.cs` queda libre para T2. |
| T2 | Ventas / POS | Hecho | T1 | En `feat/ventas-pos` (parte de `feat/migraciones-respaldo`). `Database.cs` y `Program.cs` quedan libres. |
| T3 | Dinero en centavos | Hecho | T1, T2 | En `feat/integracion-t4-t9` (parte de `feat/centavos-enteros`). Esquema v4: importes `long`/`INTEGER`; porcentajes siguen en `decimal`. `Database.cs` y `Program.cs` quedan libres. |
| T4 | Bitácora y permisos en servicios | Hecho | — | En `feat/integracion-t4-t9`. Esquema v5: bitácora inmutable, matriz de permisos y validación en servicios. |
| T5 | Reportes de utilidad | Hecho | T2 | En `feat/reportes-utilidad` (sobre `feat/integracion-t4-t9`). Utilidad bruta y neta por periodo con filtros por fecha; permiso `Consultar`. |
| T6 | Importación Excel/CSV | Libre | T2 recomendado | Dependencia nueva requiere ADR. |
| T7 | Interfaz definitiva | Tomada | ADR, T12 | Decisión MAUI validada y base implementada en T12. POS entregado en T13, caja operativa en T14, inventario en T15 y detalle/anulación con QR en T16; faltan las pantallas de clientes, proveedores y gastos en la app. |
| T8 | Corte de caja | Hecho | T2 | En `feat/corte-caja` (parte de `feat/ventas-pos`). Esquema v3: cortes, movimientos de caja y abonos con historial. |
| T9 | Cobertura adicional de pruebas | Hecho | T1–T3 | En `feat/integracion-t4-t9`. 25 pruebas de borde en gastos, saldos, inventario, usuarios y mapeo de tipos. Hallazgos sin corregir: `AjustarStock` no valida que el producto exista; se permiten PIN duplicados y `VerificarAcceso` devuelve el primer usuario por nombre; `AgregarGasto` no recorta el concepto; la búsqueda por nombre no ignora acentos. Ver [deuda-tecnica.md](deuda-tecnica.md). |
| T10 | Respaldo automático por intervalo | Hecho | T1 | En `feat/respaldo-automatico` (sobre `feat/reportes-utilidad`). Se evalúa al iniciar sesión y al cerrar; `0` deshabilita; cualquier respaldo previo cuenta y la copia queda en `Bitacora`. Pendiente: copia a USB/nube y respaldo durante sesiones largas. |
| T11 | Anulación de ventas | Hecho | T2, T4, T8 | En `feat/anulacion-ventas` (sobre `feat/respaldo-automatico`). Esquema v6; solo venta completa, bloqueada si ya está en un corte cerrado; devuelve stock con costo congelado y revierte fiado; reembolso en efectivo manual (ver [deuda-tecnica.md](deuda-tecnica.md)). |
| T12 | App MAUI con login y tablero | Hecho | T7, T11 | En `feat/ui-maui` (sobre `feat/anulacion-ventas`). Asistente, login, tablero de solo lectura, respaldo y cierre de sesión; fuera de la solución y compilada en CI. Android pendiente de probar en dispositivo. Ver [interfaz-maui.md](02-modulos/interfaz-maui.md). |
| T13 | POS en app MAUI | Hecho | T2, T12 | En `feat/maui-pos` (PR #4 contra `main`). Carrito, búsqueda/escaneo, cobro efectivo con recuadro de cambio, tarjeta, mixto, fiado y ticket copiable. Verificación manual en Windows el 2026-10-25 con ventas ficticias: los cuatro métodos de pago, cambio, límite y stock 0, ticket y totales en Ventas/Caja. Pendientes: botón Compartir y Android. |
| T14 | Caja operativa en la app MAUI | Hecho | T8, T13 | En `feat/maui-caja` sobre `feat/maui-pos` (PR #5 contra `main`). Cierre de corte con diferencia justificada, ingresos, retiros y abonos de fiado desde Caja. Verificación manual en Windows el 2026-10-25: corte #1 con faltante y observación, ingreso, retiro con confirmación, abono y corte #2 sin diferencia. Pendientes: denominaciones, historial de cortes y roles distintos al dueño. |
| T15 | Inventario y catálogo en la app MAUI | Hecho | T12, T14 | En `feat/maui-inventario` sobre `feat/maui-caja` (PR #7 contra `main`). Catálogo, escaneo/búsqueda, alta/edición, entradas, mermas, ajustes, historial y stock bajo; permisos por rol. Verificación manual en Windows el 2026-10-25 con datos ficticios: entrada, merma, ajuste, edición, alta desde código desconocido y roles dueño/encargado/empleado. Pendientes: filtro por categoría, desactivación, costo de entrada al catálogo y pruebas automatizadas de interfaz. |
| T16 | Anulación y QR en la app MAUI | Hecho | T11, T13, T15 | En `feat/maui-anulacion-qr` sobre `feat/maui-inventario` (PR #8 contra `main`, CI en verde). Detalle de venta con reimpresión, QR informativo offline y anulación completa (motivo y confirmación `ANULAR`, permiso dueño/encargado). Verificación manual en Windows el 2026-10-25; 177 pruebas. Pendientes: compartir el QR como imagen, reimpresión con efectivo recibido y validación en Android. |
| T17 | Conteo por denominaciones e historial de cortes en Caja | Hecho | T8, T14 | En `feat/maui-caja-denominaciones` sobre `feat/maui-anulacion-qr`. El conteo por billetes y monedas alimenta el efectivo contado sin cambiar el esquema; el historial muestra los últimos 20 cortes cerrados. Verificación manual en Windows el 2026-10-25; 183 pruebas. Pendientes: persistir el desglose y validar en Android. |

## Plantilla para reclamar

Copia esta fila en **En curso** y complétala:

```
| T2 | Ventas / POS | <agente> | feat/ventas-pos | Database.cs, Program.cs, Core/, Application/VentaService.cs | 2026-10-23 | Tomada |
```

Al terminar, mueve la fila a `Hecho` (o elimínala de En curso) y registra el commit en la tabla de sesiones.

## Registro de sesiones

| Fecha | Agente | Rama | Resultado | Commit |
|-------|--------|------|-----------|--------|
| 2026-10-23 | Qoder (sesión inicial) | feat/base-compilable | Solución compilable, 59 pruebas, documentación y push a GitHub | 35c5524 |
| 2026-10-23 | Qoder | feat/migraciones-respaldo | T1 terminado: migraciones v1→N, respaldo/restauración local, 68 pruebas y CI en GitHub Actions | 8ead191..HEAD |
| 2026-10-23 | Qoder | feat/ventas-pos | T2 terminado: esquema v2, venta atómica, pagos y fiado, menú POS, 81 pruebas en CI | 040e77d, d28c223 |
| 2026-10-23 | Qoder | feat/corte-caja | T8 terminado: esquema v3, corte atómico con diferencia justificada, abonos con historial y movimientos de caja, 94 pruebas en CI | fc6436b..HEAD |
| 2026-10-23 | Qoder | feat/centavos-enteros | T3 terminado: esquema v4 con importes en centavos enteros, `Dinero`, consola y servicios adaptados, migración con integridad de claves foráneas, 114 pruebas en CI | d26debc..20894d6 |
| 2026-10-23 | Qoder | feat/respaldo-automatico | T10 terminado: respaldo automático por intervalo con aviso en consola, bitácora de respaldos manuales y automáticos, 5 pruebas nuevas; 162 pruebas | bbc5257 |
| 2026-10-23 | Qoder | feat/anulacion-ventas | T11 terminado: esquema v6, anulación de venta completa con reverso de stock y fiado, bloqueo tras corte cerrado, permiso y bitácora, 8 pruebas nuevas; 170 pruebas en CI | 7c5ed91 |
| 2026-10-23 | Qoder | feat/ui-maui | T12 terminado: app MAUI Windows/Android con asistente, login, tablero, respaldo y cierre de sesión, verificación manual en Windows, 170 pruebas y job `app-maui` | 20bcccd, 221fab0 |
| 2026-10-23 | Qoder | feat/maui-pos | T13 terminado: POS en la app con carrito, cobro efectivo/tarjeta/mixto/fiado y ticket compartible; corrección de fecha local en inventario; 171 pruebas en local; PR #4 abierto | 09413fe..1fdf303 |
| 2026-10-25 | Qoder | feat/maui-pos | Verificación manual del POS en Windows: cinco ventas ficticias, cuatro métodos de pago, cambio, límite y stock 0, ticket con copiar/cerrar y totales coherentes en Ventas/Caja; CI en verde. Pendientes: Compartir y Android | 1fdf303 |
| 2026-10-25 | Qoder | feat/maui-caja | T14 terminado: Caja operativa en la app con cierre de corte, ingresos, retiros, abonos de fiado y último corte; verificación manual en Windows con dos cortes y operaciones ficticias; 171 pruebas en local; PR #5 abierto | b8dfad6, 7db70da |
| 2026-10-25 | Qoder | feat/maui-caja | CI en verde para el PR #5: compilación y pruebas, y app MAUI para Windows y Android | 7db70da |
| 2026-10-25 | Qoder | feat/maui-inventario | T15 terminado: inventario y catálogo en la app con escaneo, alta/edición, entradas, mermas, ajustes, historial y permisos por rol; verificación manual en Windows con los tres roles; 171 pruebas en local; documentación actualizada; PR #7 abierto | 735766f, b0f0094 |
| 2026-10-25 | Qoder | feat/maui-inventario | CI en verde para el PR #7: compilación y pruebas, y app MAUI para Windows y Android | b0f0094 |
| 2026-10-25 | Qoder | feat/maui-anulacion-qr | T16 terminado: ticket con QR offline, detalle y reimpresión en Ventas, anulación con motivo y confirmación, permisos y 6 pruebas nuevas; verificación manual en Windows con venta ficticia y QR decodificado offline; 177 pruebas en local; documentación actualizada; PR #8 abierto | e474bcb, dc23434, 0eefeba, d6f9cf0 |
| 2026-10-25 | Qoder | feat/maui-anulacion-qr | CI en verde para el PR #8: compilación y pruebas (177 en verde), y app MAUI para Windows y Android | d6f9cf0 |
| 2026-10-25 | Qoder | feat/maui-caja-denominaciones | T17 terminado: conteo por denominaciones e historial de cortes en Caja, 6 pruebas nuevas; verificación manual en Windows con conteo de $790.00, corte #3 e historial; 183 pruebas en local; documentación actualizada | dab055f, 0496869 |
