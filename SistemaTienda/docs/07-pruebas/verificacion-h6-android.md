# Verificación H6 en Android (2026-11-25)

Equipo: POCO X6 Pro 5G (`2311DRK48G`), Android 16, app de prueba
`com.sistematienda.p3` (Debug), base de 3053 productos con un lote de importación
de 3000.

## Resultado

| Caso | Resultado |
|---|---|
| Inventario con 3053 productos y 1 inactivo | Correcto |
| **Lotes** muestra el lote #1 de 3000 con estado Aplicada y botón Revertir | Correcto |
| Búsqueda `cafe` encuentra “Café molido” (sin acentos ni mayúsculas) | Correcto, sin retraso perceptible |
| Código `7501000000001` sugiere “Arroz 1 kg” y Enter lo agrega al carrito | Correcto |
| Guardar ticket #1 con nombre, carrito vacío y mensaje de confirmación | Correcto |
| Ticket #1 retomado y cobrado en efectivo | Venta #1; ticket `Cobrado`, `VentaId=1`, stock 40 → 39 |
| Ticket #2 sobrevive cerrar y reabrir la app (nuevo login) | Correcto |
| Ticket #2 descartado | `Descartado` y bitácora `DescartarTicketEspera` con piezas y total |
| Lista **En espera** tras cobrar/descartar | Vacía |
| Bitácora | `DejarTicketEspera` x2, `RetomarTicketEspera` x1, `DescartarTicketEspera` x1 |

## Hallazgo corregido

Los totales de **En espera** se mostraban como `32.00` sin signo de pesos. Se
cambió a `Formato.Moneda` (`$32.00`). Pendiente reverificar visualmente tras
reinstalar.

## Pendiente de esta verificación

- Vista previa de importación en Android con un CSV real (la lista de lotes sí se
  verificó).
- Revertir un lote desde la interfaz (no se ejecutó para no desactivar los 3000
  productos de prueba).
- Medición instrumentada de milisegundos en Android; la prueba de escritorio da
  33.52 ms y en el teléfono no se percibió retraso.
- Escáner físico de 13 dígitos; la entrada por `adb` del código completo funcionó.
