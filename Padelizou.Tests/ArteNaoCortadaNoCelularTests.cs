using System.Text.RegularExpressions;
using Xunit;

namespace Padelizou.Tests;

// 14/09/2026 — A ARTE DOS CAMPEÕES SAÍA CORTADA NO CELULAR. 🗣️ Felipe, com o print da 6ª
// Masculina do 2ª Etapa ER PADEL TOUR aberto no iPhone: *"Foto cortada aqui"* — o nome
// "Jean Fernandes & Lucas Pedroso (Pedrosin)" morria no meio, sem o lado direito da arte.
//
// 🔑 A CAUSA É UMA REGRA DE CSS QUE JÁ MORDEU ESTE REPOSITÓRIO UMA VEZ, na tela de erro
// (ver o comentário no `Views/Home/NaoEncontrado.cshtml`): `img-fluid` é uma CLASSE
// (`.img-fluid{max-width:100%}`) e o `style="max-width: 380px"` é INLINE. Mesma propriedade,
// e inline tem prioridade — então a classe que deveria encolher a imagem é simplesmente
// desligada, e a arte fica com 380px fixos numa caixa de 365,5px.
//
// ⚠️ E O CORTE É INVISÍVEL, que é o que torna este defeito caro: o cartão de fora tem
// `overflow-hidden`, então a página NÃO rola de lado — não há barra, não há gesto, não há
// pista. Medido no Chromium com o Bootstrap do repositório: a 390px a arte passa 31,8px da
// caixa e a 320px passa 101,8px, e os dois somem. Quem vê acha que a arte é assim.
//
// ⚠️ POR QUE TESTE DE FONTE: a suíte não renderiza Razor e não tem navegador. O que dá pra
// travar aqui é justamente o que a medição provou ser a causa — o teto inline sozinho — e
// isso é texto no arquivo. Um defeito de layout não deixa exceção em log nem teste vermelho
// em lugar nenhum: some calado, na arte que a pessoa vai postar no story.
public class ArteNaoCortadaNoCelularTests
{
    // A caixa mais estreita onde uma arte é desenhada hoje: `.container` (max-width 720px) num
    // celular de 390px, menos o respiro do container e o `card-body` de dentro. MEDIDO no
    // Chromium, não estimado. Serve de referência pro leitor — o gate abaixo não depende dela,
    // de propósito: o teto que cabe hoje é o que vaza no aparelho de 320px de amanhã.
    private const double CaixaEm390 = 365.5;

    [Fact]
    public void Nenhuma_imagem_pode_ficar_mais_larga_que_a_caixa_que_a_contem()
    {
        var vazando = new List<string>();

        foreach (var (arquivo, fonte) in TodasAsViews())
        {
            foreach (Match tag in Regex.Matches(fonte, @"<img\b[^>]*>", RegexOptions.Singleline))
            {
                var estilo = Estilo(tag.Value);
                if (estilo == null) continue;

                // Só interessa o teto escrito em PIXEL FIXO: é ele que vence o `max-width:100%`
                // da classe. Um teto em `min(...)` ou em `%` já se rende à caixa sozinho.
                if (!Regex.IsMatch(estilo, @"max-width\s*:\s*\d+(\.\d+)?px")) continue;

                // As duas saídas corretas, e as duas já existem no repositório:
                //   `width:100%` junto           — Views/Home/NaoEncontrado.cshtml
                //   `max-width: min(Npx, 100%)`  — Views/Torneios/CompartilharJogos.cshtml
                var encolhe = Regex.IsMatch(estilo, @"(^|[;\s])width\s*:\s*100%")
                              || Regex.IsMatch(estilo, @"max-width\s*:\s*min\s*\(");

                if (!encolhe) vazando.Add($"{arquivo} → style=\"{estilo}\"");
            }
        }

        Assert.True(vazando.Count == 0,
            $"Imagem com teto em pixel fixo e sem nada que a faça encolher (caixa de ~{CaixaEm390}px " +
            "num celular de 390px). O `max-width` INLINE vence o `max-width:100%` da classe " +
            "`img-fluid` — mesma propriedade, e inline tem prioridade —, então a imagem estoura a " +
            "caixa e o `overflow-hidden` do cartão come o lado direito SEM deixar a página rolar: " +
            "não há barra, não há pista, e quem vê acha que a arte é assim. A saída é " +
            "`max-width: min(Npx, 100%)`, que mantém o teto no computador e cede no celular:" +
            Environment.NewLine + string.Join(Environment.NewLine, vazando));
    }

    [Fact]
    public void Toda_arte_compartilhavel_continua_com_teto_no_computador()
    {
        // A trava do lado oposto. O gate acima também ficaria verde apagando o `max-width` —
        // e aí a arte de 1080px viraria uma faixa de 720px de largura no computador, que é
        // outro defeito, só que na direção contrária e sem ninguém pra fotografar.
        var semTeto = new List<string>();

        foreach (var (arquivo, fonte) in TodasAsViews().Where(v => v.Arquivo.Contains("Cartoes")))
        {
            foreach (Match tag in Regex.Matches(fonte, @"<img\b[^>]*>", RegexOptions.Singleline))
            {
                var estilo = Estilo(tag.Value) ?? "";
                if (!Regex.IsMatch(estilo, @"max-width\s*:[^;]*\d+px")) semTeto.Add(arquivo);
            }
        }

        Assert.True(semTeto.Count == 0,
            "Arte compartilhável sem teto de largura: no computador ela ocuparia os 720px do " +
            "container. O teto tem que ficar — o que muda é ele passar a ceder no celular: " +
            string.Join(", ", semTeto));
    }

    private static string? Estilo(string tag)
    {
        var m = Regex.Match(tag, "style\\s*=\\s*\"([^\"]*)\"", RegexOptions.Singleline);
        return m.Success ? m.Groups[1].Value : null;
    }

    private static IEnumerable<(string Arquivo, string Fonte)> TodasAsViews()
    {
        var views = Path.Combine(PastaDoProjeto(), "Views");

        return Directory.EnumerateFiles(views, "*.cshtml", SearchOption.AllDirectories)
            .OrderBy(c => c, StringComparer.Ordinal)
            .Select(c => (Path.GetRelativePath(views, c).Replace('\\', '/'), File.ReadAllText(c)));
    }

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidato = Path.Combine(dir.FullName, "Padelizou", "Views");
            if (Directory.Exists(candidato)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Não achei a pasta do projeto Padelizou a partir de " + AppContext.BaseDirectory);
    }
}
