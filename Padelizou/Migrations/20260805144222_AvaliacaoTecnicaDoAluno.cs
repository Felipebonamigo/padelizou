using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class AvaliacaoTecnicaDoAluno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AvaliacaoDeAluno",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProfessorId = table.Column<int>(type: "integer", nullable: false),
                    AlunoId = table.Column<int>(type: "integer", nullable: false),
                    Modulo = table.Column<string>(type: "text", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    VisivelParaAluno = table.Column<bool>(type: "boolean", nullable: false),
                    LiberadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RecadoAoAluno = table.Column<string>(type: "text", nullable: true),
                    AnotacaoPrivada = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvaliacaoDeAluno", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AvaliacaoDeAluno_Jogador_AlunoId",
                        column: x => x.AlunoId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AvaliacaoDeAluno_Jogador_ProfessorId",
                        column: x => x.ProfessorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FundamentoDoProfessor",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProfessorId = table.Column<int>(type: "integer", nullable: false),
                    Modulo = table.Column<string>(type: "text", nullable: false),
                    Familia = table.Column<string>(type: "text", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundamentoDoProfessor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FundamentoDoProfessor_Jogador_ProfessorId",
                        column: x => x.ProfessorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotaDeFundamento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AvaliacaoDeAlunoId = table.Column<int>(type: "integer", nullable: false),
                    FundamentoDoProfessorId = table.Column<int>(type: "integer", nullable: false),
                    Execucao = table.Column<string>(type: "text", nullable: true),
                    Acertos = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotaDeFundamento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotaDeFundamento_AvaliacaoDeAluno_AvaliacaoDeAlunoId",
                        column: x => x.AvaliacaoDeAlunoId,
                        principalTable: "AvaliacaoDeAluno",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotaDeFundamento_FundamentoDoProfessor_FundamentoDoProfesso~",
                        column: x => x.FundamentoDoProfessorId,
                        principalTable: "FundamentoDoProfessor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AvaliacaoDeAluno_AlunoId_VisivelParaAluno",
                table: "AvaliacaoDeAluno",
                columns: new[] { "AlunoId", "VisivelParaAluno" });

            migrationBuilder.CreateIndex(
                name: "IX_AvaliacaoDeAluno_ProfessorId_AlunoId_CriadoEm",
                table: "AvaliacaoDeAluno",
                columns: new[] { "ProfessorId", "AlunoId", "CriadoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_FundamentoDoProfessor_ProfessorId_Modulo_Ordem",
                table: "FundamentoDoProfessor",
                columns: new[] { "ProfessorId", "Modulo", "Ordem" });

            migrationBuilder.CreateIndex(
                name: "IX_NotaDeFundamento_AvaliacaoDeAlunoId_FundamentoDoProfessorId",
                table: "NotaDeFundamento",
                columns: new[] { "AvaliacaoDeAlunoId", "FundamentoDoProfessorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotaDeFundamento_FundamentoDoProfessorId",
                table: "NotaDeFundamento",
                column: "FundamentoDoProfessorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotaDeFundamento");

            migrationBuilder.DropTable(
                name: "AvaliacaoDeAluno");

            migrationBuilder.DropTable(
                name: "FundamentoDoProfessor");
        }
    }
}
