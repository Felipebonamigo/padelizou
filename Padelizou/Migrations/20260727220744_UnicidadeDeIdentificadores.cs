using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <summary>
    /// E-mail, CPF e login identificam UMA pessoa cada um — agora garantido pelo banco.
    ///
    /// O que existia antes desta migração:
    ///   CPF   → índice único ✅
    ///   Login → índice único, mas SENSÍVEL A MAIÚSCULA: "Bona" e "bona" cabiam os dois,
    ///           e a entrada trata os dois como a mesma pessoa
    ///   Email → nenhum índice ❌
    ///
    /// Índices por LOWER() porque a comparação do sistema ignora maiúsculas; expressão é
    /// algo que o EF não sabe modelar, daí SQL na mão em vez de HasIndex.
    ///
    /// Parciais (WHERE) porque jogador de pré-cadastro — criado pelo organizador ao
    /// inscrever a dupla — nasce sem e-mail e sem login. NULL não colide com NULL no
    /// PostgreSQL, mas string vazia colidiria com string vazia, e um único registro
    /// legado com '' faria o próximo cadastro estourar.
    ///
    /// Os três bancos (prod, dev, local) foram conferidos sem nenhum duplicado antes de
    /// escrever isto: a migração roda no start do app, e falhar aqui deixaria o app fora
    /// do ar.
    /// </summary>
    public partial class UnicidadeDeIdentificadores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Jogador_Email_minusculo""
                    ON ""Jogador"" (LOWER(""Email""))
                    WHERE ""Email"" IS NOT NULL AND ""Email"" <> '';");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Jogador_Login_minusculo""
                    ON ""Jogador"" (LOWER(""Login""))
                    WHERE ""Login"" IS NOT NULL AND ""Login"" <> '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Jogador_Email_minusculo"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Jogador_Login_minusculo"";");
        }
    }
}
