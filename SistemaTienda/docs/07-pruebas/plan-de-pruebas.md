# Plan de Pruebas

## Objetivo

Validar que el sistema funcione correctamente según las reglas de negocio, cumpla con los criterios de aceptación de cada módulo y sea usable en dispositivos modestos. Todas las pruebas deben realizarse con datos de prueba (ficticios), nunca con datos reales de clientes o proveedores.

## Estrategia de Pruebas

- **Pruebas unitarias**: Cada función/servicio por separado.
- **Pruebas integradas**: Flujos completos de usuario (venta, corte, respaldo).
- **Pruebas de rendimiento**: Con volúmenes realistas (hasta 10,000 productos, 500 ventas/día simuladas).
- **Pruebas de falla**: Apagón, disco lleno, datos corruptos, entradas invalidas.

## Áreas Críticas a Probar (por regla NO negociable)

1. **Venta atómica**
   - *Caso*: Registrar una venta multi-línea debe actualizar inventario y total correctamente.
   - *Expected*: Stock reduce, venta registrada, total correcto. Si falla, nada se guarda.
   - *Prueba*: Vender el último producto en stock; verificar que no permita vender más; verificar que el stock quede en 0.

2. **Cuadro de caja**
   - *Caso*: Calcular efectivo esperado vs. contado.
   - *Expected*: Fórmula: Fondo Inicial + Ventas Efectivo + Abonos Efectivo + Ingresos de Caja − Retiros − Gastos de Caja.
   - *Prueba*: Simular un periodo con ventas mixtas, abonos, ingresos, retiros y gastos; verificar el efectivo esperado, que la diferencia exija motivo y que el periodo empiece en el corte anterior.

3. **Integridad de Inventario**
   - *Caso*: El stock debe ser siempre el resultado de los movimientos (compras - ventas - mermas + ajustes).
   - *Expected*: Nunca un número editado directamente.
   - *Prueba*: Hacer una compra (+10 stock), hacer 3 ventas (-3 stock), hacer 1 merma (-1 stock). Verificar que stock = 10 - 3 - 1 = 6.

4. **Costo congelado**
   - *Caso*: Vender un producto, luego modificar su precio de costo en el catálogo, y verificar que las ventas antiguas sigan mostrando el costo antiguo.
   - *Expected*: Los registros de venta guardan el costo y precio del momento.
   - *Prueba*: Crear producto con costo $10, precio $15. Vender 2 unidades. Modificar costo a $12. Verificar que el reporte de utilidad siga considerando el costo $10 de las 2 ventas antiguas.

5. **Mermas**
   - *Caso*: Registrar merma y verificar que stock y costo perdido se actualicen.
   - *Expected*: Stock reduce, costo perdido registrado, merma aparece en reporte.
   - *Prueba*: Producto con stock 10. Registrar merma de 2 unidades, costo $5 cada una. Verificar stock = 8 y costo perdido = $10 en reporte.

6. **Fiado (módulo opcional)**
   - *Caso*: Venta a fiado, abono, cancelación.
   - *Expected*: Venta crea saldo; abono reduce saldo; cancelación revierte saldo.
   - *Prueba*: Vender a fiado por $50. Verificar saldo $50. Abonar $20. Verificar saldo $30. Cancelar la venta original. Verificar saldo $0 (revertido).

7. **Permisos y Bitácora**
   - *Caso*: Funciones restringidas por rol.
   - *Expected*: Empleado básico no ve menú de configuración; encargado puede registrar mermas pero no modificar precios.
   - *Prueba*: Probar cada pantalla con diferentes roles PIN.

8. **Respaldo y Restauración**
   - *Caso*: Crear respaldo, simular pérdida de datos, restaurar.
   - *Expected*: Los datos se recuperan exactamente como estaban.
   - *Prueba*: Hacer 10 ventas, hacer respaldo, "perder" los datos (borrando la base de datos nueva), restaurar desde respaldo. Verificar que las 10 ventas reaparecen.

9. **Ticket digital**
   - *Caso*: Generar ticket y compartir.
   - *Expected*: Ticket interno con QR informativo (no fiscal) visible en pantalla; compartir el texto o copiarlo. El QR no se guarda como imagen.
   - *Prueba*: Realizar una venta, decodificar el QR con un lector externo y probar Copiar y Compartir.

10. **Rendimiento**
    - *Caso*: Buscar producto, iniciar app, lista de productos.
    - *Expected*: <200 ms en buscar, <5 s en arranque, <300 ms en mostrar catálogo.
    - *Prueba*: Medir tiempos con 500 productos, 500 ventas históricas.

## Niveles de Prueba

### Nivel 1: Feliz (Happy Path)

Cada flujo principal funciona como se espera:
- Venta completa (producto + precio + pago + ticket).
- Corte de caja sin diferencias.
- Merma registrada correctamente.
- Respaldos creados y listados.

### Nivel 2: Negativo / Error

Qué pasa cuando las cosas van mal:
- Stock insuficiente para vender.
- Código de Barras no encontrado.
- Entrada de datos inválida (letras en campo numérico).
- PIN incorrecto varios intentos.
- Memoria llena a medio camino.

### Nivel 3: Falla de Hardware / Sistema

- Tablet se apaga durante una venta.
- Se extrae la memoria USB durante el respaldo.
- Se instala una versión nueva de la app sobre la antigua.
- La base de datos se corrompe (forzado con casos extremos).

### Nivel 4: Carga y Rendimiento

- 1,000 productos en catálogo.
- 500 "ventas simuladas" en la base de datos (historical).
- Búsqueda por nombre con 100 productos que coinciden.
- Reporte de utilidad de un mes entero.

## Pruebas automatizadas existentes (2026-10-25)

Proyecto `SistemaTienda.Tests` (xUnit, net8.0). Cada prueba crea una base SQLite temporal aislada y la elimina al terminar.

```bash
dotnet test SistemaTienda/SistemaTienda.sln
```

| Archivo | Cubre |
|---------|-------|
| `ConfiguracionTests` | Asistente inicial, PIN hasheado, login, roles, gastos |
| `InventarioTests` | Alta, duplicados, venta, merma, entrada, ajuste, stock bajo, búsqueda, valorizado |
| `FiadoProveedoresTests` | Cliente, abono, saldo, proveedor y pago; búsqueda de clientes por nombre o teléfono y de proveedores por nombre, clave o teléfono sin mayúsculas ni acentos; filtro con saldo; recorte de datos y bitácora de alta de ambos |
| `BaseDatosTests` | Inicialización idempotente, tablas, mapeo de tipos |
| `DineroTests` | Conversión pesos ↔ centavos con redondeo `AwayFromZero`, multiplicación con desbordamiento, porcentaje a centavos y formato invariante |
| `MigracionesRespaldoTests` | Runner de migraciones con respaldo previo, idempotencia, fallo con rollback, rechazo de base más nueva, respaldo verificado, restauración exacta con respaldo de seguridad, confirmación obligatoria, migración v3 → v4 a centavos con integridad de FKs |
| `VentasTests` | Venta atómica, stock insuficiente revierte todo, costo y precio congelados, fiado (saldo, desactivado, límite, exclusivo), pagos que no coinciden, comisión en mixto, líneas duplicadas, ventas del día, migración v1→v2 con datos |
| `AnulacionVentasTests` | Devolución de stock con costo congelado y bitácora, reverso del saldo fiado, bloqueo tras corte cerrado, exclusión del corte pendiente, doble anulación sin movimientos duplicados, permiso de encargado, motivo obligatorio y exclusión de la utilidad |
| `CorteCajaTests` | Efectivo esperado con todas las fuentes, faltante/sobrante, motivo obligatorio, gasto externo excluido, mixto solo suma efectivo, periodo entre cortes, fondo sugerido, corte sin actividad, validaciones, abono con historial, orden del historial, migración v2→v3 con respaldo |
| `BitacoraPermisosTests` | Ajuste autorizado con antes/después, empleado sin permiso, encargado sin cambio de precio pero con edición, merma, gasto, pago a proveedor, retiro denegado e ingreso permitido, inmutabilidad por disparadores, filtros de consulta, registro sin acción o sin sesión |
| `GastosTests` | Gasto de un centavo, suma de centavos sin perder precisión, monto cero o negativo rechazado |
| `SaldosBordeTests` | Abono igual al saldo, abono con saldo cero, centavos exactos, pago a proveedor que liquida el saldo |
| `InventarioBordeTests` | Venta que agota el stock, costo o cantidad inválidos en entrada, merma no positiva, código duplicado tras recortar, comodines literales en la búsqueda |
| `UsuariosPermisosTests` | Nombre recortado, PIN de cuatro dígitos, nombre en blanco, usuario inexistente, sal distinta por hash y hash mal formado |
| `MapeoTiposTests` | Rol entero a enum y fecha nullable nula o cargada |
| `ReportesTests` | Utilidad con costo congelado, costo modificado después de vender, comisión de tarjeta, mermas, gastos de ambos orígenes, venta fuera del rango y rango invertido |
| `RespaldoAutomaticoTests` | Respaldo automático sin copia previa y su auditoría, respaldo vigente que no duplica, intervalo cumplido, intervalo 0 deshabilitado y auditoría del respaldo manual |
| `TicketServiceTests` | Texto del ticket (efectivo con recibido y cambio, mixto con comisión, fiado con cliente), marcador de anulación, contenido del QR con estado, límite de diez productos y nombre de tienda por defecto |
| `ConteoEfectivoTests` | Catálogo de 11 denominaciones de mayor a menor y sin duplicados, subtotal por cantidad, cantidades cero o negativas ignoradas, total de billetes y monedas y líneas sin cantidad |

Total actual: 191 casos, todos en verde. Los puntos 1, 2, 3, 5, 6, 7, 8 y 9 del listado de áreas críticas tienen cobertura parcial; falta rendimiento y la interfaz MAUI sigue sin pruebas automatizadas. La restauración se prueba sobre una base temporal: se verifica que el contenido vuelve al del respaldo (no byte a byte).

La verificación se ejecuta en GitHub Actions (`windows-latest`) en cada push y también puede correr localmente con `dotnet test`; ver `CLAUDE-tienda.md` para el entorno actual.

## Verificación manual de la app MAUI (2026-10-23)

La interfaz no está cubierta por pruebas automatizadas. Esta tabla registra la verificación hecha en Windows con una base temporal de prueba; los datos se borraron al terminar.

| Caso | Resultado |
|------|-----------|
| Primer arranque sin base: crea el esquema v6 y muestra el asistente | Pasó |
| Asistente con PIN distinto en la confirmación | Pasó (muestra “Los PIN no coinciden”) |
| Asistente completo y transición al login | Pasó |
| Login con PIN incorrecto y contador de intentos | Pasó |
| Bloqueo tras 3 intentos hasta reabrir la app | Pasó |
| Login correcto y apertura del tablero | Pasó |
| Inicio: ventas del día, efectivo esperado, stock bajo y último respaldo | Pasó |
| Ventas: lista del día y estado de anulación | Pasó (estado vacío y venta anulada) |
| Caja: periodo abierto, desglose por método y último corte cerrado | Pasó |
| Ajustes: datos, reglas, rutas reales y conteo de respaldos | Pasó |
| Crear respaldo manual desde Ajustes | Pasó (copia verificada y ruta mostrada) |
| Respaldo automático al iniciar sesión | Pasó (se creó una copia) |
| Cerrar sesión y volver al login | Pasó |
| Arranque sin conexión y sin permisos de red | Pasó |

Pendiente:

- Instalar y ejecutar la app Android en la tablet objetivo (hoy solo compila en CI).
- Repetir el flujo con una base restaurada desde respaldo.
- Medir arranque, cambio de pestaña y respuesta del tablero con 500 ventas y 500 productos ficticios.

## Verificación manual del POS (T13)

Ejecutada en Windows el 2026-10-25 con una base demo local: cinco productos, una clienta con límite de $500.00, comisión de tarjeta de 3.5 % y fondo inicial de $200.00. Se registraron ventas ficticias de efectivo, tarjeta, mixto y fiado; las cifras de esta tabla corresponden a la pasada controlada. Los datos de la demo no forman parte del repositorio.

| Caso | Resultado |
|------|-----------|
| Buscar por código exacto y agregar al carrito | Pasó (Refresco Cola; el código entra directo y limpia el buscador) |
| Buscar por nombre y elegir entre resultados | Pasó (Agua natural; la lista muestra precio y stock) |
| Código o nombre inexistente | Pasó (mensaje “No se encontró 999999.”, sin cambios en el carrito) |
| Sumar, restar y quitar líneas | Pasó (1→2 con `+`; 2→1→fuera con `−`; `✕` quita la línea) |
| No vender por encima del stock | Pasó (Pan blanco: al llegar a 12, `+` muestra “No hay más stock… (stock 12)”) |
| Cobro en efectivo con monto mayor | Pasó (venta #5 por $432.00 con $500.00: cambio $68.00) |
| Cambio visible en el cobro y en el ticket | Pasó (venta #8 por $36.00 con $50.00: el recuadro “Cambio a entregar” muestra $14.00; con $20.00 muestra “Faltan $16.00” y no registra la venta; el ticket imprime Recibido y Cambio) |
| Cobro con tarjeta | Pasó (venta #2 por $18.00; comisión 3.5 % = $0.63 en el ticket) |
| Cobro mixto con suma incorrecta | Pasó (efectivo $5.00 + tarjeta $10.00: rechaza la suma $15.00 de $12.00, sin venta) |
| Cobro mixto correcto | Pasó (venta #3 por $12.00: $5.00 efectivo + $7.00 tarjeta; comisión $0.25) |
| Cobro a fiado sin cliente | Pasó (“Selecciona el cliente para la venta a fiado.”) |
| Cobro a fiado con cliente | Pasó (venta #4 por $16.00 a María López; aviso de saldo $0.00 y límite $500.00) |
| Confirmar venta | Pasó (cada venta descontó stock, mostró su folio y dejó el carrito vacío) |
| Ticket: detalle, copiar y cerrar | Pasó (el texto copiado coincide; “Cerrar” vuelve al POS) |
| Ticket: compartir con el sistema | No probado (abre el diálogo de Windows; queda pendiente) |
| Ventas y Caja reflejan las ventas | Pasó (5 ventas por $496.00; efectivo esperado $655.00 = fondo $200.00 + $455.00 en efectivo; tarjeta $25.00 y fiado $16.00) |
| Producto con stock 0 | Pasó (tras agotar Pan blanco, muestra “No hay stock disponible… (stock 0)” y no lo agrega) |

Observación fuera del alcance del POS: Inicio y Ventas muestran “$496.00 cobrados” e incluyen la venta a fiado de $16.00, aunque ese importe no entró a caja; Caja sí lo separa. Es una etiqueta heredada de T12 y quedó anotada en [deuda-técnica](../deuda-tecnica.md).

Pendiente del POS:

- Probar “Compartir” con el diálogo del sistema en Windows.
- Repetir el flujo en la tablet Android.

## Verificación manual de Caja (T14)

Ejecutada en Windows el 2026-10-25 sobre la misma base demo del POS (fondo inicial de $200.00). Se cerraron dos periodos y se registraron operaciones ficticias; los datos no forman parte del repositorio.

| Caso | Resultado |
|------|-----------|
| Periodo abierto con ventas | Pasó (9 ventas; efectivo esperado $760.00 con fondo de $200.00 y desglose por método) |
| Cierre con faltante sin observaciones | Pasó (mensaje “La diferencia exige explicar el motivo antes de cerrar.”; no se guardó el corte) |
| Cierre con faltante y observación | Pasó (confirmación previa; corte #1 con contado $750.00, diferencia −$10.00 y usuario Dueño; el siguiente periodo inicia en $750.00) |
| Ingreso de caja | Pasó ($50.00 con motivo; el efectivo esperado sube a $800.00) |
| Retiro de caja | Pasó (confirmación; $20.00 con motivo; el efectivo esperado baja a $780.00) |
| Abono de fiado desde Caja | Pasó ($10.00; el saldo de la clienta baja de $16.00 a $6.00 y el esperado sube a $790.00) |
| Cierre sin diferencia | Pasó (contado $790.00; corte #2 con diferencia $0.00 y sin observaciones) |
| Último corte cerrado | Pasó (muestra contado $790.00, diferencia $0.00 y responsable) |
| Retiro con rol encargado | Pendiente (la matriz de permisos solo está cubierta por pruebas automatizadas) |
| Conteo por denominaciones e historial de cortes | Pasó en T17 (ver la verificación siguiente) |

Pendiente de Caja:

- Probar el cierre y las operaciones con un usuario encargado y otro empleado.
- Repetir el flujo en la tablet Android.
- Persistir el desglose por denominaciones si se requiere auditoría.

## Verificación manual de Inventario (T15)

Ejecutada en Windows el 2026-10-25 sobre la misma base demo del POS y la Caja, con productos y movimientos ficticios que no forman parte del repositorio. Se usaron los roles dueño, encargado y empleado con PIN temporal. El escaneo se simuló con entrada de teclado terminada en Enter, el mismo mecanismo que usa un lector USB/Bluetooth en modo teclado.

| Caso | Resultado |
|------|-----------|
| Catálogo inicial con resumen | Pasó (productos, stock bajo y valorizado se recalcularon después de cada operación) |
| Escanear código conocido | Pasó (7501000000011 abrió el detalle de Refresco Cola) |
| Buscar por nombre | Pasó (Refresco mostró una coincidencia) |
| Filtrar stock bajo | Pasó (solo Pan blanco, con stock 0) |
| Registrar entrada | Pasó (5 unidades a $12.50; stock 17→22 y movimiento +5) |
| Registrar merma | Pasó (2 unidades con motivo; stock 22→20, movimiento −2 y costo perdido estimado) |
| Ajustar stock como dueño | Pasó (nuevo stock 25 con motivo; diferencia +5 y movimiento de ajuste) |
| Historial de movimientos | Pasó (entrada, merma, ajuste y ventas con cantidad firmada, usuario, motivo y fecha; límite de 200) |
| Editar producto como dueño | Pasó (precio $19.00 y mínimo 8; costo y precio editables) |
| Código desconocido | Pasó (ofreció alta; se creó Producto T15 con stock 4 y mínimo 2; el catálogo pasó a 6 productos) |
| Editar como encargado | Pasó (puede editar, registrar entrada y merma; no ve Ajustar stock; costo y precio deshabilitados con aviso) |
| Consultar como empleado | Pasó (sin + Nuevo; en el detalle solo Ver movimientos y Cerrar, con aviso de solo lectura) |
| Valorizado después de las operaciones | Pasó ($838.00 con 6 productos) |

Observación: la entrada conserva el costo del movimiento, pero no actualiza el costo del catálogo; el costo vigente se cambia desde Editar con permiso de dueño. Quedó anotado en [deuda-técnica](../deuda-tecnica.md).

Pendiente de Inventario:

- Probar con un lector de código de barras físico USB o Bluetooth.
- Repetir el flujo en la tablet Android.
- Agregar filtro por categoría y desactivación de productos.
- Cubrir la interfaz con pruebas automatizadas.

## Verificación manual de Ventas, ticket y anulación (T16)

Ejecutada en Windows el 2026-10-25 sobre la misma base demo del POS, la Caja y el Inventario. Se usó una venta ficticia de tres productos y se decodificó el QR con ZXing.Net en un proyecto temporal, sin conexión. Los datos no forman parte del repositorio.

| Caso | Resultado |
|------|-----------|
| Venta de tres productos desde el POS | Pasó (venta #10: Refresco Cola $19.00 + Agua $12.00 + Sabritas $16.00 = $47.00 en efectivo con $100.00; cambio $53.00) |
| QR del ticket recién cobrado | Pasó (decodificado offline: tienda, venta #10, fecha, estado `Vigente`, total $47.00, pago Efectivo, tres productos y “No es un comprobante fiscal.”) |
| Botones del ticket con QR | Pasó (tras corregir la cuadrícula, Compartir, Copiar y Cerrar quedan visibles y el ticket se desplaza dentro del marco) |
| Detalle de la venta | Pasó (folio, fecha, cajero, estado, líneas, pago y total) |
| Reimpresión desde el detalle | Pasó (mismo texto y QR que el ticket original; botones visibles) |
| Anulación con motivo vacío | Pasó (“Falta el motivo”; no se modificó la base) |
| Anulación con confirmación incorrecta | Pasó (al escribir `NO` muestra “Anulación cancelada”; la venta siguió vigente) |
| Anulación correcta | Pasó (motivo “Cobro duplicado T16” y `ANULAR`; registro en `AnulacionesVenta`, stock restaurado a 25, 29 y 17 unidades, bitácora `AnulacionVenta` y resumen de 9 ventas, $618.00 cobrados y 1 anulada) |
| Ticket de una venta anulada | Pasó (sello con usuario, fecha y motivo; el QR indica `Estado: Anulada` y el texto advierte que ya no es válido) |
| Venta incluida en un corte cerrado | Pasó (la venta #1 se rechaza con el mensaje de corte cerrado) |
| Visibilidad por rol | Pasó (el encargado ve **Anular venta**; el empleado solo ve **Ver ticket** y **Cerrar**) |
| Compartir el QR como imagen | Pendiente (hoy se comparte el texto y el QR solo se ve en pantalla) |
| Reimpresión con efectivo recibido | Limitación (la venta no guarda el recibido; la reimpresión no repite el cambio) |
| QR en la tablet Android | Pendiente (solo se ha compilado en CI) |

Pendiente de Ventas y ticket:

- Compartir el QR como imagen y verificar el botón Compartir con el diálogo del sistema.
- Repetir el flujo en la tablet Android.
- Decidir si se guarda el efectivo recibido para reimprimirlo o si se mantiene solo en el ticket original.
- Cubrir la interfaz con pruebas automatizadas.

## Verificación manual de conteo e historial de Caja (T17)

Ejecutada en Windows el 2026-10-25 sobre la misma base demo del POS, la Caja, el Inventario y las Ventas, con cortes #1 y #2 ya cerrados. Los datos no forman parte del repositorio.

| Caso | Resultado |
|------|-----------|
| Cantidad no entera en el conteo | Pasó (con `abc` muestra “Revisa las cantidades: solo se aceptan números enteros.”, el total queda en “—”, el contado se vacía y el cierre se bloquea) |
| Conteo 7×$100 + 1×$50 + 2×$20 | Pasó ($790.00; el contado se llena solo y queda en solo lectura; diferencia $0.00 en verde) |
| Limpiar conteo | Pasó (las 11 filas quedan vacías, el total en “—”, el contado vuelve a edición manual y el cierre se bloquea) |
| Total directo sin conteo | Pasó ($790.00 manual habilita el cierre) |
| Cierre desde el conteo | Pasó (corte #3 con esperado $790.00, contado $790.00 y diferencia $0.00; el siguiente periodo arranca con $790.00 y el formulario se limpia) |
| Historial de cortes | Pasó (#3, #2 y #1 del más reciente al más antiguo, con periodo, esperado, contado, “Cerrado … por Dueño” y nota en cursiva en el #1) |
| Diferencia en el historial | Pasó (corte #1 en rojo con −$10.00 y nota “Falta de prueba en el conteo”; cortes sin diferencia en verde) |
| Borrar la última cantidad capturada | Pasó (el contado derivado se limpia y vuelve a edición manual, sin conservar un total viejo) |
| Conteo persistido en cortes pasados | Limitación (solo se guarda el total contado; el desglose no se conserva) |

Pendiente del conteo y el historial:

- Repetir el flujo en la tablet Android.
- Decidir si el desglose se guarda para auditoría.
- Cubrir la interfaz con pruebas automatizadas.

## Verificación manual de Clientes (T18) — pendiente

La pantalla compila para Windows y las pruebas de servicio están en verde, pero el entorno de desarrollo de esta sesión no permitió abrir la interfaz gráfica. La verificación manual queda pendiente en Windows sobre una base demo, sin datos reales:

| Caso | Resultado |
|------|-----------|
| Abrir la pestaña Clientes y ver el resumen y el directorio | Pendiente |
| Alta con nombre y teléfono, sin saldo inicial | Pendiente |
| Alta con saldo inicial y nombre con acentos | Pendiente |
| Alta sin nombre | Pendiente (debe mostrar el error y no guardar) |
| Búsqueda por nombre con minúsculas y sin acentos | Pendiente |
| Búsqueda por teléfono y filtro **Con saldo** | Pendiente |
| Abono parcial con dueño y actualización del saldo | Pendiente |
| Abono mayor al saldo | Pendiente (debe rechazarse sin modificar la base) |
| Sesión de empleado: directorio y alta visibles, **Abonar** oculto | Pendiente |
| Alta y abono se reflejan en la bitácora y el corte | Pendiente |

Pendiente de la pantalla:

- Editar nombre y teléfono, borrar clientes y consultar el estado de cuenta por cliente.
- Elegir el método del abono (hoy siempre efectivo).
- Repetir el flujo en la tablet Android.
- Cubrir la interfaz con pruebas automatizadas.

## Verificación manual de Proveedores (T19) — pendiente

La pantalla compila para Windows y las pruebas de servicio están en verde, pero el entorno de desarrollo de esta sesión no permitió abrir la interfaz gráfica. La verificación manual queda pendiente en Windows sobre una base demo, sin datos reales:

| Caso | Resultado |
|------|-----------|
| Abrir la pestaña Proveedores y ver el resumen y el directorio | Pendiente |
| Alta con solo nombre, sin clave, teléfono, dirección ni saldo | Pendiente |
| Alta con clave, teléfono, dirección y saldo inicial | Pendiente |
| Alta sin nombre | Pendiente (debe mostrar el error y no guardar) |
| Búsqueda por nombre con minúsculas y sin acentos | Pendiente |
| Búsqueda por clave, por teléfono y filtro **Con saldo** | Pendiente |
| Pago mayor al saldo | Pendiente (debe mostrar el error y no modificar) |
| Pago válido y actualización del saldo y el resumen | Pendiente |
| Sesión de empleado: directorio y alta visibles, **Pagar** oculto | Pendiente |
| Alta y pago se reflejan en la bitácora | Pendiente |

Pendiente de la pantalla:

- Editar o borrar proveedores y consultar el estado de cuenta por proveedor.
- Historial de pagos con método y su entrada al corte de caja.
- Repetir el flujo en la tablet Android.
- Cubrir la interfaz con pruebas automatizadas.

## Herramientas de Prueba

- **Datos de prueba ficticios**: Un script o conjunto de datos iniciales que incluir:
  - 50-100 productos con códigos de barras, nombres, costos y precios variados.
  - 10-20 clientes de muestra (para fiado).
  - 5-10 proveedores de muestra.
  - 1 mes de historial de ventas simuladas.
- **Medición de tiempos**: `System.Diagnostics.Stopwatch` en puntos clave (búsqueda, venta, inicio).
- **Reporte de cobertura**: Después de cada prueba, anotar: Pasó / Felló / Observación.

## Checklist para Cerrar un Módulo

- [ ] Se ejecutaron todas las pruebas del Nivel 1 (feliz path) para el módulo.
- [ ] Se ejecutaron las pruebas críticas (Nivel 2) relacionadas con el módulo.
- [ ] Se documentaron los fallos encontrados y sus soluciones.
- [ ] Se actualizó el documento del módulo con el estado final.
- [ ] Se crearon o actualizaron casos de prueba automáticos (si el desarrollador los considera necesarios).
- [ ] Se probó el respaldo y restauración asociado a este módulo.
- [ ] Se validó rendimiento con volúmenes realistas.
- [ ] Se marcó con ⚠️ lo supuesto o pendiente de validar (ej. cálculos de comisión exactos, integración con hardware de lectura de códigos).