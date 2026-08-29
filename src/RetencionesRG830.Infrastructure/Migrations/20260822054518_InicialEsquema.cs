using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetencionesRG830.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InicialEsquema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Cuit = table.Column<string>(type: "TEXT", maxLength: 13, nullable: false),
                    RazonSocial = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Domicilio = table.Column<string>(type: "TEXT", nullable: false),
                    Localidad = table.Column<string>(type: "TEXT", nullable: false),
                    NombreFirmante = table.Column<string>(type: "TEXT", nullable: false),
                    CargoFirmante = table.Column<string>(type: "TEXT", nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Regimenes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Codigo = table.Column<int>(type: "INTEGER", nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    TipoCalculo = table.Column<int>(type: "INTEGER", nullable: false),
                    TasaInscripto = table.Column<decimal>(type: "TEXT", nullable: false),
                    TasaNoInscriptoHumana = table.Column<decimal>(type: "TEXT", nullable: false),
                    TasaNoInscriptoResto = table.Column<decimal>(type: "TEXT", nullable: false),
                    MontoNoSujetoARetencion = table.Column<decimal>(type: "TEXT", nullable: false),
                    MinimoRetencion = table.Column<decimal>(type: "TEXT", nullable: false),
                    VigenciaDesde = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    VigenciaHasta = table.Column<DateOnly>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Regimenes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Proveedores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClienteId = table.Column<int>(type: "INTEGER", nullable: false),
                    Cuit = table.Column<string>(type: "TEXT", maxLength: 13, nullable: false),
                    RazonSocial = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Domicilio = table.Column<string>(type: "TEXT", nullable: false),
                    Localidad = table.Column<string>(type: "TEXT", nullable: false),
                    InscriptoEnGanancias = table.Column<bool>(type: "INTEGER", nullable: false),
                    TipoPersona = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Proveedores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Proveedores_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    Rol = table.Column<int>(type: "INTEGER", nullable: false),
                    ClienteId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Usuarios_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TramosEscala",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RegimenId = table.Column<int>(type: "INTEGER", nullable: false),
                    Desde = table.Column<decimal>(type: "TEXT", nullable: false),
                    Hasta = table.Column<decimal>(type: "TEXT", nullable: true),
                    MontoFijo = table.Column<decimal>(type: "TEXT", nullable: false),
                    Porcentaje = table.Column<decimal>(type: "TEXT", nullable: false),
                    VigenciaDesde = table.Column<DateOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TramosEscala", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TramosEscala_Regimenes_RegimenId",
                        column: x => x.RegimenId,
                        principalTable: "Regimenes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Operaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClienteId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProveedorId = table.Column<int>(type: "INTEGER", nullable: false),
                    RegimenId = table.Column<int>(type: "INTEGER", nullable: false),
                    TipoComprobante = table.Column<int>(type: "INTEGER", nullable: false),
                    PuntoVenta = table.Column<int>(type: "INTEGER", nullable: false),
                    NumeroComprobante = table.Column<long>(type: "INTEGER", nullable: false),
                    FechaComprobante = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ImporteComprobante = table.Column<decimal>(type: "TEXT", nullable: false),
                    ImporteGravado = table.Column<decimal>(type: "TEXT", nullable: false),
                    FechaRetencion = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    MontoRetenido = table.Column<decimal>(type: "TEXT", nullable: false),
                    NumeroCertificado = table.Column<int>(type: "INTEGER", nullable: false),
                    CreadoPorUsuarioId = table.Column<int>(type: "INTEGER", nullable: false),
                    FechaCarga = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Operaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Operaciones_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Operaciones_Proveedores_ProveedorId",
                        column: x => x.ProveedorId,
                        principalTable: "Proveedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Operaciones_Regimenes_RegimenId",
                        column: x => x.RegimenId,
                        principalTable: "Regimenes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_Cuit",
                table: "Clientes",
                column: "Cuit",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Operaciones_ClienteId_NumeroCertificado",
                table: "Operaciones",
                columns: new[] { "ClienteId", "NumeroCertificado" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Operaciones_ProveedorId",
                table: "Operaciones",
                column: "ProveedorId");

            migrationBuilder.CreateIndex(
                name: "IX_Operaciones_RegimenId",
                table: "Operaciones",
                column: "RegimenId");

            migrationBuilder.CreateIndex(
                name: "IX_Proveedores_ClienteId_Cuit",
                table: "Proveedores",
                columns: new[] { "ClienteId", "Cuit" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Regimenes_Codigo_VigenciaDesde",
                table: "Regimenes",
                columns: new[] { "Codigo", "VigenciaDesde" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TramosEscala_RegimenId",
                table: "TramosEscala",
                column: "RegimenId");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_ClienteId",
                table: "Usuarios",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Operaciones");

            migrationBuilder.DropTable(
                name: "TramosEscala");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "Proveedores");

            migrationBuilder.DropTable(
                name: "Regimenes");

            migrationBuilder.DropTable(
                name: "Clientes");
        }
    }
}
