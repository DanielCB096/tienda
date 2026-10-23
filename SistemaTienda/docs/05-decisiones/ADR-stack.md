# ADR-001: Selección de Stack Tecnológico

## Contexto

Sistema para tiendas pequeñas en México, con estas restricciones clave:
- Costo recurrente $0 (sin hosting, sin servidores, sin suscripciones)
- Local-first y 100% offline (la base de datos vive en el dispositivo)
- Un solo dispositivo principal por tienda (una tablet Windows/Android)
- Ticket digital (PDF/imagen), sin impresora térmica ni cajón de dinero
- Una sola persona desarrollará con apoyo de IA

## Opciones Consideradas

### 1. PWA + IndexedDB + SQLite WASM
- **Ventajas**: Cero instalación, funciona en cualquier navegador con Chrome/Firefox/Edge. El usuario simplemente abre la URL.
- **Riesgo**: Rendimiento en tablets gama baja; depender del navegador y de que el usuario tenga buena experiencia con PWAs. El acceso offline en Safari/iOS puede ser limitado. No es "aplicación nativa" en la tienda de apps.

### 2. Tauri + Rust + SQLite
- **Ventajas**: Aplicación nativa, muy ligera (poco más que el motor SQLite), buen rendimiento.
- **Riesgo**: Requiere toolchain de Rust compilado; el desarrollador necesita configurar compilaciones para Windows y Android. Curva de aprendizaje si el equipo principal es C#/.NET.

### 3. **MAUI (.NET 8) + SQLite (Decisión Seleccionada)**
- **Ventajas**: 
  - Código único para Windows, Android, posible iOS en futuro.
  - Rendimiento comprobado en dispositivos modestos con .NET 8.
  - Mismo lenguaje/ecosistema que el proyecto PilotTMS analizado (rehusando conocimiento existente).
  - Distribución simple: un archivo `.exe` para Windows, `.apk` para Android.
  - El .NET Runtime (~50MB) ya viene incluido en muchas tablets modernas o se distribuye con la app.
  - Acceso total a APIs del dispositivo (cámara, código de barras, almacenamiento).
- **Riesgo**: Tamaño de la instalación (~50MB con runtime). Es necesario que el dispositivo tenga Windows 10/11 o Android 10+ compatible con MAUI.

## Decisión

Seleccionar **MAUI (inicialmente .NET 8) con SQLite local** por:

1. **Un solo código base** para Windows (tabla de la tienda) y Android (misma arquitectura).
2. **Cero costo recurrente**: El .NET runtime es distribuible sin royalties. SQLite es de dominio público. No hay servidores ni suscripciones.
3. **Offline garantizado**: SQLite local, sin dependencia de red.
4. **Ticket digital**: Generar PDF/imagen nativo en la aplicación, compartir vía API del sistema.
5. **Patrón familiar**: Arquitectura similar a PilotTMS (proyectos .NET separados, capas, servicios), lo acelera el desarrollo.

## Consecuencias

### Positivas
- Un solo desarrollador, una sola base de código.
- Despliegue sencillo: build → instalar .exe o .apk.
- Mantenimiento fácil: cambios en una capa (ej. lógica de negocio) se reflejan en todas las plataformas.
- Integración con conocimiento existente del proyecto PilotTMS.

### Negativas
- Tamaño de la aplicación instalada (~30-50MB con .NET Runtime). En tablets con poco almacenamiento, esto es relevante pero aceptable.
- Requerir versión de .NET 8 en el dispositivo (la mayoría de tablets Windows nuevas vienen con ella; en Android, el runtime se incluye con la app).
- Posible necesidad de configurar permisos de código de barras, almacenamiento en Android.

⚠️ **Decisión sujeta a validar** con los requisitos finales de hardware (tipo de tablet exacta, capacidad de almacenamiento, pantallas táctiles) y la confirmación del dueño sobre el tipo de dispositivo a recomendar.

## Estado de la decisión (2026-10-23)

La decisión se mantiene y ya tiene una primera implementación:

- `SistemaTienda.Maui` compila para Windows y Android.
- En Windows se verificó manualmente el asistente de primera configuración, el login por PIN, el tablero con cuatro pestañas, el respaldo manual y el cierre de sesión.
- Android compila en GitHub Actions; falta probarlo en la tablet objetivo.
- La app usa .NET 10 porque es el SDK con cargas de trabajo MAUI disponible en el equipo; las bibliotecas compartidas siguen en `net8.0` y se reutilizan sin cambios.
- Pendiente para cerrar la validación de hardware: instalar el APK en el dispositivo objetivo y medir arranque, navegación y uso del teclado numérico.

---

**Fecha**: 2026-10-16 (actualizado 2026-10-23)

**Autor**: Arquitectura basada en análisis de PilotTMS + restricciones del proyecto Tienda