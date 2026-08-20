using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    // A VAGA DO CONVIDADO SEM NOME: as quatro FKs de jogador do JogoSemanal passam a aceitar NULO.
    //
    // ✅ NENHUMA LINHA EXISTENTE MUDA DE VALOR, e é isso que torna esta migração segura. Não nasce
    // coluna nova, então não existe aqui a armadilha do `defaultValue: 0` de
    // 20260818133635_VencedorDoJogoSemanal — onde o zero das linhas antigas significava EMPATE e
    // precisou ser corrigido à mão depois. Aqui só se AFROUXA uma restrição: o que já está gravado
    // continua idêntico, e nulo só passa a existir a partir do primeiro jogo lançado com convidado.
    public partial class ConvidadoSemNomeNoJogoDaPanelinha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Dupla2Jogador2Id",
                table: "JogoSemanal",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "Dupla2Jogador1Id",
                table: "JogoSemanal",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "Dupla1Jogador2Id",
                table: "JogoSemanal",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "Dupla1Jogador1Id",
                table: "JogoSemanal",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");
        }

        // ⚠️ ESTE `Down` NÃO VOLTA ATRÁS DEPOIS DO PRIMEIRO CONVIDADO GRAVADO, e o
        // `defaultValue: 0` abaixo é ilusão de segurança: `AlterColumn` NÃO preenche linha
        // existente (só `AddColumn` faz), então o `SET NOT NULL` é RECUSADO pelo Postgres na cara
        // da primeira linha com nulo. E não há conserto automático possível — a resposta certa
        // pra "quem era o convidado?" não existe em lugar nenhum, é justamente o que não foi
        // coletado. Reverter de verdade exige decidir à mão o que fazer com esses jogos.
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Dupla2Jogador2Id",
                table: "JogoSemanal",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Dupla2Jogador1Id",
                table: "JogoSemanal",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Dupla1Jogador2Id",
                table: "JogoSemanal",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Dupla1Jogador1Id",
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
