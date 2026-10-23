# CLAUDE.md — Sistema de Gestión para Tiendas Pequeñas

> Lo primero que debe leer la IA al iniciar cualquier sesión.
> Los campos `[POR DEFINIR]` se llenan al cerrar el ADR correspondiente.
> Mantenerlo corto (menos de ~200 líneas). El detalle vive en `/docs`.

---

## 1. Qué es este proyecto

Sistema para tiendas pequeñas de **cualquier giro** en México. Registra ventas, compras, mermas, gastos y pagos al personal; controla inventario con código de barras, corte de caja diario, fiado a clientes y cuentas por pagar a proveedores; y muestra la utilidad real.

**Es un producto genérico y reutilizable**: puede instalarse en distintos negocios con operación similar. Cada tienda es una instalación independiente con sus propios datos locales.

**Restricciones de diseño:**
- **Costo recurrente cero**: sin hosting, servidores ni suscripciones. Todo corre en la tablet o computadora de la tienda.
- **Local-first y 100% offline**: la base de datos vive en el dispositivo.
- **Un solo dispositivo principal por tienda** (una tablet). Sin sincronización entre equipos.
- **Ticket digital** (imagen/PDF que se comparte). Sin impresora ni cajón de dinero en la versión inicial.

**Quién desarrolla:** una sola persona con apoyo de IA. Tecnologías populares y bien documentadas, soluciones simples, nada de sobreingeniería.

**Repositorio:** [PEGAR AQUÍ LA URL DEL REPOSITORIO]

## 2. Antes de escribir código

1. Lee `docs/README.md` y `docs/00-contexto/glosario.md`.
2. Lee el documento del módulo que vas a tocar: `docs/02-modulos/<modulo>.md` (incluye criterios de aceptación y estado).
3. Si tocas un flujo transversal (venta, corte, respaldo), lee `docs/03-procesos/<proceso>.md`.
4. Ante una duda técnica, revisa `docs/05-decisiones/` (ADR) antes de proponer otra cosa.
5. Si la tarea es ambigua o contradice un documento, **pregunta antes de implementar**. No inventes reglas de negocio.

## 3. Stack y estructura

- **Tipo de aplicación:** escritorio/terminal local-first. La interfaz actual es una **consola provisional**; la UI definitiva sigue pendiente de ADR (MAUI/WPF/PWA).
- **Lenguaje/framework:** C# / .NET 8 (`net8.0`), solución con capas Core → Infrastructure → Application → Presentation.
- **Base de datos local:** SQLite mediante `Microsoft.Data.Sqlite` 8.0.10 (`SistemaTienda.Infrastructure/Database.cs`).
- **Interfaz:** consola (`SistemaTienda.Presentation`), menús por rol y sesión con PIN.
- **Pruebas:** xUnit 2.9 + Microsoft.NET.Test.Sdk, con base SQLite temporal por prueba (`SistemaTienda.Tests`).
- **Distribución e instalación:** pendiente de definir; por ahora `dotnet run`. Requiere SDK .NET 8+; los ejecutables usan `RollForward=Major` para correr sobre runtimes mayores.
- **Dinero:** hoy en `decimal` (deuda registrada: migrar a centavos enteros, ver `SistemaTienda/docs/deuda-tecnica.md`).

Estructura de carpetas:

```
tienda/
├── CLAUDE-tienda.md
├── prompt.txt
└── SistemaTienda/
    ├── SistemaTienda.sln
    ├── SistemaTienda.Core/            # Entidades, enums y ProcessResult
    ├── SistemaTienda.Infrastructure/  # DatabaseTools: esquema y acceso SQLite
    ├── SistemaTienda.Application/     # Servicios de configuración e inventario
    ├── SistemaTienda.Presentation/    # Consola: asistente, login y menús
    ├── SistemaTienda.Tests/           # Pruebas xUnit con SQLite temporal
    └── docs/                          # Ver docs/README.md
```

## 4. Comandos

```bash
# Restaurar dependencias
dotnet restore SistemaTienda/SistemaTienda.sln

# Compilar
dotnet build SistemaTienda/SistemaTienda.sln

# Ejecutar la aplicación (crea la base en %LOCALAPPDATA%\SistemaTienda\tienda.db)
dotnet run --project SistemaTienda/SistemaTienda.Presentation/Presentation.csproj

# Ejecutar con otra base de datos
SISTEMATIENDA_DB=/ruta/a/tienda.db dotnet run --project SistemaTienda/SistemaTienda.Presentation/Presentation.csproj

# Pruebas
dotnet test SistemaTienda/SistemaTienda.sln

# Formato (cuando se adopte; no bloquea todavía)
dotnet format SistemaTienda/SistemaTienda.sln

# Migraciones: PENDIENTE — esquema v1 fijado con PRAGMA user_version; sin runner
```

Después de cualquier cambio: ejecuta pruebas y lint. No des una tarea por terminada si fallan.

## 5. Convenciones de código

- Los nombres de entidades, tablas y variables siguen el **glosario** (`docs/00-contexto/glosario.md`). No inventes sinónimos.
- Nombres de código en inglés; textos visibles al usuario en español (México). Moneda MXN.
- **Dinero: siempre en centavos (enteros).** Nunca float.
- Fechas/horas guardadas en formato estándar (UTC o ISO con zona) y mostradas en hora local de la tienda.
- Cantidades que pueden ser fraccionarias (kg, litros) con precisión definida en el glosario; nunca comparar decimales con igualdad directa.
- Lógica de negocio en servicios/casos de uso, no en componentes de interfaz.
- **Nada hardcodeado por giro de negocio.** Etiquetas, categorías, motivos de merma y categorías de gasto vienen de configuración/plantillas.
- Funciones pequeñas con una sola responsabilidad. Cambios pequeños y verificables. No refactorices lo que no se pidió.
- No agregues dependencias nuevas sin explicar por qué. Debe funcionar sin red y sin servicios de pago. Registra un ADR si la dependencia es relevante.
- Comentarios para explicar el *por qué*, no el *qué*.

## 6. Reglas de negocio NO negociables

No se rompen ni se "simplifican" sin aprobación explícita:

1. **La caja siempre cuadra.** Efectivo esperado = fondo inicial + ventas en efectivo + abonos en efectivo + ingresos de caja − retiros − gastos pagados de caja. Las ventas con tarjeta y el fiado NO suman al efectivo esperado.
2. **El historial no se edita ni se borra.** Toda corrección se hace con un movimiento nuevo (cancelación, devolución, ajuste, contramovimiento) con motivo, usuario y fecha.
3. **Operaciones atómicas.** Una venta, un abono o una compra se guarda completa o no se guarda: registro, líneas, descuento/aumento de inventario y pagos van en una sola transacción. Un apagón no debe dejar datos a medias.
4. **El stock es el resultado de movimientos** (compras, ventas, mermas, ajustes, devoluciones), nunca un número editado directamente.
5. **El costo se congela al vender.** Cada línea de venta guarda el costo y el precio del momento, para que la utilidad histórica no cambie al modificar precios o costos después.
6. **Método de pago por venta:** efectivo, tarjeta, mixto o fiado. El sistema NO se integra con la terminal bancaria, solo registra el método. La comisión de tarjeta es configurable y afecta la utilidad neta.
7. **Merma:** descuenta el inventario y registra el costo perdido, con motivo y usuario. Afecta la utilidad.
8. **Fiado (módulo desactivable):** una venta a fiado genera saldo pendiente. Los abonos entran al corte de caja del día en que se cobran y no se editan; se corrigen con movimientos. Cancelar una venta a fiado revierte el saldo con registro.
9. **Permisos y bitácora.** Cancelaciones, descuentos, cambios de precio, ajustes de inventario, borrados lógicos, retiros de caja y cortes requieren permiso y quedan en bitácora (quién, cuándo, qué cambió, antes y después).
10. **100% offline.** Ninguna función esencial (vender, cobrar, corte, respaldo local) puede depender de internet.
11. **Respaldo antes de migrar.** Toda actualización que cambie el esquema de datos crea primero un respaldo, ejecuta migraciones versionadas e idempotentes, y verifica la integridad. Nunca migraciones destructivas sin respaldo verificado.
12. **Respaldo visible.** La app muestra cuándo fue el último respaldo y alerta si pasaron más días de lo configurado. La restauración debe estar probada y ser guiada.
13. **Ticket no fiscal.** El ticket digital es un comprobante interno. No se promete validez fiscal. Diseñar sin impedir la facturación CFDI futura (RFC opcional del cliente, claves y desglose de impuestos).
14. **Pagos al personal** son egresos registrados, no nómina formal.
15. **Privacidad.** Datos mínimos de clientes (nombre y teléfono). El archivo de diagnóstico para soporte NO incluye datos personales ni de clientes. Los datos de prueba son siempre ficticios.
16. **Costo recurrente cero.** No introducir servicios en la nube de pago ni dependencias que requieran servidor propio.

## 7. Rendimiento (dispositivo modesto)

- Escanear un producto y verlo en el carrito: menos de 200 ms. Búsqueda por nombre: menos de 300 ms. Cobrar: máximo 3 toques. Arranque de la app: menos de 5 s.
- Referencia de dimensionamiento: hasta 10,000 productos, 300–500 ventas/día, varios años de historial, tablet de gama baja.
- Índices en columnas de búsqueda (código de barras, nombre). Paginación en listados. Resúmenes diarios precalculados para reportes.
- Estrategia de archivado para históricos grandes. No cargar todo el historial en memoria.
- Probar con volumen realista (datos ficticios generados) antes de cerrar un módulo.

## 8. Seguridad

- Acceso por PIN rápido por usuario, con bloqueo por inactividad y roles: dueño, encargado/cajero y empleado básico.
- Los permisos se validan en la lógica de la aplicación, no solo ocultando botones.
- Cifrado de respaldos y de datos locales sensibles cuando la arquitectura lo permita.
- Validar y sanear toda entrada (importación Excel/CSV incluida). Consultas parametrizadas.
- No guardar PIN en texto plano; usar hash robusto.
- Secretos y llaves fuera del repositorio.
- No registrar en logs datos de clientes ni PIN.

## 9. Pruebas

- Cada regla de la sección 6 tiene al menos una prueba automática.
- Obligatorias para: venta atómica, cuadre de caja, inventario (stock = suma de movimientos), costo congelado, mermas, fiado y abonos, permisos, migraciones con respaldo, y respaldo/restauración (probar que lo restaurado es idéntico).
- Probar escenarios de falla: apagón durante una venta, disco lleno, respaldo corrupto, cierre inesperado de la app.
- Cada módulo se da por terminado solo cuando cumple los **criterios de aceptación** de su documento.
- Para cada bug, escribe primero la prueba que lo reproduce.

## 10. Documentación: obligación en cada tarea

Antes de cerrar cualquier tarea:

- [ ] Actualiza el documento del módulo (flujos, reglas, entidades, estado).
- [ ] Si cambió el modelo de datos: actualiza `docs/01-arquitectura/modelo-de-datos.md` y agrega la migración versionada.
- [ ] Si tomaste una decisión técnica importante: crea o actualiza un ADR en `docs/05-decisiones/`.
- [ ] Agrega la entrada en `docs/changelog.md`.
- [ ] Si tomaste un atajo: anótalo en `docs/deuda-tecnica.md`.
- [ ] Si cambió un proceso: actualiza `docs/03-procesos/`.
- [ ] Si cambió algo que ve el usuario: actualiza `docs/06-operacion/manual-dueno.md` o `manual-cajero.md`.
- [ ] Indica explícitamente qué documentos modificaste.

Si el código y la documentación se contradicen, avisa y propón cuál debe corregirse.

## 11. Control de versiones (Git)

- **Antes de empezar una tarea**, verifica que el árbol de trabajo esté limpio (`git status`). Si hay cambios sin guardar, avísame antes de continuar.
- **Una rama por tarea o funcionalidad**, con nombres claros: `feat/venta-pos`, `fix/cuadre-corte`, `docs/modulo-mermas`. Nunca trabajes directo sobre `main`.
- **`main` siempre debe estar estable**: solo se integra código que pasa pruebas y lint.
- **Commits pequeños y frecuentes**, uno por cambio lógico. No mezcles funcionalidad, arreglo y documentación en un mismo commit.
- **Mensajes en español, claros y en imperativo**, con prefijo: `feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `chore:`. Ejemplo: `feat: agrega registro de merma con motivo y costo perdido`.
- **Incluye en la misma tarea** el código, sus pruebas y la documentación actualizada.
- **No hagas commit si fallan pruebas o lint**, salvo trabajo en progreso indicado con `wip:`.
- **Nunca subas al repositorio**: archivos `.env`, llaves, certificados, bases de datos locales, respaldos ni datos reales de tiendas o clientes. Verifica que `.gitignore` los cubra antes del primer commit. Si detectas un secreto ya commiteado, avísame de inmediato: hay que rotarlo, no solo borrarlo.
- **Operaciones destructivas** (`reset --hard`, `push --force`, borrar ramas, `clean -fd`) requieren mi confirmación explícita y una explicación previa.
- **Al terminar una tarea**, muéstrame el resumen de cambios (`git diff --stat` y archivos modificados) antes del commit final.
- **Versiones**: al terminar y probar un módulo, crea una etiqueta (`git tag v0.1.0-ventas`) y actualiza `docs/changelog.md`. Las versiones instalables se publican como release con su changelog.
- **Copia remota**: al final de cada sesión, `git push` al repositorio privado.

## 12. Cómo trabajar conmigo

- Responde en español.
- Antes de cambios grandes, resume tu plan en pocos pasos y espera confirmación.
- Trabaja un módulo o tarea a la vez. No avances al siguiente sin que yo lo confirme.
- Explica brevemente los conceptos técnicos no triviales; soy una sola persona desarrollando.
- Marca con ⚠️ lo que debe validar un profesional: cálculos de utilidad e impuestos (contador), integridad de datos y respaldos.
- No des por hecho que algo funciona: ejecútalo y muéstrame el resultado de las pruebas.
- Prioriza la simplicidad: quien usa el sistema no es técnico y atiende clientes con prisa. Pocos pasos, textos grandes, mensajes de error claros y en español.

## 13. Orden de desarrollo

1. Decisión de arquitectura (ADR de stack, base de datos local y distribución) y base del proyecto: repositorio, estructura, migraciones, usuarios con PIN y permisos, bitácora.
2. Configuración de la tienda y asistente de primera configuración.
3. Catálogo e inventario con código de barras e importación desde Excel/CSV.
4. Ventas / punto de venta con ticket digital.
5. Respaldo y restauración (antes de usarlo en una tienda real).
6. Corte de caja.
7. Compras y mermas.
8. Gastos y pagos al personal.
9. Reportes y utilidad.
10. Proveedores y cuentas por pagar.
11. Clientes y fiado (se reservan las entidades desde el inicio).
12. Plantillas por giro, mecanismo de actualización y diagnóstico para soporte.

Estado actual de avance: ver `docs/roadmap.md`.

## 14. Fuera de alcance (no hacer sin que yo lo pida)

- Facturación CFDI y nómina formal (diseñar sin impedirlos).
- Varias tiendas por dueño y sincronización entre dispositivos.
- Tienda en línea, promociones avanzadas y programa de lealtad.
- Impresión de tickets y cajón de dinero electrónico.
- Licenciamiento y servicios en la nube (pendiente de decisión por ADR).
- Funcionalidades de Fase 2 y 3 mientras el MVP no esté terminado.
- Reescribir módulos ya probados sin una razón documentada.
