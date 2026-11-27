# Roadmap

Estado al 2026-11-25. T1–T42 y H1–H5 están integrados en `main`; H6.1 (importación con lotes), H6.2 (tickets en espera) y H6.3 (búsqueda medida) están implementados; la búsqueda y los tickets se verificaron en Android el 2026-11-25; también se verificaron la vista previa y la reversión de un lote; faltan la medición instrumentada y el escáner físico. La suite queda en 483 pruebas. Orden sugerido en [CLAUDE-tienda.md §13](../CLAUDE-tienda.md).

## Avance por módulo

| # | Módulo | Estado | Notas |
|---|--------|--------|-------|
| 1 | Configuración y usuarios | ✅ Base implementada | Asistente, PIN hasheado, roles, gastos, bitácora inmutable y permisos en servicios; edición de la configuración global y gestión de usuarios/PIN desde Ajustes (solo dueño). El impuesto configurado se usa como predeterminado del producto y se desglosa como IVA incluido |
| 2 | Catálogo e inventario | ✅ Base implementada | Alta/edición, movimientos, stock bajo, búsqueda, escaneo, historial, valorizado, desactivación (v7), importación CSV/XLSX con vista previa, lotes y reversión (v18); operación desde la app MAUI. Falta filtro por categoría |
| 3 | Ventas / POS | ✅ Base implementada | Venta multi-línea atómica, efectivo/tarjeta/mixto/fiado, costo y precio congelados, IVA incluido congelado por línea (v14), comisión, efectivo recibido guardado para reimprimir el cambio (v12), ticket de texto con QR informativo compartible como texto o imagen PNG y anulación completa con reverso; POS, detalle, reimpresión y anulación en MAUI; descuento por venta en % o monto con permiso de dueño/encargado, reparto por línea y bitácora (v20) |
| 4 | Mermas | 🟡 Parcial | Descuenta stock y reporta costo; el historial sale de movimientos |
| 5 | Gastos y pagos | 🟡 Parcial | Gastos simples con resumen por origen, historial y alta en la app; pagos al personal no existen |
| 6 | Clientes y fiado | 🟡 Parcial | Cliente, saldo y abono con historial (`AbonosFiado`) y método (Efectivo/Tarjeta/Transferencia/Otro); directorio, búsqueda, alta, abono, edición, estado de cuenta con exportación CSV y PDF, detección de duplicados y baja/reactivación lógica solo del dueño (esquema v10); falta envío o recordatorio |
| 7 | Proveedores y cuentas por pagar | 🟡 Parcial | Proveedor, saldo, compras con detalle (esquema v13) y pagos con método e historial inmutable; la compra aumenta stock, actualiza el costo al último costo y suma al saldo; el efectivo del pago genera retiro atómico. Directorio, búsqueda, alta, compra, pago, edición, estado de cuenta con exportación CSV y PDF, detección de duplicados y baja/reactivación lógica solo del dueño (esquema v10) |
| 8 | Corte de caja | 🟡 Base implementada | Periodo desde el corte anterior, efectivo esperado, diferencia con motivo obligatorio, ingresos/retiros e historial; retiros autorizados y auditados; cierre, conteo por denominaciones con desglose persistido (v11), historial de cortes e ingresos/retiros/abonos desde Caja en MAUI; verificado en Windows y Android 16 |
| 9 | Reportes y utilidad | 🟡 Base implementada | Utilidad por periodo (ventas − costo − comisión − mermas − gastos) más valorizado y stock bajo; pestaña Reportes en la app con filtros hoy/mes/rango, desglose de cobros por método, IVA incluido informativo, comparación con el bloque anterior y exportación CSV y PDF. Incluye ventas por día, productos más vendidos y ventas por categoría; falta comparación por producto entre periodos |
| 10 | Respaldo y restauración | 🟡 Base implementada | Manual y automático por intervalo (al iniciar/cerrar), antes de migrar y antes de restaurar, con verificación y bitácora; falta USB/nube y respaldo durante sesiones largas |
| — | Interfaz definitiva | 🟡 Base implementada | App MAUI para Windows y Android con asistente, login, tablero, punto de venta con QR, inventario, ventas con anulación, clientes con abonos y proveedores con pagos, ambos con edición, estado de cuenta exportable y baja lógica, gastos, caja, reportes y respaldo. Ver [interfaz-maui.md](02-modulos/interfaz-maui.md) |
| - | Migraciones versionadas | ✅ Base implementada | Runner con `PRAGMA user_version`, respaldo previo y rollback; v14 aplicada (ventas, corte de caja, centavos, bitácora, anulaciones, estados activos de productos/clientes/proveedores, pagos a proveedores, desglose de caja, compras a proveedores e IVA incluido por línea) |
| — | Dinero en centavos | ✅ Implementado | Importes `long`/`INTEGER` en todas las tablas; porcentajes en `decimal`; `Dinero` centraliza conversión y formato |
| — | Bitácora / auditoría | ✅ Base implementada | Tabla inmutable con disparadores y permisos validados en `Application` |

## H1 — Respaldo, restauración y confiabilidad local (2026-11-23)

- Respaldo exportable y restauración desde la app, con respaldo previo.
- Paquete de soporte con resumen y base opcional.
- SQLite en WAL, `busy_timeout=30000`, `synchronous=NORMAL` e instancia única en Windows.
- 5 pruebas nuevas; suite total 373.
- Pendiente: verificación manual en Windows/Android y compilación MAUI en CI.

## Siguiente trabajo recomendado

1. **Plantillas por giro para importar catálogos**, si se requieren; CSV y XLSX de productos ya están implementados en T6/T28.
2. **Copia de respaldos a USB o nube personal** (Fase 2).
4. **Verificar en otros fabricantes y versiones de Android**, y en tabletas; la verificación de T29–T35 en Android 16 ya está hecha.

## Riesgos abiertos

- La migración v4 reconstruye todas las tablas de dinero: en bases grandes el primer arranque puede tardar; siempre se crea un respaldo `antes-migrar` verificado antes de tocar datos.
- Las operaciones de dinero, inventario, catálogo, clientes, proveedores, usuarios y configuración escriben su bitácora en la misma transacción. Solo los respaldos, que son archivos fuera de SQLite, se auditan después.
- La anulación devuelve stock y revierte el fiado, pero no mueve efectivo: la venta se excluye del corte y, si el reembolso se paga después de cerrarlo, debe registrarse como retiro en el corte siguiente.
- El respaldo automático se evalúa al iniciar o cerrar: en una sesión abierta varios días la copia puede quedar vieja hasta el cierre.
- El paquete de Windows (H5) se probó con paquetes simulados, pero falta instalarlo en una máquina limpia y cortar la energía durante una migración real.
- La app MAUI (POS, Ventas, Clientes, Proveedores, Gastos, Inventario, Caja, Reportes, Ajustes, sesión y respaldos) no tiene pruebas automatizadas de interfaz; un error de carrito, cobro, anulación, abono, edición de cliente o proveedor, baja/reactivación, pago, gasto, conteo, movimiento de inventario, configuración, usuarios o periodo de reporte se detecta en la verificación manual. El texto y el QR del ticket, la búsqueda de clientes y proveedores, el resumen de gastos, los estados de cuenta de clientes y proveedores, el cálculo del conteo, los límites de `PeriodoReporte`, la edición de configuración, los usuarios/PIN y los PDF sí están cubiertos por pruebas unitarias. Las pantallas de Clientes (T18/T22/T27), Proveedores (T19/T23/T27), la desactivación de productos (T20), Gastos (T21), Reportes (T24), la importación XLSX (T28), la baja/reactivación (T27), el desglose de caja (T29), los abonos con método (T30), la exportación CSV (T31), el QR como imagen (T32), el efectivo recibido (T33), la edición de configuración (T34), la gestión de usuarios (T35), la exportación PDF (T36), el desglose por método (T37), la comparación entre periodos (T38) y las compras a proveedores (T39) se verificaron en vivo en Windows el 2026-11-19, el 2026-11-21 y el 2026-11-22, y en Android 16 (Redmi Note 13) el 2026-11-19, el 2026-11-21 y el 2026-11-22; el IVA incluido (T41) se verificó en Windows y Android el 2026-11-22; faltan otros fabricantes, versiones y tabletas.
- Los gastos registrados en la app no se editan ni se borran, no admiten fecha retroactiva y su reporte por categoría o periodo sigue en la consola.
- El pago a proveedores elige método (efectivo, tarjeta, transferencia u otro) y, si es en efectivo, genera un retiro de caja en la misma transacción que el historial y el saldo. Los pagos anteriores al esquema v9 no tienen método ni movimiento de caja y así se muestran. El historial solo cubre los pagos hechos desde el esquema v8.
- El QR del ticket es informativo y offline: no es un comprobante fiscal, se comparte como texto o imagen PNG, resume hasta diez productos y la reimpresión repite el efectivo recibido y el cambio desde el esquema v12. La imagen y el diálogo se verificaron en Android 16 el 2026-11-21.
- El PDF de reportes y estados de cuenta se genera con `PdfDocumento`, un escritor propio sin dependencias externas; usa Helvetica y Latin-1, así que no admite todos los caracteres Unicode ni imágenes. El archivo se comparte o guarda desde el diálogo del sistema y se imprime con cualquier lector.
- La entrada de inventario actualiza el costo del catálogo (con casilla para conservarlo); el valorizado y las ventas siguientes usan ese costo.
- La app MAUI usa .NET 10 y las bibliotecas `net8.0`; si una biblioteca agrega APIs no disponibles en net8.0 habrá que subir su framework o aislar la dependencia.
- La app MAUI solo se ha probado en Windows y en un Redmi Note 13 con Android 16; falta validar en otros fabricantes y versiones de Android y en tabletas.
- T27 agrega baja lógica solo para el dueño. Las cuentas inactivas conservan historial y admiten abonos/pagos para liquidar el saldo; el nuevo fiado a clientes inactivos se bloquea también en la capa de datos.
- La corrección de fechas locales aplica a las altas nuevas de clientes y proveedores. Los registros creados antes del 2026-11-19 conservan `FechaRegistro` en UTC y no se reescriben, porque no hay forma segura de distinguir la hora original.
