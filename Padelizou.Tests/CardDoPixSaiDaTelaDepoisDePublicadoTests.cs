using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 10/09/2026 — O CARD DO PIX SAI DA TELA DEPOIS QUE AS CHAVES SÃO PUBLICADAS.
//
// 🗣️ Felipe, com o print da página do torneio no celular: *"acho que podemos remover a parte de
// Pix do organizador quando o torneio já foi publicado, teoricamente já pagaram, e aí fica melhor
// a visão da tela, porque atualmente, quando abro o site a primeira coisa que queria ver é os
// jogos ao vivo"*.
//
// O card já RECOLHIA pra quem tinha pago (10/09, pedido do Emerson). Agora ele SAI: no dia do
// jogo a tela é sobre jogo, e um bloco de cobrança no topo é rolagem entre a pessoa e o placar.
//
// ⚠️ MAS "TEORICAMENTE JÁ PAGARAM" NÃO É "TODOS PAGARAM", E ESSA DIFERENÇA TEM DONO NO CÓDIGO.
// Quem ainda deve continua vendo o card depois de publicado — e não é caso de canto:
// `PromoverDaListaDeEsperaAsync` (TorneiosController.Inscricoes.cs:1127) tira o próximo da fila
// da espera sempre que uma vaga abre, INCLUSIVE quando o organizador remove uma dupla no dia do
// jogo. Essa pessoa entra num torneio já publicado, devendo. No "por fora" o card é o único
// caminho que ELA alcança sozinha pra chave Pix e pro WhatsApp de quem recebe (o outro é o
// botão "Cobrar no WhatsApp" do painel de inscritos, e esse depende de o organizador clicar) —
// tirá-lo dela seria dizer "pague" sem dizer para quem.
public class CardDoPixSaiDaTelaDepoisDePublicadoTests
{
    private static Torneio PorFora(string status) => new()
    {
        Status = status,
        FormaPagamento = FormaDePagamentoDoTorneio.Externo,
        ChavePixOrganizador = "51999999999",
        PrecoInscricao = 150m,
    };

    [Theory]
    [InlineData("Inscrições Abertas")]
    [InlineData("Chaves em Sorteio")]
    [InlineData("Chaves em Aprovação")]
    public void Antes_de_publicar_o_card_aparece_pra_todo_mundo(string status)
    {
        // Antes das chaves saírem o torneio é sobre INSCRIÇÃO: quem chega precisa do valor e da
        // chave, tenha ou não inscrição. Nada muda pra esses status.
        Assert.True(PixDoOrganizador.ApareceParaMim(PorFora(status), devoAlguma: false));
        Assert.True(PixDoOrganizador.ApareceParaMim(PorFora(status), devoAlguma: true));
    }

    [Theory]
    [InlineData("Fase de Grupos")]
    [InlineData("Mata-Mata")]
    [InlineData("Finalizado")]
    public void Depois_de_publicado_o_card_some_de_quem_nao_deve_nada(string status)
    {
        Assert.False(PixDoOrganizador.ApareceParaMim(PorFora(status), devoAlguma: false));
    }

    [Theory]
    [InlineData("Fase de Grupos")]
    [InlineData("Mata-Mata")]
    [InlineData("Finalizado")]
    public void Mas_continua_pra_quem_ainda_deve(string status)
    {
        // Quem foi promovido da lista de espera no dia do jogo cai exatamente aqui.
        Assert.True(PixDoOrganizador.ApareceParaMim(PorFora(status), devoAlguma: true));
    }

    [Fact]
    public void Visitante_deslogado_depois_de_publicado_nao_ve_o_card()
    {
        // O controller NÃO vai ao banco por quem não está logado — usa `default`, e é este valor
        // que decide o card dele. Trocar o `DevoAlguma` por um "não sei, então mostra"
        // (`Total == 0 || NaoPagas > 0`, leitura plausível de "lado seguro") devolveria o card
        // pra arquibancada inteira no dia do jogo COM A SUÍTE VERDE: nenhum outro caso aqui
        // passa por `default`, todos entregam o `bool` na mão.
        var naoLogado = default(PixDoOrganizador.MinhasInscricoes);

        Assert.False(naoLogado.DevoAlguma);
        Assert.False(naoLogado.JaPagueiTudo);
        Assert.False(PixDoOrganizador.ApareceParaMim(PorFora("Fase de Grupos"), naoLogado.DevoAlguma));

        // E ANTES de publicar ele continua vendo: é exatamente quem ainda vai se inscrever.
        Assert.True(PixDoOrganizador.ApareceParaMim(PorFora("Inscrições Abertas"), naoLogado.DevoAlguma));
    }

    [Fact]
    public void Torneio_que_cobra_pelo_site_nunca_mostra_o_card_nem_pra_quem_deve()
    {
        // A régua estrutural (`Aparece`) continua mandando: nas formas pelo site o jogador paga
        // no checkout, e uma chave Pix na tela seria um segundo caminho pro mesmo dinheiro.
        var peloSite = PorFora("Inscrições Abertas");
        peloSite.FormaPagamento = FormaDePagamentoDoTorneio.SoPix;

        Assert.False(PixDoOrganizador.ApareceParaMim(peloSite, devoAlguma: true));
    }

    [Fact]
    public void Sem_chave_cadastrada_tambem_nao()
    {
        var semChave = PorFora("Inscrições Abertas");
        semChave.ChavePixOrganizador = null;

        Assert.False(PixDoOrganizador.ApareceParaMim(semChave, devoAlguma: true));
    }

    [Fact]
    public void A_regua_de_publicado_e_a_MESMA_do_resto_da_pagina()
    {
        // Não pode nascer uma quinta formulação de "as chaves já saíram": `ChavePublicada` já
        // decide a aba "Chaves e Grupos" e o card de ferramentas do organizador no topo.
        var fonte = File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "Services", "PixDoOrganizador.cs"));

        Assert.Contains("AprovacaoDeChaves.ChavePublicada", fonte);
        Assert.DoesNotContain("\"Fase de Grupos\"", fonte);
    }

    [Fact]
    public void A_TELA_pergunta_pela_regra_nova_e_nao_pela_estrutural()
    {
        // Se a view continuasse chamando `Aparece` direto, o card voltaria pra tela do dia de
        // jogo e o pedido morreria em silêncio — com os testes de comportamento verdes.
        var view = File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "Views", "Torneios", "Details.cshtml"));

        Assert.Contains("PixDoOrganizador.ApareceParaMim", view);

        var card = view.IndexOf("Pix do organizador", StringComparison.Ordinal);
        var gate = view.LastIndexOf("PixDoOrganizador.ApareceParaMim", card, StringComparison.Ordinal);
        Assert.True(gate >= 0 && card - gate < 2500,
            "O `@if` que abre o card do Pix precisa ser o `ApareceParaMim`.");
    }

    [Fact]
    public void O_CONTROLLER_para_de_buscar_o_contato_quando_o_card_nao_vai_aparecer()
    {
        // 🕳️ O `Aparece` mora no serviço justamente pra não existirem DUAS réguas — o comentário
        // dele diz isso: "esta condição também guarda a consulta do contato no controller". Com
        // a view passando a perguntar `ApareceParaMim` e o controller continuando no `Aparece`,
        // as duas voltaram a discordar: no dia do jogo, quem já pagou carregava do banco o
        // criador do torneio pra alimentar um card que a view não desenha — uma consulta a mais
        // em toda abertura da página mais pesada do site, no momento em que mais gente a abre.
        var controller = File.ReadAllText(
            Path.Combine(RaizDoRepo(), "Padelizou", "Controllers", "TorneiosController.cs"));

        var busca = controller.IndexOf("PixDoOrganizador.QuemRecebeOComprovanteAsync", StringComparison.Ordinal);
        Assert.True(busca >= 0, "O controller precisa buscar quem recebe o comprovante.");

        var portao = controller.LastIndexOf("PixDoOrganizador.ApareceParaMim", busca, StringComparison.Ordinal);
        Assert.True(portao >= 0 && busca - portao < 500,
            "A busca do contato precisa estar atrás do MESMO `ApareceParaMim` que a view usa.");
    }

    private static string RaizDoRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Padelizou", "wwwroot", "css", "site.css")))
            dir = dir.Parent;
        Assert.True(dir != null, "Não achei a raiz do repo.");
        return dir!.FullName;
    }
}
