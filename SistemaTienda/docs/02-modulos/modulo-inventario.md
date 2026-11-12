# Módulo 2: Catálogo e Inventario

## Propósito y Alcance

Este módulo maneja el catálogo de productos y el control de inventario. Es el corazón operativo de la tienda: aquí se registran los productos que se venden, se hace el seguimiento del stock y se activan las alertas de stock bajo. El diseño debe ser **giro-neutral**: nada en el código asume qué tipo de producto se vende (abarrotes, ropa, etc.), solo que tiene un código de barras, un costo y un precio.

## Actores y Permisos

| Rol | Permisos | Descripción |
|-----|----------|-------------|
| **Dueño/Administrador** | - Agregar, editar, eliminar productos<br>- Ver valorizado de inventario<br>- Ajustar stock manualmente | Control total del catálogo |
| **Encargado/Cajero** | - Ver catálogo y stock<br>- Registrar nuevas entradas (compras)<br>- Ver alertas de stock bajo | No puede eliminar productos ni modificar precios |
| **Empleado básico** | - Buscar productos por nombre/código<br>- Ver información pública | Acceso de solo lectura |

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

3. **Persistencia**: Se inserta el producto en la tabla `Productos` con `Stock = stock_inicial`.

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
   - Costo unitario (opcional; si se provee, actualiza el costo del producto; si no, deja el costo existente).
3. **Lógica de actualización de stock**:
   - `NuevoStock = StockAnterior + CantidadRecibida`
4. **Historial**: Cada entrada se registra en una tabla de movimientos de inventario (opcional, para auditoría), con fecha, cantidad, costo y usuario.

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

1. **Stock bajo**: Configurable por tienda (por defecto: cuando el stock ≤ 5 unidades o ≤ 10% del stock mínimo).
2. **Stock mínimo**: Configurable por producto (cuántas unidades como mínimo deben tener siempre).
3. **Alertas en la interfaz**: Al entrar al catálogo, los productos con stock bajo se marcan visualmente (ej. fondo rojo o icono de advertencia).
4. **Reporte de stock bajo**: Lista de todos los productos que actualmente están por debajo del umbral de stock mínimo.

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
6. **Stock mínimo y alertas**: El stock mínimo se configura por producto. El sistema alerta cuándo `StockActual <= StockMinimo`. El umbral de "stock bajo" es configurable por tienda (por defecto: stock actual ≤ 5 o ≤ 10% de stock mínimo, lo que sea mayor).
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

1. **Catálogo de productos** (lista con búsqueda por nombre/código, indicadores de stock bajo).
2. **Agregar producto** (formulario con todos los campos).
3. **Editar producto** (solo dueño/encargado, campos modificables).
4. **Ajustar stock** (formulario con producto, nueva cantidad, motivo).
5. **Entrada de inventario** (producto, cantidad, costo unitario opcional).
6. **Alertas de stock** (vista resumida de productos con stock ≤ mínimo).

## Criterios de Aceptación

- [ ] Se puede agregar un producto con código de barras, nombre, costo, precio, stock inicial y categoría.
- [ ] El código de barras debe ser único; intentar agregar uno duplicado falla con mensaje claro.
- [ ] La búsqueda por nombre sugiere coincidencias mientras se teclea.
- [ ] El stock se actualiza automáticamente al registrar una venta (módulo 3) o merma (módulo 4).
- [ ] El sistema bloquea la venta si el stock es insuficiente.
- [ ] Los productos con stock ≤ stock mínimo se marcan visualmente como "stock bajo".
- [ ] Cada producto puede tener impuesto diferente al predeterminado de la tienda.
- [ ] El costo y precio se "congelan" al momento de cada venta (las ventas anteriores no se ven afectadas si se modifican los precios después).
- [ ] Los ajustes de stock requieren motivo y usuario, y actualizan el stock al valor indicado.
- [ ] Los historiales de movimientos se registran (opcional pero recomendado).

## Preguntas Abiertas ⚠️

- ¿El stock mínimo debe ser por producto individual o puede ser un valor global para todas las categorías?
- ¿Se debe permitir la venta de productos con stock 0 (backorder) o siempre bloquear? El diseño actual bloquea, pero es una decisión de negocio.
- ¿El impuesto por producto debe ser obligatorio o puede omitirse (usándose siempre el global)? El diseño actual lo hace opcional con fallback al global.
- ¿Cómo manejar productos con peso/volumen (kg, litros) vs. piezas? El diseño actual usa enteros para stock, pero las cantidades en ventas pueden ser decimales.

---

**Estado actual (2026-10-23)**: base implementada y probada.

- *Implementado*: alta de producto con stock inicial como movimiento de entrada; código de barras único; edición sin tocar stock; búsqueda exacta por código y parcial por nombre; stock bajo; historial de movimientos; ajuste con diferencia y motivo; entradas; reducción por venta/merma; valorizado.
- *Desviaciones del diseño*: no existe borrado ni campo `Activo`; el filtro por categoría no está en la interfaz; el historial se limita a los últimos 200 movimientos; el stock mínimo es siempre por producto (no hay umbral global adicional).
- *Pendiente*: importación Excel/CSV, edición masiva y reporte de valorizado histórico.
- *Pruebas*: `InventarioTests` (20 pruebas) y `BaseDatosTests`.
- *Riesgo conocido*: el costo perdido de una merma solo se muestra al registrarla; no queda almacenado (ver [deuda-técnica](../deuda-tecnica.md)).

**Siguiente módulo después de confirmar**: Módulo 3 - Ventas / POS (que dependerá fortemente de este módulo de inventario).