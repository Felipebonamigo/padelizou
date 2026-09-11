using System.IO;
using System.Text.RegularExpressions;

namespace Padelizou.Tests;

// 11/09/2026 — O ESCUDO DO TIME AO LADO DO NOME, NOS JOGOS DO TORNEIO.
// 🗣️ Felipe: *"estou pensando em botar as bandeiras dos times para exibir nos jogos do torneio,
// do lado dos nomes"*.
//
// O escudo já existia em UM caso só: a categoria de TIMES (Views/Torneios/_LadoDaPartida.cshtml).
// No jogo de dupla normal o time saía como texto cinza embaixo do nome — e desaparecia na tabela
// do grupo, porque `.pdz-chip-compacto .pdz-chip-clube { display: none }` esconde aquela linha
// inteira pra cada linha da tabela não ocupar três alturas.
//
// ⚠️ A SUÍTE NÃO RENDERIZA RAZOR (ver SeloDeCampeaoDaCategoriaTests, seção 4: "a única rede é
// ler o arquivo"). Então o que se trava aqui é o ARQUIVO, e só as decisões que, desfeitas,
// apagam o escudo da tela sem deixar nenhum outro teste vermelho.
public class EscudoDoTimeNoChipTests
{
    [Fact]
    public void O_chip_do_jogador_desenha_o_escudo_do_time()
    {
        var view = LerDaWeb("Views", "Torneios", "_JogadorChip.cshtml");

        // A condição olha o LOGO, não o time: time sem logo cadastrado (o estado dos 44
        // importados do ranking) daria <img src=""> — ícone quebrado em toda linha da tabela.
        Assert.Matches(new Regex(@"IsNullOrEmpty\([^)]*\.Logo\)"), view);

        var img = Regex.Match(view, @"<img[^>]*pdz-chip-escudo[^>]*>", RegexOptions.Singleline);
        Assert.True(img.Success, "não achei o <img> do escudo no chip");
        Assert.Matches(new Regex(@"src=""@[^""]*\.Logo"""), img.Value);
    }

    [Fact]
    public void O_escudo_fica_FORA_da_linha_do_clube_porque_o_compacto_esconde_ela()
    {
        var view = LerDaWeb("Views", "Torneios", "_JogadorChip.cshtml");

        // Dentro do <small class="pdz-chip-clube"> o escudo existiria no ao vivo e sumiria na
        // TABELA DO GRUPO, que é justamente onde ele é a única pista do time — lá o nome do
        // time não é escrito.
        // ⚠️ A exigência de que o escudo EXISTA é o que faz este teste discriminar: sem ela ele
        // passaria verde com o chip de antes, onde não há escudo nenhum pra estar no lugar errado.
        Assert.Contains("pdz-chip-escudo", view);

        var linhaDoClube = Regex.Match(view, @"<small[^>]*pdz-chip-clube[^>]*>(.*?)</small>",
                                       RegexOptions.Singleline);

        Assert.True(linhaDoClube.Success, "não achei a linha do clube (<small class=\"… pdz-chip-clube\">) no chip");
        Assert.DoesNotContain("pdz-chip-escudo", linhaDoClube.Groups[1].Value);
    }

    [Fact]
    public void O_escudo_nomeia_o_time_pra_quem_nao_ve_o_texto()
    {
        var view = LerDaWeb("Views", "Torneios", "_JogadorChip.cshtml");

        // No modo compacto o nome do time não é escrito em lugar nenhum: sem o alt, o escudo é
        // um desenho sem legenda pra quem usa leitor de tela.
        var img = Regex.Match(view, @"<img[^>]*pdz-chip-escudo[^>]*>", RegexOptions.Singleline);

        Assert.True(img.Success, "não achei o <img> do escudo no chip");
        Assert.Matches(new Regex(@"alt=""@[^""]*\.Nome"""), img.Value);
    }

    [Fact]
    public void O_escudo_nao_e_cortado_no_quadrado_do_chip()
    {
        var css = LerDaWeb("wwwroot", "css", "site.css");

        // Escudo é PNG transparente de proporção LIVRE (FormatoDeImagem.LogoTime só limita o
        // lado maior). `cover` — o que a foto do jogador usa, porque rosto é quadrado — corta
        // as beiradas de escudo largo.
        var regra = Regex.Match(css, @"\.pdz-chip-escudo\s*\{([^}]*)\}", RegexOptions.Singleline);

        Assert.True(regra.Success, "não achei a regra .pdz-chip-escudo no site.css");
        Assert.Contains("object-fit: contain", regra.Groups[1].Value);
        Assert.DoesNotContain("cover", regra.Groups[1].Value);
    }

    private static string LerDaWeb(params string[] caminho)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Views")))
                return File.ReadAllText(Path.Combine(dir.FullName, "Padelizou", Path.Combine(caminho)));
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Não achei a pasta do projeto web a partir de " + AppContext.BaseDirectory);
    }
}
