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
| T8 | Corte de caja | Qoder | feat/corte-caja | `Database.cs`, `Program.cs`, `Migraciones.cs`, `Core/CorteCaja.cs`, `Application/CorteCajaService.cs`, `ConfiguracionService.cs`, pruebas de corte | 2026-10-23 | Tomada |

## Hecho

| Tarea | Agente | Rama | Resultado | Commit |
|-------|--------|------|-----------|--------|
| T1 | Qoder | feat/migraciones-respaldo | Runner de migraciones con respaldo previo, respaldo/restauración local, menú de consola, 9 pruebas y CI | 8ead191, 54901bd, 50b35be |
| T2 | Qoder | feat/ventas-pos | Esquema v2 (Ventas/VentaLineas), venta atómica multi-línea, pagos efectivo/tarjeta/mixto/fiado, comisión, menú POS y 13 pruebas | 040e77d, d28c223 |

## Tareas

| ID | Tarea | Estado | Depende de | Notas |
|----|-------|--------|------------|-------|
| T1 | Migraciones versionadas + respaldo | Hecho | — | En `feat/migraciones-respaldo`. `Database.cs` queda libre para T2. |
| T2 | Ventas / POS | Hecho | T1 | En `feat/ventas-pos` (parte de `feat/migraciones-respaldo`). `Database.cs` y `Program.cs` quedan libres. |
| T3 | Dinero en centavos | Libre | T1, T2 | Cross-cutting; toca todas las columnas de dinero y las ventas existentes. |
| T4 | Bitácora y permisos en servicios | Libre | — | Toca `Application` e `Infrastructure`. |
| T5 | Reportes de utilidad | Bloqueada | T2 | — |
| T6 | Importación Excel/CSV | Libre | T2 recomendado | Dependencia nueva requiere ADR. |
| T7 | Interfaz definitiva | Bloqueada | ADR | Primero el ADR de UI. |
| T8 | Corte de caja | Tomada | T2 | Esquema v3: cortes, movimientos de caja y abonos con historial. En `feat/corte-caja`. |

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
| 2026-10-23 | Qoder | feat/migraciones-respaldo | T1 terminado: migraciones v1→N, respaldo/restauración local, 68 pruebas y CI en GitHub Actions | 8ead191..HEAD |
| 2026-10-23 | Qoder | feat/ventas-pos | T2 terminado: esquema v2, venta atómica, pagos y fiado, menú POS, 81 pruebas en CI | 040e77d, d28c223 |
