using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class PalpitometroNoPerfil : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ⚠️ OS DOIS `defaultValue: true` FORAM ESCRITOS À MÃO. O EF gerou `false`, porque
            // ele olha o TIPO (`bool` → default do CLR) e NÃO o `= true` das propriedades — e
            // esse valor é o que BACKFILLA as linhas que já estão em produção.
            //
            // Deixando como veio, TODO jogador do sistema amanheceria com o palpitômetro
            // DESLIGADO e sem a aba de palpiteiros, sem ter tocado em nada. Não seria um erro:
            // seria o recurso sumindo da tela de todo mundo, calado — que é exatamente o que
            // ninguém reporta. É a MESMA correção da `VotacaoDeMvpOpcional` e da
            // `PalpitometroPorCategoria`, e o `GateDoDefaultDasColunasBoolTests` existe pra
            // quebrar se alguém regerar esta migration e desfazer o conserto sem perceber.
            migrationBuilder.AddColumn<bool>(
                name: "VerPalpitometro",
                table: "Jogador",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "VerQuemPalpitou",
                table: "Jogador",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VerPalpitometro",
                table: "Jogador");

            migrationBuilder.DropColumn(
                name: "VerQuemPalpitou",
                table: "Jogador");
        }
    }
}
