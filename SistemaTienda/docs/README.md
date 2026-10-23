# Documentación — Sistema de Gestión para Tiendas Pequeñas

Sistema local-first para tiendas pequeñas en México: costo recurrente $0, 100 % offline, un dispositivo por tienda.

## Estado del proyecto

La base técnica está **compilando y con pruebas automatizadas**. El núcleo implementado cubre:

- Configuración inicial de la tienda y usuarios con PIN hasheado (PBKDF2-SHA256).
- Catálogo de productos e inventario por movimientos (entradas, ventas, mermas, ajustes).
- Gastos simples y su totalización.
- Clientes con fiado (saldo y abonos) y proveedores (saldo y pagos).
- Interfaz de consola provisional.

Pendiente para el MVP: ventas/POS, corte de caja, respaldo/restauración, reportes de utilidad, migraciones versionadas, bitácora y la interfaz definitiva (ADR pendiente). Ver [roadmap.md](roadmap.md) y [deuda-tecnica.md](deuda-tecnica.md).

## Índice

| Documento | Contenido |
|-----------|-----------|
| [00-contexto/glosario.md](00-contexto/glosario.md) | Términos del dominio |
| [01-arquitectura/stack-tecnologico.md](01-arquitectura/stack-tecnologico.md) | Stack y restricciones técnicas |
| [01-arquitectura/modelo-de-datos.md](01-arquitectura/modelo-de-datos.md) | Esquema SQLite vigente |
| [02-modulos/resumen.md](02-modulos/resumen.md) | Mapa de módulos y prioridad |
| [02-modulos/modulo-configuracion.md](02-modulos/modulo-configuracion.md) | Configuración y usuarios |
| [02-modulos/modulo-inventario.md](02-modulos/modulo-inventario.md) | Catálogo e inventario |
| [03-procesos/cliente-fiado.md](03-procesos/cliente-fiado.md) | Fiado y abonos |
| [03-procesos/proveedores.md](03-procesos/proveedores.md) | Proveedores y cuentas por pagar |
| [03-procesos/venta.md](03-procesos/venta.md) | Venta (diseño, sin implementar) |
| [03-procesos/corte-caja.md](03-procesos/corte-caja.md) | Corte de caja (diseño) |
| [03-procesos/respaldo-restauracion.md](03-procesos/respaldo-restauracion.md) | Respaldo y restauración (diseño) |
| [05-decisiones/ADR-stack.md](05-decisiones/ADR-stack.md) | Decisión de stack |
| [06-operacion/instalacion-y-actualizacion.md](06-operacion/instalacion-y-actualizacion.md) | Instalación |
| [07-pruebas/plan-de-pruebas.md](07-pruebas/plan-de-pruebas.md) | Estrategia y cobertura de pruebas |
| [changelog.md](changelog.md) | Cambios por versión |
| [roadmap.md](roadmap.md) | Avance y siguiente trabajo |
| [deuda-tecnica.md](deuda-tecnica.md) | Atajos y pendientes técnicos |

## Inicio rápido

Requisitos: SDK de .NET 8 o superior (`dotnet --version`).

```bash
# Compilar la solución
dotnet build SistemaTienda/SistemaTienda.sln

# Ejecutar pruebas
dotnet test SistemaTienda/SistemaTienda.sln

# Ejecutar la aplicación de consola
dotnet run --project SistemaTienda/SistemaTienda.Presentation/Presentation.csproj
```

La base de datos se crea en `%LOCALAPPDATA%\SistemaTienda\tienda.db`. Para usar otra ruta:

```bash
SISTEMATIENDA_DB=/ruta/a/tienda.db dotnet run --project SistemaTienda/SistemaTienda.Presentation/Presentation.csproj
```

El primer arranque ejecuta el asistente de configuración y solicita el PIN del dueño.

## Reglas que rigen el proyecto

Ver [CLAUDE-tienda.md](../CLAUDE-tienda.md). Las más relevantes para entender el código:

- El stock es la suma de movimientos; nunca se edita un número de stock directamente.
- Operaciones de inventario atómicas (transacción e inserción de movimiento juntas).
- El historial no se edita ni se borra; se corrige con movimientos nuevos.
- PIN nunca en texto plano.
- 100 % offline, sin servicios de pago.
- Datos personales mínimos de clientes (nombre y teléfono).
