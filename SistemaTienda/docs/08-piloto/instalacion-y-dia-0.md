# Instalación y día 0

## 1. Hardware recomendado

| Pieza | Mínimo | Recomendado |
|---|---|---|
| Equipo | Windows 10/11 x64, 4 GB RAM, 2 GB libres | 8 GB RAM, SSD, pantalla 15" |
| Respaldo eléctrico | Regleta con protección | UPS de 600 VA |
| Lector | USB tipo teclado (código + Enter) | USB o Bluetooth con base |
| Impresión | Ninguna | Impresora láser para PDF o térmica con driver del sistema |
| Red | No requiere internet | Solo para soporte y actualizaciones |
| Respaldo externo | USB | USB + carpeta en otra computadora |

La app es **autocontenida**: no hay que instalar .NET ni dependencias.

## 2. Antes de instalar

- [ ] Equipo exclusivo o con sesión de Windows dedicada a la tienda.
- [ ] Inicio de sesión de Windows sin pantalla de bloqueo durante el horario.
- [ ] Suspensión y apagado automático desactivados.
- [ ] Antivirus con exclusión para `%LOCALAPPDATA%\SistemaTienda` (evita bloqueos de archivo).
- [ ] Reloj y zona horaria correctos (afectan cortes y reportes).
- [ ] Lector configurado para enviar **Enter** al final del código.
- [ ] USB de respaldo identificado y formateado.
- [ ] Paquete `POSale-<versión>-win-x64.zip` descargado del kit.

## 3. Instalación

1. Copiar el ZIP y `h5-instalar.ps1` a una carpeta del equipo.
2. Abrir PowerShell en esa carpeta y ejecutar:

   ```powershell
   powershell -ExecutionPolicy Bypass -File h5-instalar.ps1 -Paquete POSale-1.0.0-win-x64.zip
   ```

3. Verificar que termine con **Instalada la versión …** y anotar la ruta.
4. Abrir la app desde `%LOCALAPPDATA%\SistemaTienda\app\versiones\<versión>` y crear un
   acceso directo en el escritorio.
5. Confirmar que arranca sin pedir internet ni instalar nada más.

### Rutas de datos

| Dato | Ruta |
|---|---|
| Base | `%LOCALAPPDATA%\SistemaTienda\com.sistematienda.tienda\Data\tienda.db` |
| Respaldos | `...\Data\respaldos` |
| Exportaciones | `...\Data\exportaciones` |
| Logs de error | `...\Data\logs\errores.log` |
| Versiones de la app | `%LOCALAPPDATA%\SistemaTienda\app\versiones` |

## 4. Primera configuración

Usar el asistente con datos reales de la tienda:

1. Nombre y giro.
2. Impuesto predeterminado (si los precios ya incluyen IVA, dejar la tasa; el ticket
   muestra “IVA incluido”).
3. Comisión de tarjeta.
4. Fondo de caja inicial.
5. Fiado y límite por cliente (0 = sin límite).
6. Respaldo automático: **1 día**.
7. **Cifrado de respaldos: activado**. Anotar la clave y guardar la tarjeta de
   recuperación **fuera** del equipo.
8. PIN del dueño: 4–6 dígitos, no repetido con otros usuarios.

Después, en **Ajustes → Usuarios y PIN**, crear encargado y empleado con PIN propio.

## 5. Catálogo

- **Importación**: usar la plantilla (`CodigoBarras,Nombre,Costo,Precio,Stock,StockMinimo,Categoria,Impuesto`).
- **Vista previa**: revisar leídas, válidas, omitidas y errores antes de confirmar.
- **Lote**: anotar el número de lote; se puede revertir solo si no hubo ventas.
- **Sin código**: asignar un código interno numérico único (no reutilizar).
- **Stock inicial**: capturarlo en la importación o con entradas; nunca editar la
  columna de stock a mano.

## 6. Pruebas obligatorias del día 0

| Prueba | Cómo | Criterio |
|---|---|---|
| Venta en efectivo | Producto de $10, pago con $50 | Cambio correcto y stock −1 |
| Venta con tarjeta | Producto de $20 | Comisión y total correctos |
| Venta mixta | $10 efectivo + $10 tarjeta | Corte separa ambos |
| Fiado | Cliente con límite, venta a fiado | Saldo aumenta y aparece en estado de cuenta |
| Ticket en espera | Guardar, cerrar app, reabrir, retomar | Ticket intacto; no reserva stock |
| Anulación | Anular la venta de prueba | Stock devuelto y venta marcada |
| Corte | Contar el efectivo y cerrar | Diferencia $0.00 |
| Respaldo | Crear respaldo manual | Archivo verificado y ruta visible |
| Restauración | Restaurar en una copia o equipo de prueba | Datos vuelven al respaldo |
| Corte de energía | Apagar sin cerrar (equipo de prueba) | La base abre; venta completa o inexistente |

Si falla venta, corte o restauración, **no se arranca** el piloto.

## 7. Arranque real

1. Borrar los datos de práctica si se usaron: `SistemaTienda.Semilla --p3 --escenario limpio --reset`
   o usar una instalación limpia.
2. Repetir el asistente con los datos reales.
3. Importar el catálogo definitivo.
4. Registrar el fondo de caja y hacer el primer corte en cero.
5. Firmar el [checklist-dia-0.md](checklist-dia-0.md).
6. Entregar la [tarjeta-cajero.md](tarjeta-cajero.md) impresa.

## 8. Reversión

Si la app nueva no arranca:

```powershell
powershell -ExecutionPolicy Bypass -File h5-instalar.ps1 -Revertir
```

La versión anterior queda activa y los datos no se tocan. Si la base quedó dañada,
restaurar el respaldo `antes-migrar` o el último respaldo verificado.
