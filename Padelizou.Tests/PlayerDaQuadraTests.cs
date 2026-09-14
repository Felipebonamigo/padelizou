using System.Text.RegularExpressions;
using Xunit;

namespace Padelizou.Tests;

// 14/09/2026 — O VÍDEO DA QUADRA PARAVA QUANDO O JOGO DELA TROCAVA.
//
// 🗣️ Um espectador, pelo Felipe: *"as vezes o video do youtube trava no site"*.
//
// 🕳️ O <iframe> é do JOGO, mas a câmera é da QUADRA (Services/TransmissaoDaQuadra.cs: *"o link é
// uma propriedade do LUGAR"*). O jogo acabava, o cartão saía com o player dentro, e o próximo
// jogo da mesma quadra trazia um player NOVO da MESMA transmissão. Sem `autoplay`, o player novo
// nasce parado na miniatura da live — um quadro da própria quadra. Na tela: vídeo travado.
//
// O COMPORTAMENTO tem trava de verdade no `Padelizou.Tests/js/conferir-abas-que-ficam.js`, que
// roda o `jogos-ao-vivo-atualiza.js` de verdade num DOM falso e conta quantas vezes o player de
// cada transmissão NASCE.
//
// 🔑 O QUE ESTE ARQUIVO GUARDA É O CONTRATO ENTRE O RAZOR E O JS — e ele é novo e carregado: o JS
// PAREIA o cartão que sai com o que entra pelo `src` do `<iframe>` dentro de `.pdz-live-video`.
// Renomear a classe, mover o iframe ou pôr no `src` qualquer coisa que varie por JOGO não quebra
// compilação, não quebra teste nenhum, e o pareamento simplesmente para de casar: o vídeo volta a
// travar a cada troca de jogo, calado, no sábado do torneio.
public class PlayerDaQuadraTests
{
    [Fact]
    public void O_Razor_e_o_JS_falam_a_mesma_classe_da_transmissao()
    {
        // O seletor exato que o `transmissaoDe` usa pra descobrir qual câmera um cartão mostra.
        Assert.Contains(".pdz-live-video iframe", Js());

        // ⚠️ O ATRIBUTO, e não a palavra solta: os comentários do Razor e do JS citam esta classe
        // de propósito, e procurar a menção deixaria o teste verde com a classe renomeada na tag.
        Assert.Contains("class=\"pdz-live-video\"", Cartoes());
        Assert.Contains("<iframe src=\"https://www.youtube.com/embed/", Cartoes());
    }

    [Fact]
    public void Cada_cartao_ao_vivo_tem_no_maximo_UM_bloco_de_transmissao()
    {
        // `querySelector` devolve o PRIMEIRO. Com dois blocos no mesmo cartão, o reaproveitamento
        // pararia o player certo e preservaria o errado — e nada acusaria.
        // ⚠️ A TAG, e não a palavra: `<iframe>` aparece três vezes neste arquivo, e duas são
        // comentário explicando por que o vídeo não pode ser reescrito. Contar menção deixaria o
        // teste vermelho por alguém explicar melhor — foi o que aconteceu ao escrevê-lo.
        Assert.Equal(1, Regex.Matches(Cartoes(), "class=\"pdz-live-video\"").Count);
        Assert.Equal(1, Regex.Matches(Cartoes(), "<iframe src=").Count);
    }

    [Fact]
    public void O_src_do_iframe_identifica_a_CAMERA_e_nada_do_jogo()
    {
        var src = Regex.Match(Cartoes(), "<iframe src=\"([^\"]+)\"").Groups[1].Value;

        // O `src` é a identidade da câmera: é por ele que o cartão que sai reconhece o que entra
        // como "a mesma transmissão". Qualquer pedaço que mude de um JOGO pro outro — um
        // `?start=`, um `&t=`, o id da partida — faz os dois `src` deixarem de bater, e o
        // reaproveitamento morre sem dizer nada.
        Assert.Equal("https://www.youtube.com/embed/@youtubeId", src);

        // E o `youtubeId` sai do link da partida, que é o link da QUADRA quando o organizador o
        // aplica nela (Services/TransmissaoDaQuadra).
        Assert.Contains("var youtubeId = ExtrairYoutubeId(jogo.LinkTransmissao);", Cartoes());
    }

    private static string Cartoes() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "_JogosDoTorneio.cshtml"));

    private static string Js() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "wwwroot", "js", "jogos-ao-vivo-atualiza.js"));

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Views");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Não achei a pasta do projeto web subindo a partir de " + AppContext.BaseDirectory);
    }
}
