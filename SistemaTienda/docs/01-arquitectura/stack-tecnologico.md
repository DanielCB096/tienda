# Stack Tecnológico

## Arquitectura Local-First

- **Tipo de aplicación**: Aplicación de escritorio/tablet multiplataforma con .NET MAUI para Windows y Android.
- **Lenguaje**: C#. Las bibliotecas (`Core`, `Infrastructure`, `Application`), la consola y las pruebas usan `net8.0`; la app MAUI usa .NET 10 porque es el SDK con cargas de trabajo disponible.
- **Base de datos local**: SQLite (archivo `.db` único, ligero, funciona sin servidor).
- **Interfaz de usuario**: app MAUI (`SistemaTienda.Maui`) y consola provisional (`SistemaTienda.Presentation`) para las operaciones que aún no están en la app.

## Sin Costo Recurrente

- **Sin hosting, sin servidores, sin suscripciones mensuales**.
- El archivo SQLite es la "base de datos local": vive en el dispositivo.
- Los respaldos son copias verificadas de la base (`.db`) creadas con `VACUUM INTO` en la carpeta `respaldos`.
- Distribución: compilación de la app MAUI para Windows (sin MSIX por ahora) y APK para Android.

## Distribución e Instalación

- **Windows**: paquete ZIP por versión con `manifiesto.json` y scripts `h5-empaquetar.ps1` / `h5-instalar.ps1`; la instalación conserva la versión anterior y permite revertir sin tocar los datos ([ADR-008](../../../SistemaMaestro/docs/07-decisiones/ADR-008-instalacion-actualizacion.md)).
- **Android**: APK generado desde MAUI; todavía no se ha probado en dispositivo.
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