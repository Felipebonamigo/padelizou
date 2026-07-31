using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class PacotesDeAulaEEnderecoOpcional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ORDEM IMPORTA. O scaffold do EF pôs os DropColumn ANTES do CreateTable, o que
            // apagaria o pacote de cada local antes de existir pra onde copiá-lo. Aqui é:
            // cria a tabela -> copia o que já existe -> só então derruba as colunas velhas.

            migrationBuilder.CreateTable(
                name: "PacoteDeAulas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LocalAulaId = table.Column<int>(type: "integer", nullable: false),
                    QuantidadeAulas = table.Column<int>(type: "integer", nullable: false),
                    Preco = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PacoteDeAulas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PacoteDeAulas_LocalAula_LocalAulaId",
                        column: x => x.LocalAulaId,
                        principalTable: "LocalAula",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PacoteDeAulas_LocalAulaId",
                table: "PacoteDeAulas",
                column: "LocalAulaId");

            // O pacote que cada local já anunciava vira a primeira linha da tabela nova. Sem
            // isto, todo professor com pacote perderia a oferta sem ser avisado — e só
            // descobriria pelo aluno perguntando por que sumiu.
            //
            // Só entra quem tinha pacote DE VERDADE (ativo e com preço). Quantidade nula cai
            // em 4, que era o padrão da tela antiga.
            migrationBuilder.Sql(@"
                INSERT INTO ""PacoteDeAulas"" (""LocalAulaId"", ""QuantidadeAulas"", ""Preco"", ""Ativo"")
                SELECT ""Id"", COALESCE(""PacoteQuantidadeAulas"", 4), ""PacotePreco"", true
                FROM ""LocalAula""
                WHERE ""PacoteAtivo"" = true
                  AND ""PacotePreco"" IS NOT NULL
                  AND ""PacotePreco"" > 0;
            ");

            migrationBuilder.DropColumn(
                name: "PacoteAtivo",
                table: "LocalAula");

            migrationBuilder.DropColumn(
                name: "PacotePreco",
                table: "LocalAula");

            migrationBuilder.DropColumn(
                name: "PacoteQuantidadeAulas",
                table: "LocalAula");

            migrationBuilder.AlterColumn<string>(
                name: "Endereco",
                table: "LocalAula",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Endereço volta a ser obrigatório: o que estiver nulo vira string vazia antes,
            // senão o ALTER falha e a volta atrás fica impossível justo na hora do aperto.
            migrationBuilder.Sql(@"UPDATE ""LocalAula"" SET ""Endereco"" = '' WHERE ""Endereco"" IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "Endereco",
                table: "LocalAula",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PacoteAtivo",
                table: "LocalAula",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PacotePreco",
                table: "LocalAula",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PacoteQuantidadeAulas",
                table: "LocalAula",
                type: "integer",
                nullable: true);

            // Devolve o pacote pras colunas antigas ANTES de a tabela sumir. Local com mais de
            // um perde os outros — é o preço de voltar pra um campo só, e melhor perder o
            // segundo pacote do que voltar sem pacote nenhum.
            migrationBuilder.Sql(@"
                UPDATE ""LocalAula"" l
                SET ""PacoteAtivo"" = true,
                    ""PacoteQuantidadeAulas"" = p.""QuantidadeAulas"",
                    ""PacotePreco"" = p.""Preco""
                FROM (
                    SELECT DISTINCT ON (""LocalAulaId"") ""LocalAulaId"", ""QuantidadeAulas"", ""Preco""
                    FROM ""PacoteDeAulas""
                    WHERE ""Ativo"" = true
                    ORDER BY ""LocalAulaId"", ""QuantidadeAulas""
                ) p
                WHERE p.""LocalAulaId"" = l.""Id"";
            ");

            migrationBuilder.DropTable(
                name: "PacoteDeAulas");
        }
    }
}
