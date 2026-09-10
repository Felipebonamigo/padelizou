using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class CarimboDasChavesAvisadas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ChavesAvisadasEm",
                table: "Torneio",
                type: "timestamp without time zone",
                nullable: true);

            // ⚠️ ESTE UPDATE É A METADE IMPORTANTE DA MIGRATION, e foi escrito à mão — mesma
            // decisão, e mesmo motivo, da migration do `AvisoDeTorneioNovoEm`.
            //
            // A coluna nasce NULA, e nulo quer dizer "a rajada 'as chaves saíram' nunca saiu".
            // Sem isto, TODO torneio já publicado entraria como "nunca avisado" — e o primeiro
            // recolher→aprovar de cada um mandaria um push pra base inteira dele, alguns já
            // finalizados. É o estrago que a caixinha existe pra evitar, servido pela migration.
            //
            // QUEM JÁ PASSOU DA APROVAÇÃO recebeu o aviso de verdade lá atrás: qualquer status
            // fora dos três que vêm ANTES dela ("Inscrições Abertas", "Chaves em Sorteio",
            // "Chaves em Aprovação").
            //
            // ⚠️ O AMERICANO FICA DE FORA: ele vai direto pra "Fase de Grupos" e nunca passou
            // pelo `AprovarChaves`, então a rajada das chaves nunca saiu pra ele. E é coerente
            // com a régua nova — `PorQueNaoPodeRecolher` recusa esse formato, então o carimbo
            // dele nunca seria lido de qualquer jeito.
            //
            // ⚠️ A DATA É APROXIMADA NAS LINHAS ANTIGAS, e só a não-nulidade é que significa
            // alguma coisa: não existe registro de QUANDO cada chave foi aprovada (é o carimbo
            // que está nascendo agora). Por isso a tela não mostra esta data — ela diz só "já
            // foram avisados uma vez", que é o que de fato se sabe.
            migrationBuilder.Sql(
                """
                UPDATE "Torneio"
                   SET "ChavesAvisadasEm" = COALESCE("AprovadoEm", "DataInicio", NOW())
                 WHERE "Status" NOT IN ('Inscrições Abertas', 'Chaves em Sorteio', 'Chaves em Aprovação')
                   AND COALESCE("Formato", '') NOT IN ('Americano', 'AmericanoDuplas');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChavesAvisadasEm",
                table: "Torneio");
        }
    }
}
