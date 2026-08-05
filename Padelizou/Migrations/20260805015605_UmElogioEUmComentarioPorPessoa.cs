using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class UmElogioEUmComentarioPorPessoa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Elogios_DeJogadorId_ParaJogadorId_Tipo",
                table: "Elogios");

            // O que já existe precisa caber na regra nova ANTES do índice único, senão a
            // migração falha no start e o site não sobe. Em produção havia gente com 3
            // elogios no mesmo perfil — fica o mais recente, que é a escolha mais atual
            // dela; os anteriores saem. Decisão do Felipe em 04/08/2026, com backup da
            // tabela feito antes no VPS.
            //
            // row_number() e não DISTINCT ON: o DELETE precisa do "Id" das linhas perdedoras.
            // O desempate por "Id" existe porque dois elogios podem nascer no mesmo segundo.
            migrationBuilder.Sql("""
                DELETE FROM "Elogios" WHERE "Id" IN (
                    SELECT "Id" FROM (
                        SELECT "Id", row_number() OVER (
                            PARTITION BY "DeJogadorId", "ParaJogadorId"
                            ORDER BY "CriadoEm" DESC, "Id" DESC) AS rn
                        FROM "Elogios") x
                    WHERE x.rn > 1);
                """);

            // Mesma limpeza pros comentários. Hoje não há nenhum repetido em prod nem em dev,
            // mas o banco local e o de quem clonar o repo podem ter — e um índice único que
            // falha no start derruba o site inteiro por causa de um comentário duplicado.
            migrationBuilder.Sql("""
                DELETE FROM "ComentariosPerfil" WHERE "Id" IN (
                    SELECT "Id" FROM (
                        SELECT "Id", row_number() OVER (
                            PARTITION BY "AutorId", "PerfilId"
                            ORDER BY "CriadoEm" DESC, "Id" DESC) AS rn
                        FROM "ComentariosPerfil") x
                    WHERE x.rn > 1);
                """);

            migrationBuilder.DropIndex(
                name: "IX_ComentariosPerfil_AutorId",
                table: "ComentariosPerfil");

            migrationBuilder.CreateIndex(
                name: "IX_Elogios_DeJogadorId_ParaJogadorId",
                table: "Elogios",
                columns: new[] { "DeJogadorId", "ParaJogadorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComentariosPerfil_AutorId_PerfilId",
                table: "ComentariosPerfil",
                columns: new[] { "AutorId", "PerfilId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Elogios_DeJogadorId_ParaJogadorId",
                table: "Elogios");

            migrationBuilder.DropIndex(
                name: "IX_ComentariosPerfil_AutorId_PerfilId",
                table: "ComentariosPerfil");

            migrationBuilder.CreateIndex(
                name: "IX_Elogios_DeJogadorId_ParaJogadorId_Tipo",
                table: "Elogios",
                columns: new[] { "DeJogadorId", "ParaJogadorId", "Tipo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComentariosPerfil_AutorId",
                table: "ComentariosPerfil",
                column: "AutorId");
        }
    }
}
