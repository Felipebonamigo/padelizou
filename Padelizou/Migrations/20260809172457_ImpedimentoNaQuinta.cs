using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class ImpedimentoNaQuinta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PermiteImpedimentoQuintaNoite",
                table: "Torneio",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ImpedimentoQuintaNoite",
                table: "Dupla",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PermiteImpedimentoQuintaNoite",
                table: "Torneio");

            migrationBuilder.DropColumn(
                name: "ImpedimentoQuintaNoite",
                table: "Dupla");
        }
    }
}
