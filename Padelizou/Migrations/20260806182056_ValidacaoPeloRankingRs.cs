using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class ValidacaoPeloRankingRs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ValidarPeloRankingRs",
                table: "Torneio",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RankingRsCategoriaId",
                table: "Categoria",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BloqueioDoRanking",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TorneioId = table.Column<int>(type: "integer", nullable: false),
                    CategoriaId = table.Column<int>(type: "integer", nullable: false),
                    Cpf = table.Column<string>(type: "text", nullable: false),
                    NomeConsultado = table.Column<string>(type: "text", nullable: false),
                    CategoriaRsId = table.Column<int>(type: "integer", nullable: false),
                    CategoriaRsNome = table.Column<string>(type: "text", nullable: true),
                    Motivo = table.Column<string>(type: "text", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Situacao = table.Column<string>(type: "text", nullable: false),
                    DecididoPorId = table.Column<int>(type: "integer", nullable: true),
                    DecididoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloqueioDoRanking", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BloqueioDoRanking_Categoria_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "Categoria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BloqueioDoRanking_Jogador_DecididoPorId",
                        column: x => x.DecididoPorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BloqueioDoRanking_Torneio_TorneioId",
                        column: x => x.TorneioId,
                        principalTable: "Torneio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BloqueioDoRanking_CategoriaId_Cpf",
                table: "BloqueioDoRanking",
                columns: new[] { "CategoriaId", "Cpf" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BloqueioDoRanking_DecididoPorId",
                table: "BloqueioDoRanking",
                column: "DecididoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_BloqueioDoRanking_TorneioId",
                table: "BloqueioDoRanking",
                column: "TorneioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BloqueioDoRanking");

            migrationBuilder.DropColumn(
                name: "ValidarPeloRankingRs",
                table: "Torneio");

            migrationBuilder.DropColumn(
                name: "RankingRsCategoriaId",
                table: "Categoria");
        }
    }
}
