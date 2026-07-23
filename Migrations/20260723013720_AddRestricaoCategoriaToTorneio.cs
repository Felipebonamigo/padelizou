using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class AddRestricaoCategoriaToTorneio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RestricaoCategoria",
                table: "Torneio",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Livre");

            // Migra os torneios que já usavam o bool antigo (chegou à final) para o novo gatilho.
            migrationBuilder.Sql("UPDATE [Torneio] SET [RestricaoCategoria] = 'Final' WHERE [BloquearCategoriaInferior] = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RestricaoCategoria",
                table: "Torneio");
        }
    }
}
