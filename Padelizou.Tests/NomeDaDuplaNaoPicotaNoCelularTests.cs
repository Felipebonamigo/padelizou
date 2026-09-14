using System.Text.RegularExpressions;

namespace Padelizou.Tests;

// 14/09/2026 — NO CELULAR MENOR, O SOBRENOME SUMIA.
//
// 🗣️ Felipe, com o print da lista de Finalizadas: *"Os nomes em celulares menores nao cabem, nao
// da pra saber quem é"* — "Marina Va…", "Débora Gonçalv…", "Lucas Ped…".
//
// 🕳️ O caminho SEM check-in (`LadoDaDupla`, em _JogoEmLinha.cshtml) põe a dupla inteira num
// `.pdz-jl-nome` só, com `white-space: nowrap` + `text-overflow: ellipsis`. Como é ele que tem
// `flex: 1 1 auto`, é ele que absorve todo o aperto da linha — e absorve ESCONDENDO NOME. Com
// rostos, escudo, placar e porcentagem disputando a mesma linha, não sobra largura pra dois.
//
// 👁️ MEDIDO NO CHROMIUM, com o `site.css` do repositório e um cartão de jogo REAL extraído da
// página de produção (não markup escrito à mão — a primeira tentativa foi assim e não reproduziu
// o defeito, porque faltavam o escudo do time e o placar):
//
//   360px ANTES : "Rebeca Gergen ▪ / Laí…"  ·  "Cristina Bassols ▪ / …"   ← o 2º nome some
//   360px DEPOIS: "Rebeca Gergen /" + "Laís Rodrigues"                    ← quebra, não pica
//   600px       : idêntico antes e depois — a regra só vale abaixo do ponto de quebra
//
// ⚠️ O `nowrap` NO `<a>` É METADE DA CORREÇÃO: sem ele a quebra cairia no meio do nome da pessoa
// ("Cristina / Bassols"). Com ele, o único ponto de quebra possível é a barra entre os dois.
public class NomeDaDuplaNaoPicotaNoCelularTests
{
    private static string Css() =>
        TestInfra.SemComentarios(File.ReadAllText(Path.Combine(PastaDoProjeto(), "wwwroot", "css", "site.css")));

    // TODO o CSS que vale só pro celular — e são SETE blocos `@media (max-width: 575.98px)` no
    // arquivo, não um. A primeira versão deste teste lia só o primeiro e falhava com a correção
    // presente: o teste estava errado, não o CSS.
    private static string NoCelular()
    {
        var css = Css();
        var blocos = new System.Text.StringBuilder();

        for (int i = css.IndexOf(Celular, StringComparison.Ordinal); i >= 0;
             i = css.IndexOf(Celular, i + 1, StringComparison.Ordinal))
        {
            int prof = 0;
            for (int j = css.IndexOf('{', i); j < css.Length; j++)
            {
                if (css[j] == '{') prof++;
                else if (css[j] == '}' && --prof == 0) { blocos.Append(css[i..(j + 1)]); break; }
            }
        }

        Assert.True(blocos.Length > 0, "não achei bloco de celular no site.css");
        return blocos.ToString();
    }

    private const string Celular = "@media (max-width: 575.98px)";

    [Fact]
    public void O_nome_da_dupla_quebra_em_vez_de_picotar()
    {
        var celular = NoCelular();

        Assert.Matches(new Regex(@"\.pdz-jl-dupla\s+\.pdz-jl-nome\s*\{[^}]*white-space:\s*normal", RegexOptions.Singleline), celular);
        Assert.Matches(new Regex(@"\.pdz-jl-dupla\s+\.pdz-jl-nome\s*\{[^}]*text-overflow:\s*clip", RegexOptions.Singleline), celular);
    }

    [Fact]
    public void E_a_quebra_cai_na_barra_e_nunca_no_meio_de_um_nome()
    {
        // Sem isto a linha dobraria em "Cristina / Bassols", que é pior que picotar: parece outra
        // pessoa. O `<a>` é um nome inteiro — ele não quebra.
        Assert.Matches(
            new Regex(@"\.pdz-jl-dupla\s+\.pdz-jl-nome\s*>\s*a\s*\{[^}]*white-space:\s*nowrap", RegexOptions.Singleline),
            NoCelular());
    }

    [Fact]
    public void Fora_do_celular_o_nome_continua_numa_linha_so()
    {
        // ⚠️ O GUARDA-CORPO: a regra base (fora da media query) continua `nowrap` + `ellipsis`.
        // Em tela larga a dupla cabe numa linha, e deixá-la quebrar ali só faria o cartão crescer
        // à toa.
        // ⚠️ "FORA DO CELULAR" É O CSS MENOS OS BLOCOS DE CELULAR, e não "o que vem antes do
        // primeiro deles": a regra base do `.pdz-jl-nome` mora DEPOIS do primeiro `@media` do
        // arquivo, e cortar ali fazia este teste falhar com a regra no lugar certo.
        var basePadrao = Css();
        foreach (var bloco in new[] { NoCelular() })
            basePadrao = basePadrao.Replace(bloco, "");

        Assert.Matches(
            new Regex(@"\.pdz-jl-nome\s*\{[^}]*white-space:\s*nowrap", RegexOptions.Singleline),
            basePadrao);
    }

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "wwwroot")))
                return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Não achei a pasta do projeto web a partir de " + AppContext.BaseDirectory);
    }
}
