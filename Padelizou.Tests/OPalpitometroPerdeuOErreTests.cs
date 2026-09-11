using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Padelizou.Tests;

// 11/09/2026 — O NOME PERDEU O ERRE.
//
// 🗣️ Felipe, com um print da lista de jogos e o rótulo circulado: *"ta escrito 'palpitrometro'
// aqui"*. E, perguntado se era só o texto da tela ou o código inteiro: **o código inteiro**.
//
// 🕳️ A palavra é `palpite` + `-ômetro` = **palpitômetro**, como bafômetro e velocímetro. O erre
// nunca teve de onde sair — ele entrou no primeiro arquivo (19/08/2026) e foi copiado por 35
// arquivos, 4 nomes de arquivo, uma classe de CSS e o nome das funções do JS. O próprio Felipe
// sempre escreveu "palpitometro" nos pedidos dele; a tela é que respondia com erre.
//
// ⚠️ ESTE GATE VARRE O CÓDIGO, e é o que impede o erre de voltar pela porta do copiar-colar:
// um arquivo novo que nasça de um antigo traz o nome errado junto. Ele olha conteúdo E nome de
// arquivo — `palpitometro.js` não seria pego por um gate que só lesse o texto de dentro.
//
// ⚠️ O `.md` fica DE FORA de propósito: o `STATUS.md` é diário datado, e entrada de agosto que
// cita `conferir-palpitrometro.js` é registro do que aconteceu, não erro a corrigir. Reescrever
// o passado tiraria o único jeito de achar aquele trabalho depois.
public class OPalpitometroPerdeuOErreTests
{
    // Montado em pedaços pra este arquivo não cair no próprio gate pelo `const`.
    private const string ComErre = "palpit" + "r";

    // ⚠️ E o arquivo se exclui da varredura: ele CITA o defeito — a frase do Felipe e o nome
    // antigo do conferidor de JS — e sem esta linha o gate acusaria a própria documentação
    // dele. É a única exceção, e ela vale por um arquivo só.
    private const string EsteArquivo = "OPalpitometroPerdeuOErreTests.cs";

    [Fact]
    public void Nenhum_arquivo_de_codigo_escreve_o_nome_com_erre()
    {
        var achados = new List<string>();

        foreach (var arquivo in ArquivosDeCodigo())
        {
            var linhas = File.ReadAllLines(arquivo);
            for (var i = 0; i < linhas.Length; i++)
            {
                if (linhas[i].Contains(ComErre, StringComparison.OrdinalIgnoreCase))
                    achados.Add($"{Relativo(arquivo)}:{i + 1}  {linhas[i].Trim()}");
            }
        }

        Assert.True(achados.Count == 0,
            $"O nome se escreve sem erre (palpitômetro). Achei {achados.Count}:\n" +
            string.Join("\n", achados.Take(20)));
    }

    [Fact]
    public void Nenhum_arquivo_se_CHAMA_com_erre()
    {
        var errados = ArquivosDeCodigo()
            .Where(a => Path.GetFileName(a).Contains(ComErre, StringComparison.OrdinalIgnoreCase))
            .Select(Relativo)
            .ToList();

        Assert.True(errados.Count == 0,
            "Arquivo com erre no nome:\n" + string.Join("\n", errados));
    }

    // As duas travas acima passariam com o rótulo APAGADO da tela. Estas duas exigem que ele
    // continue lá — escrito certo.
    [Fact]
    public void O_rotulo_da_lista_de_jogos_diz_Palpitometro()
    {
        var view = File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "Views", "Torneios", "_JogoEmLinha.cshtml"));

        Assert.Contains("PALPITÔMETRO", view);
    }

    [Fact]
    public void O_rotulo_do_cartao_grande_diz_Palpitometro()
    {
        var view = File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "Views", "Torneios", "_Palpitometro.cshtml"));

        Assert.Contains("Palpitômetro", view);
    }

    private static readonly string[] Extensoes = [".cs", ".cshtml", ".js", ".css", ".html", ".yml"];

    // `wwwroot/lib` é biblioteca de terceiro (Bootstrap e afins): não é código nosso pra
    // renomear. `bin`/`obj` são gerados — o Razor compilado copia o texto das views pra lá.
    private static readonly string[] Fora = ["bin", "obj", ".git", "node_modules", "lib", "TestResults"];

    private static IEnumerable<string> ArquivosDeCodigo() =>
        Directory.EnumerateFiles(RaizDoRepo(), "*.*", SearchOption.AllDirectories)
                 .Where(a => Extensoes.Contains(Path.GetExtension(a), StringComparer.OrdinalIgnoreCase))
                 .Where(a => !Relativo(a).Split('/').Intersect(Fora).Any())
                 .Where(a => Path.GetFileName(a) != EsteArquivo);

    private static string Relativo(string caminho) =>
        Path.GetRelativePath(RaizDoRepo(), caminho).Replace('\\', '/');

    private static string RaizDoRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Views")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Não achei a raiz do repositório a partir de " + AppContext.BaseDirectory);
    }
}
