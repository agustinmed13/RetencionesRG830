using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetencionesRG830.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarAnulacionDeOperaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Anulada",
                table: "Operaciones",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "AnuladaPorUsuarioId",
                table: "Operaciones",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaAnulacion",
                table: "Operaciones",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoAnulacion",
                table: "Operaciones",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Anulada",
                table: "Operaciones");

            migrationBuilder.DropColumn(
                name: "AnuladaPorUsuarioId",
                table: "Operaciones");

            migrationBuilder.DropColumn(
                name: "FechaAnulacion",
                table: "Operaciones");

            migrationBuilder.DropColumn(
                name: "MotivoAnulacion",
                table: "Operaciones");
        }
    }
}
