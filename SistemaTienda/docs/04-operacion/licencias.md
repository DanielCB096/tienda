# Licencias de POSale

**Estado:** implementado; verificación visual pendiente.  
**Alcance:** licencia offline perpetua, sin bloqueo de la operación.

## Para el proveedor

### 1. Llaves

La llave privada se genera una sola vez y **no se guarda en el repositorio**:

```powershell
dotnet run --project SistemaTienda\SistemaTienda.Licencias -- generar-claves
```

Por defecto deja la privada en `%USERPROFILE%\.posale\private.pem`. Respalda ese
archivo en un lugar seguro y fuera de la computadora de desarrollo. La llave
pública se copia a `SistemaTienda.Application\licencia-publica.pem` y se compila
dentro de la app; se puede compartir sin riesgo.

Si se pierde la llave privada no se pueden emitir licencias nuevas con la misma
identidad. Habría que generar otro par y publicar una actualización de la app con
la nueva llave pública; las licencias anteriores dejarían de validar.

### 2. Emitir

```powershell
dotnet run --project SistemaTienda\SistemaTienda.Licencias -- emitir `
  --cliente "Abarrotes La Esquina" `
  --folio POS-2026-001 `
  --cajas 1 `
  --soporte-hasta 2027-11-27 `
  --salida licencia-POS-2026-001.poslic
```

- `--cliente` y `--folio` son obligatorios en la práctica.
- `--cajas` indica cuántas cajas cubre (por ahora el control es informativo).
- `--soporte-hasta` es la fecha de soporte/actualizaciones; no vence la licencia.
- Envía el `.poslic` por correo o mensajería. Es un archivo de texto pequeño.

### 3. Verificar

```powershell
dotnet run --project SistemaTienda\SistemaTienda.Licencias -- verificar `
  --publica SistemaTienda\SistemaTienda.Application\licencia-publica.pem `
  --archivo licencia-POS-2026-001.poslic
```

## Para el cliente

1. Abre **Ajustes → Licencia**.
2. Toca **Importar licencia**.
3. Pega el contenido completo del archivo `.poslic`.
4. **Validar y guardar**. El estado cambia a **Licencia activa** y queda en la
   bitácora.

La licencia vive en la carpeta de datos, junto a la base
(`licencia.poslic`). Si se reinstala o se restaura la carpeta de datos, se
conserva; si no, se vuelve a importar el mismo archivo.

## Reglas

- La app **no bloquea ventas** por licencia vencida o ausente: solo muestra un
  aviso en Inicio y en Ajustes.
- La prueba dura **30 días** desde la primera apertura.
- Si el soporte vence, la app sigue funcionando; el aviso recuerda renovarlo.
- El respaldo de la base no incluye la licencia.
- La licencia no está atada al equipo: se puede mover con la instalación.
- Importar una licencia requiere permiso de dueño (`Configurar`).
