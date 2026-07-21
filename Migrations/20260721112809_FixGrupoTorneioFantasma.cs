using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class FixGrupoTorneioFantasma : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sem DDL real: essas colunas/FKs/índices ("TorneioId" fantasma em Dupla e GrupoTorneio,
            // remanescente do bug já corrigido do Dupla.Torneio) nunca existiram fisicamente no banco
            // (o vínculo real sempre foi via CategoriaId -> Categoria.TorneioId). Esta migração só
            // realinha o snapshot do EF com o modelo C# e o schema físico real.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
