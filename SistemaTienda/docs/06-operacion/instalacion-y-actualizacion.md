# Instalación y Actualización

## Instalación Inicial

### Requisitos Previos

- **Dispositivo**: Tablet con Windows 10/11 o Android 10+ recomendado.
- **Almacenamiento**: Mínimo 50 MB libres (para la app + base de datos inicial).
- **Permisos**: Acceso a almacenamiento interno, cámara (opcional, para fotos de mermas/facturas), entrada de código de barras (USB o Bluetooth).

### Pasos de Instalación en Windows

1. **Descargar el instalador**: El archivo `.exe` se entrega como release en GitHub (o GitHub Pages para PWA).
2. **Ejecutar el instalador**: Doble clic en `SistemaTienda.Setup.exe` (o similar).
3. **Seleccionar carpeta**: La aplicación se instala en `C:\Program Files\SistemaTienda\` o la carpeta que el usuario elija.
4. **Primera ejecución**:
   - La app detecta que no hay base de datos local.
   - El sistema pregunta: "¿Crear nueva base de datos?"
   - El usuario confirma.
   - Se crea `tienda.localdb` en la carpeta de la app o en `Documentos\SistemaTienda`.
   - Se genera la configuración inicial (ver asistente a continuación).

### Pasos de Instalación en Android

1. **Descargar el APK**: Desde la sección de Releases en GitHub.
2. **Permitir fuentes desconocidas**: Configurar la tablet para instalar apps de fuera de Google Play (configuración de seguridad).
3. **Instalar**: Tocar el archivo `SistemaTienda.apk`.
4. **Permisos**: La primera vez, la app solicita:
   - Acceso al almacenamiento (para guardar la base de datos y respaldos).
   - Cámara (opcional, para fotos).
   - Escaneo de código de barras (si se usa un lector externo Bluetooth/USB).

### Asistente de Primera Configuración (Onboarding)

Al primera vez que la app se abre, se muestra una serie de pasos guiados para poner la tienda en funcionamiento:

1. **Nombre de la tienda**: "Mi Abarrotería", "La Central", etc.
2. **Giro de negocio**: Abarrotes, Ropa, Papelería, Ferretería, etc. (solo etiqueta, no afecta la lógica).
3. **Configuración de impuestos**: 
   - ¿Usar IVA? (Sí/No).
   - Tasa de IVA: 16% (México), 0% (exentos), u otro configurado.
   - Permitir configurar productos con impuesto 0% o exentos.
4. **Métodos de pago**: Activar/desactivar Efectivo, Tarjeta, Mixto, Fiado.
5. **Comisión de tarjeta**: Si se usa tarjeta, el % que cobra la terminal (ej. 3.5%). Esto afecta la utilidad neta.
6. **Fondo de caja inicial**: El monto de efectivo con el que se inicia cada día.
7. **PIN de acceso**: El dueño crea un PIN de 4-6 dígitos para acceder al sistema.
8. **Respaldo inicial**: El sistema sugiere crear el primer respaldo manual.

Al finalizar el asistente, la app lleva al usuario a la pantalla principal (catálogo/venta).

## Actualización de la Aplicación

### ¿Cómo se actualiza sin internet?

El sistema soporta actualizaciones **offline**: el nuevo archivo de la aplicación viene incluido en el paquete de instalación. No se necesita descargar nada en caliente.

### Pasos para Actualizar

1. **Verificar disponible**: En la pantalla de configuración o al iniciar, la app comprueba si hay una versión nueva disponible (comparando la versión local con la versión de los archivos de instalación).
2. **Descargar release**: El dueño o el desarrollador descarga la nueva versión desde GitHub Releases (archivo `.exe` o `.apk`).
3. **Crear respaldo OBLIGATORIO**: Antes de actualizar, el sistema obliga a crear un respaldo (ver sección de Respaldo y Restauración). El usuario debe confirmar.
4. **Ejecutar instalador nuevo**: 
   - **Windows**: Ejecutar el nuevo `.exe`. El instalador suele reemplazar los archivos viejos.
   - **Android**: Instalar el nuevo APK sobre el existente (las apps modernas conservan los datos de la base de datos SQLite).
5. **Aplicación migra la BD**: Al iniciar la app actualizada:
   - El sistema compara versiones: "BD v1.0 → App v1.3".
   - Ejecuta automáticamente las migraciones de esquema necesarias (añadir nuevas tablas o campos).
   - ¡Listo! El usuario tiene la versión nueva con sus datos intactos.

### Migraciones de Base de Datos en Actualizaciones

- **Versión a versión**: Cada release incluye un archivo de migraciones (scripts SQL leves).
- **Ejemplo**: Si la v1.1 añadió la tabla de mermas, la v1.2 añade el campo `motivo` a esa tabla, la actualización aplica ambos cambios automáticamente.
- **Si falla la migración**: El sistema vuelve a la versión anterior y muestra un error al usuario. Se recomienda tener el respaldo reciente.

### Desinstalación y Pérdida de Datos

- **Ojo**: Desinstalar la aplicación en Windows elimina los archivos de programa, pero la base de datos `tienda.localdb` puede quedarse en la carpeta de datos si el usuario no la elimina manualmente.
- **Recomendación**: Antes de desinstalar, siempre hacer un respaldo manual. La eliminación completa debe hacerse desde el explorador de archivos borrando la carpeta completa de la aplicación y la base de datos.

## Distribución a Múltiples Negocios

### Un Solo Dispositivo por Tienda

- El modelo es **una instalación = un negocio**.
- No hay funcionalidad de "multi-tienda" en un mismo dispositivo (eso requeriría una base de datos separada por negocio, lo cual complica el modelo local-first).
- Si el dueño tiene 2 tiendas, necesitan 2 tablets (o 2 particiones en la misma tablet, pero eso es avanzado para el MVP).

### Mismo Sistema, Diferentes Tabletas

- Cada tablet tiene su propia `tienda.localdb` independiente.
- Los reportes y análisis se pueden hacer reuniendo los datos (exportando a Excel y sumando), pero no hay nube que los una automáticamente.
- **Ventaja**: Privacidad total (LFPDPPP), no hay mezclas de datos de un negocio con otro.

### Actualizaciones Coordinadas

- El desarrollador lanza una nueva versión. El dueño de cada negocio la actualiza en su propia tabla de forma independiente.
- No hay riesgo de que una actualización afecte a otro negocio.

## Solución de Problemas Comunes

### "La app se cierra al iniciar"

- **Causa**: Base de datos corrupta o versión de BD incompatibles.
- **Solución**: Restablecer a respaldo anterior (menú de configuración → "Restaurar base de datos") o reinstalar la app (lo cual creará una BD nueva vacía, se perderán los datos viejos si no hay respaldo).

### "No hay suficiente espacio"

- **Causa**: La base de datos creció mucho por años de historial.
- **Solución**: Archivar historiales antiguos (mover datos de años atrás a un archivo `.db` de solo lectura) o limpiar mermas/ventas viejas según política de la tienda.

### "Olvidé mi PIN"

- **Solución**: El dueño (admin) puede restablecer el PIN desde una opción de emergencia (requiere validación de identidad, o responder una pregunta de seguridad configurada previamente). En el modelo "costo $0", esto se hace manualmente o mediante contacto con el desarrollador.

### "No puedo abrir el archivo de respaldo"

- **Causa**: El archivo está dañado o es de una versión muy antigua incompatble.
- **Solución**: Verificar que se esté usando la versión correcta de la app. Si el respaldo es muy antiguo, puede que ya no sea compatible y sea necesario el último respaldo automático.