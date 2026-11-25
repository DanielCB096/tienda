# Plan del piloto

- **Tienda:** [nombre y giro]
- **Domicilio:** [dirección]
- **Dueño:** [nombre, teléfono]
- **Inicio:** [fecha del día 0]
- **Duración:** 30 días naturales
- **Versión:** [versión del paquete y esquema]
- **Costo para la tienda:** [gratuito / precio con descuento]

## Objetivo

Validar en una tienda real, con una caja y usuarios definidos, que el sistema:

1. Cobra y corta caja sin diferencias atribuibles al software.
2. Mantiene stock y ventas coherentes durante 30 días.
3. Lo entiende un cajero sin conocimientos técnicos.
4. No pierde datos ante cierres, cortes de luz o respaldos.
5. Deja al dueño dispuesto a seguir usándolo y a dar testimonio.

## Alcance

**Dentro**

- Instalación en el equipo de la tienda con paquete autocontenido.
- Hasta 3 usuarios: dueño, encargado y empleado.
- Catálogo real importado o capturado; ventas, fiado, gastos, compras, cortes,
  reportes, respaldos y tickets en espera.
- Acompañamiento remoto por mensajería y una visita de arranque.
- Revisión semanal de 30–45 minutos.

**Fuera**

- Facturación fiscal (CFDI), impresora fiscal y terminal integrada.
- Nube, sincronización entre cajas o acceso desde otro dispositivo.
- Nómina, proveedores con XML, básculas y código interno avanzado.
- Soporte 24/7. El canal y horario se acuerdan abajo.

## Roles

| Rol | Responsabilidad |
|---|---|
| Dueño | Decide, da acceso a datos, firma consentimiento y evalúa |
| Encargado | Opera el día a día y apoya a cajeros; puede anular y cortar |
| Empleado | Vende, consulta y reimprime; sin costos ni configuración |
| Facilitador | Instala, capacita, registra incidentes y acompaña |
| Desarrollador | Corrige fallas, publica versiones y recupera datos |

## Calendario

| Momento | Actividad | Entregable |
|---|---|---|
| Día −7 | Visita técnica, hardware, red eléctrica y respaldo | Checklist de instalación |
| Día −1 | Instalación y configuración con datos de práctica | Sistema probado y base limpia |
| Día 0 | Capacitación, catálogo real y primera venta | Acta de arranque firmada |
| Días 1–7 | Operación acompañada; 3 días en paralelo con libreta | Primer reporte semanal |
| Día 7 | Revisión: ventas, corte, stock, respaldos, incidentes | Acta y acciones |
| Días 8–14 | Operación normal; ajustes de catálogo y usuarios | Reporte semanal |
| Día 14 | Revisión y decisión de continuar | Acta |
| Días 15–21 | Operación normal; prueba de restauración | Reporte semanal |
| Día 21 | Revisión y ajuste de metas | Acta |
| Días 22–28 | Operación normal; medición final | Reporte semanal |
| Día 29–30 | Cierre, entrevista, testimonio y decisión | Informe final |
| Día 30+ | Continuar, extender o detener | Carta de decisión |

## Puertas de decisión

- **Día 0:** no arranca si falla una venta, un corte o un respaldo/restauración.
- **Día 7:** si hay diferencias de caja repetidas o pérdida de datos, se detiene y
  se corrige antes de seguir.
- **Día 14:** se decide si el piloto continúa con el alcance actual.
- **Día 30:** se decide continuar, extender 15 días o detener.

## Riesgos y mitigación

| Riesgo | Mitigación |
|---|---|
| Corte de energía o apagón | Respaldo diario, UPS recomendada, WAL y prueba de restauración |
| Catálogo sucio o incompleto | Vista previa de importación y sesión de limpieza antes del día 0 |
| Resistencia del personal | Capacitación práctica, tarjeta de una página y acompañamiento |
| Diferencias de caja | Conteo por denominaciones, motivo obligatorio y revisión diaria |
| Pérdida del equipo | Copia semanal del respaldo cifrado fuera del equipo |
| Actualización fallida | No actualizar salvo falla crítica; respaldo y `-Revertir` |
| Datos sensibles | Aviso de privacidad, acceso por rol y paquete de soporte sin base |

## Comunicación

- **Canal:** [WhatsApp / llamada / correo]
- **Horario:** [lunes a sábado, 9:00–19:00]
- **Emergencia (caja detenida):** [teléfono]
- **Respuesta esperada:** 2 horas hábiles; falla crítica, mismo día.
- **Reporte:** plantilla [registro-incidencias.md](registro-incidencias.md).

## Compromisos

- El dueño conserva la base y los respaldos; el facilitador no se los lleva sin
  consentimiento.
- El personal no comparte PIN ni usa el usuario de otro.
- No se instalan programas ajenos ni se conecta el equipo a redes desconocidas.
- El piloto puede detenerse en cualquier momento sin costo.
