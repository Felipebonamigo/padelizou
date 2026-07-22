using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class FixJogadorEmailUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A constraint UNIQUE sem filtro em Jogador.Email só existe no schema local (drift
            // entre ambientes - produção nunca teve essa constraint, então nunca teve esse bug).
            // Ali, ela fazia dois jogadores sem e-mail (Email = NULL, comum em cadastros "avulsos"
            // via inscrição de Dupla) colidirem entre si. Só mexe em algo se essa constraint
            // realmente existir neste banco - em produção esse bloco não faz nada.
            migrationBuilder.Sql(@"
DECLARE @constraintName sysname;
SELECT @constraintName = kc.name
FROM sys.key_constraints kc
JOIN sys.index_columns ic ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE kc.parent_object_id = OBJECT_ID('Jogador') AND kc.type = 'UQ' AND c.name = 'Email';

IF @constraintName IS NOT NULL
BEGIN
    EXEC('ALTER TABLE [Jogador] DROP CONSTRAINT [' + @constraintName + ']');

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Jogador_Email' AND object_id = OBJECT_ID('Jogador'))
    BEGIN
        CREATE UNIQUE INDEX [IX_Jogador_Email] ON [Jogador]([Email]) WHERE [Email] IS NOT NULL;
    END
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Jogador_Email' AND object_id = OBJECT_ID('Jogador'))
BEGIN
    DROP INDEX [IX_Jogador_Email] ON [Jogador];
END
");
        }
    }
}
