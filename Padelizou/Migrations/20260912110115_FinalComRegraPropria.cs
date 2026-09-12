using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <summary>
    /// A FINAL COM REGRA PRÓPRIA (Felipe, 12/09/2026) — a decisão em 3 sets ou com super
    /// tie-break enquanto as semifinais seguem curtas.
    ///
    /// ⚠️ ZERO NAS TRÊS, E SEM BACKFILL NENHUM, de propósito: zero é justamente "a final segue
    /// as semifinais", que é como TODO torneio existente joga hoje. Copiar os valores de
    /// `...FaseFinal` pra cá seria um UPDATE em dado de produção cujo modo de falha é silencioso
    /// (backfill que não roda joga toda final configurada pro padrão de 9 games, sem avisar
    /// ninguém). Herança por zero não tem esse estado. Ver Torneio.GamesSoDaFinal.
    /// </summary>
    public partial class FinalComRegraPropria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GamesSoDaFinal",
                table: "Torneio",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PontosTieBreakSoDaFinal",
                table: "Torneio",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SetsSoDaFinal",
                table: "Torneio",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GamesSoDaFinal",
                table: "Torneio");

            migrationBuilder.DropColumn(
                name: "PontosTieBreakSoDaFinal",
                table: "Torneio");

            migrationBuilder.DropColumn(
                name: "SetsSoDaFinal",
                table: "Torneio");
        }
    }
}
