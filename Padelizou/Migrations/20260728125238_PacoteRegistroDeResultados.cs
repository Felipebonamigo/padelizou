using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class PacoteRegistroDeResultados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SolicitacoesRegistroResultados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TorneioId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    QuadrasNaSolicitacao = table.Column<int>(type: "integer", nullable: false),
                    DiasNaSolicitacao = table.Column<int>(type: "integer", nullable: false),
                    PessoasSugeridas = table.Column<int>(type: "integer", nullable: false),
                    Observacoes = table.Column<string>(type: "text", nullable: true),
                    SolicitadoPorId = table.Column<int>(type: "integer", nullable: false),
                    SolicitadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RespondidaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RespondidaPorId = table.Column<int>(type: "integer", nullable: true),
                    PessoasConfirmadas = table.Column<int>(type: "integer", nullable: true),
                    ValorCombinado = table.Column<decimal>(type: "numeric", nullable: true),
                    Resposta = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitacoesRegistroResultados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SolicitacoesRegistroResultados_Torneio_TorneioId",
                        column: x => x.TorneioId,
                        principalTable: "Torneio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SolicitacoesRegistroResultados_TorneioId",
                table: "SolicitacoesRegistroResultados",
                column: "TorneioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SolicitacoesRegistroResultados");
        }
    }
}
