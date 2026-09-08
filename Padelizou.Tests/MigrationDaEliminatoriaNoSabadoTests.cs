using System.IO;
using Xunit;

namespace Padelizou.Tests;

// ⚠️ O DEFAULT DA COLUNA NOVA, NO BANCO — e não só no C#.
//
// `Categoria.EliminatoriaNoSabadoANoite` nasce `true` no modelo: toda categoria que já existe
// continua podendo jogar sábado à noite, que é como o sistema sempre funcionou. Mas o
// `dotnet ef migrations add` NÃO LÊ o inicializador da propriedade — ele gerou
// `defaultValue: false`, e esse valor é o que BACKFILLA as linhas que já estão em produção.
//
// Sem esta correção, o deploy tiraria em silêncio o sábado à noite de TODAS as categorias de
// TODOS os torneios existentes. Ninguém apertou botão nenhum, e a grade do próximo "Refazer
// grade" jogaria eliminatória pro domingo em torneio que nunca pediu isso.
//
// Este teste lê a migration como TEXTO de propósito: é o arquivo gerado que erra, e regenerá-lo
// (o que acontece toda vez que alguém mexe no modelo perto disso) traria o `false` de volta.
public class MigrationDaEliminatoriaNoSabadoTests
{
    [Fact]
    public void A_coluna_nasce_liberada_pras_categorias_que_ja_existem()
    {
        var migration = File.ReadAllText(Arquivo());

        var coluna = migration.IndexOf("name: \"EliminatoriaNoSabadoANoite\"", StringComparison.Ordinal);
        Assert.True(coluna >= 0, "Não achei a coluna EliminatoriaNoSabadoANoite na migration.");

        var fimDaChamada = migration.IndexOf(");", coluna, StringComparison.Ordinal);
        Assert.Contains("defaultValue: true", migration[coluna..fimDaChamada]);
    }

    private static string Arquivo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var migrations = Path.Combine(dir.FullName, "Padelizou", "Migrations");
            if (Directory.Exists(migrations))
            {
                var achado = Directory
                    .GetFiles(migrations, "*_ConcentracaoDeJogosEEliminatoriaNoSabado.cs")
                    .FirstOrDefault();
                Assert.NotNull(achado);
                return achado!;
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Não achei a pasta Migrations.");
    }
}
