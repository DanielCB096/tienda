# Roadmap

Estado al 2026-10-23. Orden sugerido en [CLAUDE-tienda.md §13](../CLAUDE-tienda.md).

## Avance por módulo

| # | Módulo | Estado | Notas |
|---|--------|--------|-------|
| 1 | Configuración y usuarios | ✅ Base implementada | Asistente, PIN hasheado, roles, gastos |
| 2 | Catálogo e inventario | ✅ Base implementada | CRUD, movimientos, stock bajo, búsqueda |
| 3 | Ventas / POS | ⛔ No implementado | Solo descuento de stock reutilizable |
| 4 | Mermas | 🟡 Parcial | Descuenta stock y reporta costo; el historial sale de movimientos |
| 5 | Gastos y pagos | 🟡 Parcial | Gastos simples; pagos al personal no existen |
| 6 | Clientes y fiado | 🟡 Parcial | Cliente, saldo y abono; sin historial de abonos |
| 7 | Proveedores y cuentas por pagar | 🟡 Parcial | Proveedor, saldo y pago; sin historial de pagos |
| 8 | Corte de caja | ⛔ No implementado | — |
| 9 | Reportes y utilidad | ⛔ No implementado | Solo valorizado y stock bajo |
| 10 | Respaldo y restauración | 🟡 Parcial | Manual, antes de migrar y antes de restaurar, con verificación; falta agendar el automático y USB/nube |
| — | Interfaz definitiva | ⛔ Pendiente | Consola provisional; ADR de UI abierto |
| — | Migraciones versionadas | ✅ Base implementada | Runner con `PRAGMA user_version`, respaldo previo y rollback; sin migraciones pendientes aún |
| — | Bitácora / auditoría | ⛔ No implementado | Requerida por regla de negocio #9 |

## Siguiente trabajo recomendado

1. **Ventas / POS** con líneas, pagos, costo congelado y descuento atómico de stock (introduce el esquema v2 con el runner ya disponible).
2. **Corte de caja** sobre ventas y abonos.
3. **Reportería de utilidad** (ventas − costo − mermas − gastos − comisión).
4. **Bitácora** de operaciones sensibles y validación de permisos en servicios, no solo en menús.
5. **Respaldo automático** por intervalo configurado.
6. **Interfaz definitiva** según ADR de UI (MAUI/WPF/PWA).
7. **Importación Excel/CSV** y plantillas por giro.

## Riesgos abiertos

- Dinero modelado con `decimal`, no en centavos enteros. Ver [deuda-tecnica.md](deuda-tecnica.md).
- El respaldo automático por intervalo aún no se agenda: si el dueño no respalda manualmente, la copia más reciente puede ser vieja.
- La consola no es la interfaz objetivo; puede cambiar por completo.
