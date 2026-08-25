using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class AddPagaEmToAula : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PagaEm",
                table: "Aula",
                type: "timestamp without time zone",
                nullable: true);

            // ⚠️ O BACKFILL É A PARTE QUE NÃO PODE FALTAR, e não é enfeite.
            //
            // Coluna nova nasce nula. Nulo em toda aula já dada faria o Financeiro do professor
            // abrir em "Recebido R$ 0,00", com o histórico inteiro na lista de quem está
            // devendo — ele leria como "o sistema perdeu o meu dinheiro".
            //
            // Até esta migration, `Status = 'Realizada'` SIGNIFICAVA recebido: era exatamente
            // isso que a tela somava. Carimbar preserva a verdade que estava no ar, em vez de
            // inventar uma nova. A data usada é a da AULA porque é a única que existe — ninguém
            // registrou quando o Pix caiu.
            //
            // ⚠️ MENOS AS QUE ESTÃO NUMA CONTA DO MÊS AINDA ABERTA. Essas o professor sabe que
            // NÃO recebeu (foi ele quem fechou a conta e ela continua Aberta) — carimbá-las
            // apagaria dívida real de mensalista, que é o erro caro deste lado.
            //
            // A identidade do aluno é a de PrecoDaAula.Chave: conta quando existe, nome anotado
            // quando não. As duas colunas são anuláveis, e NULL = NULL é falso em SQL — que é o
            // que faz cada linha casar por UMA das duas, nunca pelas duas em branco.
            //
            // Reposição e aula de R$ 0,00 ficam de fora: nunca houve dinheiro nelas pra dizer
            // que entrou (ver Services/RecebimentoDaAula.GerouCobranca).
            migrationBuilder.Sql("""
                UPDATE "Aula" a
                SET "PagaEm" = a."DataHora"
                WHERE a."Status" = 'Realizada'
                  AND a."RecuperaAulaId" IS NULL
                  AND a."Preco" > 0
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "FaturaDoAluno" f
                      WHERE f."ProfessorId" = a."ProfessorId"
                        AND f."Status" = 'Aberta'
                        AND f."Ano" = EXTRACT(YEAR FROM a."DataHora")
                        AND f."Mes" = EXTRACT(MONTH FROM a."DataHora")
                        AND (f."AlunoId" = a."AlunoId" OR f."NomeAvulso" = a."NomeAlunoAvulso")
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Descer apaga o recebimento por aula junto com a coluna, e não há como guardá-lo
            // em outro lugar: `Status` não sabe dizer "recebida". Voltar aqui devolve o sistema
            // ao estado em que "Realizada" quer dizer as duas coisas.
            migrationBuilder.DropColumn(
                name: "PagaEm",
                table: "Aula");
        }
    }
}
