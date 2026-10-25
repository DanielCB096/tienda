# Módulo: Interfaz MAUI (Windows y Android)

## Propósito y alcance

Primera interfaz gráfica del sistema, pensada para la tablet o computadora de la tienda. Sustituye a la consola como interfaz objetivo, pero reutiliza sin cambios las capas `Core`, `Infrastructure` y `Application`.

Alcance de esta entrega:

- Asistente de primera configuración.
- Inicio de sesión con PIN.
- Tablero con seis pestañas: Inicio, Vender, Inventario, Ventas, Caja y Ajustes.
- Punto de venta: búsqueda o escaneo, carrito, cobro en efectivo, tarjeta, mixto o fiado, y ticket interno que se puede compartir o copiar.
- Inventario y catálogo: búsqueda o escaneo, alta y edición de productos, entradas, mermas, ajustes, historial de movimientos y alertas de stock bajo.
- Caja operativa: cierre de corte con diferencia justificada, ingresos, retiros y abonos de fiado.
- Respaldo automático al iniciar y cerrar sesión, y respaldo manual desde Ajustes.

Esta entrega **no** incluye todavía la anulación de ventas, el historial de cortes ni las pantallas de clientes, proveedores, gastos o reportes. La pestaña Ventas es informativa de solo lectura.

## Estado

Base implementada y verificada manualmente en Windows: asistente, login, tablero y respaldo el 2026-10-23; punto de venta el 2026-10-25 (búsqueda, carrito, límite de stock, efectivo con cambio, tarjeta, mixto, fiado y ticket con copiar/cerrar); caja operativa el 2026-10-25 (cierre con diferencia justificada, ingreso, retiro y abono); inventario el 2026-10-25 (catálogo, escaneo, alta, edición, entrada, merma, ajuste, historial y permisos por rol). La compilación para Windows y Android está en verde en CI. Quedan pendientes el botón Compartir del ticket (diálogo del sistema) y la ejecución en Android; el equipo de desarrollo no tiene Android SDK ni JDK instalados.

## Plataformas y proyecto

| Elemento | Valor |
|----------|-------|
| Proyecto | `SistemaTienda/SistemaTienda.Maui/SistemaTienda.Maui.csproj` |
| Frameworks | `net10.0-android`, `net10.0-windows10.0.19041.0` |
| Versión de app | `0.1` |
| Identificador | `com.sistematienda.tienda` |
| Empaquetado Windows | Sin MSIX (`WindowsPackageType=None`) |
| Mínimos | Android 21; Windows 10.0.17763 |
| Dependencias nuevas | `Microsoft.Maui.Controls`, `Microsoft.Extensions.Logging.Debug` |

El proyecto MAUI **no** forma parte de `SistemaTienda.sln`: así el job de pruebas de .NET 8 no paga el costo de compilar dos frameworks móviles. Se compila desde su propio `.csproj` en un job separado de CI.

La app usa .NET 10 porque es el SDK instalado en el equipo y el que trae las cargas de trabajo MAUI; las bibliotecas compartidas siguen en `net8.0` y se consumen desde la app sin cambios.

## Estructura

```
SistemaTienda.Maui/
├── App.xaml(.cs)              # Aplicación y ventana raíz
├── MauiProgram.cs             # Inyección de dependencias y ruta de la base
├── Formato.cs                 # Moneda y fechas en es-MX
├── Models/                    # Filas y opciones de las listas (ventas, carrito, productos, clientes)
├── Pages/
│   ├── ConfiguracionInicialPage   # Asistente de primera ejecución
│   ├── LoginPage                  # PIN con 3 intentos
│   ├── PrincipalPage              # TabbedPage con 6 pestañas
│   ├── InicioPage                 # Tablero del día
│   ├── PosPage                    # Punto de venta: carrito, cobro y ticket
│   ├── InventarioPage             # Catálogo, alta/edición y movimientos
│   ├── TicketPage                 # Ticket interno con compartir y copiar
│   ├── VentasPage                 # Ventas de hoy (lectura)
│   ├── CajaPage                   # Cierre de corte, movimientos y abonos
│   ├── AjustesPage                # Configuración, respaldo y cierre de sesión
│   └── ErrorPage                  # Arranque fallido
├── Services/
│   ├── ArranqueApp.cs             # Decide asistente o login al iniciar
│   ├── SesionApp.cs               # Sesión de la app sobre SesionActual
│   └── NavegacionApp.cs           # Cambio de página raíz
└── Platforms/                     # Android y Windows
```

## Arranque

1. `ArranqueApp.CrearPaginaInicial()` ejecuta `DatabaseTools.InicializarBaseDeDatos()` (migraciones y respaldo previo incluidos).
2. Si no hay usuarios, muestra el asistente; si ya hay, muestra el login.
3. Si la base no puede abrirse o migrarse, muestra `ErrorPage` con el mensaje, la ruta de la base y la carpeta de respaldos, sin arrancar el sistema a medias.

## Pantallas

| Pantalla | Contenido | Escritura |
|----------|-----------|-----------|
| Asistente | Nombre, giro, impuesto, comisión, fondo, fiado y límite, intervalo de respaldo, PIN y confirmación | Crea tienda y usuario dueño |
| Login | Nombre de la tienda y PIN numérico; 3 intentos y luego bloqueo hasta reabrir la app | Inicia sesión y evalúa respaldo automático |
| Inicio | Ventas de hoy (vigentes y anuladas), efectivo esperado del periodo, stock bajo, último respaldo y aviso si venció | Solo lectura |
| Vender | Búsqueda por código o nombre, carrito con cantidades, total, cobro efectivo con cambio visible/tarjeta/mixto/fiado y ticket interno | Venta y salida de stock |
| Inventario | Resumen de productos, stock bajo y valorizado; búsqueda o escaneo, alta y edición, entrada, merma, ajuste, historial y filtro de stock bajo | Catálogo y movimientos de inventario |
| Ventas | Lista de ventas del día con folio, total, método, hora y estado (vigente/anulada) | Solo lectura |
| Caja | Periodo abierto, efectivo esperado, fondo, ventas por método, abonos, ingresos/retiros, gastos, cierre con conteo y diferencia, y último corte cerrado | Cierre de corte, ingresos, retiros y abonos |
| Ajustes | Datos de la tienda, reglas configuradas, rutas de base y respaldos, último respaldo, crear respaldo y cerrar sesión | Respaldo manual y cierre de sesión |

El POS reutiliza `VentaService`: la validación de stock, precios congelados, pagos y fiado ocurre en la capa de aplicación; la página solo arma el carrito y captura los importes. En efectivo, la página muestra un recuadro **Cambio a entregar** con el monto recibido y el cambio, y el ticket imprime ambos importes. El ticket es texto interno (sin QR) y se abre en una página modal con opciones de compartir y copiar.

Caja reutiliza `CorteCajaService` y `ConfiguracionService`. Al cerrar, la página recalcula el efectivo esperado con el fondo capturado, muestra la diferencia en vivo y exige observaciones cuando no es cero; la confirmación advierte que el periodo queda inmutable. Los ingresos y retiros piden monto y motivo, y los retiros piden una confirmación adicional. Los abonos de fiado muestran el saldo del cliente y se listan solo si el fiado está activado. Después de cualquier operación se vuelve a leer el periodo desde la base, sin sumar importes en la interfaz.

Inventario reutiliza `InventoryService`: la búsqueda exacta por código abre el detalle, la búsqueda por nombre filtra la lista y un código desconocido de al menos ocho dígitos ofrece darlo de alta. El formulario de producto no edita el stock (solo el stock inicial al crear); los cambios posteriores se hacen con entrada, merma o ajuste, y cada uno exige cantidad, motivo y usuario. El historial muestra los últimos 200 movimientos con cantidad firmada, tipo, usuario y fecha. La entrada captura el costo del movimiento, pero no modifica el costo del catálogo; el dueño lo cambia desde Editar. El filtro de stock bajo considera `Stock <= StockMinimo`.

Las pestañas se muestran en una barra inferior en Android y en la parte superior en Windows. Los textos usan español de México y los importes se formatean con `Formato.Moneda` (centavos → `$0.00`).

## Sesión y permisos

`SesionActual` usa `AsyncLocal`, que no sobrevive entre eventos de interfaz. Por eso `SesionApp` guarda el usuario autenticado y lo vuelve a aplicar (`SesionApp.Aplicar()`) antes de cada llamada a un servicio desde un manejador de eventos. Los permisos siguen validándose en `Application`, no en la interfaz: el POS exige `Vender` al confirmar el cobro; Caja exige `CorteCaja` e `IngresoCaja` a dueño o encargado, `RetiroCaja` solo al dueño y `RegistrarAbono` a dueño o encargado. POS y Caja muestran las operaciones a todos los roles y el servicio rechaza las que no corresponden. Inventario además oculta las acciones según el rol: el empleado solo consulta y ve movimientos; el encargado edita, registra entradas y mermas, pero no cambia precios ni ajusta stock; el dueño tiene todas. Aun cuando un botón quede oculto, el servicio vuelve a validar el permiso.

Al cerrar sesión desde Ajustes se ejecuta el respaldo automático si corresponde, se limpia la sesión y se vuelve al login. No se conserva el usuario anterior.

## Almacenamiento local

| Qué | Dónde |
|-----|-------|
| Base de datos | `FileSystem.AppDataDirectory/tienda.db` |
| Respaldos | carpeta `respaldos/` junto a la base |

En Windows, con el identificador actual, la base queda en:

```
%LOCALAPPDATA%\SistemaTienda\com.sistematienda.tienda\Data\tienda.db
%LOCALAPPDATA%\SistemaTienda\com.sistematienda.tienda\Data\respaldos\
```

En Android queda dentro del almacenamiento privado de la app. Las rutas exactas se muestran en Ajustes para que el dueño sepa qué copiar si quiere llevar un respaldo a USB.

El manifiesto de Android no solicita permisos de red y desactiva el respaldo automático del sistema operativo (`allowBackup="false"`), para que los datos no salgan del dispositivo por una vía no controlada.

## Navegación

No se usa `Shell` ni rutas jerárquicas. `NavegacionApp` cambia la página raíz de la ventana (`Window.Page`) entre asistente, login y tablero. Es suficiente para las pantallas raíz sin historial de navegación y evita dejar páginas apiladas con sesiones viejas. Los detalles y formularios de Inventario se abren en una capa modal dentro de la misma pestaña.

## Respaldo

- Al iniciar sesión: `RespaldoService.EjecutarAutomaticoSiCorresponde()`; si falla, se avisa y se continúa.
- Al cerrar sesión: la misma verificación antes de limpiar la sesión.
- Manual: Ajustes → “Crear respaldo ahora”, que muestra la ruta creada.
- Inicio muestra el último respaldo y un aviso cuando el intervalo configurado ya venció.

## Limitaciones de esta entrega

- El punto de venta, el inventario y la caja ya registran operaciones desde la app; la anulación de ventas sigue en la consola.
- Caja muestra el último corte cerrado, pero el historial completo solo se consulta en la consola; el conteo se captura como un total, sin desglose por denominaciones.
- Inventario no permite borrar ni desactivar productos, filtrar por categoría, actualizar el costo del catálogo al registrar una entrada ni reportar un valorizado histórico.
- No hay pantallas de clientes, proveedores, gastos ni reportes.
- El ticket es texto interno (sin QR) con opciones de compartir y copiar; copiar sí se verificó, compartir depende del diálogo de Windows y quedó pendiente de probar.
- No hay cierre por inactividad; el bloqueo por PIN es manual (cerrar sesión).
- Android solo se ha compilado en CI, no probado en un dispositivo físico.
- La interfaz no está cubierta por pruebas automatizadas; la verificación es manual y la compilación en CI.

## Criterios de aceptación cumplidos

- [x] Un primer arranque sin base crea el esquema y muestra el asistente.
- [x] El asistente valida nombre, giro, importes, días de respaldo y confirmación de PIN.
- [x] El login acepta el PIN correcto y rechaza el incorrecto con contador de intentos.
- [x] Tras autenticarse se ve el tablero con datos reales del día.
- [x] Las ventas anuladas se distinguen de las vigentes.
- [x] El efectivo esperado del corte pendiente coincide con el servicio de corte.
- [x] Ajustes muestra las rutas reales y crea un respaldo verificable.
- [x] Cerrar sesión regresa al login.
- [x] La app abre sin conexión y sin permisos de red.
- [x] Compila para Windows localmente y para Windows y Android en CI.

## Criterios de aceptación de T13 (POS)

Implementados en código y verificados manualmente en Windows el 2026-10-25, salvo lo indicado; Android sigue pendiente:

- [x] Escanear o buscar un producto lo agrega al carrito con precio y stock.
- [x] El carrito respeta el stock y permite cambiar cantidades o quitar líneas.
- [x] El total se calcula como precio × cantidad.
- [x] El cobro en efectivo calcula el cambio y rechaza montos insuficientes.
- [x] El cobro con tarjeta y mixto valida los importes y muestra la comisión.
- [x] El fiado exige cliente y respeta el límite configurado.
- [x] La venta se registra una sola vez y descuenta el stock.
- [x] El ticket se muestra y se puede copiar.
- [ ] El ticket se puede compartir con el diálogo del sistema (implementado, sin verificación manual).

## Criterios de aceptación de T14 (Caja)

Implementados en código y verificados manualmente en Windows el 2026-10-25:

- [x] Caja muestra el periodo abierto, el efectivo esperado y el desglose por método.
- [x] El cierre calcula la diferencia con el efectivo contado y exige observaciones cuando no es cero; sin ellas no se guarda.
- [x] El cierre guarda el corte con el usuario de la sesión y el siguiente periodo inicia con lo contado.
- [x] Los ingresos y retiros piden monto y motivo; el retiro pide confirmación y el servicio valida el permiso del rol.
- [x] Los abonos de fiado descuentan el saldo del cliente y suman al efectivo esperado.
- [x] El último corte cerrado se muestra con contado, diferencia y responsable.
- [ ] El conteo por denominaciones y el historial de cortes se consultan en la app.

## Criterios de aceptación de T15 (Inventario)

Implementados en código y verificados manualmente en Windows el 2026-10-25:

- [x] El catálogo muestra productos, stock bajo y valorizado, con búsqueda por nombre y filtro de stock bajo.
- [x] Escanear un código conocido abre el detalle; un código desconocido de al menos ocho dígitos ofrece registrarlo.
- [x] Se puede dar de alta un producto con código, nombre, categoría, costo, precio, stock inicial, stock mínimo e impuesto.
- [x] El código de barras queda fijo después del alta y el stock no se edita desde el formulario de producto.
- [x] El encargado puede editar, registrar entradas y mermas, pero no cambiar costo/precio ni ajustar stock; el empleado solo consulta y ve movimientos.
- [x] Cada entrada, merma o ajuste valida cantidad, costo o motivo, actualiza el stock y deja un movimiento con usuario y fecha.
- [x] El historial muestra los últimos 200 movimientos con cantidad firmada.
- [ ] El catálogo se puede filtrar por categoría y los productos se pueden desactivar o borrar desde la app.

## Compilar y ejecutar

```bash
# Windows
dotnet build SistemaTienda/SistemaTienda.Maui/SistemaTienda.Maui.csproj -f net10.0-windows10.0.19041.0

# Ejecutar con el runtime de Windows
dotnet run --project SistemaTienda/SistemaTienda.Maui/SistemaTienda.Maui.csproj -f net10.0-windows10.0.19041.0

# Android (requiere Android SDK y JDK 17)
dotnet build SistemaTienda/SistemaTienda.Maui/SistemaTienda.Maui.csproj -f net10.0-android
```

Si faltan las cargas de trabajo: `dotnet workload install maui-windows maui-android`.
