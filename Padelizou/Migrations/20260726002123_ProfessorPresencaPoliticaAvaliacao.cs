using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class ProfessorPresencaPoliticaAvaliacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApresentacaoProfessor",
                table: "Jogador",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CobraFaltaSemAviso",
                table: "Jogador",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ExperienciaProfessor",
                table: "Jogador",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HorasMinimasCancelamento",
                table: "Jogador",
                type: "integer",
                nullable: false,
                // 24h pra bater com o default do modelo — professor antigo e novo ficam com
                // a mesma regra. Sem risco: CobraFaltaSemAviso nasce false, então nada é
                // cobrado; o campo só muda o texto que o aluno lê.
                defaultValue: 24);

            migrationBuilder.AddColumn<string>(
                name: "PoliticaCancelamentoTexto",
                table: "Jogador",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CanceladaEm",
                table: "Aula",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CanceladaPor",
                table: "Aula",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CobrarMesmoFaltando",
                table: "Aula",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Compareceu",
                table: "Aula",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HorasDeAntecedenciaCancelamento",
                table: "Aula",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AvaliacaoProfessor",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProfessorId = table.Column<int>(type: "integer", nullable: false),
                    AlunoId = table.Column<int>(type: "integer", nullable: false),
                    Nota = table.Column<int>(type: "integer", nullable: false),
                    Depoimento = table.Column<string>(type: "text", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvaliacaoProfessor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AvaliacaoProfessor_Jogador_AlunoId",
                        column: x => x.AlunoId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AvaliacaoProfessor_Jogador_ProfessorId",
                        column: x => x.ProfessorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AvaliacaoProfessor_AlunoId_ProfessorId",
                table: "AvaliacaoProfessor",
                columns: new[] { "AlunoId", "ProfessorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AvaliacaoProfessor_ProfessorId",
                table: "AvaliacaoProfessor",
                column: "ProfessorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AvaliacaoProfessor");

            migrationBuilder.DropColumn(
                name: "ApresentacaoProfessor",
                table: "Jogador");

            migrationBuilder.DropColumn(
                name: "CobraFaltaSemAviso",
                table: "Jogador");

            migrationBuilder.DropColumn(
                name: "ExperienciaProfessor",
                table: "Jogador");

            migrationBuilder.DropColumn(
                name: "HorasMinimasCancelamento",
                table: "Jogador");

            migrationBuilder.DropColumn(
                name: "PoliticaCancelamentoTexto",
                table: "Jogador");

            migrationBuilder.DropColumn(
                name: "CanceladaEm",
                table: "Aula");

            migrationBuilder.DropColumn(
                name: "CanceladaPor",
                table: "Aula");

            migrationBuilder.DropColumn(
                name: "CobrarMesmoFaltando",
                table: "Aula");

            migrationBuilder.DropColumn(
                name: "Compareceu",
                table: "Aula");

            migrationBuilder.DropColumn(
                name: "HorasDeAntecedenciaCancelamento",
                table: "Aula");
        }
    }
}
