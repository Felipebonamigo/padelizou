using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class PerfilDeOrganizadorEAprovacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AprovadoEm",
                table: "Torneio",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AprovadoPorId",
                table: "Torneio",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOrganizadorTorneio",
                table: "Jogador",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // ⚠️ TODO TORNEIO QUE JÁ EXISTE NASCE APROVADO.
            //
            // Sem isto, a regra nova apagaria da listagem e da Home todo torneio que está no
            // ar — inclusive os que já foram anunciados e têm gente inscrita. Ninguém pediu
            // pra sumir; a aprovação existe pra filtrar o que vem DEPOIS dela.
            //
            // `AprovadoPorId` fica nulo de propósito: ninguém aprovou de fato, foi a régua que
            // mudou. Gravar um admin ali seria inventar uma decisão que não aconteceu.
            migrationBuilder.Sql(@"
                UPDATE ""Torneio"" SET ""AprovadoEm"" = NOW() WHERE ""AprovadoEm"" IS NULL;");

            // ⚠️ QUEM JÁ ORGANIZA ALGUM TORNEIO CONTINUA PODENDO CRIAR.
            //
            // A porta nova é pra quem chega agora. Fechá-la na cara de quem já está rodando
            // torneio no sistema deixaria o organizador de hoje sem conseguir abrir a edição
            // do mês que vem — e ele descobriria isso no pior momento, com a quadra reservada.
            migrationBuilder.Sql(@"
                UPDATE ""Jogador"" SET ""IsOrganizadorTorneio"" = true
                WHERE ""Id"" IN (SELECT DISTINCT ""JogadorId"" FROM ""TorneioOrganizador"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AprovadoEm",
                table: "Torneio");

            migrationBuilder.DropColumn(
                name: "AprovadoPorId",
                table: "Torneio");

            migrationBuilder.DropColumn(
                name: "IsOrganizadorTorneio",
                table: "Jogador");
        }
    }
}
