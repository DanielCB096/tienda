# Privacidad y datos del piloto

> Este documento es una guía operativa, no asesoría legal. Si la tienda lo pide,
> el aviso de privacidad debe revisarlo alguien con conocimiento de la LFPDPPP.

## Qué datos existen

| Dato | Dónde | Para qué |
|---|---|---|
| Ventas, pagos, cortes y gastos | Base SQLite local | Operación y reportes |
| Productos y movimientos | Base SQLite local | Inventario |
| Clientes, teléfono y saldo | Base SQLite local | Fiado y cobranza |
| Proveedores y pagos | Base SQLite local | Cuentas por pagar |
| Usuarios y PIN | Base SQLite local | Acceso; el PIN va hasheado |
| Bitácora | Base SQLite local | Auditoría inmutable |
| Respaldos cifrados | Carpeta `respaldos` y USB | Recuperación |
| Logs de error | `logs/errores.log` | Diagnóstico |

No hay nube, ni cuentas en línea, ni telemetría. Todo vive en el equipo de la tienda.

## Principios

- **Mínimos**: solo los datos necesarios para operar.
- **Consentimiento**: el dueño autoriza el piloto y avisa a su personal.
- **Finalidad**: operación de la tienda y mejora del sistema; no venta ni publicidad.
- **Acceso**: por rol y PIN; el desarrollador solo accede con autorización y presencia.
- **Seguridad**: respaldos cifrados, tarjeta de recuperación fuera del equipo.
- **Retención**: al terminar, se devuelve o elimina según lo acordado.

## Consentimiento del dueño (plantilla)

> **Carta de participación en el piloto**
>
> Yo, ______________________________, dueño/responsable de
> ______________________________, acepto participar en el piloto de POSale
> del ____ al ____ de ______.
>
> Entiendo que:
>
> 1. El sistema se ejecuta sin internet y guarda los datos en este equipo.
> 2. Los respaldos se guardan cifrados; la clave y la tarjeta de recuperación quedan
>    bajo mi resguardo.
> 3. El personal usará usuarios y PIN individuales.
> 4. El facilitador puede ver la operación para capacitarme y corregir fallas, pero
>    no se llevará la base ni datos de clientes sin mi autorización escrita.
> 5. Puedo detener el piloto en cualquier momento.
> 6. Al terminar, recibiré una copia de la base y los respaldos, y se eliminarán las
>    copias de trabajo que existan fuera de mi equipo.
>
> Firma: ____________________  Fecha: __________

## Autorización de testimonio (plantilla)

> Autorizo el uso de mi nombre, el nombre de la tienda y mis comentarios sobre el
> piloto en material de difusión de POSale. Puedo revisar el texto antes de
> su publicación y revocar esta autorización por escrito.
>
> Firma: ____________________  Fecha: __________

## Paquete de soporte

- Se genera desde **Ajustes → Soporte** (según la versión) o con la consola.
- Incluye logs y diagnóstico, **no** la base por defecto.
- La variante con base solo se genera con consentimiento y se comparte cifrada.
- No se envía por canales públicos; se entrega en mano o por un canal acordado.

## Fin del piloto

1. Crear un respaldo final y verificarlo.
2. Entregar al dueño la base y los respaldos en USB.
3. Exportar reportes finales (CSV/PDF) si los pide.
4. Borrar copias de trabajo del facilitador (equipo, nube personal, mensajería).
5. Si la tienda no continúa, acordar por escrito qué se conserva y qué se elimina.
6. Si continúa, firmar la carta de decisión y el nuevo periodo.
