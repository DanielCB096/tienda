# Coordinación entre agentes

Actualizado: 2026-10-23. Tablero único: cualquier IA o persona que trabaje en este repositorio debe reclamar su tarea aquí antes de empezar.

Instrucciones generales: [AGENTS.md](../../AGENTS.md).

## Convenciones

- Estados: `Libre`, `Tomada`, `En revisión`, `Hecho`, `Bloqueada`.
- Una tarea = una rama = un PR. No se trabaja directo en `main`.
- Actualiza tu fila al empezar y al terminar; incluye rama y hash del commit.
- Si dos tareas tocan los mismos archivos, se serializan.
- No borres filas del registro de sesiones; agrega una nueva.
- No edites la fila de otro agente sin acordarlo.

## Archivos calientes (un solo agente a la vez)

| Archivo | Motivo |
|---------|--------|
| `SistemaTienda/SistemaTienda.Infrastructure/Database.cs` | Esquema, migraciones y acceso; casi todo lo toca |
| `SistemaTienda/SistemaTienda.Presentation/Program.cs` | Menús de consola en un solo archivo |
| `SistemaTienda/SistemaTienda.sln` | Altas/bajas de proyectos |

Para cambios de esquema, T1 (migraciones) va primero. T2 (ventas) y T3 (centavos) **no** pueden ejecutarse en paralelo.

## En curso

| Tarea | Agente | Rama | Archivos reservados | Inicio | Estado |
|-------|--------|------|---------------------|--------|--------|
| T8 | Cobertura adicional de pruebas | DeepSeek | test/cobertura-base | SistemaTienda.Tests/ (solo archivos nuevos) | 2026-10-23 | En revisión (commit fd879cc).<br>**Notas:** `AjustarStock` no valida que el producto exista: con stock mayor a cero lanza excepción por llave foránea y con stock cero reporta éxito; se permiten PIN duplicados y `VerificarAcceso` devuelve el primer usuario por nombre, lo que puede asignar el rol equivocado; `AgregarGasto` no recorta el concepto y lo agrupa distinguiendo mayúsculas y espacios; la búsqueda por nombre no ignora mayúsculas en acentos. |

## Tareas

| ID | Tarea | Estado | Depende de | Notas |
|----|-------|--------|------------|-------|
| T1 | Migraciones versionadas + respaldo | Libre | — | Prioridad alta. Reserva `Database.cs`. |
| T2 | Ventas / POS | Libre | T1 recomendado | Reserva `Database.cs` y `Program.cs`. |
| T3 | Dinero en centavos | Libre | T1 | Cross-cutting; no simultáneo con T2. |
| T4 | Bitácora y permisos en servicios | Libre | — | Toca `Application` e `Infrastructure`. |
| T5 | Reportes de utilidad | Bloqueada | T2 | — |
| T6 | Importación Excel/CSV | Libre | T2 recomendado | Dependencia nueva requiere ADR. |
| T7 | Interfaz definitiva | Bloqueada | ADR | Primero el ADR de UI. |

## Plantilla para reclamar

Copia esta fila en **En curso** y complétala:

```
| T2 | Ventas / POS | <agente> | feat/ventas-pos | Database.cs, Program.cs, Core/, Application/VentaService.cs | 2026-10-23 | Tomada |
```

Al terminar, mueve la fila a `Hecho` (o elimínala de En curso) y registra el commit en la tabla de sesiones.

## Registro de sesiones

| Fecha | Agente | Rama | Resultado | Commit |
|-------|--------|------|-----------|--------|
| 2026-10-23 | Qoder (sesión inicial) | feat/base-compilable | Solución compilable, 59 pruebas, documentación y push a GitHub | 35c5524 |
