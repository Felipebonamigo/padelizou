using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    // O TORNEIO EM MAIS DE UM CLUBE (21/08/2026).
    //
    // Três colunas, e nenhuma linha de conversão de dados: quadra e categoria nascem com
    // `ClubeId` NULO, que quer dizer "no clube do torneio" e "em qualquer quadra". Ou seja, todo
    // torneio que já existia continua sendo exatamente o que era. É por isso que as duas colunas
    // são ANULÁVEIS e não `int` liso — ver Models/Quadra e Models/Categoria.
    //
    // As FKs ficam sem ON DELETE (NO ACTION): apagar um clube que é sede de um torneio passa a
    // dar erro em vez de acontecer calado. É de propósito, e é o contrário do que
    // `Torneio.ClubeId` faz — lá o cascade apaga o torneio junto, e o próprio DbPadelContext
    // registra que "a lição do Torneio.ClubeId em cascade já custou caro". Hoje não existe
    // nenhuma tela que apague clube (grep por `Clubes.Remove`: zero), então NO ACTION não fecha
    // porta nenhuma que esteja aberta.
    public partial class MaisDeUmClubeNoTorneio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ⚠️ `defaultValue: 30` ESCRITO À MÃO — o EF gerou 0 aqui, e 0 desliga a folga.
            //
            // O padrão em C# (`= 30`) só vale pra objeto NOVO; as linhas que já estão no banco
            // recebem o que esta migration disser. Com 0, um torneio ANTIGO que o organizador
            // editasse pra ter duas sedes nasceria sem folga nenhuma entre os clubes — e a falha
            // seria muda: a grade marcaria o segundo jogo do sujeito do outro lado da cidade
            // encostado no primeiro, e ninguém descobriria antes do dia. É a mesma lição do
            // `Clube.Selecionavel`, que também precisou do default escrito à mão.
            migrationBuilder.AddColumn<int>(
                name: "MinutosParaTrocarDeClube",
                table: "Torneio",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<int>(
                name: "ClubeId",
                table: "Quadra",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClubeId",
                table: "Categoria",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quadra_ClubeId",
                table: "Quadra",
                column: "ClubeId");

            migrationBuilder.CreateIndex(
                name: "IX_Categoria_ClubeId",
                table: "Categoria",
                column: "ClubeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Categoria_Clubes_ClubeId",
                table: "Categoria",
                column: "ClubeId",
                principalTable: "Clubes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Quadra_Clubes_ClubeId",
                table: "Quadra",
                column: "ClubeId",
                principalTable: "Clubes",
                principalColumn: "Id");

            // ⚠️ A CONSTRAINT UQ_Quadra_Torneio_Nome NÃO MUDA, e isso é decisão, não esquecimento.
            //
            // A tentação é óbvia: com sede, "Quadra 1" no Nata e "Quadra 1" no Batata deveriam
            // poder coexistir, e o único viraria (TorneioId, ClubeId, Nome). Não pode. O motor
            // inteiro identifica quadra por NOME — `Partida.NomeQuadra` é texto solto, sem FK
            // (ver Models/Quadra). Duas "Quadra 1" seriam a MESMA quadra pra grade, que marcaria
            // um jogo em cada clube no mesmo horário achando que tinha dobrado a quadra, e pra
            // troca de quadra, que moveria o jogo pro prédio errado.
            //
            // Quem resolve isso é a TELA: com mais de uma sede ela sugere o nome do clube dentro
            // do nome da quadra ("Central Nata"), e a régua de Services/NomeDeQuadraUnico recusa
            // nome repetido com uma mensagem que explica justamente esse caso.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categoria_Clubes_ClubeId",
                table: "Categoria");

            migrationBuilder.DropForeignKey(
                name: "FK_Quadra_Clubes_ClubeId",
                table: "Quadra");

            migrationBuilder.DropIndex(
                name: "IX_Quadra_ClubeId",
                table: "Quadra");

            migrationBuilder.DropIndex(
                name: "IX_Categoria_ClubeId",
                table: "Categoria");

            migrationBuilder.DropColumn(
                name: "MinutosParaTrocarDeClube",
                table: "Torneio");

            migrationBuilder.DropColumn(
                name: "ClubeId",
                table: "Quadra");

            migrationBuilder.DropColumn(
                name: "ClubeId",
                table: "Categoria");
        }
    }
}
