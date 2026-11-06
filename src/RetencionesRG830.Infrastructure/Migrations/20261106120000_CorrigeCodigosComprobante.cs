using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetencionesRG830.Infrastructure.Migrations
{
    /// <summary>
    /// Corrige los códigos de comprobante ya guardados. La tabla del sistema tenía
    /// invertidos dos tipos: 2 era Nota de débito y 4 era Recibo, y en SICORE es al
    /// revés (02 Recibo, 04 Nota de débito). No cambia el esquema: sólo los datos.
    ///
    /// Hace falta porque la base guarda el número, no el nombre. Sin esta migración,
    /// una operación que el operador cargó como Recibo (guardada con 4) se mostraría
    /// como Nota de débito al corregir la tabla, también en el certificado, que se
    /// arma de nuevo en cada descarga. El intercambio conserva lo que el operador
    /// eligió. Nota de crédito es 3 en las dos tablas y Factura es 1: no se tocan.
    ///
    /// Tiene que ser una sola sentencia: en dos pasos (2 a 4 y después 4 a 2) todas
    /// las filas terminarían con el mismo valor. Incluye las anuladas, porque el
    /// detalle también les muestra el tipo.
    ///
    /// Aplicar el intercambio dos veces lo desharía. No pasa, porque EF Core registra
    /// cada migración aplicada y no la repite.
    ///
    /// Sólo para SQL Server, como las anteriores. Los tests con SQLite crean la base
    /// desde el modelo (EnsureCreated) y no pasan por acá.
    /// </summary>
    public partial class CorrigeCodigosComprobante : Migration
    {
        private const string IntercambiarRecibosYNotasDeDebito = @"
UPDATE Operaciones
SET TipoComprobante = CASE TipoComprobante WHEN 2 THEN 4 ELSE 2 END
WHERE TipoComprobante IN (2, 4);";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(IntercambiarRecibosYNotasDeDebito);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // El intercambio es su propia vuelta atrás: aplicado otra vez deja cada
            // fila como estaba.
            migrationBuilder.Sql(IntercambiarRecibosYNotasDeDebito);
        }
    }
}
