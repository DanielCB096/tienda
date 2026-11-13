# ADR: Lectura de archivos Excel para importar productos

- **Fecha**: 2026-11-12
- **Estado**: Aceptado
- **Tarea**: T28 — importación XLSX de productos

## Contexto

La importación CSV de productos ya valida encabezados y datos, detecta duplicados y guarda filas válidas mediante el flujo de inventario existente. Se requiere admitir libros Excel `.xlsx` conservando esas reglas y la operación 100 % offline. No se requieren plantillas por giro ni archivos `.xls`.

## Opciones consideradas

| Opción | Ventajas | Desventajas |
|--------|-----------|-------------|
| Implementar lectura ZIP/XML propia | Sin dependencias adicionales | Parsing complejo y frágil para cadenas compartidas, celdas dispersas y variantes del formato |
| `DocumentFormat.OpenXml` | SDK mantenido para el estándar Open XML; lectura estructurada del libro y hojas | Dependencia adicional y no evalúa fórmulas |
| Automatización de Microsoft Excel | Interpreta el libro con Excel instalado | Requiere Excel, no es portable ni adecuado para Android |

## Decisión

Usar `DocumentFormat.OpenXml` 3.5.1 en la capa Application. El servicio carga el archivo localmente, interpreta la primera hoja y entrega sus filas al mismo mapa de encabezados, validación de datos, detección de duplicados y persistencia que se usan para CSV.

La primera fila no vacía es el encabezado. Se admiten celdas numéricas, cadenas compartidas e insertadas directamente. Las fórmulas no se calculan: se omite la fila y se reporta con su número físico. Una estructura de libro ilegible o encabezados incompatibles cancela la importación antes de guardar productos; los errores por fila no impiden importar las demás filas válidas.

## Consecuencias

- La importación funciona sin Excel instalado, servidor o conexión a internet.
- Los productos existentes no se reemplazan; el stock inicial sigue generando un movimiento de inventario.
- `.xls`, plantillas por giro y la evaluación de fórmulas quedan fuera del alcance.
- El paquete se referencia en Application y en Tests (el generador de libros de prueba utiliza el SDK directamente).

## Verificación

Las pruebas de `ImportacionProductosXlsxTests` cubren celdas de texto compartido y numéricas, importación parcial con errores y duplicados, fórmulas, encabezados incompatibles, permisos y libro ilegible. La selección y el reporte de errores en la interfaz quedan pendientes de validación manual en Windows y Android.
