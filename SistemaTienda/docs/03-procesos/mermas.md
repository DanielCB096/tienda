# Mermas

## Registro de Merma

### Flujo

1. **Identificación**: El encargado o dueño identifica que hay pérdida de inventario.
2. **Captura rápida**: En la interfaz se selecciona "Registrar Merma" y se capturan:
   - **Motivo**: Dañado, caducado, robado, consumo propio, regalo, pérdida, etc.
   - **Cantidad**: Cuantas unidades (o peso/volumen) se perdieron.
   - **Costo perdido**: El costo que representa esa pérdida (generalmente el costo unitario del producto).
   - **Usuario**: Quién registra la merma (obligatorio, bitácora).
   - **Foto opcional**: Foto del producto dañado (usando cámara de la tablet).

### Reglas de Negocio

- **El stock siempre se reduce**: Al registrar una merma, el inventario del producto se disminuye automáticamente.
- **El costo se registra como perdido**: Afecta directamente la utilidad neta del período.
- **Motivo es obligatorio**: No se puede registrar una merma sin especificar el motivo.
- **Usuario obligatorio**: Quién registra queda registrado en la bitácora.

### Motivos Comunes (configurables)

- Dañado (golpe, rotura)
- Caducado (fecha de vencimiento pasada)
- Robo (hurto externo)
- Consumo propio (personal se lleva sin pagar)
- Regalo (donado a cliente o tercero)
- Pérdida (extraviado en bodega, conteo equivocado)

### Reporte de Mermas

El sistema debe mostrar:

- **Mermas por motivo**: Gráfico o lista de cuántas unidades se perdieron por cada tipo de motivo.
- **Mermas por producto**: Cuáles productos tienen más pérdidas.
- **Costo total de mermas**: En cantidad monetaria, por período (día, semana, mes).
- **Comparación con ventas**: Las mermas como porcentaje de las ventas totales (alerta si supera umbral configurado).

### Integración

- **Inventario**: La merma reduce inmediatamente el stock del producto afectado.
- **Utilidad neta**: Las mermas se restan de la utilidad bruta para calcular la utilidad neta.
- **Bitácora**: Cada merma queda registrada para auditoría e identificar patrones (¿sucede mucho con un proveedor? un producto en específico?).