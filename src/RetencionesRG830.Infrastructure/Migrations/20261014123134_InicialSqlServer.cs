using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetencionesRG830.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InicialSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Cuit = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    RazonSocial = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Domicilio = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Localidad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NombreFirmante = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CargoFirmante = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Regimenes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<int>(type: "int", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    TipoCalculo = table.Column<int>(type: "int", nullable: false),
                    TasaInscripto = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    TasaNoInscriptoHumana = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    TasaNoInscriptoResto = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    MontoNoSujetoARetencion = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MinimoRetencion = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MinimoRetencionNoInscripto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 240m),
                    VigenciaDesde = table.Column<DateOnly>(type: "date", nullable: false),
                    VigenciaHasta = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Regimenes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Proveedores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClienteId = table.Column<int>(type: "int", nullable: false),
                    Cuit = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    RazonSocial = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Domicilio = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Localidad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InscriptoEnGanancias = table.Column<bool>(type: "bit", nullable: false),
                    TipoPersona = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Rol = table.Column<int>(type: "int", nullable: false),
                    ClienteId = table.Column<int>(type: "int", nullable: true)
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RegimenId = table.Column<int>(type: "int", nullable: false),
                    Desde = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Hasta = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MontoFijo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Porcentaje = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    VigenciaDesde = table.Column<DateOnly>(type: "date", nullable: false)
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClienteId = table.Column<int>(type: "int", nullable: false),
                    ProveedorId = table.Column<int>(type: "int", nullable: false),
                    RegimenId = table.Column<int>(type: "int", nullable: false),
                    TipoComprobante = table.Column<int>(type: "int", nullable: false),
                    PuntoVenta = table.Column<int>(type: "int", nullable: false),
                    NumeroComprobante = table.Column<long>(type: "bigint", nullable: false),
                    FechaComprobante = table.Column<DateOnly>(type: "date", nullable: false),
                    ImporteComprobante = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ImporteGravado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FechaRetencion = table.Column<DateOnly>(type: "date", nullable: false),
                    BaseCalculoAcumulada = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MontoRetenido = table.Column<decimal>(type: "decimal(20,8)", precision: 20, scale: 8, nullable: false),
                    NumeroCertificado = table.Column<int>(type: "int", nullable: true),
                    CreadoPorUsuarioId = table.Column<int>(type: "int", nullable: false),
                    FechaCarga = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Anulada = table.Column<bool>(type: "bit", nullable: false),
                    FechaAnulacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoAnulacion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AnuladaPorUsuarioId = table.Column<int>(type: "int", nullable: true)
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
                unique: true,
                filter: "[NumeroCertificado] IS NOT NULL");

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
