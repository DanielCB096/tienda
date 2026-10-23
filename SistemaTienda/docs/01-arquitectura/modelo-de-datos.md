# Modelo de Datos (esquema v3)

Base local SQLite, un solo archivo. `PRAGMA user_version = 3`.

El acceso vive en `SistemaTienda.Infrastructure/Database.cs` (`DatabaseTools`). Todas las consultas son parametrizadas.

## Versionado del esquema

- La versión vigente se guarda en `PRAGMA user_version`. La **versión 1** es el esquema base que crea `DatabaseTools` en una base nueva o vacía.
- `SistemaTienda.Infrastructure/Migraciones.cs` define la lista ordenada de migraciones (`Migracion(Version, Descripcion, Aplicar)`). Versiones registradas:
  - **v2** — crea `Ventas` y `VentaLineas` con sus índices (módulo de ventas).
  - **v3** — crea `CortesCaja`, `MovimientosCaja` y `AbonosFiado` con sus índices (corte de caja e historial de abonos).
- Una base nueva se crea directamente en la versión máxima (`Migraciones.VersionMaxima`): no se crea respaldo previo porque aún no hay datos.
- `MigradorEsquema.Migrar()` se ejecuta dentro de `InicializarBaseDeDatos()` al arrancar:
  1. Lee `user_version`; si la base es más nueva que la app, aborta sin tocar nada.
  2. Si hay migraciones pendientes y la base ya existía, crea un respaldo `antes-migrar` y lo verifica con `PRAGMA integrity_check`; si falla, no aplica cambios.
  3. Aplica cada migración en su propia transacción y actualiza `user_version` dentro de la misma.
  4. Si una migración falla, hace rollback de esa migración y reporta el respaldo disponible.
  5. Al terminar, verifica la integridad de la base.
- Una base anterior al versionado (con tablas pero `user_version = 0`) se marca como v1 al abrirse; sus datos no se tocan.
- Para agregar una migración: crear `new Migracion(n, "descripción", (conn, tx) => { ... })` en `Migraciones.ObtenerTodas()`, con SQL idempotente (`IF NOT EXISTS`, etc.), y añadir su prueba.

## Respaldos

- Carpeta por defecto: `respaldos/` junto al archivo `.db` (ignorada por git).
- Nombre: `tienda-yyyyMMdd-HHmmss[-etiqueta].db`, con sufijo numérico si coincide en el mismo segundo.
- Se crean con `VACUUM INTO`, que produce una copia consistente sin bloquear la base, y se validan con `PRAGMA integrity_check` antes de darse por buenos.
- Etiquetas en uso: `antes-migrar`, `antes-restaurar`, `manual`.
- Restaurar verifica el respaldo, crea uno de seguridad `antes-restaurar`, cierra conexiones (`SqliteConnection.ClearAllPools()`), reemplaza el archivo y elimina `-wal`/`-shm`. Requiere confirmación explícita del dueño y reiniciar la app.

## Tablas

### ConfiguracionTienda

Fila única (Id = 1) creada al inicializar la base.

| Columna | Tipo | Notas |
|---------|------|-------|
| Id | INTEGER PK | Autoincremental |
| NombreTienda | TEXT | Editado por el asistente |
| Giro | TEXT | Texto libre |
| ImpuestoPredeterminado | DECIMAL | 16.00 por defecto |
| ComisionTarjeta | DECIMAL | 3.50 por defecto |
| FondoCajaInicial | DECIMAL | 200.00 por defecto |
| FiadoActivado | INTEGER | 0/1 |
| LimiCreditoCliente | DECIMAL | 0 = sin límite |
| RespaldoIntervaloDias | INTEGER | 0 = deshabilitado |
| UsuarioDueñoPINHash | TEXT | Sin uso; el hash vive en UsuariosSistema |
| FechaCreacion | DATETIME | `datetime('now')` |
| VersionApp | TEXT | Se conserva al actualizar |

### UsuariosSistema

| Columna | Tipo | Notas |
|---------|------|-------|
| Id | INTEGER PK | |
| Nombre | TEXT | |
| PINHash | TEXT | Formato `pbkdf2-sha256$100000$saltBase64$hashBase64` |
| Rol | INTEGER | 0 = Dueño, 1 = Encargado, 2 = Empleado |
| PermisosEspeciales | TEXT | Reservado |
| FechaCreacion | DATETIME | |

### Productos

| Columna | Tipo | Notas |
|---------|------|-------|
| Id | INTEGER PK | |
| CodigoBarras | TEXT UNIQUE | Obligatorio |
| Nombre | TEXT | Obligatorio |
| Costo | DECIMAL | ≥ 0 |
| Precio | DECIMAL | ≥ 0 |
| Stock | INTEGER | **Caché** de la suma de movimientos |
| StockMinimo | INTEGER | Alerta si `Stock <= StockMinimo` |
| Categoria | TEXT | Libre |
| Impuesto | DECIMAL | Override del global |
| FechaCreacion | DATETIME | |
| UltimaActualizacion | DATETIME | |

### MovimientoInventario

Historial inmutable de inventario.

| Columna | Tipo | Notas |
|---------|------|-------|
| Id | INTEGER PK | |
| ProductoId | INTEGER FK → Productos | |
| Tipo | TEXT | `Entrada`, `Salida`, `Ajuste` |
| Cantidad | INTEGER | **Firmada**: entrada > 0; salida/ajuste negativo según corresponda |
| CostoUnitario | DECIMAL | Costo al momento |
| Motivo | TEXT | `Venta`, `Merma: ...`, `Stock inicial`, etc. |
| Usuario | TEXT | Nombre del usuario |
| Fecha | DATETIME | |

### Ventas

Cabecera inmutable de cada venta (migración v2). No se edita ni se borra: un error se corrige con una venta nueva.

| Columna | Tipo | Notas |
|---------|------|-------|
| Id | INTEGER PK | |
| Fecha | DATETIME | Fecha y hora del cobro |
| Usuario | TEXT | Cajero que registró la venta |
| ClienteId | INTEGER FK → ClientesFiado | Nulo salvo en venta a fiado |
| MetodoPago | TEXT | `Efectivo`, `Tarjeta`, `Mixto`, `Fiado` |
| Total | DECIMAL | Suma de importes de las líneas |
| CostoTotal | DECIMAL | Suma de costo congelado × cantidad |
| PagoEfectivo | DECIMAL | Monto aplicado en efectivo |
| PagoTarjeta | DECIMAL | Monto aplicado con tarjeta |
| PagoFiado | DECIMAL | Monto cargado al cliente |
| ComisionTarjeta | DECIMAL | `PagoTarjeta × ComisionTarjeta%` al momento de la venta |

### VentaLineas

Detalle de productos vendidos con precio y costo congelados.

| Columna | Tipo | Notas |
|---------|------|-------|
| Id | INTEGER PK | |
| VentaId | INTEGER FK → Ventas | |
| ProductoId | INTEGER FK → Productos | |
| CodigoBarras | TEXT | Copia del código al vender |
| Nombre | TEXT | Copia del nombre al vender |
| Cantidad | INTEGER | > 0 |
| PrecioUnitario | DECIMAL | Precio de catálogo al vender |
| CostoUnitario | DECIMAL | Costo al vender |
| Importe | DECIMAL | `PrecioUnitario × Cantidad` |

### Gastos

| Columna | Tipo | Notas |
|---------|------|-------|
| Id | INTEGER PK | |
| Concepto | TEXT | Obligatorio |
| Monto | DECIMAL | > 0 |
| Fecha | DATETIME | |
| MetodoPago | TEXT | Libre |
| Origen | TEXT | `Caja` o `Externo` |
| Observaciones | TEXT | |

### ClientesFiado

| Columna | Tipo | Notas |
|---------|------|-------|
| Id | INTEGER PK | |
| Nombre | TEXT | Obligatorio |
| Telefono | TEXT | Único dato de contacto; sin RFC ni cédula por privacidad |
| SaldoActual | DECIMAL | ≥ 0 |
| FechaRegistro | DATETIME | |
| UltimaActualizacion | DATETIME | |

### Proveedores

| Columna | Tipo | Notas |
|---------|------|-------|
| Id | INTEGER PK | |
| Nombre | TEXT | Obligatorio |
| Clave | TEXT | RFC o razón social |
| Telefono | TEXT | |
| Direccion | TEXT | |
| SaldoPendiente | DECIMAL | ≥ 0 |
| FechaRegistro | DATETIME | |
| UltimaActualizacion | DATETIME | |

### AbonosFiado

Historial inmutable de pagos de clientes con fiado (migración v3). Se inserta en la misma transacción que descuenta `ClientesFiado.SaldoActual`.

| Columna | Tipo | Notas |
|---------|------|-------|
| Id | INTEGER PK | |
| ClienteId | INTEGER FK → ClientesFiado | |
| Fecha | DATETIME | Hora local del abono |
| Monto | DECIMAL | > 0 |
| MetodoPago | TEXT | `Efectivo` por defecto; solo efectivo entra al corte |
| Usuario | TEXT | Quien registró el abono |

### CortesCaja

Cabecera inmutable de cada corte (migración v3). Guarda los totales calculados y el resultado del conteo; no se edita ni se borra.

| Columna | Tipo | Notas |
|---------|------|-------|
| Id | INTEGER PK | |
| Desde | DATETIME | `Hasta` del corte anterior |
| Hasta | DATETIME | Momento del cierre; **exclusivo** para el periodo siguiente |
| FondoInicial | DECIMAL | Efectivo con el que abre el periodo |
| VentasEfectivo | DECIMAL | Suma de `PagoEfectivo` del periodo |
| VentasTarjeta | DECIMAL | Suma de `PagoTarjeta` |
| VentasFiado | DECIMAL | Suma de `PagoFiado` |
| TotalVentas | DECIMAL | Suma de `Total` |
| NumeroVentas | INTEGER | Ventas del periodo |
| NumeroVentasEfectivo | INTEGER | Con `MetodoPago = Efectivo` |
| NumeroVentasTarjeta | INTEGER | Con `MetodoPago = Tarjeta` |
| NumeroVentasMixto | INTEGER | Con `MetodoPago = Mixto` |
| NumeroVentasFiado | INTEGER | Con `MetodoPago = Fiado` |
| AbonosEfectivo | DECIMAL | Abonos cobrados en efectivo |
| IngresosCaja | DECIMAL | Movimientos de tipo `Ingreso` |
| RetirosCaja | DECIMAL | Movimientos de tipo `Retiro` |
| GastosCaja | DECIMAL | Gastos con `Origen = Caja` |
| EfectivoEsperado | DECIMAL | Fórmula del corte |
| EfectivoContado | DECIMAL | Lo contado físicamente |
| Diferencia | DECIMAL | `EfectivoContado − EfectivoEsperado` |
| Usuario | TEXT | Quien cerró el corte |
| Observaciones | TEXT | Motivo obligatorio si hay diferencia; notas opcionales |
| FechaRegistro | DATETIME | Hora local en que se guardó |

### MovimientosCaja

Entradas y salidas manuales de efectivo que no son ventas, abonos ni gastos.

| Columna | Tipo | Notas |
|---------|------|-------|
| Id | INTEGER PK | |
| Fecha | DATETIME | Hora local |
| Tipo | TEXT | `Ingreso` o `Retiro` |
| Monto | DECIMAL | > 0 |
| Motivo | TEXT | Obligatorio |
| Usuario | TEXT | Quien lo registró |

## Invariante: stock = suma de movimientos

`Productos.Stock` es una caché. Se actualiza **solo** dentro de la misma transacción que inserta el movimiento (`InsertarMovimiento`), y las consultas de verificación usan:

```sql
SELECT COALESCE(SUM(Cantidad), 0) FROM MovimientoInventario WHERE ProductoId = @id
```

Reglas implementadas:

- Alta de producto con stock inicial > 0 → movimiento `Entrada` motivo `Stock inicial`.
- Venta o merma → movimiento `Salida` con cantidad negativa, validando stock dentro de la transacción.
- Ajuste a valor absoluto → se registra únicamente la diferencia; si es cero, no hay movimiento.

## Invariante: venta atómica

`DatabaseTools.RegistrarVenta` escribe en una sola transacción: cabecera, líneas, un movimiento de salida por línea (motivo `Venta #N`) y, si aplica, el cargo al cliente en `ClientesFiado`. Si el stock de cualquier línea es insuficiente, se revierte todo y no queda registro de la venta.

El precio y el costo de cada línea se copian al vender; cambios posteriores en el catálogo no alteran ventas históricas.

## Invariante: corte de caja atómico e inmutable

`DatabaseTools.RegistrarCorteCaja` calcula los totales y guarda la cabecera en **una sola transacción**: si la inserción falla, no queda corte ni totales parciales. El periodo va de `Desde` (exclusivo) a `Hasta` (inclusivo); el siguiente corte empieza en el `Hasta` anterior.

`EfectivoEsperado = FondoInicial + VentasEfectivo + AbonosEfectivo + IngresosCaja − RetirosCaja − GastosCaja`.

Si `Diferencia ≠ 0` y `Observaciones` está vacía, la operación se rechaza y se revierte. Un corte guardado nunca se edita ni se borra. Los abonos de fiado también son atómicos: el descuento de `ClientesFiado.SaldoActual` y la fila de `AbonosFiado` se escriben juntos o no se escriben.

## Índices

`Productos(CodigoBarras)`, `Productos(Nombre)`, `Productos(Stock)`, `MovimientoInventario(ProductoId)`, `MovimientoInventario(Fecha)`, `Gastos(Fecha)`, `ClientesFiado(Nombre)`, `Proveedores(Nombre)`, `Ventas(Fecha)`, `VentaLineas(VentaId)`, `VentaLineas(ProductoId)`, `CortesCaja(Hasta)`, `MovimientosCaja(Fecha)`, `AbonosFiado(Fecha)`, `AbonosFiado(ClienteId)`.

## Notas de tipado

SQLite no impone tipos. El mapeo de filas convierte según el tipo de la propiedad:

- `bool` desde INTEGER o texto (`1`/`true`).
- `decimal` desde TEXT o REAL.
- `DateTime` desde texto ISO o formato `datetime('now')` (`yyyy-MM-dd HH:mm:ss`).
- Enums desde INTEGER.
