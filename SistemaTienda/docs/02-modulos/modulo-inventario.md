# Módulo 2: Catálogo e Inventario

## Propósito y Alcance

Este módulo maneja el catálogo de productos y el control de inventario. Es el corazón operativo de la tienda: aquí se registran los productos que se venden, se hace el seguimiento del stock y se activan las alertas de stock bajo. El diseño debe ser **giro-neutral**: nada en el código asume qué tipo de producto se vende (abarrotes, ropa, etc.), solo que tiene un código de barras, un costo y un precio.

## Actores y Permisos

| Rol | Permisos | Descripción |
|-----|----------|-------------|
| **Dueño/Administrador** | - Agregar y editar productos<br>- Ver valorizado de inventario<br>- Ajustar stock manualmente<br>- Cambiar costo y precio | Control total del catálogo; en esta versión no hay borrado |
| **Encargado/Cajero** | - Ver catálogo y stock<br>- Editar datos del producto<br>- Registrar nuevas entradas (compras) y mermas<br>- Ver alertas de stock bajo | No puede modificar precios ni ajustar stock |
| **Empleado básico** | - Buscar productos por nombre/código<br>- Consultar stock y movimientos | Acceso de solo lectura |

## Flujos Paso a paso

### 1. Agregar un nuevo producto

1. **Captura de datos**: Se solicitan los siguientes campos obligatorios:
   - **Código de barras**: Identificador único (obligatorio, debe ser único en la base de datos).
   - **Nombre**: Descripción del producto (obligatorio).
   - **Costo**: Precio de adquisición del proveedor (obligatorio, se captura en pesos y se guarda en centavos enteros).
   - **Precio**: Precio de venta al público (obligatorio, se captura en pesos y se guarda en centavos enteros).
   - **Stock inicial**: Cantidad con la que se inicia el inventario (opcional, por defecto 0).
   - **Categoría**: Etiqueta para agrupar productos (opcional, libre).
   - **Impuesto**: Tasa específica para este producto (opcional, override del impuesto predeterminado de la configuración).

2. **Validación**:
   - El código de barras no debe estar ya en uso (verificar en la base de datos).
   - El costo y precio deben ser números positivos.
   - Si el stock inicial se proporciona, debe ser un entero no negativo.

3. **Persistencia**: Se inserta el producto en la tabla `Productos` con stock 0 y, si hay stock inicial, se registra un movimiento de entrada `Stock inicial` en la misma transacción. El stock mostrado sigue siendo la suma de movimientos.

4. **Confirmación**: Se muestra el producto guardado con su ID y se ofrece la opción de agregar otro o regresar al menú.

### 2. Buscar y consultar productos

1. **Métodos de búsqueda**:
   - **Por código de barras**: Busca coincidencia exacta. Es el método más rápido y utilizado en ventas.
   - **Por nombre**: Búsqueda parcial (contiene). El sistema sugiere coincidencias a medida que se teclean las letras.
   - **Por categoría**: Filtrar todos los productos que tengan la categoría especificada.

2. **Resultados mostrados**:
   - Código de barras, nombre, costo, precio, stock actual, categoría.
   - Indicador visual de "stock bajo" si el stock es menor o igual al umbral configurado.

3. **Acciones disponibles** (según rol):
   - Ver detalle.
   - Editar (solo dueño/encargado).
   - Ninguno (solo vista para empleado).

### 3. Registrar entrada de inventario (compra)

1. **Proceso**: Se registra la entrada de productos comprados a un proveedor.
2. **Datos requeridos**:
   - Producto (seleccionado de la lista o código de barras).
   - Cantidad recibida (entera positiva).
   - Costo unitario del movimiento (obligatorio en la app; queda en el historial). En esta versión **no** actualiza el costo del catálogo: el costo vigente se edita por separado desde Editar producto.
3. **Lógica de actualización de stock**:
   - `NuevoStock = StockAnterior + CantidadRecibida`
4. **Historial**: Cada entrada se registra en la tabla de movimientos con fecha, cantidad, costo, motivo y usuario. El historial es inmutable.

### 4. Ajustar stock (manual)

1. **Cuándo usar**: Cuando el conteo físico no coincide con el sistema (pérdida, hallazgo, error de registro).
2. **Datos requeridos**:
   - Producto afectado.
   - Nueva cantidad de stock (entera).
   - Motivo del ajuste (conteo físico, pérdida encontrada, error de sistema, etc.).
   - Usuario que realiza el ajuste.
3. **Lógica**:
   - Si la nueva cantidad es mayor al stock actual: `Stock = NuevaCantidad` (se añaden productos).
   - Si la nueva cantidad es menor al stock actual: `Stock = NuevaCantidad` (se quitan productos, se registra como "ajuste negativo").
   - Se registra el movimiento de ajuste con motivo y usuario.

### 5. Conteo físico y alertas de stock bajo

1. **Stock bajo**: Se considera bajo cuando `Stock <= StockMinimo`. El umbral operativo es el mínimo de cada producto; no hay un umbral global adicional.
2. **Stock mínimo**: Configurable por producto (cuántas unidades como mínimo deben tener siempre).
3. **Alertas en la interfaz**: El catálogo marca visualmente los productos con stock bajo y muestra el conteo en el resumen.
4. **Reporte de stock bajo**: El filtro **Stock bajo** lista todos los productos en esa condición.

## Entidades de Datos

### Producto

| Campo | Tipo | Descripción |
|-------|------|-------------|
| Id | int | PK autogenerado |
| CodigoBarras | varchar(50) | Código de barras único (obligatorio) |
| Nombre | varchar(100) | Nombre del producto (obligatorio) |
| Costo | integer | Costo de adquisición en centavos (obligatorio) |
| Precio | integer | Precio de venta al público en centavos (obligatorio) |
| Stock | int | Stock actual en la tienda (obligatorio, por defecto 0) |
| StockMinimo | int | Stock mínimo alerta (obligatorio, por defecto 5) |
| Categoria | varchar(50) | Categoría libre (obligatorio) |
| Impuesto | decimal | Tasa de impuesto específica para este producto (obligatorio, por defecto impuesto predeterminado de configuración) |
| FechaCreacion | datetime | Cuándo fue registrado |
| ÚltimaActualizacion | datetime | Fecha del último movimiento de stock |

### MovimientoInventario (historial opcional)

| Campo | Tipo | Descripción |
|-------|------|-------------|
| Id | int | PK autogenerado |
| ProductoId | int | FK a Producto |
| Tipo | enum | "ENTRADA", "SALIDA", "AJUSTE" |
| Cantidad | int | Cantidad movida (positiva o negativa según tipo) |
| CostoUnitario | integer | Costo en centavos al momento del movimiento (para historial de costos) |
| Motivo | varchar | "Compra", "Venta", "Merma", "Ajuste físico", etc. |
| Usuario | varchar | Quién realizó el movimiento |
| Fecha | datetime | Cuándo ocurrió |

## Reglas de Negocio

1. **El stock es el resultado de los movimientos**: Nunca un número editado directamente. El stock cambia únicamente por:
   - Entradas de compra (`Stock += Cantidad`)
   - Ventas (`Stock -= CantidadVendida`)
   - Mermas (`Stock -= CantidadMermada`)
   - Ajustes manuales (`Stock = NuevaCantidad`)
2. **Costo congelado al vender**: Cada venta grava el costo y precio del momento. El historial de costo del producto no afecta las ventas pasadas.
3. **Código de barras único**: No se permiten dos productos con el mismo código de barras.
4. **Categorías libres**: No hay catálogo cerrado de categorías. Se registran como texto libre. Esto permite que el sistema sea usable para cualquier giro.
5. **Impuesto por producto**: Cada producto puede tener su propia tasa de impuesto, override del impuesto predeterminado de la configuración de la tienda. Si está vacío, se usa la configuración global.
6. **Stock mínimo y alertas**: El stock mínimo se configura por producto. El sistema alerta cuándo `StockActual <= StockMinimo`; no hay un umbral global adicional.
7. **Stock nunca negativo**: El sistema nunca debe permitir que el stock de un producto quede en negativo. Si una venta intenta quitar más de lo que hay, se bloquea con el mensaje "Stock insuficiente".

## Integración con Otros Módulos

- **Ventas**: Cada venta registrada (módulo 3) reduce automáticamente el stock del producto vendido (transacción atómica).
- **Mermas**: Cada merma registrada reduce el stock y registra el costo perdido (módulo 4).
- **Compras/Entradas**: Cada entrada de inventario (módulo de compras o flujo de trabajo) aumenta el stock automáticamente.
- **Configuración**: Las categorías y tasas de impuesto aquí configuradas aparecen en las ventas y reportes.
- **Reportes**: El valorizado de inventario (stock × costo actual) se calcula aquí para los reportes de utilidad.

## Casos Límite

- **Producto con stock 0**: Se puede seguir vendiendo? No, el sistema bloquea la venta con "Stock insuficiente". Sin embargo, el producto sigue en catálogo y se puede reactivar al recibir nueva existencia.
- **Mismo nombre, código distinto**: Está permitido siempre y cuando los códigos de barras sean diferentes.
- **Modificar precio o costo**: El cambio afecta a partir de la próxima venta/transacción. Las transacciones ya registradas mantienen el precio/costo del momento.
- **Producto inactivo**: No existe un campo "activo/inactivo" en el diseño actual; para "desactivar" un producto, se puede dejar con stock 0 y precio 0, o el usuario decide no ofrecerlo. (En futuras versiones se podría añadir un campo `Activo` booleano).

## Pantallas Clave

Implementadas en la pestaña **Inventario** de la app MAUI desde T15:

1. **Catálogo de productos** (resumen de productos/stock bajo/valorizado, búsqueda por nombre o escaneo de código, indicadores y filtro de stock bajo).
2. **Agregar producto** (formulario con código, nombre, categoría, costo, precio, stock inicial, stock mínimo e impuesto).
3. **Editar producto** (dueño y encargado; costo y precio solo para el dueño).
4. **Ajustar stock** (nuevo stock y motivo; solo dueño).
5. **Entrada de inventario** (cantidad, costo unitario y motivo).
6. **Registrar merma** (cantidad y motivo; reduce el stock).
7. **Historial de movimientos** (últimos 200, con cantidad firmada, usuario y fecha).

## Criterios de Aceptación

- [x] Se puede agregar un producto con código de barras, nombre, costo, precio, stock inicial y categoría.
- [x] El código de barras debe ser único; intentar agregar uno duplicado falla con mensaje claro.
- [ ] La búsqueda por nombre sugiere coincidencias mientras se teclea (hoy se ejecuta al pulsar Buscar o Enter).
- [x] El stock se actualiza automáticamente al registrar una venta (módulo 3) o merma (módulo 4).
- [x] El sistema bloquea la venta si el stock es insuficiente.
- [x] Los productos con stock ≤ stock mínimo se marcan visualmente como "stock bajo".
- [x] Cada producto puede tener impuesto diferente al predeterminado de la tienda.
- [x] El costo y precio se "congelan" al momento de cada venta (las ventas anteriores no se ven afectadas si se modifican los precios después).
- [x] Los ajustes de stock requieren motivo y usuario, y actualizan el stock al valor indicado.
- [x] Los historiales de movimientos se registran y son inmutables.

## Preguntas Abiertas ⚠️

- ~~¿El stock mínimo debe ser por producto individual o puede ser un valor global para todas las categorías?~~ Resuelto: es por producto y no hay umbral global adicional.
- ¿Se debe permitir la venta de productos con stock 0 (backorder) o siempre bloquear? El diseño actual bloquea, pero es una decisión de negocio.
- ¿El impuesto por producto debe ser obligatorio o puede omitirse (usándose siempre el global)? El diseño actual lo hace opcional con fallback al global.
- ¿Cómo manejar productos con peso/volumen (kg, litros) vs. piezas? El diseño actual usa enteros para stock; las cantidades fraccionarias siguen pendientes.

---

**Estado actual (2026-10-25)**: base implementada y probada, con pantalla de operación en la app MAUI.

- *Implementado*: alta de producto con stock inicial como movimiento de entrada; código de barras único; edición sin tocar stock; búsqueda exacta por código y parcial por nombre; stock bajo; historial de movimientos; ajuste con diferencia y motivo; entradas; reducción por venta/merma; valorizado. La app MAUI ofrece catálogo, escaneo, alta/edición, entrada, merma, ajuste, historial y filtro de stock bajo, con acciones limitadas por rol.
- *Desviaciones del diseño*: no existe borrado ni campo `Activo`; el filtro por categoría no está en la interfaz; el historial se limita a los últimos 200 movimientos; el stock mínimo es siempre por producto (no hay umbral global adicional); el costo de una entrada no actualiza el costo del catálogo; la búsqueda por nombre no sugiere mientras se teclea.
- *Pendiente*: importación Excel/CSV, edición masiva, reporte de valorizado histórico y filtro por categoría en la app.
- *Pruebas*: `InventarioTests` (20 pruebas), `InventarioBordeTests` y la verificación manual de la app descrita en el plan de pruebas; la interfaz no tiene pruebas automatizadas.
- *Riesgo conocido*: el costo perdido de una merma solo se muestra al registrarla; no queda almacenado (ver [deuda-técnica](../deuda-tecnica.md)). El costo de una entrada queda en el movimiento, no en el catálogo.

**Siguiente módulo después de confirmar**: Módulo 3 - Ventas / POS (que dependerá fortemente de este módulo de inventario).