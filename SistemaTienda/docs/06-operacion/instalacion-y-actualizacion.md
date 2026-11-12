# Instalación y Actualización

## Instalación Inicial

### Requisitos Previos

- **Dispositivo**: Tablet con Windows 10/11 o Android 10+ recomendado.
- **Almacenamiento**: Mínimo 50 MB libres (para la app + base de datos inicial).
- **Permisos**: Acceso a almacenamiento interno, cámara (opcional, para fotos de mermas/facturas), entrada de código de barras (USB o Bluetooth).

### Pasos de Instalación en Windows

Todavía no hay instalador ni release publicados. Por ahora la app se compila y ejecuta desde el repositorio:

1. Instalar el SDK de .NET 10 y las cargas de trabajo: `dotnet workload install maui-windows`.
2. Compilar: `dotnet build SistemaTienda/SistemaTienda.Maui/SistemaTienda.Maui.csproj -f net10.0-windows10.0.19041.0`.
3. Ejecutar: `dotnet run --project SistemaTienda/SistemaTienda.Maui/SistemaTienda.Maui.csproj -f net10.0-windows10.0.19041.0`.
4. **Primera ejecución**: la app crea la base automáticamente, sin preguntar, en `%LOCALAPPDATA%\SistemaTienda\com.sistematienda.tienda\Data\tienda.db`, y abre el asistente.

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

Todavía no hay canal de releases ni verificación automática de versiones. Por ahora la actualización se hace reemplazando la build:

1. **Crear respaldo manual** desde Ajustes antes de actualizar (recomendado aunque la migración también respalde).
2. **Compilar o instalar la versión nueva** sobre la anterior. En Android, instalar el APK nuevo conserva los datos de la app.
3. **Aplicación migra la BD**: al iniciar, el runner compara `PRAGMA user_version` con la versión de la app y aplica las migraciones pendientes una por una.
4. **Respaldo previo automático**: antes de la primera migración se crea un respaldo `antes-migrar` verificado en la carpeta `respaldos`.
5. **Si una migración falla**: se revierte esa migración, la app no arranca el sistema y muestra la página de error con las rutas de la base y los respaldos. El respaldo previo queda disponible para restaurar.

### Migraciones de Base de Datos en Actualizaciones

- **Versión a versión**: las migraciones viven en `SistemaTienda.Infrastructure/Migraciones.cs` y se aplican en orden.
- **Ejemplo**: la v4 convirtió los importes a centavos y la v6 agregó `AnulacionesVenta`; una base vieja recibe todas las migraciones pendientes al abrir la app.
- **Si falla la migración**: se revierte esa migración y la app queda detenida con un mensaje; no se pierde el respaldo anterior.

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
- **Solución**: la app muestra una página de error con la ruta de la base y de los respaldos. Restaura un respaldo desde la consola (Configuración → Restaurar respaldo); la app MAUI todavía no tiene restauración. Si se reinstala, se crea una base nueva y se pierden los datos sin respaldo.

### "No hay suficiente espacio"

- **Causa**: La base de datos creció mucho por años de historial.
- **Solución**: Archivar historiales antiguos (mover datos de años atrás a un archivo `.db` de solo lectura) o limpiar mermas/ventas viejas según política de la tienda.

### "Olvidé mi PIN"

- **Solución**: El dueño (admin) puede restablecer el PIN desde una opción de emergencia (requiere validación de identidad, o responder una pregunta de seguridad configurada previamente). En el modelo "costo $0", esto se hace manualmente o mediante contacto con el desarrollador.

### "No puedo abrir el archivo de respaldo"

- **Causa**: El archivo está dañado o es de una versión muy antigua incompatble.
- **Solución**: Verificar que se esté usando la versión correcta de la app. Si el respaldo es muy antiguo, puede que ya no sea compatible y sea necesario el último respaldo automático.