using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class CategoriasIniciantesDesligadas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ⚠️ defaultValue TRUE, e NÃO o `false` que o EF gera sozinho (é o default do tipo
            // bool). Com `false` toda categoria já existente sairia desligada e o sistema
            // acordaria sem catálogo nenhum — ninguém conseguiria criar torneio.
            migrationBuilder.AddColumn<bool>(
                name: "Ativa",
                table: "CategoriaPadrao",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // As duas que saem de circulação. Casa por código E por nome porque o catálogo foi
            // semeado em três bancos em momentos diferentes, e basta uma linha escapar pra
            // categoria continuar aparecendo num deles.
            migrationBuilder.Sql(@"
                UPDATE ""CategoriaPadrao"" SET ""Ativa"" = false
                WHERE ""Codigo"" IN ('ICatM', 'ICatF')
                   OR ""Nome"" IN ('Categoria Iniciantes Masculina', 'Categoria Iniciantes Feminina');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Ativa",
                table: "CategoriaPadrao");
        }
    }
}
