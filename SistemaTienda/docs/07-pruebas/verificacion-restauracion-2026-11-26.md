# Verificación de restauración en Windows (2026-11-26)

Equipo: Windows 11, app de prueba `com.sistematienda.p3`, base nueva (sin usuarios).
Respaldo de origen: escenario `configurado` con 53 productos y tres usuarios.

## Flujo probado

1. Base P3 limpia: `usuarios=0`, tienda `Mi Tienda` (valor por defecto).
2. Se ejecutó el mismo servicio que usa el botón **Restaurar un respaldo** del asistente:
   `RespaldoService.RestaurarEnInstalacionNueva(respaldo, confirmado: true)`.
3. Resultado: éxito; se creó `tienda-<fecha>-antes-restaurar.db` y la base quedó con
   **3 usuarios**, tienda **Abarrotes La Esquina** y **53 productos**.
4. Se abrió la app: mostró el **login de Abarrotes La Esquina** con el encabezado
   POSale, no el asistente. Es el resultado esperado tras restaurar.

## Pendiente

- Pulsar el botón y elegir el archivo desde el selector de Windows/Android (la
  automatización de la ventana no logró abrir el selector en esta sesión).
- Probar la pantalla de arranque fallido con una base corrupta.
- Repetir con un respaldo `.db.stb` y la clave de 24 caracteres desde la interfaz.

El servicio y la apertura posterior quedan verificados en este equipo.
