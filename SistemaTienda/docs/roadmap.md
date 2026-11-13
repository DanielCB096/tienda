# Roadmap

Estado al 2026-11-12. Todo el trabajo de T1 a T25 está integrado en `main` (`86cbf67`); T26 y T27 quedaron publicadas en sus ramas (`e87ea40` y `a8f6876`). El siguiente trabajo recomendado es la importación Excel/CSV. Orden sugerido en [CLAUDE-tienda.md §13](../CLAUDE-tienda.md).

## Avance por módulo

| # | Módulo | Estado | Notas |
|---|--------|--------|-------|
| 1 | Configuración y usuarios | ✅ Base implementada | Asistente, PIN hasheado, roles, gastos, bitácora inmutable y permisos en servicios |
| 2 | Catálogo e inventario | ✅ Base implementada | Alta/edición, movimientos, stock bajo, búsqueda, escaneo, historial, valorizado y desactivación de productos (v7); operación desde la app MAUI. Falta filtro por categoría |
| 3 | Ventas / POS | ✅ Base implementada | Venta multi-línea atómica, efectivo/tarjeta/mixto/fiado, costo y precio congelados, comisión, ticket de texto con QR informativo y anulación completa con reverso; POS, detalle, reimpresión y anulación en MAUI. Descuentos quedan para Fase 2 |
| 4 | Mermas | 🟡 Parcial | Descuenta stock y reporta costo; el historial sale de movimientos |
| 5 | Gastos y pagos | 🟡 Parcial | Gastos simples con resumen por origen, historial y alta en la app; pagos al personal no existen |
| 6 | Clientes y fiado | 🟡 Parcial | Cliente, saldo y abono con historial (`AbonosFiado`); directorio, búsqueda, alta, abono, edición, estado de cuenta, detección de duplicados y baja/reactivación lógica solo del dueño (esquema v10); faltan método del abono y estado de cuenta impreso |
| 7 | Proveedores y cuentas por pagar | 🟡 Parcial | Proveedor, saldo, pago con método e historial inmutable; efectivo genera retiro atómico. Directorio, búsqueda, alta, pago, edición, estado de cuenta, detección de duplicados y baja/reactivación lógica solo del dueño (esquema v10); faltan compras por proveedor y estado de cuenta impreso |
| 8 | Corte de caja | 🟡 Base implementada | Periodo desde el corte anterior, efectivo esperado, diferencia con motivo obligatorio, ingresos/retiros e historial; retiros autorizados y auditados; cierre, conteo por denominaciones, historial de cortes e ingresos/retiros/abonos desde Caja en MAUI. Falta persistir el desglose y llevar el conteo a la consola |
| 9 | Reportes y utilidad | 🟡 Base implementada | Utilidad por periodo (ventas − costo − comisión − mermas − gastos) más valorizado y stock bajo; pestaña Reportes en la app con filtros hoy/mes/rango. Faltan ventas por día, comparativos y exportación |
| 10 | Respaldo y restauración | 🟡 Base implementada | Manual y automático por intervalo (al iniciar/cerrar), antes de migrar y antes de restaurar, con verificación y bitácora; falta USB/nube y respaldo durante sesiones largas |
| — | Interfaz definitiva | 🟡 Base implementada | App MAUI para Windows y Android con asistente, login, tablero, punto de venta con QR, inventario, ventas con anulación, clientes con abonos y proveedores con pagos, ambos con edición, estado de cuenta y baja lógica, gastos, caja, reportes y respaldo. Ver [interfaz-maui.md](02-modulos/interfaz-maui.md) |
| - | Migraciones versionadas | ✅ Base implementada | Runner con `PRAGMA user_version`, respaldo previo y rollback; v10 aplicada (ventas, corte de caja, centavos, bitácora, anulaciones, estados activos de productos/clientes/proveedores y pagos a proveedores) |
| — | Dinero en centavos | ✅ Implementado | Importes `long`/`INTEGER` en todas las tablas; porcentajes en `decimal`; `Dinero` centraliza conversión y formato |
| — | Bitácora / auditoría | ✅ Base implementada | Tabla inmutable con disparadores y permisos validados en `Application` |

## Siguiente trabajo recomendado

1. **Importación Excel/CSV** y plantillas por giro.
3. **Copia de respaldos a USB o nube personal** (Fase 2).
4. **Descuentos** por línea o venta (Fase 2).
5. **Persistir el desglose por denominaciones** y llevarlo a la consola, si se requiere auditoría.

## Riesgos abiertos

- La migración v4 reconstruye todas las tablas de dinero: en bases grandes el primer arranque puede tardar; siempre se crea un respaldo `antes-migrar` verificado antes de tocar datos.
- La bitácora se escribe después de la operación: una falla al registrarla deja la acción aplicada sin auditoría.
- La anulación devuelve stock y revierte el fiado, pero no mueve efectivo: la venta se excluye del corte y, si el reembolso se paga después de cerrarlo, debe registrarse como retiro en el corte siguiente.
- El respaldo automático se evalúa al iniciar o cerrar: en una sesión abierta varios días la copia puede quedar vieja hasta el cierre.
- La app MAUI (POS, Ventas, Clientes, Proveedores, Gastos, Inventario, Caja, Reportes, sesión y respaldos) no tiene pruebas automatizadas de interfaz; un error de carrito, cobro, anulación, abono, edición de cliente o proveedor, baja/reactivación, pago, gasto, conteo, movimiento de inventario o periodo de reporte se detecta en la verificación manual. El texto y el QR del ticket, la búsqueda de clientes y proveedores, el resumen de gastos, los estados de cuenta de clientes y proveedores, el cálculo del conteo y los límites de `PeriodoReporte` sí están cubiertos por pruebas unitarias. Las pantallas de Clientes (T18/T22/T27), Proveedores (T19/T23/T27), la desactivación de productos (T20), Gastos (T21), Reportes (T24) y la baja/reactivación (T27) quedaron sin verificación manual porque el entorno no permitió abrir la GUI.
- Los gastos registrados en la app no se editan ni se borran, no admiten fecha retroactiva y su reporte por categoría o periodo sigue en la consola.
- El pago a proveedores elige método (efectivo, tarjeta, transferencia u otro) y, si es en efectivo, genera un retiro de caja en la misma transacción que el historial y el saldo. Los pagos anteriores al esquema v9 no tienen método ni movimiento de caja y así se muestran. El historial solo cubre los pagos hechos desde el esquema v8.
- El QR del ticket es informativo y offline: no es un comprobante fiscal, no se comparte como imagen, resume hasta diez productos y la reimpresión no incluye el efectivo recibido.
- El costo de una entrada de inventario no actualiza el costo del catálogo; el valorizado usa el costo que se edita por separado.
- La app MAUI usa .NET 10 y las bibliotecas `net8.0`; si una biblioteca agrega APIs no disponibles en net8.0 habrá que subir su framework o aislar la dependencia.
- La app MAUI solo se ha probado en Windows; Android se compila en CI pero no se ha ejecutado en un dispositivo real.
- T27 agrega baja lógica solo para el dueño. Las cuentas inactivas conservan historial y admiten abonos/pagos para liquidar el saldo; el nuevo fiado a clientes inactivos se bloquea también en la capa de datos.
