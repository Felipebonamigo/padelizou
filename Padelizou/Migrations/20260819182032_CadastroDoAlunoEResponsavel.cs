using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class CadastroDoAlunoEResponsavel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CadastroDoAluno",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProfessorId = table.Column<int>(type: "integer", nullable: false),
                    AlunoId = table.Column<int>(type: "integer", nullable: true),
                    NomeAvulso = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Celular = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ResponsavelNome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ResponsavelCelular = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ResponsavelCpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    Observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CadastroDoAluno", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CadastroDoAluno_Jogador_AlunoId",
                        column: x => x.AlunoId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CadastroDoAluno_Jogador_ProfessorId",
                        column: x => x.ProfessorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CadastroDoAluno_AlunoId",
                table: "CadastroDoAluno",
                column: "AlunoId");

            migrationBuilder.CreateIndex(
                name: "IX_CadastroDoAluno_Celular",
                table: "CadastroDoAluno",
                column: "Celular");

            migrationBuilder.CreateIndex(
                name: "IX_CadastroDoAluno_ProfessorId",
                table: "CadastroDoAluno",
                column: "ProfessorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CadastroDoAluno");
        }
    }
}
