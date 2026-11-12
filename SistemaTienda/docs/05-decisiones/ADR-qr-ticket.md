# ADR: QR del ticket en la app MAUI

- **Fecha**: 2026-10-25
- **Estado**: Aceptado
- **Tarea**: T16 — anulación y QR en la app MAUI

## Contexto

El ticket interno debe incluir un QR con los datos esenciales de la venta (folio, fecha, total, método de pago y productos). La app es 100 % offline y no tiene servidor ni servicio de validación. El QR es informativo: no sustituye un comprobante fiscal y no permite cobrar ni verificar la autenticidad de la venta.

El proyecto MAUI ya tiene `Microsoft.Maui.Controls`; no se quiere sumar una biblioteca de escaneo con cámara porque esa función no está en el alcance.

## Opciones evaluadas

| Opción | Ventajas | Desventajas |
|--------|----------|-------------|
| Codificador propio | Sin dependencias | Implementar Reed-Solomon y corrección de errores es riesgoso y difícil de mantener |
| ZXing.Net.Maui | Genera y escanea códigos | Dependencia grande, pensada para cámara y con inicialización adicional |
| QRCoder | Madura y popular | Arrastra `System.Drawing.Common` en algunos frameworks, que no es portable a Android |
| `Net.Codecrete.QrCodeGenerator` | MIT, sin dependencias, solo genera la matriz QR | No incluye renderizado; hay que dibujar los módulos |

## Decisión

Usar **`Net.Codecrete.QrCodeGenerator` 3.2.1** en el proyecto MAUI y dibujar la matriz con un `GraphicsView` (`QrTicketView`). El paquete es MIT, no tiene dependencias transitivas y funciona offline.

El contenido del ticket y del QR se construye en `Application/Services/TicketService.cs`, sin dependencias de MAUI, para poder probarlo con pruebas unitarias. La página del ticket solo dibuja el QR y conserva las acciones de compartir y copiar el texto.

## Consecuencias

- El QR se muestra en pantalla y no se guarda como imagen ni se incluye en el texto compartido.
- El QR resume hasta diez productos; si hay más, indica cuántos quedaron fuera. El ticket en pantalla y el texto compartido conservan todas las líneas.
- El QR no es un comprobante fiscal ni un mecanismo de validación; un cliente no puede usarlo para reclamar una devolución sin el ticket y la revisión del negocio.
- La generación es local y no requiere permisos de red.
- La app Android sigue compilándose en CI; la visualización del QR en un dispositivo físico queda pendiente de validar.

## Implementación

- `SistemaTienda.Maui.csproj`: referencia al paquete.
- `Services/QrTicketView.cs`: dibuja módulos de tamaño entero con zona de silencio y sin red.
- `Application/Services/TicketService.cs`: genera el texto completo y el resumen del QR.
- `Pages/TicketPage.cs`: muestra el QR y mantiene compartir/copiar.
- `Pages/VentasPage.xaml(.cs)`: permite reabrir el ticket y anular ventas con el permiso correspondiente.

## Verificación

El 2026-10-25 se probó en Windows con una venta ficticia de tres productos: el QR se decodificó sin conexión con ZXing.Net y contenía tienda, folio, fecha, estado `Vigente`, total, método de pago, tres productos y la leyenda de que no es un comprobante fiscal. Tras anular la venta, el QR pasó a `Estado: Anulada`. Los botones del ticket se mantuvieron visibles al agregar el QR. El detalle está en [plan-de-pruebas.md](../07-pruebas/plan-de-pruebas.md).

Las reglas del texto y del resumen del QR están cubiertas por `TicketServiceTests` (6 casos). Quedan pendientes compartir el QR como imagen y validarlo en un dispositivo Android.
