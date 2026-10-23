# Módulo 1: Configuración de la tienda y usuarios

## Propósito y Alcance

Este módulo establece la configuración inicial y permanente del sistema, definiendo cómo opera la tienda, quiénes pueden acceder y cómo se comportan los diversos aspectos del sistema. Se ejecuta una vez al instalar la aplicación y puede revisarse/actualizarse posteriormente por el dueño o administrador.

## Actores y Permisos

| Rol | Permisos | Descripción |
|-----|----------|-------------|
| **Dueño/Administrador** | - Configurar todos los parámetros<br>- Gestionar usuarios y PINs<br>- Ver todos los reportes<br>- Realizar cortes de caja<br>- Activar/desactivar módulos (fiado, proveedores) | La máxima autoridad en la tienda |
| **Encargado/Cajero** | - Registrar ventas, mermas, gastos<br>- Ver reportes de ventas y stock<br>- Realizar cortes de caja (con restricciones)<br>- Ver catálogo y clientes | Puede operar el día a día pero sin acceso a configuración profunda |
| **Empleado básico** | - Vender (solo productos básicos)<br>- Consultar precios<br>- Ver catálogo público | Acceso restringido a funcionalidades de venta solamente |

## Flujos Paso a Paso

### 1. Asistente de Primera Configuración (Onboarding)

Al iniciar la aplicación por primera vez, se presenta una secuencia guiada:

1. **Nombre de la tienda**: Texto libre (ej. "Mi Abarrotería").
2. **Giro de negocio**: Selección de una plantilla o texto libre (abarrotes, ropa, papelería, ferretería, comida preparada, servicios con insumos). Esto carga categorías sugeridas pero no afecta la lógica central.
3. **Configuración de impuestos**:
   - ¿Usar IVA? (Sí/No).
   - Tasa predeterminada: 16% (México), pero configurable por producto.
   - Permitir productos con tasa 0% o exentos.
4. **Métodos de pago**: Activar/desactivar cada uno:
   - Efectivo (siempre activado).
   - Tarjeta (comisión configurable %).
   - Mixto (parte efectivo + parte tarjeta).
   - Fiado (opcional, requiere activación explícita).
5. **Comisión de tarjeta**: Porcentaje que cobra la terminal (ej. 3.5%). Este % se resta de la utilidad neta automáticamente.
6. **Fondo de caja inicial**: Monto de efectivo con el que se inicia cada día (ej. $200.00).
7. **PIN de acceso (Dueño)**: Creación de PIN de 4-6 dígitos para el usuario administrador.
8. **Primer respaldo**: El sistema sugiere crear el archivo `tienda.localdb.copy` inmediatamente.

### 2. Configuración Post-Inicial (Siempre accesible desde menú admin)

Después del asistente, el dueño puede modificar en cualquier momento:

- **Datos de la tienda**: Nombre, giro, teléfono de contacto.
- **Impuestos por producto**: Cada producto puede tener su propia tasa (0%, 16%, u otro valor), override del predeterminado.
- **Métodos de pago**: Activar/desactivar fiado o mixto en cualquier momento.
- **Comisión de tarjeta**: Actualizar el porcentaje en cualquier momento (afecta a partir de la próxima venta).
- **Usuarios y permisos**: Agregar nuevos cajeros/empleados, asignarles PINs y roles.
- **Límites de crédito (si fiado está activado)**: Configurar límite por cliente (alerta, no bloqueo) o "sin límite" (por defecto).
- **Configuración de respaldo**: Intervalo automático (ninguno, cada día, cada 5 ventas).
- **Llave maestra** (opcional): Para cifrado de archivos de respaldo.

## Entidades de Datos

### ConfiguraciónTienda (tabla de configuración global)

| Campo | Tipo | Descripción |
|-------|------|-------------|
| Id | int | PK autogenerado |
| NombreTienda | varchar(100) | Nombre de la tienda |
| Giro | varchar(50) | Abarrotes, ropa, etc. |
| ImpuestoPredeterminado | decimal(5,2) | Tasa por defecto (ej. 16.00) |
| ComisionTarjeta | decimal(5,2) | % comisión tarjeta (ej. 3.50) |
| FondoCajaInicial | decimal(15,2) | Monto inicial de caja diario |
| FiadoActivado | bit | 1 = sí, 0 = no |
| LimiCreditoCliente | decimal(15,2) | Límite de crédito por cliente (0 = sin límite) |
| RespaldoIntervalo | int | Días entre respaldos automáticos (0 = deshabilitado) |
| UsuarioDueñoPINHash | varchar(64) | Hash del PIN del dueño (nunca texto plano) |
| FechaCreacion | datetime | Cuándo se creó la configuración |
| VersionApp | varchar(20) | Versión de la aplicación en la que se guardó |

### UsuariosSistema (tabla de usuarios)

| Campo | Tipo | Descripción |
|-------|------|-------------|
| Id | int | PK autogenerado |
| Nombre | varchar(50) | Nombre del usuario |
| PINHash | varchar(64) | Hash del PIN (4-6 dígitos) |
| Rol | varchar(20) | "dueño", "encargado", "empleado" |
| PermisosEspeciales | varchar(100) | JSON o lista separada por comas de permisos adicionales |
| FechaCreacion | datetime | Cuándo se creó el usuario |

## Reglas de Negocio

1. **Siempre debe haber al menos un usuario "dueño"** con PIN configurado. No se puede borrar el último dueño.
2. **El PIN del dueño** es el único que puede cambiar la configuración global (impuestos, comisión, métodos de pago).
3. **Los PINs de empleados** pueden ser reasignados por el dueño o encargado.
4. **Si se desactiva el fiado**, las ventas existentes a fiado siguen estando en el sistema pero ya no se pueden crear nuevas ventas a fiado (el método desaparece de la interfaz de venta).
5. **Las plantillas de giro** cargan categorías y configuraciones sugeridas, pero el dueño puede modificarlas todas. Nada en el código asume un tipo de negocio específico.
6. **La comisión de tarjeta** se configura como porcentaje y se aplica automáticamente a todas las ventas con método "tarjeta" para calcular la utilidad neta.

## Integración con Otros Módulos

- **Ventas**: El método de pago configurado determina qué opciones aparecen en el flujo de venta.
- **Inventario**: Las categorías configuradas aquí aparecen en la gestión de productos.
- **Reportes**: Los filtros de fecha y categoría usan los parámetros de configuración.
- **Gastos**: La comisión de tarjeta configurada aquí se usa automáticamente al registrar ventas con tarjeta.
- **Fiado**: Si está desactivado, el módulo de clientes no muestra opciones de fiado.

## Casos Límite

- **Primer uso absoluto**: El asistente debe ser obligatorio y no permitir saltar pasos.
- **Olvidar PIN del dueño**: Requiere contacto con el desarrollador para restablecer (no hay backdoor), registrando un ADR sobre el proceso de recuperación.
- **Cambio de giro de negocio**: No borra datos existentes, pero las categorías y configuraciones sugeridas ya no "coinciden" - el dueño debe reconfigurar manualmente.
- **Actualización de comisión**: El cambio afecta a partir de la próxima venta; ventas ya registradas mantienen la comisión antigua en su registro.

## Pantallas Clave

1. **Pantalla de bienvenida/onboarding** (solo primera vez).
2. **Menú Configuración** (acceso por PIN de dueño).
3. **Ficha de tienda** (nombre, giro, datos de contacto).
4. **Ficha de impuestos** (tasa predeterminada, productos con tasas especiales).
5. **Configuración de métodos de pago** (interruptores activ/desactiv, campo de comisión).
6. **Gestión de usuarios** (lista de usuarios, asignar PIN, roles).
7. **Configuración de respaldo** (selector de intervalo, botón "crear respaldo ahora").

## Criterios de Aceptación

- [ ] El asistente de primera configuración guía al usuario por todos los pasos sin saltar
- [ ] Los datos de configuración se guardan en la base de datos SQLite en una tabla dedicada
- [ ] El PIN de dueño se hash con algoritmo robusto (ej. SHA-256 con salt) y nunca se almacena en texto plano
- [ ] Al menos un usuario dueño debe existir siempre (validación en el inicio de la app)
- [ ] Los cambios en comisión de tarjeta afectan a partir de la próxima venta registrada
- [ ] Si el fiado está desactivado, la opción no aparece en el flujo de venta
- [ ] Los cambios en métodos de pago se reflejan inmediatamente en la interfaz de venta
- [ ] El asistente carga plantillas de categorías sugeridas según el giro seleccionado

## Preguntas Abiertas ⚠️

- Validar con un contador: ¿El cálculo de la comisión de tarjeta debe aplicarse sobre el total de la venta o sobre el subtotal antes de impuestos?
- ¿El campo "Límite de crédito por cliente" debería aplicarse como alerta visual o como bloqueo automático de venta? El prompt sugiere "alerta, no bloqueo".
- ¿Qué información mínima del cliente es necesaria para el fiado cumpliendo LFPDPPP? (Solo nombre y teléfono están definidos como obligatorios).

---

**Estado actual (2026-10-23)**: base implementada y probada.

- *Implementado*: asistente de primera configuración (solo si no hay usuarios); PIN de 4-6 dígitos hasheado con PBKDF2-SHA256 (100 000 iteraciones, salt de 16 bytes); login con 3 intentos; roles Dueño/Encargado/Empleado; alta de usuarios; validación de roles en los menús; gastos simples con total y agrupación por concepto.
- *Desviaciones del diseño*: el asistente pide nombre, giro, impuesto, comisión, fondo, fiado, límite, intervalo de respaldo y PIN; no pregunta métodos de pago ni crea respaldo inicial (no hay módulo de respaldo). Los roles se guardan como `INTEGER`, no como texto.
- *Pendiente*: edición de usuarios y PIN, bitácora, permisos validados en servicios, plantillas por giro, respaldo inicial, recuperación de PIN.
- *Pruebas*: `ConfiguracionTests` (18 casos) y `BaseDatosTests`.