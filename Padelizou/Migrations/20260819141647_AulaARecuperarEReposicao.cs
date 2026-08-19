using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class AulaARecuperarEReposicao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RecuperaAulaId",
                table: "Aula",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Aula_RecuperaAulaId",
                table: "Aula",
                column: "RecuperaAulaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Aula_Aula_RecuperaAulaId",
                table: "Aula",
                column: "RecuperaAulaId",
                principalTable: "Aula",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Aula_Aula_RecuperaAulaId",
                table: "Aula");

            migrationBuilder.DropIndex(
                name: "IX_Aula_RecuperaAulaId",
                table: "Aula");

            migrationBuilder.DropColumn(
                name: "RecuperaAulaId",
                table: "Aula");
        }
    }
}
