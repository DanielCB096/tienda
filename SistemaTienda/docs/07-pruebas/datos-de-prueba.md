# Datos de prueba

Herramienta para generar bases de demostración sin pasar por el asistente cada vez.
Vive en `SistemaTienda/SistemaTienda.Semilla` y no forma parte de la app: es solo para
desarrollo, pruebas manuales y demostraciones.

## Cuándo usarla y cuándo no

| Situación | Recomendación |
|---|---|
| Probar el asistente de primera vez, una migración o la prueba de usabilidad P3 | **No usar datos**: escenario `limpio` |
| Capturar una venta desde cero sin configurar la tienda | `configurado` |
| Revisar reportes, caja, ticket, respaldos o una demo con el dueño | `demo` |
| Verificar que la app arranca de cero en otro dispositivo | `limpio` o `configurado` |

Arrancar de cero sigue siendo el caso por defecto de las pruebas de primera vez: el
asistente, la creación de usuarios y el PIN solo se validan con una base vacía.

## Seguridad

- La ruta por defecto es `%LOCALAPPDATA%\SistemaTienda\semilla\tienda.db`, no la app real.
- Se niega a escribir en la carpeta de la app real (`com.sistematienda.tienda`) salvo `--forzar`.
- Se niega a sobrescribir una base existente salvo `--reset`.
- `--reset` borra la base, sus `-wal`/`-shm`, la carpeta `respaldos` y `exportaciones` del
  mismo directorio. Nada fuera de ese directorio.

## Escenarios

### `limpio`

Borra todo y deja el directorio sin base. Sirve para el asistente de primera vez, las
migraciones desde cero y la prueba de usabilidad P3.

### `configurado`

Tienda **Abarrotes La Esquina**, tres usuarios y 53 productos con categorías, stock,
stock bajo y un producto inactivo. Sin ventas, sin clientes y sin proveedores.

### `demo`

Todo lo anterior más:

- 6 clientes, 5 proveedores.
- 21 ventas: efectivo con cambio, tarjeta, mixto y fiado; una anulada antes del corte.
- 2 abonos: efectivo y transferencia.
- 2 compras a proveedor y 1 pago en efectivo con retiro de caja.
- 4 gastos: 2 de caja y 2 externos (renta e internet).
- Ingreso y retiro de caja.
- Un corte cerrado con conteo por denominaciones y diferencia cero.
- Ventas posteriores al corte para dejar el periodo abierto.
- Un respaldo verificado en `respaldos/`.

PIN: dueño `1234`, encargada `5678`, empleado `4321`.

## Comandos

```bash
# Demo en la ruta por defecto
dotnet run --project SistemaTienda/SistemaTienda.Semilla -- --escenario demo --reset

# Base limpia para el asistente
dotnet run --project SistemaTienda/SistemaTienda.Semilla -- --escenario limpio --reset

# Solo tienda y catálogo
dotnet run --project SistemaTienda/SistemaTienda.Semilla -- --escenario configurado --reset

# Base aislada de la prueba de usabilidad (Windows)
dotnet run --project SistemaTienda/SistemaTienda.Semilla -- --p3 --escenario demo --reset

# Una ruta concreta
dotnet run --project SistemaTienda/SistemaTienda.Semilla -- --db C:\ruta\tienda.db --escenario demo --reset

# Catálogo grande para medir búsqueda (53 del catálogo + 3000 de medición)
dotnet run --project SistemaTienda/SistemaTienda.Semilla -- --db C:\ruta\tienda.db --escenario configurado --productos 3000 --reset
```

`--p3` apunta a
`%LOCALAPPDATA%\SistemaTienda\com.sistematienda.p3\Data\tienda.db`, la base de la app
aislada que se usa para la prueba con una persona no técnica.

El escenario `demo` incluye ventas de los cuatro métodos, una venta con descuentos
distintos por producto, una venta en efectivo anulada y reembolsada por completo,
una venta con tarjeta anulada con reembolso parcial, un corte cerrado y un periodo
abierto. Sirve para revisar ventas, tickets, reportes, caja y reembolsos sin capturar
nada a mano.

## Qué valida la suite

`SemillaTests` genera los escenarios en un directorio temporal y comprueba:

- Conteos de productos, clientes, proveedores, usuarios y ventas.
- Que el corte tenga diferencia cero y que el desglose sume el efectivo contado.
- Que el stock de cada producto siga siendo la suma de sus movimientos, incluso con
  anulación y compras.
- Que existan las anulaciones, los reembolsos y sus retiros de caja.
- Que `DiaCompletoTests` cuadre un día con ventas, descuentos, reembolsos, abono,
  gasto, merma y compra contra ventas, reportes y corte.
- Que `limpio` borre base y respaldos.
- Que sin `--reset` no se sobrescriba una base existente.

## Limitación conocida

Todas las fechas se generan con la fecha actual porque los servicios usan `DateTime.Now`
y el historial no se edita. Para un histórico de varios días hay que usar la app o
esperar a que exista un reloj inyectable; el escenario `demo` ya deja un corte cerrado y
un periodo abierto del mismo día.
