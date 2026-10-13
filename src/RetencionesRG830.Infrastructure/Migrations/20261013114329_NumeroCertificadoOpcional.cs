using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetencionesRG830.Infrastructure.Migrations
{
    /// <summary>
    /// Regla 10: una operación que no retuvo no emite certificado, así que
    /// NumeroCertificado pasa a admitir null. En SQLite cambiar la nulabilidad
    /// obliga a reconstruir la tabla; EF Core lo hace solo y conserva los datos y
    /// el índice único (ClienteId, NumeroCertificado), donde los null no chocan.
    ///
    /// Cuidado con Down: pone 0 en las operaciones sin número, y con dos o más de
    /// un mismo cliente el índice único lo rechaza. No se puede revertir sin antes
    /// resolver esas filas.
    /// </summary>
    public partial class NumeroCertificadoOpcional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "NumeroCertificado",
                table: "Operaciones",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "NumeroCertificado",
                table: "Operaciones",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
