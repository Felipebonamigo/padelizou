using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class GruposDoAmericano : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Grupo",
                table: "InscricaoAmericana",
                type: "text",
                nullable: true);

            // ⚠️ defaultValue 1, e NÃO o 0 que o EF gera a partir do default do int: toda
            // categoria que já existe é de GRUPO ÚNICO, e zero grupos não é torneio nenhum.
            // Com 0 aqui, todo Americano sorteado antes deste deploy passaria a ter uma
            // divisão inválida. O Postgres preenche as linhas existentes com este valor.
            migrationBuilder.AddColumn<int>(
                name: "GruposAmericano",
                table: "Categoria",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "PassamPorGrupo",
                table: "Categoria",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Grupo",
                table: "InscricaoAmericana");

            migrationBuilder.DropColumn(
                name: "GruposAmericano",
                table: "Categoria");

            migrationBuilder.DropColumn(
                name: "PassamPorGrupo",
                table: "Categoria");
        }
    }
}
