using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PalletBalancer.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarMlo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Mlos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MloNo = table.Column<string>(type: "text", nullable: false),
                    FdoId = table.Column<int>(type: "integer", nullable: false),
                    FechaEntrega = table.Column<DateOnly>(type: "date", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mlos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mlos_Fdos_FdoId",
                        column: x => x.FdoId,
                        principalTable: "Fdos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MloLineas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MloId = table.Column<int>(type: "integer", nullable: false),
                    SlipNo = table.Column<string>(type: "text", nullable: false),
                    ModelNo = table.Column<string>(type: "text", nullable: false),
                    Class = table.Column<string>(type: "text", nullable: false),
                    CaseNo = table.Column<string>(type: "text", nullable: false),
                    FromLocation = table.Column<string>(type: "text", nullable: false),
                    FromQty = table.Column<int>(type: "integer", nullable: false),
                    Check = table.Column<string>(type: "text", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    ToLocation = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MloLineas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MloLineas_Mlos_MloId",
                        column: x => x.MloId,
                        principalTable: "Mlos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MloLineas_MloId",
                table: "MloLineas",
                column: "MloId");

            migrationBuilder.CreateIndex(
                name: "IX_Mlos_FdoId",
                table: "Mlos",
                column: "FdoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MloLineas");

            migrationBuilder.DropTable(
                name: "Mlos");
        }
    }
}
