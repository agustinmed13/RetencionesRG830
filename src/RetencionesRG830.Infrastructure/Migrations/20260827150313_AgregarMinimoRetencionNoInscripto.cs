using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetencionesRG830.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarMinimoRetencionNoInscripto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MinimoRetencionNoInscripto",
                table: "Regimenes",
                type: "TEXT",
                nullable: false,
                defaultValue: 240m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinimoRetencionNoInscripto",
                table: "Regimenes");
        }
    }
}
