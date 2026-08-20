using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    // A CORTESIA DO PLANO DO PROFESSOR — permuta e afins (20/08/2026).
    //
    // ✅ AS QUATRO COLUNAS SÃO NULÁVEIS E NASCEM SEM `defaultValue`, o que aqui é a decisão e
    // não o acaso: nulo significa "não tem cortesia", que é o que a base inteira precisa dizer.
    // Fosse `CortesiaConcedidaPorJogadorId` um `int` não-nulável, ele nasceria ZERO em todo
    // mundo — "concedida pelo jogador 0" em cada linha da tabela.
    //
    // ⚠️ E NENHUMA DELAS É `AssinaturaProfessorPagaAte`. Aquela responde "até quando ele PAGOU"
    // e é a contraprova de que existiu dinheiro; estas respondem "até quando ele tem DIREITO".
    // Juntar as duas faria a tela do admin dizer "Pago até 20/08/2027" pra quem nunca pagou um
    // centavo. Ver Models/Jogador e Services/PlanoDoProfessor.
    public partial class CortesiaDoProfessor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CortesiaConcedidaEm",
                table: "Jogador",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CortesiaConcedidaPorJogadorId",
                table: "Jogador",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CortesiaProfessorAte",
                table: "Jogador",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoDaCortesia",
                table: "Jogador",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CortesiaConcedidaEm",
                table: "Jogador");

            migrationBuilder.DropColumn(
                name: "CortesiaConcedidaPorJogadorId",
                table: "Jogador");

            migrationBuilder.DropColumn(
                name: "CortesiaProfessorAte",
                table: "Jogador");

            migrationBuilder.DropColumn(
                name: "MotivoDaCortesia",
                table: "Jogador");
        }
    }
}
