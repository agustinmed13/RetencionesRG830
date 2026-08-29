# Sistema de Retenciones RG 830

Aplicación web para el cálculo, registro y presentación de retenciones del
Impuesto a las Ganancias bajo el régimen de la **RG 830** de AFIP.

Desarrollado a partir del relevamiento de un estudio contable en funcionamiento,
para reemplazar la planilla de cálculo que hoy entregan a cada uno de sus
clientes.

---

## El problema

Hoy el estudio le entrega a cada cliente una copia de una planilla de Excel para
que calcule sus propias retenciones. De la entrevista de relevamiento surgieron
estos problemas concretos:

| Situación actual | Consecuencia |
|---|---|
| El acumulado mensual por proveedor se controla a ojo | Ya derivó en retenciones mal calculadas |
| La numeración de certificados se lleva a mano | Hubo números repetidos y salteados |
| Cada cliente tiene su propia copia del archivo | Cuando AFIP actualiza la norma hay que corregirlas una por una |
| El archivo para SICORE se arma copiando celdas | A veces AFIP lo rechaza por el separador decimal |
| Media hora de trabajo por operación | Cuello de botella en el estudio |

## Qué resuelve el sistema

- **Acumulado mensual automático.** El operador carga sólo el comprobante; el
  sistema busca las operaciones anteriores del mes para ese proveedor y régimen,
  aplica el mínimo no sujeto a retención una sola vez y descuenta lo ya retenido.
- **Numeración correlativa** de certificados asignada por el sistema, sin saltos
  ni repeticiones.
- **Reglas normativas como datos, no como código.** Tasas, pisos, mínimos y
  escalas se cargan con fecha de vigencia y valen para todos los clientes. Una
  actualización de AFIP no requiere tocar el código.
- **Certificado de retención en PDF** con los datos del firmante.
- **Exportación del archivo para SICORE** por rango de fechas, con formato
  numérico independiente de la configuración regional de la computadora.
- **Anulación, no borrado.** Una operación anulada conserva su número de
  certificado, registra motivo y responsable, deja de sumar al acumulado y no se
  informa en SICORE.
- **Roles diferenciados**: el estudio administra todo; cada cliente ve sólo lo
  suyo.

## Alcance del cálculo

Se implementan los tres mecanismos de cálculo que contempla la RG 830:

| Régimen | Concepto | Mecanismo |
|---|---|---|
| 78 | Enajenación de bienes muebles y bienes de cambio | Tasa fija |
| 94 | Locaciones de obra y servicios | Tasa fija diferenciada por tipo de sujeto |
| 116 | Honorarios de director y cargos similares | Escala progresiva por tramos |
| 31 | Alquileres de inmuebles urbanos | Mínimo de retención diferenciado |

El diseño de reglas versionadas por fecha permite incorporar el resto de los
códigos de la norma cargando datos, sin escribir código nuevo.

---

## Arquitectura

Solución .NET organizada en capas, para mantener el motor de cálculo como lógica
pura, independiente de la base de datos y de la interfaz:

```
src/
  RetencionesRG830.Domain          Entidades del negocio. No depende de nada.
  RetencionesRG830.Application     Motor de cálculo, validador de CUIT, hashing.
  RetencionesRG830.Infrastructure  EF Core, generadores de PDF y de SICORE.
  RetencionesRG830.Web             ASP.NET Core MVC: controladores y vistas.
tests/
  RetencionesRG830.Tests           Tests unitarios con xUnit.
```

El motor de cálculo (`ServicioCalculoRetencion`) no consulta la base de datos ni
conoce las pantallas: recibe los datos como parámetros y devuelve un resultado.
Eso es lo que permite validarlo automáticamente contra casos reales.

### Tecnologías

- .NET 8 / C#
- ASP.NET Core MVC
- Entity Framework Core 8 con SQLite
- QuestPDF para la generación de certificados
- xUnit para los tests

---

## Validación con datos reales

El motor de cálculo y el generador del archivo de SICORE se verifican
automáticamente contra operaciones que el estudio contable efectivamente
practicó y presentó ante AFIP.

- **31 tests automáticos**, entre ellos los cálculos de cinco operaciones reales
  reproducidos hasta el cuarto decimal.
- El caso más relevante son dos comprobantes del mismo proveedor en el mismo mes:
  el segundo sólo da correcto si se acumula, se aplica el piso una sola vez y se
  descuenta lo ya retenido.
- El formato del archivo para SICORE (registro de 145 posiciones fijas) se obtuvo
  analizando el archivo real presentado por el estudio, y se validó
  reproduciéndolo carácter por carácter.
- Un test genera la misma línea bajo dos configuraciones regionales distintas y
  comprueba que el resultado sea idéntico: es la comprobación que cubre el error
  de separador decimal que hoy provoca rechazos.

> **Nota sobre los datos.** Los CUIT, nombres y domicilios que aparecen en el
> código y en los tests son **ficticios**. Los datos reales del estudio y de sus
> proveedores fueron reemplazados antes de publicar el repositorio. Los CUIT
> ficticios son matemáticamente válidos, y la estructura de los registros es la
> misma que se validó contra los archivos originales.

---

## Cómo ejecutarlo

Requiere el [SDK de .NET 8](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/agustinmed13/RetencionesRG830.git
cd RetencionesRG830
dotnet run --project src/RetencionesRG830.Web
```

La base de datos SQLite se crea sola en el primer arranque, con las migraciones
aplicadas y los datos iniciales cargados (regímenes, un cliente de ejemplo y sus
proveedores).

Abrir `http://localhost:5149` e ingresar con:

| Usuario | Contraseña | Rol |
|---|---|---|
| `estudio@ejemplo.com` | `Estudio123!` | Estudio (acceso total) |
| `operador@cliente.com` | `Cliente123!` | Cliente (acceso restringido) |

Para correr los tests:

```bash
dotnet test
```

---

## Estado del proyecto

| Etapa | Contenido | Estado |
|---|---|---|
| 1 | Modelo de datos, arquitectura en capas, autenticación con roles, ABM de clientes y proveedores con validación de CUIT | Completo |
| 2 | Motor de cálculo con tests contra datos reales | Completo |
| 3 | Registro de operaciones con acumulado mensual automático | Completo |
| 4 | Certificado en PDF y exportación para SICORE | Completo |
| 4 | Envío del certificado por correo | Pendiente |
| 5 | Historial y búsqueda de certificados, reportes, despliegue | Pendiente |
| 6 | Manual de usuario y validación con el estudio | Pendiente |

---

## Licencia

La librería QuestPDF se utiliza bajo su
[licencia Community](https://www.questpdf.com/license/community.html), gratuita
para uso individual y para organizaciones por debajo del umbral de facturación
que allí se establece.
