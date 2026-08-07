using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class PontuaNoRankingAmericano : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Aqui o `false` que o EF gera sozinho está CERTO, ao contrário das duas migrações
            // de bool do mesmo dia (GruposAmericano e Ativa, que precisaram de edição à mão):
            // pontuar no Ranking Americano é contratado e pago pelo organizador, então torneio
            // que já existe nunca contratou. Ligar isso retroativamente daria ponto de graça a
            // quem não pediu — que é exatamente o que este campo existe pra impedir.
            migrationBuilder.AddColumn<bool>(
                name: "PontuaNoRankingAmericano",
                table: "Torneio",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PontuaNoRankingAmericano",
                table: "Torneio");
        }
    }
}
