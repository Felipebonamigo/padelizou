using Xunit;

namespace Padelizou.Tests;

// 04/09/2026 — O BOTÃO "CONVIDAR" NA PÁGINA DO TORNEIO, pedido do Felipe num print com a área
// ao lado do "Cartaz pra divulgar" marcada de verde: "crie um botão 'convidar' e que é um botão
// que direciona o link para a pessoa se inscrever no torneio".
//
// ⚠️ POR QUE TESTE DE FONTE: a suíte não renderiza Razor. Se o botão sumir da view, se o link
// perder o `#inscricao`, ou se o script que abre a aba for apagado, NENHUM teste de
// comportamento fica vermelho — o `ConviteProTorneio` continuaria passando sozinho, feliz,
// testando um texto que a tela não mostra mais.
//
// A REGRA e o TEXTO têm trava de comportamento de verdade em `ConviteProTorneioTests`. Este
// arquivo trava só o que é exclusivo da view: que o botão existe, que ele usa o serviço em vez
// de uma cópia da regra, e que a hash vira aba aberta.
public class ConvidarNaPaginaDoTorneioTests
{
    [Fact]
    public void A_pagina_do_torneio_tem_o_botao_convidar()
    {
        var fonte = Details();

        Assert.Contains("Convidar", fonte);
        Assert.Contains("id=\"modalConvidarPraOTorneio\"", fonte);
    }

    // ⚠️ O CORAÇÃO DO PEDIDO: "direciona o link para a pessoa SE INSCREVER". Sem o `#inscricao`
    // o convidado cai na aba "Inscritos" — a lista de quem JÁ entrou, a única aba da página que
    // não serve pra se inscrever. O convite prometeria inscrição e entregaria plateia.
    [Fact]
    public void O_link_do_convite_aponta_pra_aba_de_inscricao()
    {
        var bloco = BlocoDoConvite();

        Assert.Contains("\"#inscricao\"", bloco);
        Assert.Contains("Url.Action(\"Details\", \"Torneios\"", bloco);
    }

    // Absoluto, mesmo motivo do QR do cartaz (`CartoesController.CartazImagem`): o link viaja
    // pro WhatsApp de outra pessoa, e um caminho relativo colado lá não leva a lugar nenhum.
    [Fact]
    public void O_link_do_convite_e_absoluto()
    {
        var bloco = BlocoDoConvite();

        Assert.Contains("protocol: Context.Request.Scheme", bloco);
    }

    // Os dois caminhos: o zap cobre a maioria, e o link cru cobre quem cola no Instagram, no
    // e-mail ou num grupo que não é do WhatsApp. Só o botão verde deixaria essas pessoas sem
    // saída — é a mesma lição já escrita no convite da panelinha.
    [Fact]
    public void O_convite_sai_pelo_whatsapp_e_tambem_como_link_cru()
    {
        var bloco = BlocoDoConvite();

        Assert.Contains("https://wa.me/?text=", bloco);
        Assert.Contains("linkDoConviteDoTorneio", bloco);
    }

    // A view NÃO escreve o texto do convite à mão. Se escrevesse, viraria a segunda cópia de
    // uma regra que já tem dono — e os testes do `ConviteProTorneio` estariam travando um
    // texto que ninguém manda.
    [Fact]
    public void O_texto_do_convite_vem_do_servico()
    {
        var bloco = BlocoDoConvite();

        Assert.Contains("ConviteProTorneio.Texto(", bloco);
    }

    // Idem pra regra de QUANDO o botão existe: quem responde é o serviço, não um
    // `Model.Status == "..."` solto na view. Uma cópia aqui divergiria no dia em que o nome do
    // status mudasse — e o nome do status deste projeto já é histórico e já enganou gente.
    [Fact]
    public void A_regra_de_quando_convidar_vem_do_servico()
    {
        var fonte = Details();

        Assert.Contains("ConviteProTorneio.PodeConvidar(Model.Status)", fonte);
    }

    // ⚠️ SEM ESTE SCRIPT O `#inscricao` NÃO FAZ NADA. O Bootstrap 5 não lê a URL pra escolher
    // aba; a hash sozinha é decoração, e o convidado continua caindo em "Inscritos".
    [Fact]
    public void A_hash_abre_a_aba_de_inscricao()
    {
        var fonte = Details();

        Assert.Contains("location.hash !== '#inscricao'", fonte);
        Assert.Contains("#torneioTabs [data-bs-target=\"#inscricao\"]", fonte);
        Assert.Contains("bootstrap.Tab.getOrCreateInstance(aba).show()", fonte);
    }

    // O cartaz continua existindo. O convite foi somado a ele, não pôs no lugar dele: são duas
    // divulgações diferentes — a aberta (story, mural) e a dirigida (privado).
    [Fact]
    public void O_cartaz_continua_na_pagina()
    {
        var fonte = Details();

        Assert.Contains("Cartaz pra divulgar", fonte);
    }

    // ── A BARRA DE AÇÕES DO ORGANIZADOR NÃO PODE VAZAR NO CELULAR ────────────────────
    //
    // Print do Felipe (04/09/2026): "ta estourando o limite da tela", na lista Gerenciar
    // Inscritos. A linha de quem está SEM PARCEIRO ganha quatro botões — "Marcar como pago",
    // "Convidar por link", "Definir por CPF" e o de remover — e a barra era `flex-nowrap` com
    // `flex-shrink: 0`: não quebrava nem encolhia. Medido no Chromium a 390px, ocupava 468px,
    // com o "Definir por CPF" saindo pela borda direita.
    //
    // ⚠️ DEFEITO ANTERIOR AO BOTÃO "CONVIDAR" — o markup é de 17/08 (e093c71). Passou
    // despercebido porque a linha da dupla COMPLETA tem três botões e cabia.
    [Fact]
    public void A_barra_de_acoes_do_inscrito_quebra_no_celular()
    {
        var fonte = Details();

        // `flex-wrap` até 992px, `flex-lg-nowrap` do desktop pra cima (lá a barra vira coluna
        // alinhada entre as 25 linhas, e quebrar estragaria esse alinhamento).
        Assert.Contains("d-flex gap-2 flex-wrap flex-lg-nowrap ms-lg-auto pdz-inscrito-acoes", fonte);

        // E o `flex-shrink: 0` fica restrito ao desktop: no celular a barra já ocupa a largura
        // inteira, então ele não protegia de nada e ainda segurava o quarto botão fora da tela.
        var css = fonte[fonte.IndexOf("A barra de ações não encolhe", StringComparison.Ordinal)..];
        var media = css.IndexOf("@@media (min-width: 992px)", StringComparison.Ordinal);
        var regra = css.IndexOf(".pdz-inscrito-acoes { flex-shrink: 0; }", StringComparison.Ordinal);
        Assert.True(media >= 0, "O `flex-shrink: 0` da barra precisa estar dentro de um @@media de desktop.");
        Assert.True(media < regra, "A regra `flex-shrink: 0` ficou FORA do @@media — ela volta a valer no celular.");
    }

    // A âncora é o `id` do modal, e não a palavra "Convidar": "Convidar" aparece em mais de um
    // lugar desta página (o convite de parceiro tem o dele), e uma âncora genérica fatiaria o
    // bloco errado — foi exatamente esse erro que já custou uma rodada nas travas de fonte
    // deste projeto.
    private static string BlocoDoConvite()
    {
        var fonte = Details();

        var inicio = fonte.IndexOf("id=\"modalConvidarPraOTorneio\"", StringComparison.Ordinal);
        Assert.True(inicio >= 0,
            "Não achei o modal de convite pro torneio (id=\"modalConvidarPraOTorneio\") no Details. "
            + "Ele foi renomeado ou removido, e esta trava parou de olhar pra ele.");

        var fim = fonte.IndexOf("copiarConviteDoTorneio(botao)", inicio, StringComparison.Ordinal);
        Assert.True(fim > inicio, "Não achei o fim do bloco do convite (a função de copiar vem depois dele).");

        // As variáveis do link são declaradas ANTES do modal, no mesmo `@if`. Volta um pedaço
        // pra que elas caiam dentro da fatia — sem isso o teste do `Url.Action` procuraria o
        // link numa fatia que começa depois de ele ter sido montado.
        var comOPreambulo = Math.Max(0, inicio - 600);
        return fonte[comOPreambulo..fim];
    }

    private static string Details() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Details.cshtml"));

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
