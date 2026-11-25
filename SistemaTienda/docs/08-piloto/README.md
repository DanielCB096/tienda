# Piloto en tienda real

Documentación para instalar, capacitar y acompañar a un abarrotes durante 30 días
con una caja y usuarios definidos. El objetivo no es vender: es comprobar que el
sistema aguanta el mostrador y que el dueño lo adoptaría.

## Documentos

| Documento | Para qué |
|---|---|
| [plan-piloto.md](plan-piloto.md) | Alcance, roles, calendario, riesgos y criterios de continuación |
| [instalacion-y-dia-0.md](instalacion-y-dia-0.md) | Hardware, instalación, configuración inicial y validación del día 0 |
| [checklist-dia-0.md](checklist-dia-0.md) | Lista imprimible de instalación y arranque |
| [capacitacion.md](capacitacion.md) | Sesiones para dueño y cajeros, ejercicios y evaluación |
| [tarjeta-cajero.md](tarjeta-cajero.md) | Guía de una página para el mostrador |
| [guion-30-dias.md](guion-30-dias.md) | Rutina diaria, revisiones semanales y puertas de decisión |
| [registro-incidencias.md](registro-incidencias.md) | Plantilla y severidades para reportar fallas |
| [privacidad-y-datos.md](privacidad-y-datos.md) | Consentimiento, respaldos, soporte y fin del piloto |
| [criterios-de-exito.md](criterios-de-exito.md) | Métricas y decisión continuar / extender / detener |

## Reglas del piloto

- **Una tienda, una caja, un dispositivo.** Sin nube ni sincronización.
- **Datos reales solo con consentimiento** del dueño y aviso al personal.
- **Respaldo diario verificado** y copia semanal fuera del equipo.
- **Ninguna actualización** durante el piloto salvo falla crítica y con respaldo previo.
- **Cada incidente se registra** con hora, usuario, pantalla y evidencia; sin capturas
  de datos de clientes.

## Herramientas

- Paquete de Windows: `SistemaMaestro/scripts/h5-empaquetar.ps1` (autocontenido).
- Instalador y reversión: `SistemaMaestro/scripts/h5-instalar.ps1`.
- Kit de piloto: `SistemaMaestro/scripts/piloto-preparar.ps1`.
- Datos de práctica: `SistemaTienda.Semilla --escenario demo` o `configurado`.
- Base real: `limpio` y el asistente de primera vez.
