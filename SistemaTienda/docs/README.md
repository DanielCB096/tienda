# Documentación — Sistema de Gestión para Tiendas Pequeñas

Sistema local-first para tiendas pequeñas en México: costo recurrente $0, 100 % offline, un dispositivo por tienda.

## Estado del proyecto

La base técnica está **compilando y con pruebas automatizadas**. El núcleo implementado cubre:

- Configuración inicial de la tienda y usuarios con PIN hasheado (PBKDF2-SHA256).
- Catálogo de productos e inventario por movimientos (entradas, ventas, mermas, ajustes).
- Gastos simples y su totalización.
- Clientes con fiado (saldo y abonos con historial) y proveedores (saldo y pagos).
- Ventas / POS: venta atómica multi-línea con costo y precio congelados, pagos en efectivo, tarjeta (comisión), mixto y fiado, ticket de texto y anulación completa con devolución de stock y reverso de fiado.
- Corte de caja: periodo desde el corte anterior, efectivo esperado, diferencia con motivo obligatorio, ingresos/retiros e historial.
- Reporte de utilidad por periodo: ventas, costo congelado, comisión de tarjeta, mermas y gastos.
- Bitácora inmutable de operaciones sensibles (antes/después, usuario y motivo) y permisos validados en la capa de aplicación.
- Migraciones de esquema versionadas (`PRAGMA user_version`) y respaldo/restauración local verificado.
- Respaldo automático por intervalo configurado al iniciar y cerrar la aplicación, con aviso y bitácora.
- Dinero en centavos enteros (`long`/`INTEGER`, esquema v4) con conversión y formato centralizados en `Dinero`.
- App MAUI para Windows y Android con asistente de primera configuración, login por PIN, tablero (Inicio, Vender, Ventas, Caja y Ajustes), punto de venta con carrito y cobro, ticket interno compartible, respaldo manual y automático, y cierre de sesión. Caja sigue siendo de solo lectura.
- Interfaz de consola provisional para las operaciones que todavía no están en la app (anular ventas, cerrar corte, inventario, clientes y proveedores).

Pendiente para el MVP: cierre de corte en MAUI, anulación desde la app, ticket con QR, copia de respaldos a USB/nube, conteo de caja por denominaciones y validación de Android en dispositivo. Ver [roadmap.md](roadmap.md) y [deuda-tecnica.md](deuda-tecnica.md).

## Índice

| Documento | Contenido |
|-----------|-----------|
| [00-contexto/glosario.md](00-contexto/glosario.md) | Términos del dominio |
| [01-arquitectura/stack-tecnologico.md](01-arquitectura/stack-tecnologico.md) | Stack y restricciones técnicas |
| [01-arquitectura/modelo-de-datos.md](01-arquitectura/modelo-de-datos.md) | Esquema SQLite vigente |
| [02-modulos/resumen.md](02-modulos/resumen.md) | Mapa de módulos y prioridad |
| [02-modulos/modulo-configuracion.md](02-modulos/modulo-configuracion.md) | Configuración y usuarios |
| [02-modulos/modulo-inventario.md](02-modulos/modulo-inventario.md) | Catálogo e inventario |
| [02-modulos/interfaz-maui.md](02-modulos/interfaz-maui.md) | App MAUI: navegación, pantallas y límites |
| [03-procesos/cliente-fiado.md](03-procesos/cliente-fiado.md) | Fiado y abonos |
| [03-procesos/proveedores.md](03-procesos/proveedores.md) | Proveedores y cuentas por pagar |
| [03-procesos/venta.md](03-procesos/venta.md) | Venta / POS (base implementada en consola) |
| [03-procesos/corte-caja.md](03-procesos/corte-caja.md) | Corte de caja (implementado en consola) |
| [03-procesos/reportes.md](03-procesos/reportes.md) | Reportes y utilidad por periodo (implementado en consola) |
| [03-procesos/respaldo-restauracion.md](03-procesos/respaldo-restauracion.md) | Respaldo y restauración (implementado) |
| [05-decisiones/ADR-stack.md](05-decisiones/ADR-stack.md) | Decisión de stack |
| [06-operacion/instalacion-y-actualizacion.md](06-operacion/instalacion-y-actualizacion.md) | Instalación |
| [07-pruebas/plan-de-pruebas.md](07-pruebas/plan-de-pruebas.md) | Estrategia y cobertura de pruebas |
| [changelog.md](changelog.md) | Cambios por versión |
| [roadmap.md](roadmap.md) | Avance y siguiente trabajo |
| [deuda-tecnica.md](deuda-tecnica.md) | Atajos y pendientes técnicos |
| [coordinacion.md](coordinacion.md) | Tablero para agentes que trabajan en paralelo |

## Inicio rápido

Requisitos:

- SDK de .NET 8 o superior para la solución, las pruebas y la consola.
- SDK de .NET 10 con las cargas de trabajo `maui-windows` y `maui-android` para la app MAUI (`dotnet workload install maui-windows maui-android`).
- Android SDK y JDK 17 solo si vas a compilar para Android en local.

```bash
# Compilar la solución (Core, Infrastructure, Application, consola y pruebas)
dotnet build SistemaTienda/SistemaTienda.sln

# Ejecutar pruebas
dotnet test SistemaTienda/SistemaTienda.sln

# Ejecutar la aplicación de consola
dotnet run --project SistemaTienda/SistemaTienda.Presentation/Presentation.csproj

# Compilar y ejecutar la app MAUI en Windows
dotnet build SistemaTienda/SistemaTienda.Maui/SistemaTienda.Maui.csproj -f net10.0-windows10.0.19041.0
dotnet run --project SistemaTienda/SistemaTienda.Maui/SistemaTienda.Maui.csproj -f net10.0-windows10.0.19041.0
```

La consola crea la base en `%LOCALAPPDATA%\SistemaTienda\tienda.db`. Para usar otra ruta:

```bash
SISTEMATIENDA_DB=/ruta/a/tienda.db dotnet run --project SistemaTienda/SistemaTienda.Presentation/Presentation.csproj
```

La app MAUI usa el almacenamiento privado de la aplicación y muestra las rutas en Ajustes. En Windows, con el identificador actual:

```
%LOCALAPPDATA%\SistemaTienda\com.sistematienda.tienda\Data\tienda.db
%LOCALAPPDATA%\SistemaTienda\com.sistematienda.tienda\Data\respaldos\
```

El primer arranque de cualquiera de las dos interfaces ejecuta el asistente de configuración y solicita el PIN del dueño, siempre que la base no tenga usuarios.

## Reglas que rigen el proyecto

Ver [CLAUDE-tienda.md](../CLAUDE-tienda.md). Las más relevantes para entender el código:

- El stock es la suma de movimientos; nunca se edita un número de stock directamente.
- Operaciones de inventario atómicas (transacción e inserción de movimiento juntas).
- El historial no se edita ni se borra; se corrige con movimientos nuevos.
- PIN nunca en texto plano.
- 100 % offline, sin servicios de pago.
- Datos personales mínimos de clientes (nombre y teléfono).
