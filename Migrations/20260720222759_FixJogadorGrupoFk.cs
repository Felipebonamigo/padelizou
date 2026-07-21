using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class FixJogadorGrupoFk : Migration
    {
        // Migration vazia de propósito: a tabela JogadorGrupo já tinha o índice e a FK certos em
        // GrupoId desde o schema original (confirmado via sys.indexes/sys.foreign_keys). O que
        // estava errado era só o modelo C# (faltava [ForeignKey("GrupoId")] na navegação
        // GrupoPrivado), o que fazia o EF inventar uma shadow property "GrupoPrivadoId" que nunca
        // existiu de verdade no banco. Esta migration só realinha o snapshot do EF com a
        // realidade, sem nenhuma alteração de schema.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
