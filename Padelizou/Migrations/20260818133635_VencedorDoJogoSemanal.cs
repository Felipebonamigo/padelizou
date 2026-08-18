using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class VencedorDoJogoSemanal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "GamesDupla2",
                table: "JogoSemanal",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "GamesDupla1",
                table: "JogoSemanal",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "VencedorLado",
                table: "JogoSemanal",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // ⚠️ O BACKFILL NÃO É OPCIONAL — sem ele esta migração REESCREVE O RANKING DE TODAS
            // AS PANELINHAS.
            //
            // A coluna nasce com `defaultValue: 0`, e aqui 0 não é "vazio": é **EMPATE**. Todo
            // jogo que já existe ficaria empatado, valendo 2 pontos pra cada um, e o
            // `RecalcularPontuacaoAsync` (que refaz o total a partir dos jogos no primeiro
            // registrar/editar/apagar de cada grupo) espalharia isso pro ranking gravado. Nada
            // acusaria: o número simplesmente ficaria errado.
            //
            // A conta abaixo é a MESMA que a propriedade calculada fazia até esta migração —
            // e é exata, porque até aqui as duas colunas de games eram NOT NULL: todo jogo
            // existente tem placar, então o vencedor de todos eles é dedutível sem perda.
            migrationBuilder.Sql("""
                UPDATE "JogoSemanal"
                SET "VencedorLado" = CASE
                    WHEN "GamesDupla1" = "GamesDupla2" THEN 0
                    WHEN "GamesDupla1" > "GamesDupla2" THEN 1
                    ELSE 2
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VencedorLado",
                table: "JogoSemanal");

            migrationBuilder.AlterColumn<int>(
                name: "GamesDupla2",
                table: "JogoSemanal",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "GamesDupla1",
                table: "JogoSemanal",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
