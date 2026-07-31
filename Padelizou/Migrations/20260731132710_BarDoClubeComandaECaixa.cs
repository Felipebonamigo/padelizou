using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class BarDoClubeComandaECaixa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CaixaDoDia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClubeId = table.Column<int>(type: "integer", nullable: false),
                    Dia = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ValorAbertura = table.Column<decimal>(type: "numeric", nullable: false),
                    AbertoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AbertoPorId = table.Column<int>(type: "integer", nullable: true),
                    ValorContado = table.Column<decimal>(type: "numeric", nullable: true),
                    FechadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FechadoPorId = table.Column<int>(type: "integer", nullable: true),
                    Observacao = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaixaDoDia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaixaDoDia_Clubes_ClubeId",
                        column: x => x.ClubeId,
                        principalTable: "Clubes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Comanda",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClubeId = table.Column<int>(type: "integer", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    DiaReferencia = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    NomeCliente = table.Column<string>(type: "text", nullable: false),
                    JogadorId = table.Column<int>(type: "integer", nullable: true),
                    MarcacaoJogoId = table.Column<int>(type: "integer", nullable: true),
                    Celular = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    AbertaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AbertaPorId = table.Column<int>(type: "integer", nullable: true),
                    FechadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FechadaPorId = table.Column<int>(type: "integer", nullable: true),
                    FormaPagamento = table.Column<string>(type: "text", nullable: true),
                    Desconto = table.Column<decimal>(type: "numeric", nullable: false),
                    Observacao = table.Column<string>(type: "text", nullable: true),
                    MotivoCancelamento = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comanda", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Comanda_Clubes_ClubeId",
                        column: x => x.ClubeId,
                        principalTable: "Clubes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Comanda_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Comanda_MarcacaoJogo_MarcacaoJogoId",
                        column: x => x.MarcacaoJogoId,
                        principalTable: "MarcacaoJogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProdutoBar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClubeId = table.Column<int>(type: "integer", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Preco = table.Column<decimal>(type: "numeric", nullable: false),
                    Categoria = table.Column<string>(type: "text", nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProdutoBar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProdutoBar_Clubes_ClubeId",
                        column: x => x.ClubeId,
                        principalTable: "Clubes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemComanda",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ComandaId = table.Column<int>(type: "integer", nullable: false),
                    ProdutoBarId = table.Column<int>(type: "integer", nullable: true),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    PrecoUnitario = table.Column<decimal>(type: "numeric", nullable: false),
                    Quantidade = table.Column<int>(type: "integer", nullable: false),
                    LancadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LancadoPorId = table.Column<int>(type: "integer", nullable: true),
                    CanceladoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CanceladoPorId = table.Column<int>(type: "integer", nullable: true),
                    MotivoCancelamento = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemComanda", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemComanda_Comanda_ComandaId",
                        column: x => x.ComandaId,
                        principalTable: "Comanda",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemComanda_ProdutoBar_ProdutoBarId",
                        column: x => x.ProdutoBarId,
                        principalTable: "ProdutoBar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CaixaDoDia_ClubeId_Dia",
                table: "CaixaDoDia",
                columns: new[] { "ClubeId", "Dia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Comanda_ClubeId_DiaReferencia_Numero",
                table: "Comanda",
                columns: new[] { "ClubeId", "DiaReferencia", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Comanda_ClubeId_Status",
                table: "Comanda",
                columns: new[] { "ClubeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Comanda_JogadorId",
                table: "Comanda",
                column: "JogadorId");

            migrationBuilder.CreateIndex(
                name: "IX_Comanda_MarcacaoJogoId",
                table: "Comanda",
                column: "MarcacaoJogoId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemComanda_ComandaId",
                table: "ItemComanda",
                column: "ComandaId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemComanda_ProdutoBarId",
                table: "ItemComanda",
                column: "ProdutoBarId");

            migrationBuilder.CreateIndex(
                name: "IX_ProdutoBar_ClubeId_Ativo",
                table: "ProdutoBar",
                columns: new[] { "ClubeId", "Ativo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CaixaDoDia");

            migrationBuilder.DropTable(
                name: "ItemComanda");

            migrationBuilder.DropTable(
                name: "Comanda");

            migrationBuilder.DropTable(
                name: "ProdutoBar");
        }
    }
}
