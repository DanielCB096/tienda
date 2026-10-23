# Deuda Técnica

Atajos y pendientes aceptados conscientemente. Cada entrada indica impacto y qué la cerraría.

## Alta prioridad

### Dinero en `decimal` en lugar de centavos enteros

- **Dónde**: entidades `Producto`, `Venta`, `VentaLinea`, `Gasto`, `ClienteFiado`, `Proveedor`, `ConfiguracionTienda` y columnas `DECIMAL` de SQLite.
- **Por qué**: la regla del proyecto pide centavos enteros; la base actual usa `decimal` para no rehacer el modelo antes de tener ventas. Las ventas ya redondean totales, costo y comisión a 2 decimales al guardar.
- **Impacto**: riesgo de diferencias por redondeo y comparaciones de decimales frágiles, ahora sobre historial de ventas que no se corrige. SQLite no tiene tipo decimal nativo: los valores se guardan como texto/numérico con afinidad.
- **Cierre**: migración a `long Centavos` en entidades y `INTEGER` en base, con utilidades de formato/parseo, pruebas de aritmética y conversión de las ventas existentes. Hacerlo antes de operar con ventas reales.

### Sin bitácora ni validación de permisos en servicios

- **Dónde**: `ConfiguracionService`, `InventoryService` y `VentaService`.
- **Impacto**: los roles solo se validan en los menús de consola; un cambio de interfaz podría saltarse restricciones. No hay registro de quién cambió precios, ajustó stock, vendió a fiado o canceló.
- **Cierre**: servicio de bitácora (usuario, fecha, acción, antes/después) y autorización en la capa de aplicación.

### Abonos y pagos no quedan como historial inmutable

- **Dónde**: `ActualizarSaldoCliente`, `ActualizarSaldoProveedor`.
- **Impacto**: se actualiza el saldo, pero no se guarda el movimiento de abono/pago. No se puede auditar ni reconstruir el saldo. Contradice "el historial no se edita".
- **Cierre**: tablas `AbonosFiado` y `PagosProveedor` inmutables; el saldo se deriva o se actualiza dentro de la misma transacción.

### Ventas sin anulación ni descuentos

- **Dónde**: `VentaService` y `DatabaseTools.RegistrarVenta`.
- **Impacto**: la venta es inmutable pero no existe una operación de anulación: un cobro equivocado solo se compensa manualmente por ahora. Tampoco hay descuentos; el total es siempre precio de catálogo × cantidad.
- **Cierre**: anulación con movimiento de reverso (devolver stock, ajustar fiado), permiso de encargado/dueño y registro en bitácora; descuentos como fase 2 con autorización.

### Ventas sin ticket QR ni compartir

- **Dónde**: `Program.ImprimirTicket`.
- **Impacto**: el ticket actual es texto en consola. No se genera PDF/imagen, no lleva QR y no se puede compartir por WhatsApp o correo.
- **Cierre**: generación de ticket con QR al construir la interfaz definitiva (requiere decisión de UI).

### Ventas sin integrar a corte de caja

- **Dónde**: `Ventas.PagoEfectivo` se guarda, pero no hay módulo de corte.
- **Impacto**: el efectivo esperado todavía se calcula a mano.
- **Cierre**: corte de caja que tome fondo inicial, ventas en efectivo, abonos y gastos de caja.

## Media prioridad

### Respaldo automático por intervalo todavía no se agenda

- **Dónde**: `ConfiguracionTienda.RespaldoIntervaloDias` y `RespaldoService`.
- **Impacto**: ya existen respaldo manual, respaldo previo a migrar y respaldo previo a restaurar, todos verificados; pero si el dueño no crea respaldos manuales, no hay copias recientes. La consola muestra la fecha del último respaldo.
- **Cierre**: agendar el respaldo según el intervalo configurado (al iniciar o cerrar la app), avisar cuando esté vencido y registrarlo en la bitácora.

### Interfaz de consola provisional

- **Dónde**: `SistemaTienda.Presentation`.
- **Impacto**: no usable por el cajero objetivo. La lógica está separada, lo que permite reemplazarla sin tocar servicios.
- **Cierre**: ADR de UI y pantallas reales (MAUI/WPF/PWA).

### Mermas sin entidad propia

- **Dónde**: `InventoryService.RegistrarMerma` registra una salida con motivo `Merma: ...`.
- **Impacto**: el costo perdido se calcula al momento y se muestra en el mensaje; no queda guardado como merma con su costo. Los reportes no pueden sumar pérdidas por merma sin recalcular.
- **Cierre**: tabla `Mermas` o columnas de costo en el movimiento que permitan reportes directos.

### Reportes incompletos

- **Dónde**: `MenuReportes`.
- **Impacto**: solo valorizado, stock bajo, gastos y saldos. Las ventas ya guardan `CostoTotal` y `ComisionTarjeta`, pero no hay reporte de utilidad, ventas por día ni comparativos.
- **Cierre**: consultas agregadas con índices por fecha (ya existen en `Ventas(Fecha)`) y paginación.

## Baja prioridad

- `Merma` comparte historial con ajustes y ventas; falta filtrar por tipo en la interfaz.
- `ConfiguracionTienda.UsuarioDueñoPINHash` existe en la tabla pero no se usa (el hash vive en `UsuariosSistema`). Eliminar en la próxima migración.
- `Rol` se documentó como texto en algunos documentos; en la base es `INTEGER` con enum. Unificar documentación.
- Sin `Activo` en productos: no se puede retirar un producto del catálogo sin borrarlo.
- Sin pruebas de rendimiento con volúmenes realistas (10 000 productos / 500 ventas por día).
- Comentarios XML heredados de la versión anterior en `InventoryService`; no afectan.
