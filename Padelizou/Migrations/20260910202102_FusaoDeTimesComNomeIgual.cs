using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class FusaoDeTimesComNomeIgual : Migration
    {
        // DOIS TIMES COM O MESMO NOME DEIXAM DE SER POSSÍVEIS.
        //
        // Motivo, medido em produção em 10/09/2026: a aba Times do 2ª Etapa ER PADEL TOUR
        // listava "ER Padel" (21 jogadores, 318 pontos) e "Er padel" (2 jogadores, 25 pontos)
        // como cadastros DIFERENTES, com o pódio partido entre as duas linhas.
        //
        // ⚠️ A ORDEM AQUI NÃO É ARBITRÁRIA: funde primeiro, cria o índice depois. O índice não
        // aplica com a duplicata viva — se ele viesse antes, esta migration falharia, o
        // /healthz responderia 503 (ele confere migration pendente) e o deploy.sh faria
        // rollback sozinho. A ordem é o que faz a publicação passar.
        //
        // ⚠️ Esta migration é um RETRATO CONGELADO do reparo de uma base num dia; a régua viva
        // é o Services/FusaoDeTimes, que roda toda vez que alguém salva o perfil. As duas
        // precisam concordar no CRITÉRIO — sobrevive o de mais jogadores, empate pelo menor
        // Id, e administração nunca é herdada — e é por isso que o critério está escrito por
        // extenso lá, e citado aqui.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O mapa "quem morre → quem fica", por nome normalizado. `btrim` junto com `lower`
            // porque os três caminhos que criam time já fazem Trim(): sem ele, "ER Padel " e
            // "ER Padel" continuariam escapando da trava por um espaço invisível.
            migrationBuilder.Sql(@"
                CREATE TEMP TABLE _fusao_de_times AS
                WITH normalizado AS (
                    SELECT t.""Id"",
                           lower(btrim(t.""Nome"")) AS chave,
                           (SELECT count(*) FROM ""Jogador"" j WHERE j.""TimeId"" = t.""Id"") AS jogadores
                    FROM ""Times"" t
                ),
                sobrevivente AS (
                    SELECT DISTINCT ON (chave) chave, ""Id"" AS sobrevivente_id
                    FROM normalizado
                    ORDER BY chave, jogadores DESC, ""Id"" ASC
                )
                SELECT n.""Id"" AS absorvido_id, s.sobrevivente_id
                FROM normalizado n
                JOIN sobrevivente s ON s.chave = n.chave
                WHERE n.""Id"" <> s.sobrevivente_id;
            ");

            // Quem vestia a camisa do absorvido passa a vestir a do sobrevivente. Não entra
            // linha em TransferenciasDeTime: ninguém trocou de time — quem mudou foi o
            // cadastro, e "saiu do Er padel e entrou no ER Padel" seria movimento que não houve.
            migrationBuilder.Sql(@"
                UPDATE ""Jogador"" j SET ""TimeId"" = m.sobrevivente_id
                FROM _fusao_de_times m WHERE j.""TimeId"" = m.absorvido_id;
            ");

            // O vínculo da dupla com o time é só o escudo na tela, mas deixá-lo apontando pra
            // um Id que vai sumir apagaria o escudo de um torneio já encerrado.
            migrationBuilder.Sql(@"
                UPDATE ""Dupla"" d SET ""TimeId"" = m.sobrevivente_id
                FROM _fusao_de_times m WHERE d.""TimeId"" = m.absorvido_id;
            ");

            migrationBuilder.Sql(@"
                UPDATE ""TransferenciasDeTime"" t SET ""TimeAnteriorId"" = m.sobrevivente_id
                FROM _fusao_de_times m WHERE t.""TimeAnteriorId"" = m.absorvido_id;
            ");
            migrationBuilder.Sql(@"
                UPDATE ""TransferenciasDeTime"" t SET ""TimeNovoId"" = m.sobrevivente_id
                FROM _fusao_de_times m WHERE t.""TimeNovoId"" = m.absorvido_id;
            ");

            // Quem já tinha ido de uma grafia pra outra virou "saiu do ER Padel e entrou no
            // ER Padel" no repontamento acima — a linha que o TransferenciasDeTime.Registrar
            // se recusa a criar por não querer dizer nada.
            migrationBuilder.Sql(@"
                DELETE FROM ""TransferenciasDeTime""
                WHERE ""TimeAnteriorId"" IS NOT NULL AND ""TimeAnteriorId"" = ""TimeNovoId"";
            ");

            // Sedes: traz as que o sobrevivente ainda não tem. TimeSedes tem chave composta
            // (TimeId, ClubeId), então mover cego a sede que os dois já têm estouraria a chave.
            // O DISTINCT cobre dois absorvidos trazendo o mesmo clube.
            migrationBuilder.Sql(@"
                INSERT INTO ""TimeSedes"" (""TimeId"", ""ClubeId"")
                SELECT DISTINCT m.sobrevivente_id, s.""ClubeId""
                FROM ""TimeSedes"" s
                JOIN _fusao_de_times m ON s.""TimeId"" = m.absorvido_id
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""TimeSedes"" x
                    WHERE x.""TimeId"" = m.sobrevivente_id AND x.""ClubeId"" = s.""ClubeId""
                );
            ");

            // ⚠️ ADMINISTRAÇÃO NÃO É HERDADA, e é o `ON DELETE CASCADE` de TimeAdministradores
            // que cuida disso — de propósito, não por esquecimento. Quem comandava o absorvido
            // não passa a comandar o sobrevivente: quem mandava em 2 pessoas não vira o dono
            // de 21 porque o sistema arrumou o próprio cadastro. Reparo de dado não pode virar
            // promoção. Quem administrava o sobrevivente segue administrando — essas linhas
            // não são tocadas. Time que ficar sem nenhum administrador cai no mesmo estado dos
            // 44 importados do ranking, e um admin do Padelizou concede o primeiro.
            //
            // As linhas de TimeSedes que sobraram no absorvido caem pelo mesmo cascade.
            migrationBuilder.Sql(@"
                DELETE FROM ""Times"" WHERE ""Id"" IN (SELECT absorvido_id FROM _fusao_de_times);
            ");

            migrationBuilder.Sql(@"DROP TABLE _fusao_de_times;");

            // A TRAVA. É o degrau 4 da escada do CLAUDE.md — chave do banco em vez de `if` em
            // C# — e vale pros três caminhos que criam time de uma vez só, inclusive um quarto
            // que alguém escreva daqui a seis meses sem ler esta linha.
            //
            // Índice por EXPRESSÃO, então vai em SQL cru e fica fora do snapshot do EF: o
            // modelo não sabe expressá-lo, e o `has-pending-model-changes` continua limpo
            // porque nada mudou no modelo.
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX ""IX_Times_NomeNormalizado""
                ON ""Times"" (lower(btrim(""Nome"")));
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ⚠️ Só o índice volta. A fusão NÃO tem desfazer: os times absorvidos foram
            // apagados e os jogadores repontados, e não existe registro do que era de quem
            // antes. Voltar esta migration devolve a permissão de criar nome repetido, não os
            // cadastros que foram juntados — quem precisar deles vai no backup do dia.
            migrationBuilder.Sql(@"DROP INDEX ""IX_Times_NomeNormalizado"";");
        }
    }
}
