# Roadmap

Estado al 2026-10-23. Orden sugerido en [CLAUDE-tienda.md §13](../CLAUDE-tienda.md).

## Avance por módulo

| # | Módulo | Estado | Notas |
|---|--------|--------|-------|
| 1 | Configuración y usuarios | ✅ Base implementada | Asistente, PIN hasheado, roles, gastos, bitácora inmutable y permisos en servicios |
| 2 | Catálogo e inventario | ✅ Base implementada | CRUD, movimientos, stock bajo, búsqueda |
| 3 | Ventas / POS | 🟡 Base implementada | Venta multi-línea atómica, efectivo/tarjeta/mixto/fiado, costo y precio congelados, comisión, ticket de texto y anulación completa con reverso; POS en MAUI con ticket compartible. Falta QR y anulación desde la app |
| 4 | Mermas | 🟡 Parcial | Descuenta stock y reporta costo; el historial sale de movimientos |
| 5 | Gastos y pagos | 🟡 Parcial | Gastos simples; pagos al personal no existen |
| 6 | Clientes y fiado | 🟡 Parcial | Cliente, saldo y abono con historial (`AbonosFiado`); falta estado de cuenta impreso |
| 7 | Proveedores y cuentas por pagar | 🟡 Parcial | Proveedor, saldo y pago; sin historial de pagos |
| 8 | Corte de caja | 🟡 Base implementada | Periodo desde el corte anterior, efectivo esperado, diferencia con motivo obligatorio, ingresos/retiros e historial; retiros autorizados y auditados; falta conteo por denominaciones |
| 9 | Reportes y utilidad | 🟡 Base implementada | Utilidad por periodo (ventas − costo − comisión − mermas − gastos) más valorizado y stock bajo; faltan ventas por día y comparativos |
| 10 | Respaldo y restauración | 🟡 Base implementada | Manual y automático por intervalo (al iniciar/cerrar), antes de migrar y antes de restaurar, con verificación y bitácora; falta USB/nube y respaldo durante sesiones largas |
| — | Interfaz definitiva | 🟡 Base implementada | App MAUI para Windows y Android con asistente, login, tablero, punto de venta y respaldo; Caja sigue de solo lectura. Ver [interfaz-maui.md](02-modulos/interfaz-maui.md) |
| — | Migraciones versionadas | ✅ Base implementada | Runner con `PRAGMA user_version`, respaldo previo y rollback; v6 aplicada (ventas, corte de caja, centavos, bitácora y anulaciones) |
| — | Dinero en centavos | ✅ Implementado | Importes `long`/`INTEGER` en todas las tablas; porcentajes en `decimal`; `Dinero` centraliza conversión y formato |
| — | Bitácora / auditoría | ✅ Base implementada | Tabla inmutable con disparadores y permisos validados en `Application` |

## Siguiente trabajo recomendado

1. **Cierre de corte y operaciones de caja** desde la app (ingresos, retiros, abonos).
2. **Inventario y catálogo** en la app, con lector de código de barras.
3. **Ticket con QR** y anulación de ventas desde la app.
4. **Importación Excel/CSV** y plantillas por giro.
5. **Copia de respaldos a USB o nube personal** (Fase 2).

## Riesgos abiertos

- La migración v4 reconstruye todas las tablas de dinero: en bases grandes el primer arranque puede tardar; siempre se crea un respaldo `antes-migrar` verificado antes de tocar datos.
- La bitácora se escribe después de la operación: una falla al registrarla deja la acción aplicada sin auditoría.
- La anulación devuelve stock y revierte el fiado, pero no mueve efectivo: la venta se excluye del corte y, si el reembolso se paga después de cerrarlo, debe registrarse como retiro en el corte siguiente.
- El respaldo automático se evalúa al iniciar o cerrar: en una sesión abierta varios días la copia puede quedar vieja hasta el cierre.
- El POS y el resto de la app MAUI no tienen pruebas automatizadas; un error de armado del carrito o de cobro se detecta en la verificación manual.
- La app MAUI usa .NET 10 y las bibliotecas `net8.0`; si una biblioteca agrega APIs no disponibles en net8.0 habrá que subir su framework o aislar la dependencia.
- La app MAUI solo se ha probado en Windows; Android se compila en CI pero no se ha ejecutado en un dispositivo real.
