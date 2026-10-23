# Deuda Técnica

Atajos y pendientes aceptados conscientemente. Cada entrada indica impacto y qué la cerraría.

## Alta prioridad

### Dinero en `decimal` en lugar de centavos enteros

- **Dónde**: entidades `Producto`, `Gasto`, `ClienteFiado`, `Proveedor`, `ConfiguracionTienda` y columnas `DECIMAL` de SQLite.
- **Por qué**: la regla del proyecto pide centavos enteros; la base actual usa `decimal` para no rehacer el modelo antes de tener ventas.
- **Impacto**: riesgo de diferencias por redondeo y comparaciones de decimales frágiles. SQLite no tiene tipo decimal nativo: los valores se guardan como texto/numérico con afinidad.
- **Cierre**: migración a `long Centavos` en entidades y `INTEGER` en base, con utilidades de formato/parseo y pruebas de aritmética.

### Sin bitácora ni validación de permisos en servicios

- **Dónde**: `ConfiguracionService` e `InventoryService`.
- **Impacto**: los roles solo se validan en los menús de consola; un cambio de interfaz podría saltarse restricciones. No hay registro de quién cambió precios, ajustó stock o canceló.
- **Cierre**: servicio de bitácora (usuario, fecha, acción, antes/después) y autorización en la capa de aplicación.

### Abonos y pagos no quedan como historial inmutable

- **Dónde**: `ActualizarSaldoCliente`, `ActualizarSaldoProveedor`.
- **Impacto**: se actualiza el saldo, pero no se guarda el movimiento de abono/pago. No se puede auditar ni reconstruir el saldo. Contradice "el historial no se edita".
- **Cierre**: tablas `AbonosFiado` y `PagosProveedor` inmutables; el saldo se deriva o se actualiza dentro de la misma transacción.

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

### Costo congelado sin ventas

- **Dónde**: los movimientos guardan `CostoUnitario`, pero no existe módulo de ventas.
- **Impacto**: no se puede garantizar todavía utilidad histórica.
- **Cierre**: módulo de ventas que copie costo y precio a cada línea.

### Reportes incompletos

- **Dónde**: `MenuReportes`.
- **Impacto**: solo valorizado, stock bajo, gastos y saldos. No hay utilidad, ventas por día ni comparativos.
- **Cierre**: consultas agregadas con índices por fecha y paginación.

## Baja prioridad

- `Merma` comparte historial con ajustes y ventas; falta filtrar por tipo en la interfaz.
- `ConfiguracionTienda.UsuarioDueñoPINHash` existe en la tabla pero no se usa (el hash vive en `UsuariosSistema`). Eliminar en la próxima migración.
- `Rol` se documentó como texto en algunos documentos; en la base es `INTEGER` con enum. Unificar documentación.
- Sin `Activo` en productos: no se puede retirar un producto del catálogo sin borrarlo.
- Sin pruebas de rendimiento con volúmenes realistas (10 000 productos / 500 ventas por día).
- Comentarios XML heredados de la versión anterior en `InventoryService`; no afectan.
