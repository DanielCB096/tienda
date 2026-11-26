# Verificación de restauración en Windows (2026-11-26)

Equipo: Windows 11, app de prueba `com.sistematienda.p3`, base nueva (sin usuarios).
Respaldo de origen: escenario `configurado` con 53 productos y tres usuarios.

## Flujo probado

1. Base P3 limpia: `usuarios=0`, tienda `Mi Tienda` (valor por defecto).
2. Se ejecutó el mismo servicio que usa el botón **Restaurar un respaldo** del asistente:
   `RespaldoService.RestaurarEnInstalacionNueva(respaldo, confirmado: true)`.
3. Resultado: éxito; se creó `tienda-<fecha>-antes-restaurar.db` y la base quedó con
   **3 usuarios**, tienda **Abarrotes La Esquina** y **53 productos**.
4. Se pulsó **Restaurar un respaldo** en la interfaz, se pegó la ruta del archivo y
   se confirmó. La app mostró el bloqueo/login de **Abarrotes La Esquina** y el PIN
   del respaldo (1234) permitió entrar.
5. La app mostró el **login de Abarrotes La Esquina** con el encabezado POSale, no
   el asistente. Es el resultado esperado tras restaurar.

## Selector en Windows

El `FilePicker` de MAUI falla con `COMException 0x80004005` en esta app sin
empaquetar. El selector de WinRT falla igual y el diálogo nativo de comdlg32 quedó
colgado. Por eso la app incluye un **explorador propio**: carpetas, Descargas,
Escritorio, Documentos, carpeta de respaldos, Subir, ruta manual y filas con
contorno. Android mantiene el selector del sistema.

## Pendiente

- Probar la pantalla de arranque fallido con una base corrupta.
- Repetir con un respaldo `.db.stb` y la clave de 24 caracteres desde la interfaz.
- Un empaquetado MSIX permitiría usar el selector nativo en Windows; el explorador
  propio ya cubre la operación.
