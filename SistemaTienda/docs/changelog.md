# Registro de Cambios (Changelog)

## Estructura

Cada entrada tiene formato:
- **vX.Y.Z - YYYY-MM-DD**: Nombre de la funcionalidad o corrección.
  - *Agregado*: Lo nuevo incorporado.
  - *Modificado*: Lo que cambió de versiones anteriores.
  - *Corregido*: Bugs solucionados.
  - *Eliminado*: Lo que se removió.

## Versionado

- **Mayor (X)**: Cambios incompatibles en la API o arquitectura.
- **Menor (Y)**: Nueva funcionalidad sin romper lo existente.
- **Parche (Z)**: Correcciones de errores, seguridad o rendimiento.

---

## Próxima Versión Planificada: v0.1.0-MVP

Próximamente después de completar los módulos críticos.

### [En desarrollo] v0.1.0-base

- Estructura de solución y proyectos (.NET 8).
- Base de datos SQLite con tablas iniciales (Productos, Ventas, Clientes, Proveedores).
- Capa de servicios de inventario y ventas.
- Interfaz de menú principal y catálogo.
- Sistema de PIN y roles de usuario.
- Respaldo y restauración manual.
- Configuración básica de tienda (nombre, giro, impuestos, métodos de pago).
- Documentación inicial (glosario, stack, módulos, ADR).

### v0.0.12 - 2026-10-23: Punto de venta en la app MAUI

- *Agregado*: pestaña **Vender** en la app MAUI con búsqueda por código o nombre, alta directa al escanear, lista de coincidencias y carrito con cantidades editables.
- *Agregado*: cobro en efectivo con cambio y validación de faltantes, tarjeta con aviso de comisión, mixto con suma exacta del total y fiado con selección de cliente y límite de crédito.
- *Agregado*: al confirmar, la venta se registra con `VentaService` y el stock se descuenta en la misma transacción; el carrito se limpia y se muestra el ticket.
- *Agregado*: página de ticket interno (no fiscal) con el detalle de líneas, total y forma de pago, más botones para compartir con el sistema o copiar al portapapeles.
- *Agregado*: modelos `ProductoFila`, `CarritoFila` y `ClienteOpcion`, y registro de `PosPage` en la inyección de dependencias.
- *Corregido*: los movimientos de inventario registran la fecha local en lugar de la fecha UTC.
- *Limitación*: la app todavía no anula ventas, no cierra cortes ni edita inventario; el ticket no incluye QR. La interfaz no tiene pruebas automatizadas.
- Verificación manual en Windows (2026-10-25): cinco ventas ficticias con búsqueda por código y nombre, límite de stock, producto agotado, efectivo con cambio, tarjeta, mixto, fiado, ticket con copiar/cerrar y totales coherentes en Ventas y Caja. El botón Compartir y la ejecución en Android quedan pendientes.
- Total del proyecto: 171 pruebas (sin cambios); CI en verde para el PR #4.

### v0.0.11 - 2026-10-23: App MAUI con login y tablero

- *Agregado*: proyecto `SistemaTienda.Maui` para Windows y Android (`net10.0-windows10.0.19041.0` y `net10.0-android`) con inyección de dependencias, ruta de base en `FileSystem.AppDataDirectory` y arranque que elige entre asistente y login.
- *Agregado*: asistente de primera configuración con los mismos datos y validaciones que la consola; al terminar abre el login.
- *Agregado*: login por PIN con 3 intentos, bloqueo hasta reabrir la app y reaplicación de la sesión en cada evento de interfaz mediante `SesionApp`.
- *Agregado*: tablero con pestañas Inicio (ventas del día, efectivo esperado, stock bajo y último respaldo), Ventas (lista del día con anuladas marcadas), Caja (corte pendiente y último corte) y Ajustes (datos, rutas, respaldo manual y cierre de sesión).
- *Agregado*: respaldo automático al iniciar y cerrar sesión desde la app, con aviso cuando falla; Ajustes permite crear un respaldo y muestra la ruta.
- *Agregado*: página de error de arranque con el mensaje y las rutas de base y respaldos, y botón para cerrar la aplicación.
- *Modificado*: `SQLitePCLRaw.bundle_e_sqlite3` 2.1.14 en Infrastructure para corregir la vulnerabilidad alta GHSA-2m69-gcr7-jv3q que traía 2.1.6.
- *Modificado*: el manifiesto de Android ya no pide permisos de red y desactiva el respaldo del sistema operativo; en Windows se corrigió el nombre visible del publicador para que la carpeta de datos sea `SistemaTienda`.
- *Agregado*: job `app-maui` en GitHub Actions que compila la app para Windows y Android en cada push.
- *Limitación*: las pestañas Ventas y Caja son informativas; todavía no hay punto de venta, cierre de corte, inventario ni ticket en la app. Android solo se ha compilado en CI, no probado en dispositivo.
- Total del proyecto: 170 pruebas (sin cambios; la interfaz se verificó manualmente).

### v0.0.10 - 2026-10-23: Anulación de ventas

- *Agregado*: esquema v6 con `AnulacionesVenta` (una por venta, `UNIQUE`), índice por fecha y disparadores que impiden `UPDATE` y `DELETE`; la venta original y sus líneas no se modifican.
- *Agregado*: `DatabaseTools.AnularVenta` anula la venta completa en una transacción: registra la anulación, devuelve el stock de cada línea con el costo congelado y revierte el saldo fiado del cliente.
- *Agregado*: permiso `AnularVenta` para dueño y encargado; `VentaService.AnularVenta` exige motivo y usuario, y deja la acción `AnulacionVenta` en la bitácora.
- *Agregado*: se bloquea la anulación cuando la venta ya quedó incluida en un corte de caja cerrado.
- *Modificado*: las ventas anuladas se excluyen del corte pendiente y del reporte de utilidad, y se marcan `[ANULADA]` en ventas del día.
- *Agregado*: menú Ventas → [3] Anular venta con resumen de líneas y confirmación escribiendo `ANULAR`; recuerda que el reembolso en efectivo es manual.
- *Agregado*: 8 pruebas de anulación (stock, fiado, corte cerrado, corte abierto, doble anulación, permisos, motivo y utilidad).
- *Limitación*: la anulación no registra un movimiento de caja. La venta se excluye del corte; si el reembolso en efectivo se paga después de cerrar el corte, se registra manualmente como retiro en el corte siguiente.
- Total del proyecto: 170 pruebas.

### v0.0.9 - 2026-10-23: Respaldo automático por intervalo

- *Agregado*: `RespaldoService.EjecutarAutomaticoSiCorresponde` crea un respaldo verificado cuando pasó `RespaldoIntervaloDias` desde el último (0 = deshabilitado); cualquier respaldo previo cuenta para el intervalo.
- *Agregado*: la consola revisa el intervalo al iniciar sesión y al cerrar; avisa cuando crea el respaldo y advierte cuando está vencido y no se pudo crear.
- *Agregado*: `ResultadoRespaldoAutomatico` y `EstadoRespaldoAutomatico` en Core, y la acción `Respaldo` en la bitácora.
- *Agregado*: la creación manual y automática queda auditada con ruta, etiqueta y usuario de la sesión.
- *Agregado*: 5 pruebas de respaldo automático (vencido, vigente, intervalo cumplido, deshabilitado y auditoría del respaldo manual).
- *Modificado*: el respaldo automático usa la etiqueta `automatico` y el menú de Configuración sigue mostrando la fecha del último respaldo.
- Total del proyecto: 162 pruebas.

### v0.0.8 - 2026-10-23: Reporte de utilidad

- *Agregado*: `ReporteUtilidad` y `ReporteUtilidadService` calculan utilidad bruta (ventas − costo congelado − comisión de tarjeta) y neta (bruta − mermas − gastos) por periodo.
- *Agregado*: `DatabaseTools.ObtenerReporteUtilidad` agrega ventas, comisión, mermas desde `MovimientoInventario` (motivo `Merma:%`, costo congelado) y gastos de ambos orígenes en un solo reporte.
- *Agregado*: menú [7] → [5] Ventas y utilidad con filtros de hoy, mes actual o rango de fechas; permiso `Consultar` validado en el servicio.
- *Agregado*: 7 pruebas de utilidad, costo congelado, comisión, mermas, gastos, rango y validación de fechas.
- Total del proyecto: 157 pruebas.

### v0.0.7 - 2026-10-23: Bitácora inmutable y permisos en servicios

- *Agregado*: esquema v5 con tabla `Bitacora` (usuario, fecha, acción, entidad, antes/después, motivo), índices y disparadores que impiden `UPDATE` y `DELETE`.
- *Agregado*: `BitacoraService` para registrar y consultar la auditoría; se escribe en altas de producto, cambios de precio, ajustes de inventario, entradas, mermas, gastos, abonos, pagos a proveedores, ventas, movimientos y cortes de caja.
- *Agregado*: `AutorizacionService` con matriz por rol y `SesionActual` para identificar al usuario firmado; los servicios validan permisos antes de ejecutar operaciones sensibles.
- *Agregado*: 11 pruebas de bitácora y permisos.
- *Modificado*: la consola inicia `SesionActual` al autenticar; `BaseDatosPrueba` usa un dueño de sistema por defecto para las pruebas que no evalúan permisos.
- *Modificado*: los detalles de la bitácora formatean el dinero en centavos con `Dinero.Formatear`.
- *Agregado*: 25 pruebas de borde (gastos, saldos, inventario, usuarios y mapeo de tipos) integradas desde T9 y adaptadas a centavos.
- Total del proyecto: 150 pruebas.

### v0.0.6 - 2026-10-23: Dinero en centavos enteros

- *Agregado*: `Dinero` en Core con conversión pesos ↔ centavos (`AwayFromZero`), multiplicación, porcentaje y formato invariante a dos decimales.
- *Agregado*: esquema v4 que reconstruye todas las tablas de dinero con columnas `INTEGER` (centavos) y multiplica por 100 los importes existentes con `CAST(ROUND(col * 100) AS INTEGER)`, conservando Id, FKs e índices.
- *Agregado*: el migrador desactiva `PRAGMA foreign_keys` durante las reconstrucciones, valida `PRAGMA foreign_key_check` y vuelve a activar las claves foráneas al terminar.
- *Agregado*: `LeerDinero` en la consola para capturar pesos y guardar centavos; todos los importes se muestran con `Dinero.Formatear`, sin depender de la cultura.
- *Agregado*: pruebas de `Dinero` (redondeo, multiplicación, porcentaje y formato) y de la migración v3 → v4 con datos en todas las tablas.
- *Modificado*: entidades y servicios de producto, venta, corte, gasto, fiado y proveedores usan `long` para dinero; los porcentajes (`Impuesto`, `ImpuestoPredeterminado`, `ComisionTarjeta`) siguen en `decimal`.
- *Modificado*: la comisión de tarjeta se calcula con `Dinero.Porcentaje` y se guarda en centavos.
- *Corregido*: se elimina el redondeo implícito de `decimal` al guardar y comparar importes; el dinero ya no depende de la afinidad numérica de SQLite.
- Total del proyecto: 114 pruebas.

### v0.0.5 - 2026-10-23: Corte de caja

- *Agregado*: esquema v3 con `CortesCaja`, `MovimientosCaja` y `AbonosFiado` (migración con respaldo previo), índices por fecha y cliente.
- *Agregado*: `DatabaseTools.RegistrarCorteCaja` calcula ventas por método, abonos en efectivo, ingresos, retiros y gastos de caja dentro de una sola transacción, y guarda la cabecera solo si el cálculo y el conteo son consistentes.
- *Agregado*: efectivo esperado = fondo + ventas en efectivo (incluida la parte en efectivo de pagos mixtos) + abonos en efectivo + ingresos − retiros − gastos con origen Caja.
- *Agregado*: diferencia entre contado y esperado; si no es cero, el motivo es obligatorio y el corte no se guarda sin él.
- *Agregado*: periodo desde el `Hasta` del corte anterior (exclusivo); el fondo sugerido es el efectivo contado del corte previo.
- *Agregado*: historial de abonos de fiado (`AbonosFiado`) guardado en la misma transacción que descuenta el saldo del cliente.
- *Agregado*: `CorteCajaService` con resumen pendiente, cierre, ingresos/retiros e historial; menú [8] Corte de caja en la consola.
- *Agregado*: 13 pruebas de corte, abonos y migración v2 → v3; total del proyecto: 94.
- *Modificado*: el menú principal pasa a [1]–[9] con Corte de caja en [8] y Configuración en [9].

### v0.0.4 - 2026-10-23: Ventas y punto de venta

- *Agregado*: esquema v2 con `Ventas` y `VentaLineas` (migración con respaldo previo), índices por fecha, venta y producto.
- *Agregado*: registro atómico de ventas multi-línea en `DatabaseTools.RegistrarVenta`: cabecera, líneas, movimiento de salida por producto y cargo al cliente en una sola transacción.
- *Agregado*: precio y costo congelados por línea; cambios posteriores de catálogo no alteran el historial.
- *Agregado*: `VentaService` valida líneas, duplicados, cantidades, coincidencia de pagos y reglas de fiado (exclusivo, cliente obligatorio, límite de crédito).
- *Agregado*: métodos de pago efectivo (con cambio), tarjeta (con comisión configurada), mixto y fiado.
- *Agregado*: menú de ventas en consola con carrito, búsqueda por código o nombre, cobro y ticket de texto; listado de ventas del día.
- *Agregado*: 13 pruebas de ventas, fiado y migración v1 → v2; total del proyecto: 81.
- *Modificado*: la base nueva se crea en la versión máxima del esquema sin respaldo previo.

### v0.0.3 - 2026-10-23: Migraciones versionadas y respaldo local

- *Agregado*: runner de migraciones basado en `PRAGMA user_version` (`Infrastructure/Migraciones.cs`), con respaldo previo verificado, transacción por migración, rollback y verificación de integridad final.
- *Agregado*: `RespaldoLocal` (crear, verificar, listar, restaurar) con `VACUUM INTO`, nombres `tienda-yyyyMMdd-HHmmss[-etiqueta].db` y carpeta `respaldos/` junto a la base.
- *Agregado*: restauración con respaldo de seguridad `antes-restaurar`, limpieza de `-wal`/`-shm` y confirmación explícita.
- *Agregado*: `RespaldoService` y menú en Configuración: crear respaldo, ver respaldos con fecha y tamaño, restaurar (solo dueño, escribiendo `RESTAURAR`).
- *Agregado*: 9 pruebas de migraciones y respaldo; total del proyecto: 68.
- *Agregado*: flujo de GitHub Actions que compila y ejecuta las pruebas en cada push.
- *Modificado*: la base marca `user_version = 1` al crearse y también al abrir bases anteriores al versionado.
- *Modificado*: cada prueba usa un directorio temporal único, incluida su carpeta de respaldos.
- *Corregido*: la inicialización ya no puede dejar una base sin versión ni aplicar cambios sin respaldo previo.

### v0.0.2 - 2026-10-23: Base compilable, segura y con pruebas

- *Agregado*: proyecto `SistemaTienda.Tests` (xUnit, 59 pruebas) sobre base SQLite temporal por prueba.
- *Agregado*: entidades `ClienteFiado` y `Proveedor` en Core; `ProcessResult` y `ProcessResult<T>`.
- *Agregado*: historial de inventario transaccional con cantidad firmada y caché de stock.
- *Agregado*: hash PBKDF2-SHA256 (100 000 iteraciones, salt de 16 bytes) para PIN y validación de roles.
- *Agregado*: variables de entorno `SISTEMATIENDA_DB` para elegir la base de datos.
- *Agregado*: `.gitignore` para binarios, bases locales y respaldos.
- *Modificado*: la solución .NET referencia correctamente los cuatro proyectos.
- *Modificado*: el esquema crea `ConfiguracionTienda`, `UsuariosSistema`, `Productos`, `MovimientoInventario`, `Gastos`, `ClientesFiado` y `Proveedores` (`PRAGMA user_version = 1`).
- *Modificado*: la interfaz de consola solo muestra funciones implementadas; ventas/POS se marca como pendiente.
- *Modificado*: clientes fiado ya no guardan RFC/cédula; se guarda teléfono.
- *Corregido*: el proyecto no compilaba por GUIDs de solución inválidos, referencias a proyectos inexistentes, falta de `Microsoft.Data.Sqlite`, namespaces ausentes, entidades duplicadas y una firma de `Main` incorrecta.
- *Corregido*: la detección de primera ejecución ahora usa el conteo de usuarios, no la versión de configuración.
- *Eliminado*: `Entities.cs` con entidades duplicadas.

### Próximas versiones (Fase 2 y siguientes)

- Reportes y utilidad completos.
- Asistente de primera configuración mejorado.
- Copia de respaldos a USB y nube personal.
- Distribución optimizada (.exe, .apk, PWA).