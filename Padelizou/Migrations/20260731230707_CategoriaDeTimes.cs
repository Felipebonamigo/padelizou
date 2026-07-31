using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class CategoriaDeTimes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NomeTime",
                table: "Dupla",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TimeId",
                table: "Dupla",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClassificadosPorGrupo",
                table: "Categoria",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DeTimes",
                table: "Categoria",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "QuantidadeGrupos",
                table: "Categoria",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuantidadeTimes",
                table: "Categoria",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dupla_TimeId",
                table: "Dupla",
                column: "TimeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Dupla_Times_TimeId",
                table: "Dupla",
                column: "TimeId",
                principalTable: "Times",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Dupla_Times_TimeId",
                table: "Dupla");

            migrationBuilder.DropIndex(
                name: "IX_Dupla_TimeId",
                table: "Dupla");

            migrationBuilder.DropColumn(
                name: "NomeTime",
                table: "Dupla");

            migrationBuilder.DropColumn(
                name: "TimeId",
                table: "Dupla");

            migrationBuilder.DropColumn(
                name: "ClassificadosPorGrupo",
                table: "Categoria");

            migrationBuilder.DropColumn(
                name: "DeTimes",
                table: "Categoria");

            migrationBuilder.DropColumn(
                name: "QuantidadeGrupos",
                table: "Categoria");

            migrationBuilder.DropColumn(
                name: "QuantidadeTimes",
                table: "Categoria");
        }
    }
}
