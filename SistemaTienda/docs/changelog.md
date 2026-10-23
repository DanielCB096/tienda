# Registro de Cambios (Changelog)

## Estructura

Cada entrada tiene formato:
- **vX.Y.Z - YYYY-MM-DD**: Nombre de la funcionalidad o corrección.
  - *Agregado*: Lo nuevo incorporado.
  - *Modificado*: Lo que cambió de versiones anteriores.
  - *Corregido*: Bugs solucionados.
  - *Eliminado*: Lo que se removió.

## Versionado

- **Mayor (X)**: Cambios incompatibles en la API o arquitectura.
- **Menor (Y)**: Nueva funcionalidad sin romper lo existente.
- **Parche (Z)**: Correcciones de errores, seguridad o rendimiento.

---

## Próxima Versión Planificada: v0.1.0-MVP

Próximamente después de completar los módulos críticos.

### [En desarrollo] v0.1.0-base

- Estructura de solución y proyectos (.NET 8).
- Base de datos SQLite con tablas iniciales (Productos, Ventas, Clientes, Proveedores).
- Capa de servicios de inventario y ventas.
- Interfaz de menú principal y catálogo.
- Sistema de PIN y roles de usuario.
- Respaldo y restauración manual.
- Configuración básica de tienda (nombre, giro, impuestos, métodos de pago).
- Documentación inicial (glosario, stack, módulos, ADR).

### v0.0.2 - 2026-10-23: Base compilable, segura y con pruebas

- *Agregado*: proyecto `SistemaTienda.Tests` (xUnit, 59 pruebas) sobre base SQLite temporal por prueba.
- *Agregado*: entidades `ClienteFiado` y `Proveedor` en Core; `ProcessResult` y `ProcessResult<T>`.
- *Agregado*: historial de inventario transaccional con cantidad firmada y caché de stock.
- *Agregado*: hash PBKDF2-SHA256 (100 000 iteraciones, salt de 16 bytes) para PIN y validación de roles.
- *Agregado*: variables de entorno `SISTEMATIENDA_DB` para elegir la base de datos.
- *Agregado*: `.gitignore` para binarios, bases locales y respaldos.
- *Modificado*: la solución .NET referencia correctamente los cuatro proyectos.
- *Modificado*: el esquema crea `ConfiguracionTienda`, `UsuariosSistema`, `Productos`, `MovimientoInventario`, `Gastos`, `ClientesFiado` y `Proveedores` (`PRAGMA user_version = 1`).
- *Modificado*: la interfaz de consola solo muestra funciones implementadas; ventas/POS se marca como pendiente.
- *Modificado*: clientes fiado ya no guardan RFC/cédula; se guarda teléfono.
- *Corregido*: el proyecto no compilaba por GUIDs de solución inválidos, referencias a proyectos inexistentes, falta de `Microsoft.Data.Sqlite`, namespaces ausentes, entidades duplicadas y una firma de `Main` incorrecta.
- *Corregido*: la detección de primera ejecución ahora usa el conteo de usuarios, no la versión de configuración.
- *Eliminado*: `Entities.cs` con entidades duplicadas.

### Próximas versiones (Fase 2 y siguientes)

- Reportes y utilidad completos.
- Asistente de primera configuración mejorado.
- Migraciones de esquema versionadas.
- Distribución optimizada (.exe, .apk, PWA).