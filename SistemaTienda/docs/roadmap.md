# Roadmap

Estado al 2026-10-25. Orden sugerido en [CLAUDE-tienda.md §13](../CLAUDE-tienda.md).

## Avance por módulo

| # | Módulo | Estado | Notas |
|---|--------|--------|-------|
| 1 | Configuración y usuarios | ✅ Base implementada | Asistente, PIN hasheado, roles, gastos, bitácora inmutable y permisos en servicios |
| 2 | Catálogo e inventario | ✅ Base implementada | Alta/edición, movimientos, stock bajo, búsqueda, escaneo, historial y valorizado; operación desde la app MAUI. Falta filtro por categoría y desactivación |
| 3 | Ventas / POS | ✅ Base implementada | Venta multi-línea atómica, efectivo/tarjeta/mixto/fiado, costo y precio congelados, comisión, ticket de texto con QR informativo y anulación completa con reverso; POS, detalle, reimpresión y anulación en MAUI. Descuentos quedan para Fase 2 |
| 4 | Mermas | 🟡 Parcial | Descuenta stock y reporta costo; el historial sale de movimientos |
| 5 | Gastos y pagos | 🟡 Parcial | Gastos simples; pagos al personal no existen |
| 6 | Clientes y fiado | 🟡 Parcial | Cliente, saldo y abono con historial (`AbonosFiado`); directorio, búsqueda, alta y abono en la app; faltan edición, estado de cuenta impreso y método del abono |
| 7 | Proveedores y cuentas por pagar | 🟡 Parcial | Proveedor, saldo y pago; directorio, búsqueda, alta y pago en la app; sin historial de pagos, edición ni estado de cuenta |
| 8 | Corte de caja | 🟡 Base implementada | Periodo desde el corte anterior, efectivo esperado, diferencia con motivo obligatorio, ingresos/retiros e historial; retiros autorizados y auditados; cierre, conteo por denominaciones, historial de cortes e ingresos/retiros/abonos desde Caja en MAUI. Falta persistir el desglose y llevar el conteo a la consola |
| 9 | Reportes y utilidad | 🟡 Base implementada | Utilidad por periodo (ventas − costo − comisión − mermas − gastos) más valorizado y stock bajo; faltan ventas por día y comparativos |
| 10 | Respaldo y restauración | 🟡 Base implementada | Manual y automático por intervalo (al iniciar/cerrar), antes de migrar y antes de restaurar, con verificación y bitácora; falta USB/nube y respaldo durante sesiones largas |
| — | Interfaz definitiva | 🟡 Base implementada | App MAUI para Windows y Android con asistente, login, tablero, punto de venta con QR, inventario, ventas con anulación, clientes con abonos, proveedores con pagos, caja operativa con conteo e historial, y respaldo. Faltan gastos. Ver [interfaz-maui.md](02-modulos/interfaz-maui.md) |
| — | Migraciones versionadas | ✅ Base implementada | Runner con `PRAGMA user_version`, respaldo previo y rollback; v6 aplicada (ventas, corte de caja, centavos, bitácora y anulaciones) |
| — | Dinero en centavos | ✅ Implementado | Importes `long`/`INTEGER` en todas las tablas; porcentajes en `decimal`; `Dinero` centraliza conversión y formato |
| — | Bitácora / auditoría | ✅ Base implementada | Tabla inmutable con disparadores y permisos validados en `Application` |

## Siguiente trabajo recomendado

1. **Gastos** en la app; después, edición y estado de cuenta de clientes y proveedores.
2. **Importación Excel/CSV** y plantillas por giro.
3. **Copia de respaldos a USB o nube personal** (Fase 2).
4. **Descuentos** por línea o venta (Fase 2).
5. **Persistir el desglose por denominaciones** y llevarlo a la consola, si se requiere auditoría.

## Riesgos abiertos

- La migración v4 reconstruye todas las tablas de dinero: en bases grandes el primer arranque puede tardar; siempre se crea un respaldo `antes-migrar` verificado antes de tocar datos.
- La bitácora se escribe después de la operación: una falla al registrarla deja la acción aplicada sin auditoría.
- La anulación devuelve stock y revierte el fiado, pero no mueve efectivo: la venta se excluye del corte y, si el reembolso se paga después de cerrarlo, debe registrarse como retiro en el corte siguiente.
- El respaldo automático se evalúa al iniciar o cerrar: en una sesión abierta varios días la copia puede quedar vieja hasta el cierre.
- La app MAUI (POS, Ventas, Clientes, Proveedores, Inventario, Caja, sesión y respaldos) no tiene pruebas automatizadas de interfaz; un error de carrito, cobro, anulación, abono, pago, conteo o movimiento de inventario se detecta en la verificación manual. El texto y el QR del ticket, la búsqueda de clientes y proveedores y el cálculo del conteo sí están cubiertos por pruebas unitarias. Las pantallas de Clientes (T18) y Proveedores (T19) quedaron sin verificación manual porque el entorno no permitió abrir la GUI.
- El pago a proveedores reduce el saldo y deja bitácora, pero no entra al corte ni tiene historial propio; si se paga en efectivo hay que registrar además un retiro en Caja.
- El QR del ticket es informativo y offline: no es un comprobante fiscal, no se comparte como imagen, resume hasta diez productos y la reimpresión no incluye el efectivo recibido.
- El costo de una entrada de inventario no actualiza el costo del catálogo; el valorizado usa el costo que se edita por separado.
- La app MAUI usa .NET 10 y las bibliotecas `net8.0`; si una biblioteca agrega APIs no disponibles en net8.0 habrá que subir su framework o aislar la dependencia.
- La app MAUI solo se ha probado en Windows; Android se compila en CI pero no se ha ejecutado en un dispositivo real.
