using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class VotacaoDeMvpOpcional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ⚠️ `defaultValue: true` FOI ESCRITO À MÃO. O EF gerou `false` aqui, porque ele
            // olha o tipo (`bool` → default do CLR) e NÃO o `= true` da propriedade em C# —
            // aquele valor só vale pra objeto NOVO criado pelo app, nunca pra linha que já
            // existe no banco.
            //
            // Deixando como veio, TODO torneio já criado nasceria com a votação de MVP
            // DESLIGADA — o oposto do combinado ("o padrão é vir ativo") — e o recurso
            // estrearia invisível em produção, sem erro nenhum pra denunciar.
            //
            // É a mesma correção que a migração `CheckInOpcional` fez no sentido contrário, e o
            // mesmo motivo: quem já tinha torneio no ar não pode ser surpreendido pelo default
            // de um campo que ele nunca viu.
            migrationBuilder.AddColumn<bool>(
                name: "UsaVotacaoDeMvp",
                table: "Torneio",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UsaVotacaoDeMvp",
                table: "Torneio");
        }
    }
}
