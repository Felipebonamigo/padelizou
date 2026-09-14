using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class PalpitometroPorCategoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ⚠️ `defaultValue: "Todas"` FOI ESCRITO À MÃO. O EF gerou string VAZIA aqui, porque
            // ele olha o TIPO da coluna e NÃO o `= AlcanceDoPalpitometro.Todas` da propriedade em
            // C# — aquele valor só vale pra objeto NOVO criado pelo app, nunca pra linha que já
            // existe no banco. É a mesma correção, e a mesma lição, da `VotacaoDeMvpOpcional`.
            //
            // Deixando como veio, TODO torneio já criado ficaria com um alcance que nenhum dos
            // quatro rádios da tela de gestão reconhece: o `AlcanceDoPalpitometro.Normalizar`
            // ainda leria isso como "Todas" (e por isso nada QUEBRARIA), mas o banco passaria a
            // guardar uma verdade que a tela não consegue mostrar — e a primeira sessão a
            // confiar na coluna crua herdaria o buraco calado.
            migrationBuilder.AddColumn<string>(
                name: "PalpitometroEm",
                table: "Torneio",
                type: "text",
                nullable: false,
                defaultValue: "Todas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PalpitometroEm",
                table: "Torneio");
        }
    }
}
