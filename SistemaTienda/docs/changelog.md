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

### Próximas versiones (Fase 2 y siguientes)

- Reportes y utilidad completos.
- Asistente de primera configuración mejorado.
- Migraciones de esquema versionadas.
- Distribución optimizada (.exe, .apk, PWA).