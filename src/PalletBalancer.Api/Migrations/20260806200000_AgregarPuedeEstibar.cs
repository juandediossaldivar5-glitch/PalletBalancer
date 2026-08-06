using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PalletBalancer.Api.Migrations
{
    public partial class AgregarPuedeEstibar : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PuedeEstibar",
                table: "Items",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PuedeEstibar", table: "Items");
        }
    }
}
