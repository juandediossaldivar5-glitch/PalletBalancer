using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PalletBalancer.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Fdos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FdoSlipNo = table.Column<string>(type: "text", nullable: false),
                    DsbDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ShipDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Customer = table.Column<string>(type: "text", nullable: false),
                    Consignee = table.Column<string>(type: "text", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fdos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Items",
                columns: table => new
                {
                    ModelNo = table.Column<string>(type: "text", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    SpPiezasPorPallet = table.Column<int>(type: "integer", nullable: false),
                    SpPesoKg = table.Column<double>(type: "double precision", nullable: false),
                    SpLargoCm = table.Column<double>(type: "double precision", nullable: false),
                    SpAnchoCm = table.Column<double>(type: "double precision", nullable: false),
                    SpAltoCm = table.Column<double>(type: "double precision", nullable: false),
                    CajaPiezasPorCaja = table.Column<int>(type: "integer", nullable: false),
                    CajaPesoKg = table.Column<double>(type: "double precision", nullable: false),
                    CajaLargoCm = table.Column<double>(type: "double precision", nullable: false),
                    CajaAnchoCm = table.Column<double>(type: "double precision", nullable: false),
                    CajaAltoCm = table.Column<double>(type: "double precision", nullable: false),
                    PiezaPesoKg = table.Column<double>(type: "double precision", nullable: false),
                    PiezaLargoCm = table.Column<double>(type: "double precision", nullable: false),
                    PiezaAnchoCm = table.Column<double>(type: "double precision", nullable: false),
                    PiezaAltoCm = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.ModelNo);
                });

            migrationBuilder.CreateTable(
                name: "FdoLineas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FdoId = table.Column<int>(type: "integer", nullable: false),
                    CustomerPoNo = table.Column<string>(type: "text", nullable: false),
                    ModelNo = table.Column<string>(type: "text", nullable: false),
                    ItemModelNo = table.Column<string>(type: "text", nullable: true),
                    ReqQty = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FdoLineas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FdoLineas_Fdos_FdoId",
                        column: x => x.FdoId,
                        principalTable: "Fdos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FdoLineas_Items_ItemModelNo",
                        column: x => x.ItemModelNo,
                        principalTable: "Items",
                        principalColumn: "ModelNo");
                });

            migrationBuilder.CreateIndex(
                name: "IX_FdoLineas_FdoId",
                table: "FdoLineas",
                column: "FdoId");

            migrationBuilder.CreateIndex(
                name: "IX_FdoLineas_ItemModelNo",
                table: "FdoLineas",
                column: "ItemModelNo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FdoLineas");

            migrationBuilder.DropTable(
                name: "Fdos");

            migrationBuilder.DropTable(
                name: "Items");
        }
    }
}
