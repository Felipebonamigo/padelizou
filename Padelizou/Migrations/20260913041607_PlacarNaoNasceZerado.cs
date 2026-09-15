using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class PlacarNaoNasceZerado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "SetsDupla2",
                table: "Partida",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true,
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "SetsDupla1",
                table: "Partida",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true,
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "GamesDupla2",
                table: "Partida",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true,
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "GamesDupla1",
                table: "Partida",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true,
                oldDefaultValue: 0);

            // ── E AS LINHAS QUE JÁ NASCERAM 0 x 0 ────────────────────────────────────────
            //
            // Tirar o DEFAULT só conserta os jogos FUTUROS. Os que já estão no banco seguem
            // com o 0 que o DEFAULT gravou — e são justamente os que o Felipe está vendo na
            // chave do 2ª Etapa ER PADEL TOUR agora.
            //
            // ⚠️ AS DUAS ÚLTIMAS CONDIÇÕES SÃO A TRAVA, e é o que torna este UPDATE
            // provadamente sem perda: ele só troca ZERO por NULO. Nenhuma linha com número de
            // verdade pode ser tocada, mesmo que as outras condições estivessem erradas.
            //
            // As três de cima são independentes entre si e cada uma sozinha já protege o que
            // importa: jogo finalizado tem VencedorId, jogo que entrou em quadra tem
            // HorarioInicioReal, e placar marcado pela Mesa tem PlacarMarcadoEm.
            //
            // Sobra exatamente o caso alvo: partida agendada, nunca chamada, sem ninguém
            // tendo marcado nada — em que "0 x 0" não carrega informação nenhuma.
            migrationBuilder.Sql("""
                UPDATE "Partida"
                   SET "GamesDupla1" = NULL, "GamesDupla2" = NULL,
                       "SetsDupla1"  = NULL, "SetsDupla2"  = NULL
                 WHERE "Status" = 'Agendada'
                   AND "VencedorId" IS NULL
                   AND "HorarioInicioReal" IS NULL
                   AND "PlacarMarcadoEm" IS NULL
                   AND COALESCE("GamesDupla1", 0) = 0
                   AND COALESCE("GamesDupla2", 0) = 0
                   AND COALESCE("SetsDupla1", 0) = 0
                   AND COALESCE("SetsDupla2", 0) = 0;
                """);
        }

        /// <inheritdoc />
        // ⚠️ O Down devolve os DEFAULTs e para aí. Ele NÃO reescreve 0 nas linhas que o Up
        // anulou, e não deve: quais eram elas é informação que o Up apagou de propósito, e
        // um UPDATE de volta pintaria de 0 também as partidas que já nasciam nulas antes.
        // Voltar o DEFAULT basta pra restaurar o comportamento anterior do schema.
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "SetsDupla2",
                table: "Partida",
                type: "integer",
                nullable: true,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "SetsDupla1",
                table: "Partida",
                type: "integer",
                nullable: true,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "GamesDupla2",
                table: "Partida",
                type: "integer",
                nullable: true,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "GamesDupla1",
                table: "Partida",
                type: "integer",
                nullable: true,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
