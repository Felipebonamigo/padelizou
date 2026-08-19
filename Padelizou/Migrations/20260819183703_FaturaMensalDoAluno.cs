using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class FaturaMensalDoAluno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FaturaDoAluno",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProfessorId = table.Column<int>(type: "integer", nullable: false),
                    AlunoId = table.Column<int>(type: "integer", nullable: true),
                    NomeAvulso = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Ano = table.Column<int>(type: "integer", nullable: false),
                    Mes = table.Column<int>(type: "integer", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    QuantidadeAulas = table.Column<int>(type: "integer", nullable: false),
                    PagadorNome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PagadorCelular = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PagadorCpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    FechadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Vencimento = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PagaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PagamentoId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaturaDoAluno", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FaturaDoAluno_Jogador_AlunoId",
                        column: x => x.AlunoId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FaturaDoAluno_Jogador_ProfessorId",
                        column: x => x.ProfessorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FaturaDoAluno_Pagamento_PagamentoId",
                        column: x => x.PagamentoId,
                        principalTable: "Pagamento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FaturaDoAluno_AlunoId",
                table: "FaturaDoAluno",
                column: "AlunoId");

            migrationBuilder.CreateIndex(
                name: "IX_FaturaDoAluno_PagamentoId",
                table: "FaturaDoAluno",
                column: "PagamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_FaturaDoAluno_ProfessorId_Ano_Mes",
                table: "FaturaDoAluno",
                columns: new[] { "ProfessorId", "Ano", "Mes" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FaturaDoAluno");
        }
    }
}
