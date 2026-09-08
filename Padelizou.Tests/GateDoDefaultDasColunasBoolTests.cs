using System.IO;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Padelizou.Tests;

// GATE MECÂNICO: TODA COLUNA `bool` NOVA NASCE COM O MESMO VALOR QUE O MODELO DÁ A ELA.
//
// ⚠️ POR QUE ISTO EXISTE, E POR QUE É GATE E NÃO TESTE DE UM CASO: em 08/09/2026 o
// `dotnet ef migrations add` errou DUAS VEZES no mesmo dia, do mesmo jeito. Ele NÃO LÊ o
// inicializador da propriedade — `public bool X { get; set; } = true;` vira
// `defaultValue: false` na migration — e esse valor é o que BACKFILLA as linhas que já estão
// em produção.
//
// As duas: `Categoria.EliminatoriaNoSabadoANoite` teria tirado em silêncio o sábado à noite de
// TODAS as categorias de TODOS os torneios; `Categoria.PodeJogarNaSedeExtra` teria prendido na
// sede principal todas as categorias soltas de todo torneio de duas sedes. Ninguém apertaria
// botão nenhum, e a próxima grade sairia diferente sem explicação.
//
// Duas vezes no mesmo dia é padrão, não acidente — e a terceira ia acontecer numa sessão com
// pressa. Este gate varre TODAS as migrations e cruza cada `AddColumn<bool>` com o valor que o
// modelo dá pra aquela propriedade hoje.
//
// ⚠️ FALSO POSITIVO POSSÍVEL E LEGÍTIMO: uma coluna cujo default do MODELO mudou depois da
// migration. A migration antiga está certa (ela backfillou o que valia na época) e o gate vai
// reclamar. Quando isso acontecer, a saída é anotar a exceção em `HistoricoDivergente` com o
// motivo — não relaxar o gate.
public class GateDoDefaultDasColunasBoolTests
{
    // Colunas em que a migration e o modelo divergem DE PROPÓSITO.
    private static readonly HashSet<string> HistoricoDivergente = new()
    {
        // 31/07/2026, `CheckInOpcional`: o check-in era OBRIGATÓRIO até então, e virou opção.
        // Torneio NOVO nasce sem ele (é o caso comum); os que já estavam NO AR receberam `true`
        // pra não perderem uma tela que já usavam. As duas coisas estão certas, e é justamente
        // por isso que elas divergem — está escrito em Models/Torneio.UsaCheckIn.
        "Torneio.UsaCheckIn",
    };

    [Fact]
    public void Toda_coluna_bool_backfilla_o_mesmo_valor_que_o_modelo()
    {
        using var ctx = TestInfra.NovoContexto();

        // Tabela + propriedade -> o valor que uma entidade NOVA tem, que é o que as linhas
        // existentes precisam receber pra não mudarem de comportamento no deploy.
        var padraoDoModelo = new Dictionary<string, bool>();
        foreach (var entidade in ctx.Model.GetEntityTypes())
        {
            var tabela = entidade.GetTableName();
            if (tabela == null || entidade.ClrType.IsAbstract) continue;

            object? novo;
            try { novo = Activator.CreateInstance(entidade.ClrType); }
            catch { continue; }   // entidade sem construtor sem parâmetros — não dá pra perguntar
            if (novo == null) continue;

            foreach (var prop in entidade.ClrType.GetProperties())
            {
                if (prop.PropertyType != typeof(bool) || !prop.CanRead) continue;
                padraoDoModelo[$"{tabela}.{prop.Name}"] = (bool)prop.GetValue(novo)!;
            }
        }

        var problemas = new List<string>();

        foreach (var arquivo in Directory.GetFiles(PastaDeMigrations(), "*.cs"))
        {
            if (arquivo.EndsWith(".Designer.cs") || arquivo.Contains("Snapshot")) continue;

            var fonte = File.ReadAllText(arquivo);
            foreach (Match chamada in Regex.Matches(fonte, @"AddColumn<bool>\((.*?)\);", RegexOptions.Singleline))
            {
                var corpo = chamada.Groups[1].Value;
                var nome = Regex.Match(corpo, @"name:\s*""([^""]+)""");
                var tabela = Regex.Match(corpo, @"table:\s*""([^""]+)""");
                if (!nome.Success || !tabela.Success) continue;

                var chave = $"{tabela.Groups[1].Value}.{nome.Groups[1].Value}";
                if (HistoricoDivergente.Contains(chave)) continue;

                // Coluna que não existe mais no modelo (renomeada, removida): a migration é
                // história, e história não se audita contra o modelo de hoje.
                if (!padraoDoModelo.TryGetValue(chave, out var doModelo)) continue;

                var declarado = Regex.Match(corpo, @"defaultValue:\s*(true|false)");
                var naMigration = declarado.Success && declarado.Groups[1].Value == "true";

                if (naMigration != doModelo)
                {
                    problemas.Add(
                        $"{Path.GetFileNameWithoutExtension(arquivo)}: {chave} nasce "
                        + $"{(doModelo ? "true" : "false")} no modelo, mas a migration backfilla "
                        + $"{(declarado.Success ? declarado.Groups[1].Value : "nada (= false)")}. "
                        + "As linhas que já estão no banco receberiam o valor errado.");
                }
            }
        }

        Assert.True(problemas.Count == 0, string.Join("\n", problemas));
    }

    private static string PastaDeMigrations()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Migrations");
            if (Directory.Exists(alvo)) return alvo;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Não achei a pasta Migrations.");
    }
}
