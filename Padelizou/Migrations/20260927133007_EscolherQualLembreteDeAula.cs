using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class EscolherQualLembreteDeAula : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ⚠️ `defaultValue: true`, E O EF GEROU `false`. O `= true` do Jogador é
            // inicializador de PROPRIEDADE: vale pra objeto novo em memória, e NÃO pro backfill
            // das linhas que já existem. Com o padrão gerado, este deploy desligaria a véspera
            // da base inteira, calado — exatamente o contrário da decisão ("ligado; nada muda
            // até a pessoa mexer"). É o tipo de defeito que a suíte não pega: o EF InMemory
            // constrói objetos novos, que herdam o `true` do C# e nunca passam por aqui.
            migrationBuilder.AddColumn<bool>(
                name: "NotificarVesperaDaAula",
                table: "Jogador",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // ⚠️ E O BACKFILL COPIA A ESCOLHA QUE A PESSOA JÁ FEZ, em vez de dar `true` pra
            // todo mundo. Até hoje `NotificarLembreteDeAula` valia pelos DOIS marcos: quem o
            // desligou desligou o de 24h também. Um `true` cego aqui faria essas pessoas
            // VOLTAREM A RECEBER a véspera no dia do deploy — religar aviso de quem pediu
            // silêncio é o pior desfecho possível desta mudança, e é da mesma família da
            // armadilha que o `<input type="hidden">` das preferências existe pra evitar.
            //
            // Quem tinha ligado continua com os dois ligados; quem tinha desligado, com os
            // dois desligados. "Nada muda até a pessoa mexer", de verdade.
            migrationBuilder.Sql(
                @"UPDATE ""Jogador"" SET ""NotificarVesperaDaAula"" = ""NotificarLembreteDeAula"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotificarVesperaDaAula",
                table: "Jogador");
        }
    }
}
