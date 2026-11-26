# Instalación y Actualización

## Instalación Inicial

### Requisitos Previos

- **Dispositivo**: Tablet con Windows 10/11 o Android 10+ recomendado.
- **Almacenamiento**: Mínimo 50 MB libres (para la app + base de datos inicial).
- **Permisos**: Acceso a almacenamiento interno, cámara (opcional, para fotos de mermas/facturas), entrada de código de barras (USB o Bluetooth).

### Pasos de Instalación en Windows

El paquete de Windows (H5) se arma con `SistemaMaestro/scripts/h5-empaquetar.ps1` y se instala con `h5-instalar.ps1`. Cada paquete trae la app publicada y un `manifiesto.json` con la versión de la app y la versión de esquema que espera.

1. **Armar el paquete** (en el equipo de desarrollo):

   ```powershell
   powershell -ExecutionPolicy Bypass -File SistemaMaestro\scripts\h5-empaquetar.ps1 -Version 1.1.0
   ```

   Genera `dist/POSale-1.1.0-win-x64.zip` con la app autocontenida (no requiere instalar .NET), el manifiesto y un `LEEME.txt`. El empaquetador verifica que el paquete incluya el runtime antes de comprimirlo.
2. **Instalar** (en el equipo de la tienda, sin internet):

   ```powershell
   powershell -ExecutionPolicy Bypass -File h5-instalar.ps1 -Paquete POSale-1.1.0-win-x64.zip
   ```

   La app queda en `%LOCALAPPDATA%\SistemaTienda\app\versiones\<versión>` y el puntero en `app\actual.json`. Los datos **no** viven ahí y no se tocan.
3. **Abrir la app** desde la carpeta de la versión instalada.
4. **Primera ejecución**: la app crea la base automáticamente en `%LOCALAPPDATA%\SistemaTienda\com.sistematienda.tienda\Data\tienda.db` y abre el asistente.

Para desarrollo también se puede compilar y ejecutar desde el repositorio con `dotnet build` y `dotnet run`.

### Pasos de Instalación en Android

Todavía no se publica un APK. La app compila en GitHub Actions y está pendiente probarla en un dispositivo real.

1. **Compilar el APK**: `dotnet build SistemaTienda/SistemaTienda.Maui/SistemaTienda.Maui.csproj -f net10.0-android` (requiere Android SDK y JDK 17), o descargar el artefacto del job `app-maui`.
2. **Permitir fuentes desconocidas**: Configurar la tablet para instalar apps de fuera de Google Play.
3. **Instalar**: Tocar el archivo `.apk`.
4. **Permisos**: la app no pide red ni almacenamiento externo; la base y los respaldos viven en su almacenamiento privado.

### Asistente de Primera Configuración (Onboarding)

Al primera vez que la app se abre, se muestra una serie de pasos guiados para poner la tienda en funcionamiento:

1. **Nombre de la tienda**: "Mi Abarrotería", "La Central", etc.
2. **Giro de negocio**: Abarrotes, Ropa, Papelería, Ferretería, etc. (solo etiqueta, no afecta la lógica).
3. **Impuesto predeterminado**: tasa en porcentaje (16.00 por defecto).
4. **Comisión de tarjeta**: % que cobra la terminal (3.50 por defecto). Afecta la utilidad neta.
5. **Fondo de caja inicial**: efectivo con el que se inicia cada día (200.00 por defecto).
6. **Fiado**: activar o desactivar y, si se activa, el límite de crédito por cliente (0 = sin límite).
7. **Respaldo automático**: días entre respaldos (1 por defecto; 0 desactiva).
8. **PIN del dueño**: se captura dos veces; se guarda hasheado con PBKDF2-SHA256.

Al finalizar el asistente, la app lleva al login para que el dueño entre con su PIN.

## Actualización de la Aplicación

### ¿Cómo se actualiza sin internet?

El sistema soporta actualizaciones **offline**: el nuevo archivo de la aplicación viene incluido en el paquete de instalación. No se necesita descargar nada en caliente.

### Pasos para Actualizar

1. **Crear respaldo manual** desde Ajustes antes de actualizar (recomendado, aunque la actualización también respalde).
2. **Instalar el paquete nuevo** con `h5-instalar.ps1 -Paquete <zip>`. La versión anterior se conserva en `app\versiones` y el puntero `actual.json` cambia solo cuando la copia terminó.
3. **La app actualiza la base al arrancar**: `ActualizadorEsquema` compara `PRAGMA user_version` con el esquema que declara el manifiesto y aplica las migraciones pendientes.
4. **Respaldo previo automático**: antes de migrar se crea un respaldo `antes-migrar` verificado en `respaldos`.
5. **Arranque de prueba**: al terminar se comprueba integridad, claves foráneas, tablas esenciales y una escritura que se revierte.
6. **Si una migración o la verificación falla**: se restaura automáticamente el respaldo y la versión anterior de los datos sigue operativa. La app muestra el error con la ruta del respaldo.
7. **Si la app nueva no arranca**: `h5-instalar.ps1 -Revertir` vuelve a la carpeta de la versión anterior. Los datos siguen intactos.

### Actualización interrumpida

- El paquete se copia primero a `versiones\<versión>.tmp` y se renombra al final. Si la copia se corta, la versión anterior sigue siendo la activa.
- Si la luz se corta durante la migración, SQLite aplica su diario y el siguiente arranque vuelve a entrar por el actualizador; el respaldo `antes-migrar` permite restaurar.
- El manifiesto declara el esquema esperado. Si la base quedó más nueva que la app, la app se niega a abrirla y pide instalar la versión correspondiente; no la degrada.
- Verificado el 2026-11-25: el ZIP real 1.1.0 se instaló en una carpeta temporal, la app autocontenida arrancó y creó su base, se actualizó a un 1.1.1 y se revirtió a 1.1.0 con ambas carpetas conservadas.

### Migraciones de Base de Datos en Actualizaciones

- **Versión a versión**: las migraciones viven en `SistemaTienda.Infrastructure/Migraciones.cs` y se aplican en orden.
- **Ejemplo**: la v4 convirtió los importes a centavos y la v6 agregó `AnulacionesVenta`; una base vieja recibe todas las migraciones pendientes al abrir la app.
- **Si falla la migración o la verificación**: se restaura el respaldo `antes-migrar` y la base queda en la versión anterior; la app muestra el motivo y la ruta del respaldo.
- **Filtros por día**: las consultas usan `date(Fecha)` sobre la fecha local guardada. La prueba `VentasDelDia_RespetaElDiaLocalEnLosBordes` fija que una venta de las 23:59 pertenece a su día y no al siguiente.
- **CSV seguro**: los campos de texto libre que empiezan con `=`, `+`, `-`, `@`, tabulador o retorno se exportan con apóstrofo para que Excel no ejecute fórmulas.

### Desinstalación y Pérdida de Datos

- **Ojo**: desinstalar la app en Windows elimina los archivos de programa, pero la base de datos queda en `%LOCALAPPDATA%\SistemaTienda\com.sistematienda.tienda\Data\tienda.db` hasta que se borre manualmente esa carpeta. En Android, desinstalar la app borra sus datos privados.
- **Recomendación**: antes de desinstalar, hacer un respaldo manual y copiar la carpeta `respaldos` a una USB. La eliminación completa se hace desde el explorador de archivos borrando la carpeta de datos.

## Distribución a Múltiples Negocios

### Un Solo Dispositivo por Tienda

- El modelo es **una instalación = un negocio**.
- No hay funcionalidad de "multi-tienda" en un mismo dispositivo (eso requeriría una base de datos separada por negocio, lo cual complica el modelo local-first).
- Si el dueño tiene 2 tiendas, necesitan 2 tablets (o 2 particiones en la misma tablet, pero eso es avanzado para el MVP).

### Mismo Sistema, Diferentes Tabletas

- Cada tablet tiene su propia base `tienda.db` independiente.
- Los reportes y análisis se pueden hacer reuniendo los datos (exportando a Excel y sumando), pero no hay nube que los una automáticamente.
- **Ventaja**: Privacidad total (LFPDPPP), no hay mezclas de datos de un negocio con otro.

### Actualizaciones Coordinadas

- El desarrollador lanza una nueva versión. El dueño de cada negocio la actualiza en su propia tabla de forma independiente.
- No hay riesgo de que una actualización afecte a otro negocio.

## Solución de Problemas Comunes

### "La app se cierra al iniciar"

- **Causa**: Base de datos corrupta o versión de BD incompatible.
- **Solución**: si la falla ocurrió al actualizar, la app ya restauró el respaldo `antes-migrar`; revisa el mensaje de error. Si no, usa **Restaurar un respaldo** en la pantalla de error, en el asistente de instalación nueva o en **Ajustes** con sesión. También puedes volver a la versión anterior con `h5-instalar.ps1 -Revertir`. Si reinstalas sin restaurar, se crea una base nueva y se pierden los datos sin respaldo.

### "No hay suficiente espacio"

- **Causa**: La base de datos creció mucho por años de historial.
- **Solución**: Archivar historiales antiguos (mover datos de años atrás a un archivo `.db` de solo lectura) o limpiar mermas/ventas viejas según política de la tienda.

### "Olvidé mi PIN"

- **Solución**: El dueño (admin) puede restablecer el PIN desde una opción de emergencia (requiere validación de identidad, o responder una pregunta de seguridad configurada previamente). En el modelo "costo $0", esto se hace manualmente o mediante contacto con el desarrollador.

### "No puedo abrir el archivo de respaldo"

- **Causa**: El archivo está dañado o es de una versión muy antigua incompatble.
- **Solución**: Verificar que se esté usando la versión correcta de la app. Si el respaldo es muy antiguo, puede que ya no sea compatible y sea necesario el último respaldo automático.