# Registro de incidencias

## Severidades

| Nivel | Definición | Respuesta |
|---|---|---|
| **S1 Crítica** | No se puede cobrar, la caja queda detenida o hay pérdida/corrupción de datos | Mismo día; respaldo antes de tocar nada |
| **S2 Alta** | Cobro o corte incorrecto, stock descuadrado, ticket no recuperable | ≤ 1 día hábil |
| **S3 Media** | Función incómoda pero con rodeo; error visual que confunde | ≤ 3 días hábiles |
| **S4 Baja** | Texto, alineación o sugerencia | Próxima versión |

## Qué anotar siempre

- Fecha y hora exactas.
- Usuario que estaba en la app.
- Pantalla y acción (vender, cobrar, corte, importar, respaldo).
- Mensaje literal o foto (sin datos de clientes).
- ¿Se repite? ¿Con qué producto/cliente?
- ¿Se perdió dinero, stock o un ticket?

**Nunca** enviar PIN, base completa, respaldos ni datos de clientes por mensajería.

## Plantilla

| ID | Fecha/hora | Usuario | Pantalla | Qué pasó | Severidad | ¿Repite? | Evidencia | Estado | Cierre |
|---|---|---|---|---|---|---|---|---|---|
| P-001 | | | | | | | | Abierta | |
| P-002 | | | | | | | | Abierta | |
| P-003 | | | | | | | | Abierta | |
| P-004 | | | | | | | | Abierta | |
| P-005 | | | | | | | | Abierta | |

Estados: `Abierta`, `En análisis`, `Corregida en versión`, `Verificada`, `Descartada`.

## Flujo

1. **Registrar** en el momento; no esperar al cierre.
2. **Contener**: si es S1, detener la operación, respaldar y avisar.
3. **Reproducir** en un equipo de prueba con datos ficticios.
4. **Corregir** con prueba automatizada cuando sea código.
5. **Publicar** solo si es crítico; respaldo y `-Revertir` listos.
6. **Verificar** con quien reportó y cerrar con fecha y versión.

## Ejemplos

| Síntoma | Severidad | Primer paso |
|---|---|---|
| “No cobra, se queda pensando” | S1 | Cerrar la app, respaldar, revisar `logs/errores.log` |
| “El corte salió $10 abajo” | S2 | Revisar movimientos y conteo antes de ajustar |
| “El ticket en espera no aparece” | S2 | Revisar `TicketsEspera` y si quedó `Retomado` |
| “No encuentra un producto por acento” | S3 | Probar `cafe` y revisar nombre/código |
| “El botón se ve chico” | S4 | Anotar para la siguiente versión |

## Resumen semanal

- Abiertas al inicio: ____
- Nuevas: ____
- Cerradas: ____
- S1/S2 abiertas: ____
- Riesgo para el piloto: [ninguno / vigilar / detener]
