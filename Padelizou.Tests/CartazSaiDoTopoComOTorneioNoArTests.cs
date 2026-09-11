using Xunit;

namespace Padelizou.Tests;

// 11/09/2026 — O CARTAZ SAI DO TOPO QUANDO O TORNEIO JÁ ESTÁ ROLANDO. 🗣️ Felipe, olhando a
// página do 2ª Etapa ER PADEL TOUR no dia do torneio: *"tambem oculte esse 'cartaz pra
// divulgar' ou coloque ele em outro lugar"*.
//
// 🕳️ O botão é a peça do ANTES — o QR dele leva pra inscrição. Com a chave publicada, a
// inscrição fechou: ele vira a primeira coisa que 100+ jogadores veem antes dos jogos, e não
// serve a nenhum deles.
//
// ⚠️ NÃO É "sumir": ele MUDA DE LUGAR, pelo mesmo desenho do card de ferramentas
// (`_FerramentasDoOrganizador`) — dois lugares possíveis, **um de cada vez**, decididos pelo
// MESMO predicado (`AprovacaoDeChaves.ChavePublicada`). Antes da chave, onde sempre esteve, pra
// todo mundo: cada inscrito que chama a turma no grupo é divulgação que não custa nada. Depois
// dela, dentro do card do organizador — que nessa hora já é a primeira coisa da tela.
public class CartazSaiDoTopoComOTorneioNoArTests
{
    [Fact]
    public void O_cartaz_do_topo_obedece_a_CHAVE_PUBLICADA()
    {
        var view = Ler("Views", "Torneios", "Details.cshtml");

        var cartaz = view.IndexOf("Cartaz pra divulgar", StringComparison.Ordinal);
        Assert.True(cartaz >= 0, "Não achei o botão do cartaz no topo.");

        // O `@if` que envolve o botão é o mais próximo acima dele.
        var gate = view.LastIndexOf("@if (", cartaz, StringComparison.Ordinal);
        Assert.True(gate >= 0, "Não achei o `@if` que envolve o cartaz.");
        Assert.Contains("cartazNoTopo", view[gate..cartaz]);
    }

    [Fact]
    public void A_LINHA_do_topo_nao_fica_vazia_quando_o_cartaz_sai()
    {
        // ⚠️ O `@if` de FORA decide se a linha de botões existe. Sem somar a mesma condição ali,
        // um torneio com a chave publicada e sem convite nem fotos desenharia um `d-flex` vazio
        // — uma faixa de margem sem nada dentro, que ninguém entende de onde veio.
        var view = Ler("Views", "Torneios", "Details.cshtml");

        var linha = view.IndexOf("cartazNoTopo || podeConvidar", StringComparison.Ordinal);
        Assert.True(linha >= 0, "A condição da LINHA precisa usar a mesma régua do cartaz.");
    }

    [Fact]
    public void Com_a_chave_no_ar_o_cartaz_mora_nas_FERRAMENTAS_DO_ORGANIZADOR()
    {
        var card = Ler("Views", "Torneios", "_FerramentasDoOrganizador.cshtml");

        Assert.Contains("Cartaz pra divulgar", card);

        // ⚠️ Gateado pelo MESMO predicado do topo, e por isso os dois nunca aparecem juntos: o
        // card também é desenhado no fim do Painel de Controle ANTES da publicação, e sem isto o
        // organizador veria o cartaz duas vezes na mesma página.
        Assert.Contains("ChavePublicada", card);
        Assert.Contains("PodeDivulgar", card);
    }

    private static string Ler(params string[] caminho) =>
        File.ReadAllText(Path.Combine(new[] { PastaDoProjeto() }.Concat(caminho).ToArray()));

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
