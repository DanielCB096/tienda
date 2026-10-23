# Respaldo y Restauración

## ¿Por qué es crítico?

En una arquitectura local-first con un solo dispositivo por tienda, **el respaldo es la única protección contra pérdida de datos**. Si la tablet se daña, se pierde, se robo o se formatea, los datos desaparecen si no hay respaldo.

## Estado de implementación (2026-10-23)

| Función | Estado |
|---------|--------|
| Crear respaldo manual y verificado | ✅ Implementado (`RespaldoService`, menú Configuración) |
| Respaldo automático antes de migrar | ✅ Implementado (`MigradorEsquema`) |
| Respaldo de seguridad antes de restaurar | ✅ Implementado (`RespaldoLocal.Restaurar`) |
| Listar respaldos e indicar el último | ✅ Implementado en consola |
| Restauración guiada con confirmación | ✅ Implementado (requiere escribir `RESTAURAR` y reiniciar) |
| Respaldo automático por intervalo configurado | ⛔ Pendiente: `RespaldoIntervaloDias` se guarda pero no se agenda |
| Copia a USB y nube personal | ⛔ Pendiente (Fase 2) |
| Cifrado de respaldos | ⛔ Pendiente (Fase 2) |
| Bitácora de respaldos | ⛔ Pendiente: existe `Bitacora`, pero las acciones de respaldo aún no se registran |

Formato real implementado: carpeta `respaldos/` junto a la base, archivos `tienda-yyyyMMdd-HHmmss[-etiqueta].db`, generados con `VACUUM INTO` y verificados con `PRAGMA integrity_check`.

## Estrategia de Respaldo

### 1. Respaldo Automático Programado

- El sistema creará una copia de la base de datos SQLite a intervalos configurables (pendiente de agendar).
- **Intervalo recomendado**: Cada 1 día (24 horas) o cada 5 ventas, el primero que ocurra.
- **Archivo generado**: `tienda-yyyyMMdd-HHmmss.db`.
- **Ubicación**: `respaldos/` junto al archivo de base de datos.
- **Indicador en UI**: La pantalla principal muestra: "Último respaldo: hace 2 días" o "⚠️ No hay respaldo hace 5 días (configurar intervalo)".

### 2. Respaldo Manual

- El usuario puede en cualquier momento seleccionar "Crear respaldo ahora".
- Útil antes de una actualización importante o si el día fue muy intenso.

### 3. Respaldo a Memoria USB / Dispositivo Externo

- El usuario conecta una memoria USB o un segundo dispositivo.
- El sistema copia el archivo `.db` y los `.copy` a la memoria externa.
- **Riesgo mitigado**: Si la tablet se daña, los datos quedan en el otro dispositivo.

### 4. Opcional: Google Drive Personal (costo $0)

- Si el dueño tiene una cuenta de Google Drive gratuita, el sistema puede subir el respaldo automáticamente.
- **Ventaja**: Los datos quedan en la nube personal del dueño, no en servidores de terceros.
- **Riesgo**: Requiere conectividad en el momento del respaldo. Si la tienda tiene internet intermitente, el respaldo se pospone y se hace manualmente después.

## Restauración (Recuperación de Datos)

### Cuándo se necesita

- La tablet se dañó, se perdió o se robó.
- Se formateó el dispositivo sin querer.
- La base de datos corrompió (raro en SQLite, pero posible por error de hardware).
- Se quiere migrar a una tablet nueva.

### Paso a Paso de Restauración

1. **Identificar el respaldo a usar**: El sistema muestra una lista de respaldos disponibles con fechas:
   - `tienda.localdb.copy` (más reciente automático)
   - `tienda.localdb.20261014.db` (fecha específica)
   - `tienda USB/respaldo 2026-10-10.db`

2. **Seleccionar respaldo**: El usuario elige cuál restaurar (generalmente el más reciente que tenga datos consistentes).

3. **Detener la aplicación**: Se cierra el sistema de forma controlada.

4. **Reemplazar el archivo**: El archivo `.db` actual se reemplaza por el archivo de respaldo seleccionado.

5. **Verificar integridad**: Al iniciar el sistema, este puede ejecutar un "check" básico (contar registros de productos, ventas, etc.) y avisar si el archivo parece vacío o corrupto.

6. **Reiniciar**: La aplicación inicia con los datos del respaldo.

### Herramienta de Verificación (opcional pero recomendada)

- El sistema podría ofrecer un botón "Verificar respaldo" que:
  - Intenta leer las tablas principales (Productos, Ventas, Clientes).
  - Informa: "El respaldo tiene X productos, Y ventas, Z mermas. ✓ Consistente" o ✗ "Archivo vacío o corrupto, elija otro respaldo".

## Migración a Otro Dispositivo

### Escenario

El dueño compra una tablet nueva y quiere pasar los datos de la antigua.

### Pasos

1. **En la tablet vieja**: Crear un respaldo manual y copiarlo a memoria USB o Google Drive.
2. **En la tablet nueva**: Instalar la aplicación.
3. **En la tablet nueva**: Usar la función "Restaurar desde archivo" y seleccionar el archivo de respaldo copiado.
4. **Verificar**: Revisar que los productos, ventas y configuración se ven correctamente.

### Consideraciones

- **Versiones de la app**: Si la app se actualizó entre la creación del respaldo y la restauración, la migración debe ser **versión compatible** (las migraciones de esquema versionadas manejan esto; ver sección de Migraciones a continuación).
- **Configuración**: Los datos de configuración de la tienda (logo, nombre, impuestos, métodos de pago) también se restauran ya que van en la misma base de datos.

## Migraciones de Esquema (Base de Datos) - Versionadas y Automáticas

### Contexto

A medida que el sistema evoluciona (nuevos módulos, nuevos campos), la estructura de la base de datos SQLite cambia. SQLite es flexible, pero hay que manejar los cambios ordenadamente.

### Estrategia

1. **Cada versión de la app lleva un número de versión** (ej. v1.0.0, v1.1.0, v2.0.0).
2. **Al iniciar la app, este compara la versión actual con la versión grabada en la base de datos**.
3. **Si la base de datos es antigua**, se ejecutan las migraciones necesarias en orden, de forma idempotente (pueden correrse una y otra vez sin dañar).

### Ejemplos de Migraciones Soportadas

- **Añadir una nueva tabla**: `CREATE TABLE IF NOT EXISTS nueva_tabla (...)`
- **Añadir un campo a una tabla existente**: `ALTER TABLE tabla ADD COLUMN nuevo_campo TIPO`
- **Crear un índice**: `CREATE INDEX IF NOT EXISTS idx_nombre ON tabla(nombre)`

### Flujo en una Actualización

1. **Usuario tiene la v1.0** y sus datos en `tienda.localdb`.
2. **Salen una actualización a v1.2**.
3. **Al abrir la app en la tablet**:
   - El sistema lee: "Versión actual: v1.2 | Versión en BD: v1.0".
   - Ejecuta migración v1.0 → v1.1 (ej. añade tabla de mermas).
   - Ejecuta migración v1.1 → v1.2 (ej. añade campo "límitede_credito" a clientes).
   - Guarda la versión v1.2 en la tabla de configuración.
   - ¡La app inicia con los datos antiguos más las nuevas estructuras!

### Respaldo Obligatorio Antes de Migrar

**NUNCA** se deben aplicar migraciones sin antes haber creado un respaldo. El sistema:

1. Detecta migraciones pendientes al iniciar y crea un respaldo etiquetado `antes-migrar`.
2. Verifica el respaldo con `PRAGMA integrity_check`; si no pasa, aborta sin aplicar cambios.
3. Ejecuta cada migración dentro de su propia transacción y actualiza `PRAGMA user_version`.
4. Verifica la integridad de la base al terminar.

Si una migración falla, se revierte esa migración, la base queda en la última versión aplicada y el mensaje indica la ruta del respaldo. En la interfaz de consola no hay cancelación interactiva: la migración ocurre al arrancar, antes de mostrar menús.

## Bitácora de Acciones de Respaldo

Cada operación de respaldo y restauración debe registrarse:

- **Tipo**: Automático, Manual, USB, Nube, Restauración.
- **Fecha y hora**: Cuándo ocurrió.
- **Ruta del archivo**: Dónde se guardó o de dónde se restauró.
- **Resultado**: Éxito, Advertencia (archivo bloqueado), Error (no se pudo guardar).
- **Usuario**: Quién o qué proceso lo triggered.

## Cifrado de Respaldo (Opcional)

- El usuario puede configurar una "Llave Maestra" para cifrar los archivos de respaldo.
- Los archivos `.copy` quedaría cifrados y solo serían restaurables con la llave correcta.
- **Recomendación**: Para el MVP, el cifrado es opcional. El enfoque "costo $0" sugiere que el respaldo simple (archivo copiado) es suficiente si el dispositivo está físicamente seguro.

## Checklist de Respaldo (para el dueño)

Mostrar en la app, tipo lista de verificación:

- [ ] ¿Cuándo fue el último respaldo? (hace X días)
- [ ] ¿El intervalo de respaldo es adecuado? (día sí, día no, o cada N ventas)
- [ ] ¿Has probado la restauración? (recomendado cada mes)
- [ ] ¿Tienes un respaldo en USB o nube personal?
- [ ] ¿La batería de la tablet está buena? (hacer respaldo cuando la tablet esté cargando)