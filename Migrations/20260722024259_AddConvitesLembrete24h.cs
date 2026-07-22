using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class AddConvitesLembrete24h : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AceitaConvitesJogo",
                table: "Jogador",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "CategoriaPadraoId",
                table: "GrupoPrivado",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClubeId",
                table: "GrupoPrivado",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiaSemanaFixo",
                table: "GrupoPrivado",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnviarLembrete24h",
                table: "GrupoPrivado",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "HorarioFixo",
                table: "GrupoPrivado",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VagasMaximas",
                table: "GrupoPrivado",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorAvulso",
                table: "GrupoPrivado",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorMensalidade",
                table: "GrupoPrivado",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MensalidadeGrupo",
                columns: table => new
                {
                    GrupoId = table.Column<int>(type: "int", nullable: false),
                    JogadorId = table.Column<int>(type: "int", nullable: false),
                    Ano = table.Column<int>(type: "int", nullable: false),
                    Mes = table.Column<int>(type: "int", nullable: false),
                    Pago = table.Column<bool>(type: "bit", nullable: false),
                    DataPagamento = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MensalidadeGrupo", x => new { x.GrupoId, x.JogadorId, x.Ano, x.Mes });
                    table.ForeignKey(
                        name: "FK_MensalidadeGrupo_GrupoPrivado_GrupoId",
                        column: x => x.GrupoId,
                        principalTable: "GrupoPrivado",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MensalidadeGrupo_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessaoGrupo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GrupoId = table.Column<int>(type: "int", nullable: false),
                    DataHora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessaoGrupo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessaoGrupo_GrupoPrivado_GrupoId",
                        column: x => x.GrupoId,
                        principalTable: "GrupoPrivado",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConfirmacaoSessao",
                columns: table => new
                {
                    SessaoId = table.Column<int>(type: "int", nullable: false),
                    JogadorId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Lado = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Avulso = table.Column<bool>(type: "bit", nullable: false),
                    RespondidoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LembreteEnviadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfirmacaoSessao", x => new { x.SessaoId, x.JogadorId });
                    table.ForeignKey(
                        name: "FK_ConfirmacaoSessao_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConfirmacaoSessao_SessaoGrupo_SessaoId",
                        column: x => x.SessaoId,
                        principalTable: "SessaoGrupo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GrupoPrivado_CategoriaPadraoId",
                table: "GrupoPrivado",
                column: "CategoriaPadraoId");

            migrationBuilder.CreateIndex(
                name: "IX_GrupoPrivado_ClubeId",
                table: "GrupoPrivado",
                column: "ClubeId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmacaoSessao_JogadorId",
                table: "ConfirmacaoSessao",
                column: "JogadorId");

            migrationBuilder.CreateIndex(
                name: "IX_MensalidadeGrupo_JogadorId",
                table: "MensalidadeGrupo",
                column: "JogadorId");

            migrationBuilder.CreateIndex(
                name: "IX_SessaoGrupo_GrupoId_DataHora",
                table: "SessaoGrupo",
                columns: new[] { "GrupoId", "DataHora" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_GrupoPrivado_CategoriaPadrao_CategoriaPadraoId",
                table: "GrupoPrivado",
                column: "CategoriaPadraoId",
                principalTable: "CategoriaPadrao",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GrupoPrivado_Clubes_ClubeId",
                table: "GrupoPrivado",
                column: "ClubeId",
                principalTable: "Clubes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GrupoPrivado_CategoriaPadrao_CategoriaPadraoId",
                table: "GrupoPrivado");

            migrationBuilder.DropForeignKey(
                name: "FK_GrupoPrivado_Clubes_ClubeId",
                table: "GrupoPrivado");

            migrationBuilder.DropTable(
                name: "ConfirmacaoSessao");

            migrationBuilder.DropTable(
                name: "MensalidadeGrupo");

            migrationBuilder.DropTable(
                name: "SessaoGrupo");

            migrationBuilder.DropIndex(
                name: "IX_GrupoPrivado_CategoriaPadraoId",
                table: "GrupoPrivado");

            migrationBuilder.DropIndex(
                name: "IX_GrupoPrivado_ClubeId",
                table: "GrupoPrivado");

            migrationBuilder.DropColumn(
                name: "AceitaConvitesJogo",
                table: "Jogador");

            migrationBuilder.DropColumn(
                name: "CategoriaPadraoId",
                table: "GrupoPrivado");

            migrationBuilder.DropColumn(
                name: "ClubeId",
                table: "GrupoPrivado");

            migrationBuilder.DropColumn(
                name: "DiaSemanaFixo",
                table: "GrupoPrivado");

            migrationBuilder.DropColumn(
                name: "EnviarLembrete24h",
                table: "GrupoPrivado");

            migrationBuilder.DropColumn(
                name: "HorarioFixo",
                table: "GrupoPrivado");

            migrationBuilder.DropColumn(
                name: "VagasMaximas",
                table: "GrupoPrivado");

            migrationBuilder.DropColumn(
                name: "ValorAvulso",
                table: "GrupoPrivado");

            migrationBuilder.DropColumn(
                name: "ValorMensalidade",
                table: "GrupoPrivado");
        }
    }
}
