using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class DesafiosFase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnuncioDeDesafio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Jogador1Id = table.Column<int>(type: "integer", nullable: false),
                    Jogador2Id = table.Column<int>(type: "integer", nullable: true),
                    ConviteToken = table.Column<string>(type: "text", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ValeAte = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Observacao = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnuncioDeDesafio", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnuncioDeDesafio_Jogador_Jogador1Id",
                        column: x => x.Jogador1Id,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnuncioDeDesafio_Jogador_Jogador2Id",
                        column: x => x.Jogador2Id,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnuncioDesafioCategoria",
                columns: table => new
                {
                    AnuncioDeDesafioId = table.Column<int>(type: "integer", nullable: false),
                    CategoriaPadraoId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnuncioDesafioCategoria", x => new { x.AnuncioDeDesafioId, x.CategoriaPadraoId });
                    table.ForeignKey(
                        name: "FK_AnuncioDesafioCategoria_AnuncioDeDesafio_AnuncioDeDesafioId",
                        column: x => x.AnuncioDeDesafioId,
                        principalTable: "AnuncioDeDesafio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AnuncioDesafioCategoria_CategoriaPadrao_CategoriaPadraoId",
                        column: x => x.CategoriaPadraoId,
                        principalTable: "CategoriaPadrao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnuncioDesafioCidade",
                columns: table => new
                {
                    AnuncioDeDesafioId = table.Column<int>(type: "integer", nullable: false),
                    CidadeId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnuncioDesafioCidade", x => new { x.AnuncioDeDesafioId, x.CidadeId });
                    table.ForeignKey(
                        name: "FK_AnuncioDesafioCidade_AnuncioDeDesafio_AnuncioDeDesafioId",
                        column: x => x.AnuncioDeDesafioId,
                        principalTable: "AnuncioDeDesafio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AnuncioDesafioCidade_Cidades_CidadeId",
                        column: x => x.CidadeId,
                        principalTable: "Cidades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnuncioDesafioClube",
                columns: table => new
                {
                    AnuncioDeDesafioId = table.Column<int>(type: "integer", nullable: false),
                    ClubeId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnuncioDesafioClube", x => new { x.AnuncioDeDesafioId, x.ClubeId });
                    table.ForeignKey(
                        name: "FK_AnuncioDesafioClube_AnuncioDeDesafio_AnuncioDeDesafioId",
                        column: x => x.AnuncioDeDesafioId,
                        principalTable: "AnuncioDeDesafio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AnuncioDesafioClube_Clubes_ClubeId",
                        column: x => x.ClubeId,
                        principalTable: "Clubes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Desafio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AnuncioDeDesafioId = table.Column<int>(type: "integer", nullable: true),
                    DesafianteJogador1Id = table.Column<int>(type: "integer", nullable: false),
                    DesafianteJogador2Id = table.Column<int>(type: "integer", nullable: false),
                    DesafiadoJogador1Id = table.Column<int>(type: "integer", nullable: false),
                    DesafiadoJogador2Id = table.Column<int>(type: "integer", nullable: false),
                    CategoriaPadraoId = table.Column<int>(type: "integer", nullable: false),
                    ClubeId = table.Column<int>(type: "integer", nullable: false),
                    DataHora = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Formato = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    PropostoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RespondidoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Observacao = table.Column<string>(type: "text", nullable: true),
                    SetsDesafiante = table.Column<int>(type: "integer", nullable: true),
                    SetsDesafiado = table.Column<int>(type: "integer", nullable: true),
                    GamesDesafiante = table.Column<int>(type: "integer", nullable: true),
                    GamesDesafiado = table.Column<int>(type: "integer", nullable: true),
                    LancadoPorId = table.Column<int>(type: "integer", nullable: true),
                    LancadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ConfirmadoPorId = table.Column<int>(type: "integer", nullable: true),
                    ConfirmadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    MotivoDaDisputa = table.Column<string>(type: "text", nullable: true),
                    PontosDesafiante = table.Column<int>(type: "integer", nullable: true),
                    PontosDesafiado = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Desafio", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Desafio_AnuncioDeDesafio_AnuncioDeDesafioId",
                        column: x => x.AnuncioDeDesafioId,
                        principalTable: "AnuncioDeDesafio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Desafio_CategoriaPadrao_CategoriaPadraoId",
                        column: x => x.CategoriaPadraoId,
                        principalTable: "CategoriaPadrao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Desafio_Clubes_ClubeId",
                        column: x => x.ClubeId,
                        principalTable: "Clubes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Desafio_Jogador_DesafiadoJogador1Id",
                        column: x => x.DesafiadoJogador1Id,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Desafio_Jogador_DesafiadoJogador2Id",
                        column: x => x.DesafiadoJogador2Id,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Desafio_Jogador_DesafianteJogador1Id",
                        column: x => x.DesafianteJogador1Id,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Desafio_Jogador_DesafianteJogador2Id",
                        column: x => x.DesafianteJogador2Id,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnuncioDeDesafio_Jogador1Id",
                table: "AnuncioDeDesafio",
                column: "Jogador1Id");

            migrationBuilder.CreateIndex(
                name: "IX_AnuncioDeDesafio_Jogador2Id",
                table: "AnuncioDeDesafio",
                column: "Jogador2Id");

            migrationBuilder.CreateIndex(
                name: "IX_AnuncioDeDesafio_Status_ValeAte",
                table: "AnuncioDeDesafio",
                columns: new[] { "Status", "ValeAte" });

            migrationBuilder.CreateIndex(
                name: "IX_AnuncioDesafioCategoria_CategoriaPadraoId",
                table: "AnuncioDesafioCategoria",
                column: "CategoriaPadraoId");

            migrationBuilder.CreateIndex(
                name: "IX_AnuncioDesafioCidade_CidadeId",
                table: "AnuncioDesafioCidade",
                column: "CidadeId");

            migrationBuilder.CreateIndex(
                name: "IX_AnuncioDesafioClube_ClubeId",
                table: "AnuncioDesafioClube",
                column: "ClubeId");

            migrationBuilder.CreateIndex(
                name: "IX_Desafio_AnuncioDeDesafioId",
                table: "Desafio",
                column: "AnuncioDeDesafioId");

            migrationBuilder.CreateIndex(
                name: "IX_Desafio_CategoriaPadraoId",
                table: "Desafio",
                column: "CategoriaPadraoId");

            migrationBuilder.CreateIndex(
                name: "IX_Desafio_ClubeId",
                table: "Desafio",
                column: "ClubeId");

            migrationBuilder.CreateIndex(
                name: "IX_Desafio_DesafiadoJogador1Id",
                table: "Desafio",
                column: "DesafiadoJogador1Id");

            migrationBuilder.CreateIndex(
                name: "IX_Desafio_DesafiadoJogador2Id",
                table: "Desafio",
                column: "DesafiadoJogador2Id");

            migrationBuilder.CreateIndex(
                name: "IX_Desafio_DesafianteJogador1Id",
                table: "Desafio",
                column: "DesafianteJogador1Id");

            migrationBuilder.CreateIndex(
                name: "IX_Desafio_DesafianteJogador2Id",
                table: "Desafio",
                column: "DesafianteJogador2Id");

            migrationBuilder.CreateIndex(
                name: "IX_Desafio_Status_DataHora",
                table: "Desafio",
                columns: new[] { "Status", "DataHora" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnuncioDesafioCategoria");

            migrationBuilder.DropTable(
                name: "AnuncioDesafioCidade");

            migrationBuilder.DropTable(
                name: "AnuncioDesafioClube");

            migrationBuilder.DropTable(
                name: "Desafio");

            migrationBuilder.DropTable(
                name: "AnuncioDeDesafio");
        }
    }
}
