# Roadmap

Estado al 2026-10-23. Orden sugerido en [CLAUDE-tienda.md §13](../CLAUDE-tienda.md).

## Avance por módulo

| # | Módulo | Estado | Notas |
|---|--------|--------|-------|
| 1 | Configuración y usuarios | ✅ Base implementada | Asistente, PIN hasheado, roles, gastos |
| 2 | Catálogo e inventario | ✅ Base implementada | CRUD, movimientos, stock bajo, búsqueda |
| 3 | Ventas / POS | 🟡 Base implementada | Venta multi-línea atómica, efectivo/tarjeta/mixto/fiado, costo y precio congelados, comisión, ticket de texto; falta QR, compartir y anulación |
| 4 | Mermas | 🟡 Parcial | Descuenta stock y reporta costo; el historial sale de movimientos |
| 5 | Gastos y pagos | 🟡 Parcial | Gastos simples; pagos al personal no existen |
| 6 | Clientes y fiado | 🟡 Parcial | Cliente, saldo y abono con historial (`AbonosFiado`); falta estado de cuenta impreso |
| 7 | Proveedores y cuentas por pagar | 🟡 Parcial | Proveedor, saldo y pago; sin historial de pagos |
| 8 | Corte de caja | 🟡 Base implementada | Periodo desde el corte anterior, efectivo esperado, diferencia con motivo obligatorio, ingresos/retiros e historial; faltan permisos y conteo por denominaciones |
| 9 | Reportes y utilidad | ⛔ No implementado | Solo valorizado y stock bajo |
| 10 | Respaldo y restauración | 🟡 Parcial | Manual, antes de migrar y antes de restaurar, con verificación; falta agendar el automático y USB/nube |
| — | Interfaz definitiva | ⛔ Pendiente | Consola provisional; ADR de UI abierto |
| — | Migraciones versionadas | ✅ Base implementada | Runner con `PRAGMA user_version`, respaldo previo y rollback; v3 aplicada (ventas y corte de caja) |
| — | Bitácora / auditoría | ⛔ No implementado | Requerida por regla de negocio #9 |

## Siguiente trabajo recomendado

1. **Reportería de utilidad** (ventas − costo congelado − mermas − gastos − comisión de tarjeta).
2. **Dinero en centavos enteros** (deuda T3, antes de acumular ventas reales).
3. **Bitácora** de operaciones sensibles y validación de permisos en servicios, no solo en menús (incluye autorizar retiros de caja).
4. **Anulación de ventas** con movimiento de reverso, permiso y registro en bitácora.
5. **Respaldo automático** por intervalo configurado.
6. **Interfaz definitiva** según ADR de UI (MAUI/WPF/PWA), incluido el ticket con QR y compartir.
7. **Importación Excel/CSV** y plantillas por giro.

## Riesgos abiertos

- Dinero modelado con `decimal`, no en centavos enteros. Ver [deuda-tecnica.md](deuda-tecnica.md).
- El respaldo automático por intervalo aún no se agenda: si el dueño no respalda manualmente, la copia más reciente puede ser vieja.
- La consola no es la interfaz objetivo; puede cambiar por completo.
