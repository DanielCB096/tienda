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
| 9 | Reportes y utilidad | ⛔ No implementado | Solo valorizado y stock bajo |
| 10 | Respaldo y restauración | 🟡 Parcial | Manual, antes de migrar y antes de restaurar, con verificación; falta agendar el automático y USB/nube |
| — | Interfaz definitiva | ⛔ Pendiente | Consola provisional; ADR de UI abierto |
| — | Migraciones versionadas | ✅ Base implementada | Runner con `PRAGMA user_version`, respaldo previo y rollback; v4 aplicada (ventas, corte de caja y bitácora) |
| — | Bitácora / auditoría | ✅ Base implementada | Tabla inmutable con disparadores y permisos validados en `Application` |

## Siguiente trabajo recomendado

1. **Reportería de utilidad** (ventas − costo congelado − mermas − gastos − comisión de tarjeta).
2. **Dinero en centavos enteros** (deuda T3, antes de acumular ventas reales).
3. **Anulación de ventas** con movimiento de reverso, permiso y registro en bitácora.
4. **Respaldo automático** por intervalo configurado.
5. **Interfaz definitiva** según ADR de UI (MAUI/WPF/PWA), incluido el ticket con QR y compartir.
6. **Importación Excel/CSV** y plantillas por giro.

## Riesgos abiertos

- Dinero modelado con `decimal`, no en centavos enteros. Ver [deuda-tecnica.md](deuda-tecnica.md).
- La bitácora se escribe después de la operación: una falla al registrarla deja la acción aplicada sin auditoría.
- El respaldo automático por intervalo aún no se agenda: si el dueño no respalda manualmente, la copia más reciente puede ser vieja.
- La consola no es la interfaz objetivo; puede cambiar por completo.
