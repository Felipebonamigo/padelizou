using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class AddQuemMarcaPlacarAoTorneio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QuemMarcaPlacar",
                table: "Torneio",
                type: "text",
                nullable: false,
                // Torneio que já existe cai no modo de sempre — só a organização marca.
                defaultValue: "Organizacao");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuemMarcaPlacar",
                table: "Torneio");
        }
    }
}
