using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class AulaEmDuplaPrecoDeAlunoEAppInstalado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PrecoDupla",
                table: "LocalAula",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PrecoTrio",
                table: "LocalAula",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InstalouAppEm",
                table: "Jogador",
                type: "timestamp without time zone",
                nullable: true);

            // defaultValue 1, NÃO o 0 que o EF gera sozinho: toda aula anterior a esta coluna
            // era individual — era o único preço que existia. Deixar 0 escreveria "aula com
            // zero alunos" em cima de todo o histórico do primeiro professor de verdade.
            migrationBuilder.AddColumn<int>(
                name: "QuantidadeAlunos",
                table: "Aula",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "PrecoDeAluno",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProfessorId = table.Column<int>(type: "integer", nullable: false),
                    AlunoId = table.Column<int>(type: "integer", nullable: true),
                    NomeAvulso = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Preco = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Observacao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrecoDeAluno", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrecoDeAluno_Jogador_AlunoId",
                        column: x => x.AlunoId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrecoDeAluno_Jogador_ProfessorId",
                        column: x => x.ProfessorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrecoDeAluno_AlunoId",
                table: "PrecoDeAluno",
                column: "AlunoId");

            migrationBuilder.CreateIndex(
                name: "IX_PrecoDeAluno_ProfessorId",
                table: "PrecoDeAluno",
                column: "ProfessorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrecoDeAluno");

            migrationBuilder.DropColumn(
                name: "PrecoDupla",
                table: "LocalAula");

            migrationBuilder.DropColumn(
                name: "PrecoTrio",
                table: "LocalAula");

            migrationBuilder.DropColumn(
                name: "InstalouAppEm",
                table: "Jogador");

            migrationBuilder.DropColumn(
                name: "QuantidadeAlunos",
                table: "Aula");
        }
    }
}
