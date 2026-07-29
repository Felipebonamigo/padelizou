using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class AvaliacaoNotaDezEAnotacoesDeAula : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // TRUE, não o false que o EF gerou: o padrão do produto é aceitar comentários, e
            // o default do banco vale pras LINHAS EXISTENTES — com false, todo professor já
            // cadastrado acordaria com os comentários desligados sem ter escolhido isso.
            migrationBuilder.AddColumn<bool>(
                name: "DepoimentosDeAulaHabilitados",
                table: "Jogador",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // A escala mudou de 1-5 (estrelas) pra 0-10 (nota). As avaliações antigas são
            // convertidas na mesma proporção: 5 estrelas = nota 10, 3 = 6, 1 = 2. Sem isto,
            // um professor 5 estrelas viraria "5/10" — nota de reprovação — da noite pro dia.
            migrationBuilder.Sql("""UPDATE "AvaliacaoProfessor" SET "Nota" = "Nota" * 2 WHERE "Nota" <= 5;""");

            migrationBuilder.CreateTable(
                name: "AnotacaoAula",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AulaId = table.Column<int>(type: "integer", nullable: false),
                    AutorId = table.Column<int>(type: "integer", nullable: false),
                    Texto = table.Column<string>(type: "text", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnotacaoAula", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnotacaoAula_Aula_AulaId",
                        column: x => x.AulaId,
                        principalTable: "Aula",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AnotacaoAula_Jogador_AutorId",
                        column: x => x.AutorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnotacaoAula_AulaId",
                table: "AnotacaoAula",
                column: "AulaId");

            migrationBuilder.CreateIndex(
                name: "IX_AnotacaoAula_AutorId",
                table: "AnotacaoAula",
                column: "AutorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnotacaoAula");

            migrationBuilder.DropColumn(
                name: "DepoimentosDeAulaHabilitados",
                table: "Jogador");
        }
    }
}
