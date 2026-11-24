# Estado de H1 — respaldo, restauración y WAL

**Actualizado:** 2026-11-24, H1 mergeado a `main`.  
**Rama:** `main` (merge de `feat/h1-respaldo-restauracion`)  
**Commit:** `41c5983` (merge de PR #40); fallback de exportación en `546900f`; implementación en `4e89fe0`  
**Repositorio:** <https://github.com/DanielCB096/tienda>  
**PR:** <https://github.com/DanielCB096/tienda/pull/40> (merged)

## Hecho y subido

- Respaldo exportable y restauración desde Ajustes.
- Paquete de soporte con base opcional.
- SQLite en WAL, `busy_timeout=30000`, `synchronous=NORMAL`.
- Instancia única en Windows.
- Conexiones de respaldo sin pooling.
- 5 pruebas nuevas en `RespaldoWalTests`.

## Verificación local

- `dotnet build SistemaTienda.sln -c Release`: OK, 0 errores, 0 advertencias.
- `dotnet test SistemaTienda.sln -c Release`: **376/376** en verde (3 pruebas nuevas de carpeta de exportación).
- Compilación MAUI Windows: **OK, 0 errores**, 2 advertencias XAML preexistentes (`CajaPage` y `PosPage`).
- Compilación Android: **OK** con SDK .NET 10.0.100 local y workload `maui-android`; APK Debug con ensamblados embebidos.

## Corrección de CI MAUI (`10fb923`)

- Error: `CS0234` en `AjustesPage.xaml.cs`: `Application.Current` se resolvía contra el espacio de nombres `SistemaTienda.Application` por el `using SistemaTienda.Application.Services`.
- Corrección: usar `Microsoft.Maui.Controls.Application` con alias local y envolver el cierre de ventana en `#if WINDOWS`.
- Verificado con:
  - `dotnet build SistemaTienda/SistemaTienda.Maui/SistemaTienda.Maui.csproj -f net10.0-windows10.0.19041.0 -c Release`
  - `dotnet test SistemaTienda/SistemaTienda.sln -c Release` → 373/373.

## CI en GitHub

- Run final: <https://github.com/DanielCB096/tienda/actions/runs/45320190550> (commit `f748af8`)
  - Job “Compilar y probar”: **exitoso**.
  - Job “Compilar app MAUI (Windows y Android)”: **exitoso**; Windows y Android compilan.
- Run de la corrección: <https://github.com/DanielCB096/tienda/actions/runs/45301168757> (commit `7e09cb7`)
  - Ambos jobs exitosos.
- Run anterior: <https://github.com/DanielCB096/tienda/actions/runs/45292691228>
  - Job “Compilar y probar”: exitoso.
  - Job MAUI: falló en “Compilar para Windows” (`CS0234`), corregido en `10fb923`.

## Verificación manual — Windows (2026-11-23)

Entorno: Windows 11, build Release, base de prueba aislada en `%TEMP%\opencode\h1-manual`. Tienda de prueba con 1 usuario dueño, 1 producto (`H1TEST`) y fondo de caja de $200.

| Escenario | Resultado | Evidencia |
|---|---|---|
| Exportar respaldo | **PASA** con fallback | `Documentos` no permite crear archivos en este equipo; se usó `Escritorio\SistemaTienda\tienda-20261123-212648-exportacion.db` |
| Restaurar respaldo | **PASA** | Producto `H1TEST`, usuario y configuración conservados |
| Respaldo previo a restaurar | **PASA** | `respaldos\tienda-20261123-212806-antes-restaurar.db` |
| Restaurar archivo corrupto | **PASA** | Rechazado: “no pasó la verificación de integridad”; base actual intacta |
| Dos instancias | **PASA** | La segunda muestra “SistemaTienda ya está abierto” y no deja operar |
| Paquete sin base | **PASA** | ZIP con `LEEME.txt` y `resumen.txt`; sin `datos.db` |
| Paquete con base | **PASA** | Confirmación explícita; ZIP con `datos.db`; producto y usuario presentes |
| Android | **PENDIENTE** | Sin `adb` ni dispositivo/emulador disponible |
| Otro equipo físico | **PENDIENTE** | Restauración probada en base aislada, no en hardware distinto |
| Corte de energía | **PENDIENTE** | Cubierto parcialmente por pruebas de WAL, no manual |
| Dueño no técnico | **PENDIENTE** | La ejecución fue automatizada; falta prueba con una persona real |

### Hallazgo y corrección (`546900f`)

- En este equipo `C:\Users\PC\Documents` no permite crear archivos (`FileNotFoundException` incluso con permisos de escritura); es un problema de la carpeta conocida, no de la app.
- Se agregó `CarpetaExportacion`, que elige la primera carpeta escribible: `Documentos\SistemaTienda` → `Escritorio\SistemaTienda` → `Descargas\SistemaTienda`.
- Se agregaron 3 pruebas: primera carpeta, salto de ruta inválida y fallo cuando ninguna sirve.
- La app informa la ruta final en el diálogo de exportación.

## Verificación manual - Android (2026-11-24)

Dispositivo: POCO X6 Pro 5G, Android 16 / HyperOS 3.0, arm64-v8a.  
APK: Debug con ensamblados embebidos, `ApplicationId=com.sistematienda.verificacionh1`.  
Instalación: `adb install` bloqueado por `INSTALL_FAILED_USER_RESTRICTED`; se instaló desde el archivo APK con el instalador del sistema.

| Escenario | Resultado | Evidencia |
|---|---|---|
| Exportar respaldo | **PASA** | Share sheet con `tienda-20261124-090919-exportacion.db`; 221,184 B en caché |
| Restaurar respaldo | **PASA** | Respaldo previo `tienda-20261124-091237-antes-restaurar.db`; la app reabre y conserva configuración y usuario |
| Paquete sin base | **FALLA en esta prueba** | `The process cannot access the file ... because it is being used by another process` |
| Paquete con base | **PENDIENTE** | Depende del mismo flujo |

### Hallazgo y corrección (rama `fix/android-soporte-cache`)

En Android, `ExportacionRespaldoApp` copiaba el ZIP a `CacheDirectory` cuando el archivo ya estaba en esa misma carpeta. `File.Copy` sobre sí mismo lanza “archivo en uso”.

- Se agregó `CarpetaExportacion.EsMismaRuta` y se omite la copia cuando origen y destino coinciden.
- Se agregaron 2 pruebas de rutas.
- Total: **378/378** en verde.
- Pendiente reconstruir el APK y retestar el paquete de soporte con el dispositivo conectado.

## Pendiente después del merge

1. Probar en Android: exportar por el diálogo de compartir, restaurar desde un archivo y abrir la app. **Android no se ofrece a clientes hasta completar esta prueba.**
2. Restaurar en otro equipo físico, no solo en una base aislada.
3. Simular corte de energía o cierre forzado durante una venta y durante un respaldo.
4. Probar el ciclo completo con un dueño no técnico, sin ayuda verbal.
5. Actualizar `docs/changelog.md`, `docs/roadmap.md` y `docs/deuda-tecnica.md` cuando pase la verificación manual completa.

## Cómo continuar desde otro dispositivo

```bash
git fetch origin
git checkout feat/h1-respaldo-restauracion
git pull
dotnet test SistemaTienda/SistemaTienda.sln -c Release
# Para MAUI, instalar workloads:
# dotnet workload install maui-windows maui-android
dotnet build SistemaTienda/SistemaTienda.Maui/SistemaTienda.Maui.csproj \
  -f net10.0-windows10.0.19041.0 -c Release
```

Archivos principales:

- `SistemaTienda/SistemaTienda.Infrastructure/Database.cs`
- `SistemaTienda/SistemaTienda.Infrastructure/RespaldoLocal.cs`
- `SistemaTienda/SistemaTienda.Application/Services/RespaldoService.cs`
- `SistemaTienda/SistemaTienda.Application/Services/PaqueteSoporteService.cs`
- `SistemaTienda/SistemaTienda.Maui/MauiProgram.cs`
- `SistemaTienda/SistemaTienda.Maui/App.xaml.cs`
- `SistemaTienda/SistemaTienda.Maui/Services/ExportacionRespaldoApp.cs`
- `SistemaTienda/SistemaTienda.Maui/Services/InstanciaUnica.cs`
- `SistemaTienda/SistemaTienda.Maui/Pages/AjustesPage.xaml(.cs)`
- `SistemaTienda/SistemaTienda.Maui/Pages/InstanciaDuplicadaPage.cs`
- `SistemaTienda/SistemaTienda.Tests/RespaldoWalTests.cs`
