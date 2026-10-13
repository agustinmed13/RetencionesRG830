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
dotnet test                                      # 130 tests, deben dar todos verde
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
  RetencionesRG830.Application     Motor de cálculo, ValidadorCuit, LectorImporte, vigencia de regímenes, hashing.
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

### Reglas confirmadas por el contador

Fuente: **consulta al contador del estudio, octubre de 2026.** Son tan firmes como las
anteriores, pero **no todas están implementadas todavía**: cada una dice cómo está hoy el
código. Mientras una figure como "no implementada", el sistema la viola.

10. **Una operación que no alcanza el mínimo de retención no emite certificado, pero se
    registra.** Tiene que sumar al acumulado del mes: si no se guardara, el pago siguiente
    al mismo proveedor calcularía sobre una base incompleta. Queda registrada, **sin número
    de certificado, y no se informa en SICORE.**
    *Hoy:* no implementada. El motor ya da retención 0 con la observación "Retención mínima",
    pero `ServicioRegistroOperaciones.RegistrarAsync` le asigna número de certificado a toda
    operación, `Operacion.NumeroCertificado` es `int` (no admite "sin número") y
    `SicoreController` informa todas las no anuladas, también las de retención cero.
    Implementarla toca el modelo de datos (migración), la numeración, el PDF y SICORE.

11. **No se registran operaciones de un cliente dado de baja.** No existen operaciones
    atrasadas: el estudio da de baja a un cliente el día 1 de un mes, después de cerrar el
    mes anterior. **La validación va en el servidor**, en el registro de la operación, no
    sólo en la vista: mismo criterio que el filtro por `ClienteId` del rol Cliente.
    *Hoy:* implementada (octubre de 2026). `ServicioRegistroOperaciones.RegistrarAsync`
    rechaza la operación de un cliente de baja (test
    `No_se_puede_registrar_una_operacion_de_un_cliente_dado_de_baja`), y
    `OperacionesController.ValidarClienteAsync` lo controla antes con un mensaje amable en
    Create, Simular, Revisar, Corregir y Confirmar. Ni el listado de Clientes ni el de
    Operaciones ofrecen "Nueva operación" para un cliente de baja.

12. **Una factura pagada en cuotas genera varios certificados, y en ese caso se informa la
    orden de pago, no la factura.** La retención se practica al pagar, no al facturar. **No
    hay que impedir que se repita un número de comprobante.**
    *Hoy:* implementada en parte. Un comprobante repetido del mismo proveedor (tipo, punto de
    venta y número, contra operaciones vigentes) **avisa pero no bloquea**: el aviso sale en
    el panel de cálculo y en la confirmación con el certificado y la fecha de la operación
    existente (`ServicioRegistroOperaciones.OperacionesConMismoComprobanteAsync`). Queda así,
    confirmado. Las anuladas no cuentan como repetidas, porque recargar un comprobante después
    de anular su operación es la forma normal de corregir un error. **Falta la orden de pago
    como tipo de comprobante:** hoy sólo existen Factura, Nota de débito, Nota de crédito y
    Recibo (ver pendientes).

13. **El domicilio del proveedor es obligatorio.** Siempre está, porque figura en la factura.
    *Hoy:* implementada. `Proveedor.Domicilio` y `Proveedor.Localidad` tienen `[Required]` con
    mensaje en español (octubre de 2026).

14. **Los certificados no se reimprimen: se guarda el PDF emitido.** Si hay que corregir un
    CUIT o una razón social, se anula el certificado y se emite uno nuevo.
    *Hoy:* no implementada, y es la de más consecuencias. `OperacionesController.Certificado`
    arma el PDF de nuevo cada vez, con los datos **actuales** del proveedor. Como
    `ServicioProveedores` permite editar CUIT, razón social y domicilio, volver a descargar
    un certificado viejo después de una edición produce un documento fiscal distinto del que
    se entregó.

15. **Los valores de la RG 830 rigen siempre desde el día 1 de un mes.** Nunca cambiaron a
    mitad de mes. Confirma el criterio de la regla 9: cada operación usa la versión vigente a
    su fecha de retención, y dentro de un mes rige una sola versión.
    *Hoy:* implementada (ver "Resuelto: la regla 9"). El caso de dos versiones en un mismo mes
    no se da en la práctica. El test
    `ReglaVigenciaRegistroTests.Si_la_version_cambia_a_mitad_de_mes_el_acumulado_junta_las_dos`
    se conserva porque documenta qué hace el motor si alguna vez pasara.

---

## Volumen esperado

Fuente: el autor, octubre de 2026. Sirve para dimensionar.

| | Empresas | Proveedores por empresa | Pagos por proveedor y mes | Retenciones por mes | Por año |
|---|---|---|---|---|---|
| Hoy | 2 | ~10 | 2 o 3 | 40 a 60 | 480 a 720 |
| Si lo adoptan todas | 20 | ~10 | 2 o 3 | 400 a 600 | 4.800 a 7.200 |

Es poco para la base (SQLite alcanza sin problema), pero mucho para una pantalla: el listado
de operaciones del rol Estudio junta todas las empresas, y en pocos años son decenas de miles
de filas. **Es lo que justifica la búsqueda y la paginación** en los listados.

---

## El problema del separador decimal

Es el defecto que el sistema vino a resolver y aparece en dos lugares. Bajo la cultura
es-AR, `87190.09` se interpreta como `8719009`: el punto se lee como separador de miles.

- **En los formularios** la regla está en `LectorImporte.Interpretar` (Application): acepta
  punto o coma decimal sin confundir el punto con separador de miles, y distingue tres
  casos: vacío, un número o inválido. `src/RetencionesRG830.Web/Infraestructura/DecimalModelBinder.cs`,
  registrado al principio de la cadena de model binders, sólo la conecta con ASP.NET. Un
  campo vacío **no es cero**: en un `decimal?` llega como `null` (y la obligatoriedad la
  decide su `[Required]`); en un `decimal` es el error "Ingresá un importe.".
- **Un importe con más de dos decimales es inválido** (octubre de 2026, decisión del
  autor). La plata tiene dos decimales: un importe de factura con tres no existe. Antes
  "1.234" se leía como 1,234, que era correcto según la regla del punto decimal pero casi
  siempre equivocado según la intención del operador: en la Argentina la plata se escribe
  con puntos, y "50.000" registraba una operación de **$ 50** sin ningún aviso. Ahora
  "50.000", "1.234" y "1.2345" son inválidos, y el formulario muestra un solo mensaje que
  enseña las dos reglas: "Escribí el importe sin separador de miles y con dos decimales
  como máximo: por ejemplo 50000 o 50000,75.". Se controla con `decimal.Scale`, que
  conserva los ceros finales: "1.500" tiene tres decimales y es inválido; "1.50" vale.
- **`LectorImporte` es sólo para importes en pesos**, pero `DecimalModelBinder` lo aplica a
  **todo** `decimal` de los formularios. Hoy son cuatro campos, todos de plata (los dos
  importes de Nueva operación y los dos del Simulador). Un campo con más de dos decimales
  legítimos, como una alícuota en la futura pantalla de carga de regímenes, necesita otra
  lectura: con este binder sería rechazado.
- **Los importes de Nueva operación son `type="text" inputmode="decimal"`**, no
  `type="number"`: con `type="number"` el navegador descarta lo que no entiende
  ("5.000.000") y manda el campo vacío, así que el mensaje que enseña nunca llegaba a
  verse. Los del Simulador todavía son `type="number"`: ahí el servidor rechaza igual, pero
  el operador ve lo que diga el navegador.
- **En el archivo de SICORE**, `GeneradorArchivoSicore` formatea siempre con
  `CultureInfo.InvariantCulture` y después reemplaza el punto por coma. Es una de las dos
  causas de los rechazos de AFIP que hoy sufre el estudio (ver "Archivo de SICORE").

Qué cubre cada test, de verdad:

- **`GeneradorArchivoSicoreTests`**: el archivo de SICORE. Las cinco líneas reales
  presentadas por el estudio, carácter por carácter, y la misma línea generada bajo en-US y
  es-AR, que tiene que dar idéntica.
- **`LectorImporteTests`**: cómo se lee un importe escrito en un formulario. Fija el
  comportamiento medido en vivo antes de sacar la regla del binder (octubre de 2026): punto
  y coma decimal, qué textos son inválidos, y que vacío no es cero. Un único cambio
  deliberado desde esa medición: "1.234" pasó de leerse como 1,234 a ser inválido, por la
  regla de los dos decimales. Nunca se lee como 1234. Cada caso corre bajo es-AR y bajo
  en-US.
- **Los golden tests no pasan por `LectorImporte`**: le entregan los importes directamente
  al motor. Que sigan en verde después de un cambio en la lectura no prueba nada sobre la
  lectura. Lo que sí se verificó (octubre de 2026): al motor le llegan importes sólo desde
  el formulario (por `LectorImporte`), desde el seed (importes enteros) y desde los tests
  (dos decimales como máximo).
- **Lo que no tiene test automático:** el `DecimalModelBinder` en sí, es decir, cómo traduce
  el resultado de `LectorImporte` para ASP.NET (sobre todo, qué hace con un vacío según el
  campo sea `decimal` o `decimal?`). El proyecto de tests no referencia la Web. Se verificó
  en vivo; si se lo toca, hay que volver a probarlo a mano en un formulario.

Hasta octubre de 2026 este documento decía que las dos piezas estaban cubiertas por tests.
No era cierto: el binder no tenía ninguno.

No tocar ninguna de las dos piezas sin correr los tests.

---

## Tests

130 tests en verde. Incluyen **golden tests**: cinco operaciones reales que el estudio ya
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

Cómo se migra cada pantalla:

- **`docs/prototipo-interfaz.html` es la referencia normativa del diseño.** Cuando algo no
  coincide, manda el prototipo. `sigere.css` replica sus valores exactos (colores, tamaños
  de letra, altos de fila, espaciados, radios, pesos) con nombres de clase en español.
- **El prototipo es la referencia del aspecto, no de los estados de interacción.** Es una
  imagen estática: no puede mostrar hover, foco, botón presionado ni deshabilitado. Que no
  aparezcan no es una decisión de diseño. Esos estados se diseñan aparte en `sigere.css`,
  con los colores de la paleta: resaltado suave de fila en las tablas, hover en botones y
  campos, foco visible en todos los controles, y botones deshabilitados atenuados que no
  reaccionan al mouse.
- **Antes de dar por terminada la migración de una pantalla, compararla contra la pantalla
  equivalente del prototipo y corregir las diferencias.** El prototipo tiene cinco
  pantallas: login, panel principal, listado de operaciones, nueva operación y
  confirmación. Las demás (Clientes, Proveedores, Regímenes, Simulador, SICORE, Detalle,
  Anular) no tienen diseño propio: se arman con los mismos componentes y se comparan contra
  la pantalla del prototipo más parecida (un listado contra el de operaciones, un formulario
  contra el de nueva operación).
- `wwwroot/css/sigere.css` **convive con Bootstrap** durante el rediseño. Sus clases tienen
  nombre propio en español (`boton`, `tarjeta`, `tabla`, `campo`...) y ninguna pisa una de
  Bootstrap, para que las vistas sin migrar sigan funcionando.
- Al migrar una vista hay que reemplazar **todas** sus clases de Bootstrap, incluida la
  grilla (`container`, `row`, `col-*`), no sólo botones, tarjetas y tablas. Al terminar el
  paso 5 se quita Bootstrap del layout; si queda una clase suya en alguna vista, esa
  pantalla se desarma.

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

### Pendiente: habilitar el rol Cliente (RF-04, RN-17)

Corresponde a los requerimientos **RF-04** y **RN-17** del análisis. Hoy todos los
controladores tienen `[Authorize(Roles = "Estudio")]`, así que un usuario Cliente sólo puede
iniciar sesión y ver el Inicio. Por eso el menú lateral oculta al rol Cliente los dos grupos
(Operación y Administración): mostrar enlaces que devuelven "Acceso denegado" es peor que no
mostrarlos.

Cuando se implemente, **el filtro por `ClienteId` va en las consultas del servidor**, no
alcanza con ocultar enlaces en la interfaz. Un enlace oculto no impide escribir la URL a
mano: cada consulta de operaciones, proveedores, certificados y SICORE tiene que restringirse
a la empresa del usuario autenticado.

### Resuelto: la regla 9 (vigencia de los regímenes) en el cálculo

Hasta octubre de 2026 el cálculo no miraba la vigencia: los desplegables listaban todas las
versiones de cada régimen con el mismo texto, y el acumulado agrupaba por versión
(`RegimenId`). Con una segunda versión del 78 cargada, se reprodujo que un mes de dos
operaciones retenía $ 3.520 en lugar de $ 12.000. Criterio adoptado: **el régimen es el
código; la versión es una consecuencia de la fecha, no una decisión del operador.**

Cómo quedó:

- **El desplegable ofrece el código**, una opción por régimen, en Nueva operación y en el
  Simulador. El formulario manda `CodigoRegimen`, no una versión. El Simulador tiene su propio
  campo "Fecha de retención" (hoy como valor inicial).
- **La versión se resuelve con la fecha de retención** de la operación, no con la de hoy:
  `ServicioRegistroOperaciones.VersionVigenteAsync(codigo, fecha)`, que usa
  `VigenciaRegimen.VersionVigente`, la misma regla que la pantalla de Regímenes.
  `SimularAsync` vuelve a resolverla por su cuenta: el `RegimenId` que recibe sólo le dice el
  código. Así ni el seed, ni un formulario alterado, ni ningún otro código pueden calcular con
  una versión que no rige en esa fecha. Si no rige ninguna, es un error que nombra el código
  y la fecha.
- **El acumulado agrupa por código** (`Regimen.Codigo`), no por `RegimenId`.
- **La operación guarda la versión usada** (`RegimenId`), para auditar con qué valores se
  calculó. El modelo de datos no cambió. La confirmación, el Detalle y el resultado del
  Simulador dicen "valores vigentes desde …".
- Con una sola versión por régimen el resultado es idéntico al de antes: los golden tests no
  se tocaron y siguen en verde. El escenario de dos versiones está cubierto en
  `ReglaVigenciaRegistroTests`, no en el seed: el seed no lleva valores que AFIP no dictó.

### Confirmado: valores que cambian a mitad de mes

El contador confirmó que los valores de la RG 830 rigen siempre desde el día 1 de un mes
(regla 15), así que el caso no se da. Queda anotado qué haría el motor si pasara: cada
operación usa la versión vigente a su fecha, y la posterior al cambio aplica los valores
**nuevos a todo el acumulado del mes** y descuenta lo retenido con los viejos. Ejemplo
(valores inventados): $ 400.000 el 10/09 con la versión vieja (2 %, $ 224.000 no sujeto)
retiene $ 3.520; $ 300.000 el 20/09 con la nueva (3 %, $ 300.000 no sujeto) retiene
$ 8.480. Lo fija `ReglaVigenciaRegistroTests.Si_la_version_cambia_a_mitad_de_mes_el_acumulado_junta_las_dos`.

### Pendiente para la próxima sesión de código: nombres oficiales del SICORE

- Los comentarios de `GeneradorArchivoSicore` usan nombres propios para algunos campos
  ("Fecha del boletín de exclusión", "Número de certificado"...). Hay que pasarlos a los
  nombres de la especificación oficial (ver la tabla en "Archivo de SICORE"). La numeración
  de posiciones ya es la oficial y no cambia.
- `README.md` y el comentario de `GeneradorArchivoSicoreTests` todavía dicen que el formato
  salió por ingeniería inversa. Hay que agregar contra qué documento se validó.

### Pendientes para después de la entrega del 16 de noviembre de 2026

1. **Pantalla para cargar versiones nuevas de los regímenes.** Es el más urgente de la
   lista: el contador avisó que el anexo de la RG 830 no se actualiza hace años y hay
   presión para que lo cambien pronto. Hoy la pantalla de Regímenes es sólo de consulta: una
   versión nueva únicamente se carga por código (seed) o directo en la base. La pantalla agrega una versión nueva con su fecha de vigencia; no
   edita la vigente, porque las operaciones ya registradas guardan la versión con la que se
   calcularon (regla 9).
2. **Guardar el PDF al emitir el certificado** (regla 14). Después, descargar un certificado
   devuelve el PDF guardado, no uno armado de nuevo con los datos actuales.
3. **La tabla completa de tipos de comprobante, con la orden de pago** (regla 12). Los tipos
   están repetidos en dos lugares que hay que mantener iguales:
   `FormatoComprobante.Tipos` (Web) y `GeneradorCertificadoPdf.DescribirComprobante`.
   Conviene dejarlos en un solo lugar al ampliarlos. Ojo con el SICORE: ver el pendiente de
   prioridad alta en "Archivo de SICORE".
4. **Cargar la tabla completa de regímenes de la RG 830.** Hoy el seed trae sólo 78, 94 y
   116. Con datos reales del anexo, nunca inventados (regla 9).
5. **Búsqueda y paginación en los listados.** La justifica el volumen esperado.

Las reglas confirmadas 10, 11, 13 y 14 todavía no están implementadas. Su orden respecto de
la entrega no está decidido.

---

## Archivo de SICORE

### Formato: validado contra la especificación oficial, salvo un campo

El registro de 145 posiciones fijas se dedujo primero por ingeniería inversa del archivo
real que presentó el estudio. En octubre de 2026 se comparó campo por campo contra el
documento oficial de AFIP **"Formatos de Importación — Estándar Retenciones, Versión 9.0"**
y coincide en las 17 posiciones. El riesgo de formato **quedó acotado a un único campo**, el
132-145 (ver abajo). El resto del formato está validado.

| Pos. | Campo | Tipo | Valor que escribe el sistema |
|---|---|---|---|
| 1-2 | Código de comprobante | Entero (2) | `TipoComprobante` |
| 3-12 | Fecha de emisión del comprobante | Fecha (10) | dd/MM/yyyy |
| 13-28 | Número del comprobante | Texto (16) | punto de venta (5) + número (8), espacios a la derecha |
| 29-44 | Importe del comprobante | Decimal (16) | |
| 45-48 | Código de impuesto | Entero (4) | `0217` (Ganancias) |
| 49-51 | Código de régimen | Entero (3) | `Regimen.Codigo` |
| 52 | Código de operación | Entero (1) | `1` (retención) |
| 53-66 | Base de cálculo | Decimal (14) | el **acumulado del mes**, no el importe suelto |
| 67-76 | Fecha de emisión de la retención | Fecha (10) | |
| 77-78 | Código de condición | Entero (2) | `01` |
| 79 | Retención a sujetos suspendidos | Entero (1) | `0` |
| 80-93 | Importe de la retención | Decimal (14) | |
| 94-99 | Porcentaje de exclusión | Decimal (6) | `000,00` |
| 100-109 | Fecha publicación o fin de vigencia | Fecha (10) | `00/00/0000` |
| 110-111 | Tipo de documento del retenido | Entero (2) | `80` (CUIT) |
| 112-131 | Número de documento del retenido | Texto (20) | CUIT sin guiones, ceros a la izquierda |
| 132-145 | Número certificado original | Entero (14) | el número del certificado emitido (ver abajo) |

Los decimales llevan coma, dos decimales, sin separador de miles y ceros a la izquierda.

**El campo 132-145: validado contra una presentación aceptada, pendiente de confirmar
contra la descripción oficial del campo.** La especificación lo llama "certificado
*original*", un nombre que en los formatos de AFIP suele referirse al certificado que se
modifica o anula, y el documento disponible trae sólo la tabla de posiciones, sin
descripciones. El sistema escribe el número del certificado emitido. Lo respalda un
argumento empírico: en el archivo real del estudio ese campo lleva los números de
certificado correlativos, ese archivo se importó en SICORE y se presentó, y las causas de
rechazo que menciona el contador son otras. Falta que el contador confirme si esa
presentación fue aceptada sin observaciones. Si el campo tuviera que ir en ceros, cambian el
generador y los cinco golden tests del SICORE.

### Por qué AFIP rechaza los archivos

Confirmado por el contador (consulta de octubre de 2026). Son dos causas:

1. **Un registro que no mide exactamente 145 caracteres.**
2. **El separador decimal.** Lo resuelve `GeneradorArchivoSicore` formateando con
   `CultureInfo.InvariantCulture` y reemplazando el punto por coma (ver "El problema del
   separador decimal"). Lo cubre el test que genera la misma línea bajo dos culturas.

### Pendiente de PRIORIDAD ALTA: el largo de 145 no está garantizado

La causa de rechazo 1 **no está cubierta.** El generador rellena cada campo hasta su ancho,
pero nunca recorta ni avisa: un valor que no entra en su campo produce, sin ningún error, un
renglón de más de 145 caracteres que AFIP rechaza. Los tests verifican `Length == 145` sólo
en siete operaciones fijas, con valores que entran holgados.

El caso que lo vuelve probable es la **orden de pago**: el contador confirmó que se usa
cuando una factura se paga en cuotas (regla 12), y puede traer otro formato de numeración
que desborde el campo 13-28, armado hoy como punto de venta (5) + número (8). También
desbordan un tipo de comprobante de tres dígitos o un importe demasiado grande.

Qué hay que hacer:

- **El generador tiene que fallar ruidosamente** cuando un valor no entra en su campo, con
  un error que nombre la operación, el campo y el valor, en lugar de producir un renglón
  mal medido. Un archivo que no se genera se nota; uno mal medido se descubre cuando AFIP lo
  rechaza.
- **Un test que garantice que todo renglón generado mide exactamente 145**, incluidos
  valores en el límite de cada campo, y otro que verifique el error cuando un valor no
  entra.
- Hacerlo **antes** de agregar la orden de pago como tipo de comprobante.

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
- **Nunca guardar una entidad armada directamente con lo que llega de un formulario**, ni en
  el alta (`Add`) ni en la edición (`Update`). Se hace en un servicio que arma o carga la
  entidad y copia campo por campo sólo lo que el formulario ofrece, con un test que lo
  demuestre (ver `ServicioClientes` y `ServicioProveedores`). Por qué: ASP.NET llena el
  objeto con todo lo que venga en el POST, y así editar un proveedor lo mudaba de cliente
  (y reactivaba lo dado de baja), y un alta armada a mano podía crear una operación con
  importe y certificado inventados, sin pasar por el motor ni por la confirmación.

Se trabaja en dos máquinas sincronizadas por Git: `git pull` al empezar, `git push` al
terminar.
