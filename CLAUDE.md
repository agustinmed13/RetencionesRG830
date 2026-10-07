# SIGERE — Sistema de Gestión de Retenciones

Aplicación web para calcular, registrar y presentar retenciones del Impuesto a las
Ganancias bajo el régimen de la **RG 830** de AFIP (Argentina).

Reemplaza una planilla de Excel que un estudio contable real entrega a cada una de sus
empresas clientes. Es a la vez el proyecto final de una Tecnicatura en Programación y un
sistema destinado a usarse en producción.

**Idioma del proyecto: español.** Nombres de clases, métodos, variables, comentarios,
mensajes de error y documentación, todo en español. El código existente sigue esa
convención sin excepciones; mantenerla.

---

## Cómo ejecutarlo

```bash
dotnet run --project src/RetencionesRG830.Web   # http://localhost:5149
dotnet test                                      # 31 tests, deben dar todos verde
```

La base SQLite se crea sola en el primer arranque (`DbInitializer`), con los regímenes y
datos de prueba cargados. No está versionada: cada máquina tiene la suya.

Usuarios de prueba:

| Usuario | Contraseña | Rol |
|---|---|---|
| `estudio@ejemplo.com` | `Estudio123!` | Estudio (acceso total) |
| `operador@cliente.com` | `Cliente123!` | Cliente (sólo su empresa) |

---

## Arquitectura

```
src/
  RetencionesRG830.Domain          Entidades del negocio. NO depende de nada.
  RetencionesRG830.Application     Motor de cálculo, ValidadorCuit, hashing.
  RetencionesRG830.Infrastructure  EF Core 8 + SQLite, generador de PDF y de SICORE.
  RetencionesRG830.Web             ASP.NET Core MVC: controladores y vistas Razor.
tests/
  RetencionesRG830.Tests           xUnit.
```

.NET 8. La dirección de las dependencias es Domain ← Application ← Infrastructure ← Web
y **no se invierte nunca**. En particular, `ServicioCalculoRetencion` no consulta la base
de datos ni conoce las pantallas: recibe parámetros y devuelve un resultado. Eso es lo que
permite verificarlo automáticamente contra casos reales.

---

## Reglas de negocio que no se pueden romper

Salen de la norma y de la operatoria real del estudio. Cada una está cubierta por tests.

1. **El acumulado es mensual, por proveedor y por régimen.** La base de cálculo es la suma
   de los importes gravados del mes para ese proveedor y ese régimen, incluida la operación
   que se está registrando. No es el importe del comprobante suelto.
2. **El monto no sujeto a retención se aplica UNA SOLA VEZ por mes**, no por comprobante.
3. **De la retención determinada se descuentan las retenciones ya practicadas** al mismo
   proveedor, mismo régimen, mismo mes.
4. **La escala progresiva se aplica SÓLO a proveedores inscriptos en Ganancias.** A los no
   inscriptos se les aplica la alícuota fija. Esto estuvo mal implementado una vez y lo
   detectó un test de borde; el marcador de escala aparece únicamente en la columna de
   inscriptos de la planilla original.
5. **El mínimo de retención es distinto según el proveedor esté inscripto o no**
   (`MinimoRetencion` vs `MinimoRetencionNoInscripto`).
6. **Las operaciones no se borran: se anulan**, con motivo obligatorio, fecha y responsable.
   Una operación anulada conserva su número de certificado, deja de sumar al acumulado y no
   se informa en SICORE. El número no se reutiliza.
7. **La numeración de certificados es correlativa por cliente**, asignada por el sistema.
   Hay índice único en `(ClienteId, NumeroCertificado)`.
8. **Un proveedor pertenece a un único cliente.** Índice único en `(ClienteId, Cuit)`:
   dos clientes distintos pueden tener el mismo proveedor, uno no puede tenerlo dos veces.
9. **Las reglas de la RG 830 son datos, no código.** Viven en la entidad `Regimen` con
   fecha de vigencia. Una operación se calcula con los valores vigentes a su fecha de
   retención. Nunca escribir una tasa o un piso dentro del código.

---

## El problema del separador decimal

Es el defecto que el sistema vino a resolver y aparece en dos lugares. Bajo la cultura
es-AR, `87190.09` se interpreta como `8719009`: el punto se lee como separador de miles.

- **En los formularios** lo resuelve `src/RetencionesRG830.Web/Infraestructura/DecimalModelBinder.cs`,
  registrado al principio de la cadena de model binders. Acepta punto o coma sin confundir
  el punto con separador de miles.
- **En el archivo de SICORE**, `GeneradorArchivoSicore` formatea siempre con
  `CultureInfo.InvariantCulture` y después reemplaza el punto por coma. Es la causa de los
  rechazos de AFIP que hoy sufre el estudio.

No tocar ninguna de las dos piezas sin correr los tests. Hay un test que genera la misma
línea bajo dos configuraciones regionales distintas y verifica que el resultado sea idéntico.

---

## Tests

31 tests en verde. Incluyen **golden tests**: cinco operaciones reales que el estudio ya
practicó y presentó ante AFIP, reproducidas hasta el cuarto decimal, y las cinco líneas del
archivo de SICORE reproducidas carácter por carácter.

**Esos tests son el activo más valioso del proyecto.** Si un cambio los rompe, el cambio
está mal, no el test. Correr `dotnet test` antes de cada commit.

El caso más importante son dos comprobantes del mismo proveedor en el mismo mes: el segundo
sólo da correcto si se acumula, se aplica el piso una sola vez y se descuenta lo ya retenido.

---

## Datos: regla estricta

El repositorio es **público**. Los datos reales del estudio y de sus proveedores fueron
reemplazados por ficticios antes de publicarlo.

**Nunca volver a poner datos reales de terceros en el código, los tests, el seed, la
documentación ni los commits.** Los CUIT ficticios del seed son matemáticamente válidos.
Si hace falta un dato nuevo, inventarlo.

---

## Estado del proyecto

| Etapa | Contenido | Estado |
|---|---|---|
| 1 | Modelo de datos, capas, autenticación con roles, ABM de clientes y proveedores con validación de CUIT | Completa |
| 2 | Motor de cálculo con tests contra datos reales | Completa |
| 3 | Registro de operaciones con acumulado mensual automático | Completa |
| 4 | Certificado en PDF (QuestPDF) y exportación para SICORE | Completa |
| 5 | **Rediseño de la interfaz** (en curso), historial y búsqueda, reportes, despliegue | En curso |
| 6 | Manual de usuario y validación con el estudio | Pendiente |

### Trabajo en curso: rediseño de la interfaz

Se adopta un aspecto nuevo: barra lateral oscura fija, azul de acción, tarjetas, tablas con
tags de estado. **Se implementa con CSS sobre las vistas Razor existentes.** Se descartó
reescribir el frontend como SPA en React: obligaría a convertir los controladores en API y
pondría en riesgo un sistema que funciona y está verificado, a cambio de algo estético.

Orden de trabajo:

1. `_Layout.cshtml` + hoja de estilos propia en `wwwroot/css/`  ← paso actual
2. Login
3. Listado de operaciones con filtros
4. Nueva operación con panel de cálculo al costado, y la pantalla de confirmación
5. El resto: Clientes, Proveedores, Regímenes, Simulador, SICORE, Detalle, Anular
6. Panel principal (último: es lo único que requiere consultas nuevas)

Decisiones de la interfaz que no se negocian:

- La columna y el selector de régimen muestran el **código** (78, 94, 116, 31). "RG 830" es
  la norma, no el régimen.
- El desglose del cálculo tiene **siete renglones en este orden**: base acumulada del mes,
  monto no sujeto a retención, neto sujeto a retención, alícuota aplicada, retención
  determinada, retenciones anteriores del mes, retención a practicar. Replica la hoja
  Cálculo de la planilla para que el operador pueda contrastar ambos resultados.
- **Se conserva el paso de confirmación** entre calcular y guardar. Se agregó a propósito
  después de detectar que un Enter accidental dejaba la operación registrada, y los
  documentos fiscales no se pueden borrar.
- Enter avanza al campo siguiente, no envía el formulario (`wwwroot/js/site.js`).
- Los roles son **Estudio** y **Cliente**, no "Administrador".

---

## Riesgo abierto más importante

El formato del archivo de SICORE (registro de 145 posiciones fijas) fue **deducido por
ingeniería inversa** del archivo real que presentó el estudio, no de la especificación
oficial de AFIP. Se reproduce carácter por carácter contra ese archivo, pero **debe
validarse contra la especificación oficial antes de usarse en producción**.

---

## Cómo trabajar en este proyecto

El autor está aprendiendo a programar y necesita poder explicar cada decisión ante un
tribunal académico.

- Explicar el porqué, no sólo el qué.
- Un archivo por vez. Mensajes largos con varios archivos lo pierden.
- Ante un error propio, decirlo directamente y corregirlo.
- Antes de agregar una regla de cálculo, verificar contra la planilla original y los tests.
- No introducir dependencias nuevas sin motivo claro. Las actuales son EF Core, QuestPDF y
  xUnit.

Se trabaja en dos máquinas sincronizadas por Git: `git pull` al empezar, `git push` al
terminar.
