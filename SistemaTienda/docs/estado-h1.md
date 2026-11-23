# Estado de H1 — respaldo, restauración y WAL

**Actualizado:** 2026-11-23, antes de apagar el equipo.  
**Rama:** `feat/h1-respaldo-restauracion`  
**Commit:** `4e89fe0`  
**Repositorio:** <https://github.com/DanielCB096/tienda>

## Hecho y subido

- Respaldo exportable y restauración desde Ajustes.
- Paquete de soporte con base opcional.
- SQLite en WAL, `busy_timeout=30000`, `synchronous=NORMAL`.
- Instancia única en Windows.
- Conexiones de respaldo sin pooling.
- 5 pruebas nuevas en `RespaldoWalTests`.

## Verificación local

- `dotnet build SistemaTienda.sln -c Release`: OK, 0 errores, 0 advertencias.
- `dotnet test SistemaTienda.sln -c Release`: **373/373** en verde.
- La app MAUI no se pudo compilar en este equipo por cargas de trabajo faltantes.

## CI en GitHub

- Run: <https://github.com/DanielCB096/tienda/actions/runs/45292691228>
- Job “Compilar y probar”: **exitoso**.
- Job “Compilar app MAUI (Windows y Android)”: **en progreso** al apagar el equipo.
- Revisar el resultado antes de mergear. Si falla, corregir el error de compilación de MAUI y volver a ejecutar.

## Pendiente

1. Revisar el job MAUI y corregir cualquier error de compilación.
2. Probar en Windows: exportar respaldo, restaurar en otro equipo, abrir dos instancias y confirmar que la segunda muestra el aviso.
3. Probar en Android: exportar por el diálogo de compartir, restaurar desde un archivo y abrir la app.
4. Confirmar que el paquete de soporte no se comparte sin consentimiento.
5. Abrir la PR desde una cuenta colaboradora; la cuenta usada en Edge no tiene permiso:
   <https://github.com/DanielCB096/tienda/pull/new/feat/h1-respaldo-restauracion>
6. Actualizar `docs/changelog.md`, `docs/roadmap.md` y `docs/deuda-tecnica.md` cuando pase la verificación manual.

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
