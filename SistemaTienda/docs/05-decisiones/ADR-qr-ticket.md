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

- El QR se muestra en pantalla y, desde T32, también se genera como PNG y se comparte con el diálogo del sistema.
- Desde el 2026-11-24 el QR **no incluye productos**: solo tienda, folio, fecha, total, método de pago, el estado cuando está anulada y la leyenda de que no es un comprobante fiscal. El ticket en pantalla y el texto compartido conservan todas las líneas.
- El QR no es un comprobante fiscal ni un mecanismo de validación; un cliente no puede usarlo para reclamar una devolución sin el ticket y la revisión del negocio.
- La generación es local y no requiere permisos de red.
- La app Android sigue compilándose en CI; la visualización del QR y el compartir la imagen en un dispositivo físico quedan pendientes de validar.

## Actualización 2026-11-21 (T32)

- **Decisión**: renderizar el QR a PNG con un codificador propio mínimo (firma, `IHDR`, `IDAT` con `ZLibStream`, `IEND` y CRC32) en `Application/Services/QrImagenService.cs`. No se agrega una biblioteca de dibujo: se reutiliza `Net.Codecrete.QrCodeGenerator` solo para la matriz y se mantiene todo offline.
- **Consecuencias**: el PNG es en escala de grises, sin pérdida, con zona de silencio y módulos de tamaño entero. `TicketPage` agrega **Compartir QR como imagen** y usa la caché de la app; el texto sigue disponible para compartir o copiar. La imagen no se adjunta al texto ni se guarda en la galería por sí sola: eso lo decide el diálogo del sistema.
- **Pruebas**: `QrImagenServiceTests` valida firma, dimensiones, chunks, CRC implícito en la estructura, píxeles contra la matriz y parámetros inválidos.

## Actualización 2026-11-24 (P7) — el QR no se podía escanear

- **Contexto**: en la prueba de usabilidad P3 el cliente no pudo escanear el QR al cerrar la venta. El contenido incluía hasta diez productos y llegaba a ~500 caracteres; con corrección `M` el código subía a 89×89 módulos. En un control de 220 px cada módulo quedaba en 2 px, demasiado pequeño para la cámara.
- **Decisión**: el QR lleva solo el resumen (tienda, folio, fecha, total, método y, si aplica, `Estado: Anulada`). Se eliminaron los productos y el IVA, que siguen en el ticket de texto y en el PNG. El control pasa a 240 dp y se muestra como **imagen** rasterizada con `QrImagenService`: dibujar rectángulos dependía del escalado de la ventana y recortaba la última fila del patrón.
- **Resultado medido**: el contenido baja a ~130 caracteres y 49×49 módulos. El QR se muestra en 258×258 px, cuadrado y completo, y se decodificó con `pyzbar` directamente desde una captura de pantalla; antes medía 89×89 módulos y 2 px por módulo.
- **Pruebas**: `TicketServiceTests` verifica que el QR no crece con los productos y que no excede 53 módulos. La decodificación de la captura y del PNG se hizo con `pyzbar` como evidencia manual.
- **Pendiente**: ~~confirmar el escaneo desde un teléfono físico, en pantalla y con el PNG compartido~~. **Validado el 2026-11-24** con un teléfono real desde la pantalla: el lector devolvió el resumen esperado. Queda opcional probarlo con un segundo teléfono de otra marca.

## Implementación

- `SistemaTienda.Maui.csproj`: referencia al paquete.
- `Services/QrTicketView.cs`: muestra el QR como imagen de 240 dp con zona de silencio.
- `Application/Services/TicketService.cs`: genera el texto completo y el resumen del QR.
- `Application/Services/QrImagenService.cs`: genera el PNG del QR.
- `Pages/TicketPage.cs`: muestra el QR y permite compartirlo como texto, como imagen, copiarlo o cerrarlo.
- `Pages/VentasPage.xaml(.cs)`: permite reabrir el ticket y anular ventas con el permiso correspondiente.

## Verificación

El 2026-10-25 se probó en Windows con una venta ficticia de tres productos: el QR se decodificó sin conexión con ZXing.Net y contenía tienda, folio, fecha, estado `Vigente`, total, método de pago, tres productos y la leyenda de que no es un comprobante fiscal. Tras anular la venta, el QR pasó a `Estado: Anulada`. Los botones del ticket se mantuvieron visibles al agregar el QR. El detalle está en [plan-de-pruebas.md](../07-pruebas/plan-de-pruebas.md).

Las reglas del texto y del resumen del QR están cubiertas por `TicketServiceTests` (8 casos) y el PNG por `QrImagenServiceTests` (8 casos). Queda pendiente validar el compartir la imagen en Windows y Android.

El 2026-11-24 se decodificó el QR del ticket con `pyzbar` desde una captura de pantalla en Windows: 258×258 px, cuadrado, con tienda, folio, fecha, total, método y la leyenda. El QR anterior, con productos, medía 89×89 módulos y no se pudo escanear en la prueba P3.
