using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class AvisoDeTorneioNovo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AvisoDeTorneioNovoEm",
                table: "Torneio",
                type: "timestamp without time zone",
                nullable: true);

            // ⚠️ ESTA LINHA É A METADE IMPORTANTE DA MIGRATION, e ela foi escrita à mão.
            //
            // A coluna nasce NULA, e nulo quer dizer "o aviso ainda não saiu". Sem este UPDATE,
            // TODO torneio que já existe entraria como "nunca avisado" — e a primeira vez que
            // alguém escondesse e publicasse um deles, o Padelizou anunciaria pra base inteira
            // um torneio velho, alguns já finalizados. É o mesmo tipo de estrago do
            // `defaultValue` zero, só que em forma de push.
            //
            // Quem já está APROVADO E VISÍVEL recebeu o aviso de verdade lá atrás (o disparo
            // sempre morou na aprovação, e só era pulado quando o torneio estava oculto):
            // carimba com a própria data de aprovação, que é quando ele saiu.
            //
            // ⚠️ Os aprovados e OCULTOS ficam NULOS de propósito — são exatamente os que nunca
            // foram anunciados e que ainda esperam o dia da divulgação. Para eles, publicar vai
            // disparar o aviso, que é o comportamento novo que esta migration existe pra
            // sustentar.
            migrationBuilder.Sql(
                """
                UPDATE "Torneio"
                   SET "AvisoDeTorneioNovoEm" = "AprovadoEm"
                 WHERE "AprovadoEm" IS NOT NULL
                   AND "Oculto" = false;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvisoDeTorneioNovoEm",
                table: "Torneio");
        }
    }
}
