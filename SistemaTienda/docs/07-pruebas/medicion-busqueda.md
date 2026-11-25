# Medición de búsqueda y escaneo (H6.3)

Objetivo: comprobar que el mostrador responde con 3000 productos y que el escáner
no pierde dígitos. La medición de escritorio es automática; la de Android se hace
en el equipo del piloto.

## Prueba automática

```powershell
dotnet test SistemaTienda/SistemaTienda.sln -c Release `
  --filter "FullyQualifiedName~BusquedaRendimientoTests"
```

`BusquedaRendimientoTests` siembra 3000 productos en una sola transacción y mide:

- `SugerirProductos("producto generico 0149", 8)` 20 veces.
- `BuscarPorNombre("producto generico 015", maximo: 200)` 20 veces (100 coincidencias).
- `BuscarPorCodigo("H63-01499")` 50 veces.

Los umbrales del CI son 250 ms para nombre/sugerencias y 100 ms para código; son
topes anti-regresión, no la meta de experiencia. La clase corre en una colección
sin paralelismo para que otras pruebas no contaminen la medición.

### Resultado local (Windows 11, .NET 10, Release, 2026-11-25)

| Operación | Promedio |
|---|---|
| Sugerencias mientras se escribe | 33.52 ms |
| Búsqueda por nombre (100 coincidencias) | 28.08 ms |
| Código de barras exacto | 0.07 ms |

El POS espera 250 ms desde la última tecla antes de buscar, así que el usuario ve
resultados en menos de 300 ms. La optimización incluye un camino rápido para texto
ASCII y una sola normalización por producto y consulta.

## Medición en Android (campo)

1. Generar un CSV con 3000 productos y códigos únicos.
2. Importarlo desde **Inventario → Importar CSV/Excel**.
3. En **Vender**, escribir tres letras de un nombre con acento (`cafe`, `azucar`)
   y confirmar que aparece el producto correcto.
4. Escanear un código completo de 13 dígitos y confirmar que se agrega el producto
   correcto sin perder dígitos.
5. Repetir con la app recién abierta y con el catálogo ya cargado.
6. Anotar el equipo, la versión de Android y si hubo retraso perceptible.

Criterio: sin bloqueo visible de la interfaz y menos de 300 ms entre la última
tecla y los resultados. Si falla, medir antes de cambiar a FTS5 o caché global.

## Lo que no se mide aquí

- Lectores Bluetooth con su propio buffer: se prueban en el equipo del cliente.
- Tabletas de gama baja: el piloto debe incluir al menos un equipo modesto.
