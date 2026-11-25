using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class TretaChaveDtn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ForaDoRanking",
                table: "Torneio",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "CategoriaDeOrigemId",
                table: "Categoria",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DuplasNoDtnPorGrupo",
                table: "Categoria",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PapelDaChave",
                table: "Categoria",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParDaFinalissimaId",
                table: "Categoria",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TrofeuDtnJogadorId",
                table: "Categoria",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categoria_CategoriaDeOrigemId",
                table: "Categoria",
                column: "CategoriaDeOrigemId");

            migrationBuilder.CreateIndex(
                name: "IX_Categoria_ParDaFinalissimaId",
                table: "Categoria",
                column: "ParDaFinalissimaId");

            migrationBuilder.CreateIndex(
                name: "IX_Categoria_TrofeuDtnJogadorId",
                table: "Categoria",
                column: "TrofeuDtnJogadorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Categoria_Categoria_CategoriaDeOrigemId",
                table: "Categoria",
                column: "CategoriaDeOrigemId",
                principalTable: "Categoria",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Categoria_Categoria_ParDaFinalissimaId",
                table: "Categoria",
                column: "ParDaFinalissimaId",
                principalTable: "Categoria",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Categoria_Jogador_TrofeuDtnJogadorId",
                table: "Categoria",
                column: "TrofeuDtnJogadorId",
                principalTable: "Jogador",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categoria_Categoria_CategoriaDeOrigemId",
                table: "Categoria");

            migrationBuilder.DropForeignKey(
                name: "FK_Categoria_Categoria_ParDaFinalissimaId",
                table: "Categoria");

            migrationBuilder.DropForeignKey(
                name: "FK_Categoria_Jogador_TrofeuDtnJogadorId",
                table: "Categoria");

            migrationBuilder.DropIndex(
                name: "IX_Categoria_CategoriaDeOrigemId",
                table: "Categoria");

            migrationBuilder.DropIndex(
                name: "IX_Categoria_ParDaFinalissimaId",
                table: "Categoria");

            migrationBuilder.DropIndex(
                name: "IX_Categoria_TrofeuDtnJogadorId",
                table: "Categoria");

            migrationBuilder.DropColumn(
                name: "ForaDoRanking",
                table: "Torneio");

            migrationBuilder.DropColumn(
                name: "CategoriaDeOrigemId",
                table: "Categoria");

            migrationBuilder.DropColumn(
                name: "DuplasNoDtnPorGrupo",
                table: "Categoria");

            migrationBuilder.DropColumn(
                name: "PapelDaChave",
                table: "Categoria");

            migrationBuilder.DropColumn(
                name: "ParDaFinalissimaId",
                table: "Categoria");

            migrationBuilder.DropColumn(
                name: "TrofeuDtnJogadorId",
                table: "Categoria");
        }
    }
}
