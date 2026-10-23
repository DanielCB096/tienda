# Roadmap

Estado al 2026-10-23. Orden sugerido en [CLAUDE-tienda.md §13](../CLAUDE-tienda.md).

## Avance por módulo

| # | Módulo | Estado | Notas |
|---|--------|--------|-------|
| 1 | Configuración y usuarios | ✅ Base implementada | Asistente, PIN hasheado, roles, gastos, bitácora inmutable y permisos en servicios |
| 2 | Catálogo e inventario | ✅ Base implementada | CRUD, movimientos, stock bajo, búsqueda |
| 3 | Ventas / POS | 🟡 Base implementada | Venta multi-línea atómica, efectivo/tarjeta/mixto/fiado, costo y precio congelados, comisión, ticket de texto; falta QR, compartir y anulación |
| 4 | Mermas | 🟡 Parcial | Descuenta stock y reporta costo; el historial sale de movimientos |
| 5 | Gastos y pagos | 🟡 Parcial | Gastos simples; pagos al personal no existen |
| 6 | Clientes y fiado | 🟡 Parcial | Cliente, saldo y abono con historial (`AbonosFiado`); falta estado de cuenta impreso |
| 7 | Proveedores y cuentas por pagar | 🟡 Parcial | Proveedor, saldo y pago; sin historial de pagos |
| 8 | Corte de caja | 🟡 Base implementada | Periodo desde el corte anterior, efectivo esperado, diferencia con motivo obligatorio, ingresos/retiros e historial; retiros autorizados y auditados; falta conteo por denominaciones |
| 9 | Reportes y utilidad | 🟡 Base implementada | Utilidad por periodo (ventas − costo − comisión − mermas − gastos) más valorizado y stock bajo; faltan ventas por día y comparativos |
| 10 | Respaldo y restauración | 🟡 Base implementada | Manual y automático por intervalo (al iniciar/cerrar), antes de migrar y antes de restaurar, con verificación y bitácora; falta USB/nube y respaldo durante sesiones largas |
| — | Interfaz definitiva | ⛔ Pendiente | Consola provisional; ADR de UI abierto |
| — | Migraciones versionadas | ✅ Base implementada | Runner con `PRAGMA user_version`, respaldo previo y rollback; v5 aplicada (ventas, corte de caja, centavos y bitácora) |
| — | Dinero en centavos | ✅ Implementado | Importes `long`/`INTEGER` en todas las tablas; porcentajes en `decimal`; `Dinero` centraliza conversión y formato |
| — | Bitácora / auditoría | ✅ Base implementada | Tabla inmutable con disparadores y permisos validados en `Application` |

## Siguiente trabajo recomendado

1. **Anulación de ventas** con movimiento de reverso, permiso y registro en bitácora.
2. **Interfaz definitiva** según ADR de UI (MAUI/WPF/PWA), incluido el ticket con QR y compartir.
3. **Importación Excel/CSV** y plantillas por giro.
4. **Copia de respaldos a USB o nube personal** (Fase 2).

## Riesgos abiertos

- La migración v4 reconstruye todas las tablas de dinero: en bases grandes el primer arranque puede tardar; siempre se crea un respaldo `antes-migrar` verificado antes de tocar datos.
- La bitácora se escribe después de la operación: una falla al registrarla deja la acción aplicada sin auditoría.
- El respaldo automático se evalúa al iniciar o cerrar: en una sesión abierta varios días la copia puede quedar vieja hasta el cierre.
- La consola no es la interfaz objetivo; puede cambiar por completo.
