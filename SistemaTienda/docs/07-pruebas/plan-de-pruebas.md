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
   - *Caso*: Registrar una venta con descuento debe actualizar inventario y total correctamente.
   - *Expected*: Stock reduce, venta registrada, total correcto. Si falla, nada se guarda.
   - *Prueba*: Vender el último producto en stock; verificar que no permita vender más; verificar que el stock quede en 0.

2. **Cuadro de caja**
   - *Caso*: Calcular efectivo esperado vs. contado.
   - *Expected*: Fórmula: Fondo Inicial + Ventas Efectivo + Abonos Efectivo - Retiros - Gastos de Caja.
   - *Prueba*: Simular un día con ventas mixtas, retiros y gastos; verificar que la diferencia sea correcta (o esperado si hay error).

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
   - *Expected*: Ticket PDF/imagen generado y función de compartir disponible.
   - *Prueba*: Realizar una venta, verificar que el ticket aparezca, probar las opciones de compartir (WhastApp puede requerir configuración de dispositivo).

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