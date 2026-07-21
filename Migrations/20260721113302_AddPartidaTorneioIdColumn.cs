using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class AddPartidaTorneioIdColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A tabela Partida foi criada fora do EF Migrations (era mantida manualmente antes da
            // adoção do EF Migrations) sem a coluna TorneioId, embora o modelo C# (Partida.TorneioId)
            // e dezenas de pontos do código sempre a tenham pressuposto. Como TorneioId é sempre
            // redundante com Categoria.TorneioId, adiciona a coluna física e faz o backfill.
            migrationBuilder.AddColumn<int>(
                name: "TorneioId",
                table: "Partida",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE p SET p.TorneioId = c.TorneioId
                FROM Partida p
                INNER JOIN Categoria c ON c.Id = p.CategoriaId;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Partida_TorneioId",
                table: "Partida",
                column: "TorneioId");

            migrationBuilder.AddForeignKey(
                name: "FK_Partida_Torneio_TorneioId",
                table: "Partida",
                column: "TorneioId",
                principalTable: "Torneio",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Partida_Torneio_TorneioId",
                table: "Partida");

            migrationBuilder.DropIndex(
                name: "IX_Partida_TorneioId",
                table: "Partida");

            migrationBuilder.DropColumn(
                name: "TorneioId",
                table: "Partida");
        }
    }
}
