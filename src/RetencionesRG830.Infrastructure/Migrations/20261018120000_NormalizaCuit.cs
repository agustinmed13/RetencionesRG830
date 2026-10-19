using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetencionesRG830.Infrastructure.Migrations
{
    /// <summary>
    /// Deja los CUIT ya guardados de Clientes y Proveedores en la forma canónica:
    /// sólo los 11 dígitos, sin guiones, puntos, espacios, espacios duros ni
    /// tabulaciones (la misma regla que NormalizadorCuit aplica desde ahora a todo
    /// CUIT nuevo). No cambia el esquema: sólo los datos.
    ///
    /// Antes de modificar nada, controla dos cosas y, si alguna falla, la migración
    /// se cancela entera (EF Core la corre dentro de una transacción) con un mensaje
    /// que nombra las filas:
    ///
    /// - Que dos filas no queden con el mismo CUIT: el mismo cliente cargado dos
    ///   veces, o el mismo proveedor dos veces en un cliente (regla 8). El índice
    ///   único las rechazaría con un error que no dice cuáles son. Además puede
    ///   haber operaciones registradas contra cada una: cuál se conserva y qué se
    ///   hace con sus operaciones lo decide una persona, no la migración.
    /// - Que cada CUIT quede con exactamente 11 dígitos. Uno que no los tenga no es
    ///   un CUIT, y guardarlo "normalizado" escondería el error.
    ///
    /// No controla el dígito verificador: eso lo hace ValidadorCuit al guardar, y
    /// todo CUIT que entró por un formulario ya pasó por él.
    ///
    /// Sólo para SQL Server, como la migración inicial. Los tests con SQLite crean
    /// la base desde el modelo (EnsureCreated) y no pasan por acá.
    /// </summary>
    public partial class NormalizaCuit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Un solo bloque de SQL: las variables y las tablas temporales tienen que
            // vivir en el mismo lote.
            //
            // T-SQL no tiene una función para "sacar todo lo que no sea dígito", así
            // que se saca el primer carácter que no lo es, de a uno, hasta que no
            // quede ninguno. PATINDEX se evalúa con la intercalación binaria
            // (Latin1_General_BIN): con la de la base (Modern_Spanish_CI_AS), el
            // rango [0-9] compara por orden alfabético y puede dejar pasar caracteres
            // que no son los dígitos del 0 al 9.
            //
            // El control de los 11 dígitos usa DATALENGTH (2 bytes por carácter en
            // nvarchar), no LEN, que no cuenta los espacios finales.
            //
            // Las tablas temporales viven en tempdb y, sin COLLATE DATABASE_DEFAULT,
            // tomarían la intercalación del servidor, que puede no ser la de la base
            // (LocalDB viene con otra): mezclarlas con Clientes y Proveedores daría
            // un error de conflicto de intercalación.
            migrationBuilder.Sql(@"
SET NOCOUNT ON;

CREATE TABLE #CuitClientes (
    Id int NOT NULL PRIMARY KEY,
    Original nvarchar(13) COLLATE DATABASE_DEFAULT NOT NULL,
    Normalizado nvarchar(13) COLLATE DATABASE_DEFAULT NOT NULL);

CREATE TABLE #CuitProveedores (
    Id int NOT NULL PRIMARY KEY,
    ClienteId int NOT NULL,
    Original nvarchar(13) COLLATE DATABASE_DEFAULT NOT NULL,
    Normalizado nvarchar(13) COLLATE DATABASE_DEFAULT NOT NULL);

INSERT INTO #CuitClientes (Id, Original, Normalizado)
SELECT Id, Cuit, Cuit FROM Clientes;

INSERT INTO #CuitProveedores (Id, ClienteId, Original, Normalizado)
SELECT Id, ClienteId, Cuit, Cuit FROM Proveedores;

WHILE EXISTS (SELECT 1 FROM #CuitClientes
              WHERE PATINDEX(N'%[^0-9]%', Normalizado COLLATE Latin1_General_BIN) > 0)
    UPDATE #CuitClientes
    SET Normalizado = STUFF(Normalizado, PATINDEX(N'%[^0-9]%', Normalizado COLLATE Latin1_General_BIN), 1, N'')
    WHERE PATINDEX(N'%[^0-9]%', Normalizado COLLATE Latin1_General_BIN) > 0;

WHILE EXISTS (SELECT 1 FROM #CuitProveedores
              WHERE PATINDEX(N'%[^0-9]%', Normalizado COLLATE Latin1_General_BIN) > 0)
    UPDATE #CuitProveedores
    SET Normalizado = STUFF(Normalizado, PATINDEX(N'%[^0-9]%', Normalizado COLLATE Latin1_General_BIN), 1, N'')
    WHERE PATINDEX(N'%[^0-9]%', Normalizado COLLATE Latin1_General_BIN) > 0;

DECLARE @problemas nvarchar(max) = N'';

-- Clientes que quedarían con el mismo CUIT.
SELECT @problemas = @problemas + NCHAR(10) + N'- Clientes con el CUIT ' + n.Normalizado + N': '
    + STRING_AGG(CONVERT(nvarchar(max), N'Id ' + CAST(c.Id AS nvarchar(10)) + N' (' + cl.RazonSocial
        + N', guardado como ""' + c.Original + N'"")'), N'; ')
FROM (SELECT Normalizado FROM #CuitClientes GROUP BY Normalizado HAVING COUNT(*) > 1) n
JOIN #CuitClientes c ON c.Normalizado = n.Normalizado
JOIN Clientes cl ON cl.Id = c.Id
GROUP BY n.Normalizado;

-- Proveedores de un mismo cliente que quedarían con el mismo CUIT.
SELECT @problemas = @problemas + NCHAR(10) + N'- Proveedores del cliente Id ' + CAST(n.ClienteId AS nvarchar(10))
    + N' con el CUIT ' + n.Normalizado + N': '
    + STRING_AGG(CONVERT(nvarchar(max), N'Id ' + CAST(p.Id AS nvarchar(10)) + N' (' + pr.RazonSocial
        + N', guardado como ""' + p.Original + N'"")'), N'; ')
FROM (SELECT ClienteId, Normalizado FROM #CuitProveedores GROUP BY ClienteId, Normalizado HAVING COUNT(*) > 1) n
JOIN #CuitProveedores p ON p.ClienteId = n.ClienteId AND p.Normalizado = n.Normalizado
JOIN Proveedores pr ON pr.Id = p.Id
GROUP BY n.ClienteId, n.Normalizado;

-- CUIT que no quedan con 11 dígitos.
SELECT @problemas = @problemas + NCHAR(10) + N'- Cliente Id ' + CAST(c.Id AS nvarchar(10)) + N' (' + cl.RazonSocial
    + N'): ""' + c.Original + N'"" no tiene 11 dígitos.'
FROM #CuitClientes c JOIN Clientes cl ON cl.Id = c.Id
WHERE DATALENGTH(c.Normalizado) / 2 <> 11;

SELECT @problemas = @problemas + NCHAR(10) + N'- Proveedor Id ' + CAST(p.Id AS nvarchar(10)) + N' (' + pr.RazonSocial
    + N'): ""' + p.Original + N'"" no tiene 11 dígitos.'
FROM #CuitProveedores p JOIN Proveedores pr ON pr.Id = p.Id
WHERE DATALENGTH(p.Normalizado) / 2 <> 11;

IF @problemas <> N''
BEGIN
    -- THROW admite hasta 2048 caracteres: con muchas filas, el mensaje se corta.
    DECLARE @mensaje nvarchar(2048) = LEFT(
        N'NormalizaCuit: no se normalizó ningún CUIT. Hay filas que hay que corregir a mano antes de volver a correr la migración:'
        + @problemas, 2048);
    THROW 50000, @mensaje, 1;
END;

UPDATE cl SET Cuit = c.Normalizado
FROM Clientes cl JOIN #CuitClientes c ON c.Id = cl.Id;

UPDATE pr SET Cuit = p.Normalizado
FROM Proveedores pr JOIN #CuitProveedores p ON p.Id = pr.Id;

DROP TABLE #CuitClientes;
DROP TABLE #CuitProveedores;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No hay vuelta atrás: la forma en que se había escrito cada CUIT no se
            // guardó en ningún lado. Tampoco hace falta: el CUIT normalizado entra en
            // la columna de antes y lo lee igual el código anterior a esta migración.
        }
    }
}
