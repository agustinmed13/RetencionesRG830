# Sistema de Retenciones RG 830 — Proyecto final

Este es el proyecto en .NET que reemplaza la planilla Excel de retención de
Ganancias (RG 830). Está recién arrancado: por ahora sólo tiene la
estructura de carpetas y las clases del "Domain" (los datos del negocio).
Se va a ir completando en los próximos avances.

## Qué es cada carpeta (la arquitectura en capas)

```
RetencionesRG830.sln
src/
  RetencionesRG830.Domain/          <- Las entidades: Cliente, Proveedor,
                                        Usuario, Regimen, TramoEscala, Operacion.
                                        No depende de ningún otro proyecto.
  RetencionesRG830.Application/     <- Acá van a vivir los servicios: el que
                                        calcula la retención, el que genera
                                        el certificado, el que arma el SICORE.
                                        Depende de Domain.
  RetencionesRG830.Infrastructure/  <- Acá va a vivir el acceso a la base de
                                        datos (Entity Framework Core).
                                        Depende de Domain y Application.
  RetencionesRG830.Web/             <- La aplicación web (ASP.NET Core MVC):
                                        las pantallas que va a usar el
                                        estudio y cada cliente.
                                        Depende de los tres anteriores.
tests/
  RetencionesRG830.Tests/           <- Los tests automáticos, donde vamos a
                                        comprobar que el cálculo da lo mismo
                                        que la planilla Excel actual.
```

La idea de separar en capas es que el "Domain" no sepa nada de bases de
datos ni de páginas web -sólo son clases de datos simples-, así se puede
testear la lógica de cálculo de forma aislada y cambiar la tecnología de
alguna capa sin romper las demás.

## Cómo abrir el proyecto

**Opción A - Visual Studio Community (recomendado si usás Windows):**
Es gratis. Se descarga desde visualstudio.microsoft.com/es/vs/community.
Al instalarlo, elegir la carga de trabajo "Desarrollo de ASP.NET y web".
Una vez instalado, abrir el archivo `RetencionesRG830.sln` con doble clic.

**Opción B - Visual Studio Code (multiplataforma):**
Instalar VS Code y el SDK de .NET 8 desde dotnet.microsoft.com/download
(elegir ".NET 8.0", que es la versión LTS). Instalar además la extensión
"C# Dev Kit" desde el marketplace de VS Code. Después abrir la carpeta
`RetencionesRG830` con VS Code.

## Cómo restaurar y compilar

Este proyecto se armó en un entorno sin acceso a NuGet (el repositorio
oficial de paquetes de .NET), así que **la primera vez que lo abras en tu
computadora con internet, necesitás restaurar los paquetes**. Desde una
terminal, parado en la carpeta `RetencionesRG830`:

```
dotnet restore
dotnet build
```

Si usás Visual Studio, alcanza con abrir la solución: restaura solo. Los
proyectos Domain, Application, Infrastructure y Web no necesitan ningún
paquete externo todavía, así que deberían compilar sin problema apenas los
abras. El proyecto de Tests sí necesita paquetes de xUnit (el framework de
testeo) que se descargan automáticamente la primera vez que tengas
internet.

## Próximos pasos

Esto es sólo el punto de partida (hito 1 del cronograma). Lo que sigue es:
agregar Entity Framework Core para conectar esto a una base de datos real,
armar el motor de cálculo en el proyecto Application con sus tests, y
recién después las pantallas del proyecto Web.
