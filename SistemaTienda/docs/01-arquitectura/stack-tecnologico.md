# Stack Tecnológico

## Arquitectura Local-First

- **Tipo de aplicación**: Aplicación de escritorio multiplataforma con .NET MAUI (.NET 8) o WPF para Windows, OPCIONALMENTE PWA con IndexedDB/SQLite WASM para navegadores.
- **Lenguaje**: C# .NET 8 (misma base que el proyecto PilotTMS analizado, reutilizando conocimientos y patrones).
- **Base de datos local**: SQLite (archivo .db único, ligero, funciona sin servidor).
- **Interfaz de usuario**: MAUI (Windows + Android) o WPF (Windows solo), o PWA responsive.

## Sin Costo Recurrente

- **Sin hosting, sin servidores, sin suscripciones mensuales**.
- El archivo SQLite es la "base de datos local": vive en el dispositivo.
- Los respaldos son archivos `.copy` del archivo `.db` (copia exacta).
- Distribución: archivo de instalación `.exe` (Windows) o `.app` (MAC), o para PWA: archivos estáticos en GitHub Pages (gratis).

## Distribución e Instalación

- **Windows**: Archivo ejecutable `.exe` creado con proyecto .NET MAUI/WPF, el usuario lo descarga y ejecuta.
- **Android**: APK generado desde MAUI, o PWA añadida a pantalla principal.
- **Máquina única por tienda**: No hay sincronización entre dispositivos. Lo que sí existe: restauración desde respaldo si la tablet se daña, se pierde o se formatea.

## Seguridad

- **Acceso por PIN**: 4-6 dígitos, validado en la aplicación, hash en la base de datos (nunca texto plano).
- **Cifrado opcional**: Llave maestra para el archivo SQLite si se requiere protección adicional.
- **Validación de entradas**: Todas las importaciones (Excel/CSV) se sanitizan y validan antes de grabar.
- **Sin datos en la nube**: Los datos del dueño y clientes quedan exclusivamente en el dispositivo de la tienda.

## Rendimiento (dispositivos modestos)

- Hasta 10,000 productos, 300–500 ventas/día, varios años de historial.
- Índices en columnas de búsqueda: `CodigoBarras`, `Nombre`.
- Paginación en listados (nunca cargar todo el historial).
- Resúmenes diarios precalculados para reportes.
- Probar con datos ficticios realistas antes de cerrar un módulo.