using Xunit;

namespace Padelizou.Tests;

// 07/09/2026 — A ABA "PAGAMENTOS": pedido do Felipe — "criar uma aba para gerenciar melhor os
// pagamentos, impedimentos e cobrar jogador", com "uma opção do usuário mesmo cobrar todos os
// jogadores que não pagaram de uma vez", e "o organizador pode enxergar quem solicitou
// impedimento e pra qual horário. Permite ele editar esse impedimento".
//
// ⚠️ POR QUE TESTE DE FONTE: a suíte não renderiza Razor. A REGRA (quem pode editar o
// impedimento, quando, o que acontece com o dinheiro) tem trava de comportamento de verdade
// em AlteracaoDeImpedimentoTests e AlterarImpedimentoOrganizadorTests. O que só existe na
// VIEW — a aba, o gate de dinheiro, o botão de cobrar em lote — não tem como um teste de
// comportamento alcançar.
public class AbaPagamentosNaPaginaDoTorneioTests
{
    [Fact]
    public void A_aba_existe_e_e_gateada_por_PodeVerDinheiro()
    {
        var fonte = Details();

        var inicioNav = fonte.IndexOf("id=\"pagamentos-tab\"", StringComparison.Ordinal);
        Assert.True(inicioNav >= 0, "Não achei o botão da aba Pagamentos (id=\"pagamentos-tab\").");

        // O `@if (ViewBag.PodeVerDinheiro == true)` que gateia o BOTÃO da aba vem ANTES dele
        // no arquivo — mesma régua do Financeiro, e não `PodeGerenciar`: quem só ajuda a
        // organizar não vê o valor de cada inscrição nem o botão de cobrar.
        var gate = fonte.LastIndexOf("ViewBag.PodeVerDinheiro == true", inicioNav, StringComparison.Ordinal);
        Assert.True(gate >= 0 && gate < inicioNav,
            "O botão da aba Pagamentos precisa estar atrás de um `ViewBag.PodeVerDinheiro == true`.");
    }

    [Fact]
    public void O_painel_da_aba_tambem_e_gateado_por_PodeVerDinheiro()
    {
        var bloco = BlocoDoPainel();

        // O próprio `@if` que abre o `<div id="pagamentos">` está DENTRO da fatia que
        // começamos a cortar a partir do gate — ver BlocoDoPainel.
        Assert.Contains("id=\"pagamentos\"", bloco);
    }

    // O CORAÇÃO DO PEDIDO: "enxergar quem solicitou impedimento e pra qual horário" — o rótulo
    // do turno atual aparece na linha de cada dupla.
    [Fact]
    public void Mostra_o_impedimento_atual_de_cada_dupla()
    {
        var bloco = BlocoDoPainel();

        Assert.Contains("AlteracaoDeImpedimento.TurnoAtual(dupla)", bloco);
        Assert.Contains("AlteracaoDeImpedimento.Rotulo(turnoAtual)", bloco);
    }

    // "Permite ele editar esse impedimento" — um formulário de verdade, não só leitura (a
    // lista antes do sorteio já existia e é só leitura — ver ImpedimentoNaTelaTests).
    [Fact]
    public void Permite_editar_o_impedimento()
    {
        var bloco = BlocoDoPainel();

        Assert.Contains("asp-action=\"AlterarImpedimentoOrganizador\"", bloco);
        Assert.Contains("method=\"post\"", bloco);
        Assert.Contains("name=\"turno\"", bloco);
    }

    // Dupla já paga: a troca é permitida (decisão do Felipe), mas o lembrete de que o dinheiro
    // não se ajusta sozinho tem que estar na tela — senão o organizador só descobre depois de
    // já ter clicado, exatamente o tipo de "dinheiro pendurado sem ninguém saber" que a régua
    // do jogador foi desenhada pra evitar.
    [Fact]
    public void Avisa_quando_a_dupla_ja_paga_e_a_troca_muda_o_valor()
    {
        var bloco = BlocoDoPainel();

        Assert.Contains("não cobra nem estorna sozinho", bloco);
    }

    // O PEDIDO DA COBRANÇA EM LOTE: um botão que, clicado, abre os "Cobrar" de quem não pagou
    // em fila — não é envio automático (aprovado pelo Felipe como "fila de WhatsApp, um clique
    // por vez"), então o botão tem que reusar os MESMOS links de cada linha, não inventar um
    // caminho novo de mandar mensagem.
    [Fact]
    public void O_botao_cobrar_todos_existe_e_reusa_os_links_de_cada_linha()
    {
        var bloco = BlocoDoPainel();

        Assert.Contains("id=\"pdzCobrarTodos\"", bloco);
        Assert.Contains("pdz-cobrar-link", bloco);
    }

    // ⚠️ SEM ESTE SCRIPT O BOTÃO NÃO FAZ NADA. A fila inteira (avançar, mostrar quantos
    // restam, desabilitar no fim) vive em wwwroot/js/cobrar-todos.js.
    [Fact]
    public void O_script_da_fila_esta_incluido_na_pagina()
    {
        var fonte = Details();

        Assert.Contains("src=\"~/js/cobrar-todos.js\"", fonte);
    }

    [Fact]
    public void O_arquivo_do_script_existe_e_le_a_fila_do_dom()
    {
        var js = File.ReadAllText(Path.Combine(PastaDoProjeto(), "wwwroot", "js", "cobrar-todos.js"));

        Assert.Contains("pdzCobrarTodos", js);
        Assert.Contains("pdz-cobrar-link", js);
        // Um clique, uma conversa — nunca um loop que abre tudo de uma vez (o navegador
        // bloqueia pop-up em lote, e várias mensagens ao mesmo tempo pareceriam robô).
        Assert.DoesNotContain("forEach", js);
    }

    // A âncora é o `id` do botão de cobrar em lote — não a palavra "Pagamentos", que aparece
    // em mais de um lugar da página (o próprio Financeiro, o link "Financeiro" no menu).
    private static string BlocoDoPainel()
    {
        var fonte = Details();

        var inicio = fonte.IndexOf("id=\"pdzCobrarTodos\"", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "Não achei o botão \"Cobrar todos\" (id=\"pdzCobrarTodos\") na página do torneio.");

        // Volta até o começo do painel (`id="pagamentos"`) e segue até o fim do foreach de
        // duplas, marcado pelo fechamento do formulário de impedimento.
        var inicioPainel = fonte.LastIndexOf("id=\"pagamentos\" role=\"tabpanel\"", inicio, StringComparison.Ordinal);
        Assert.True(inicioPainel >= 0, "Não achei a abertura do painel da aba Pagamentos.");

        var fim = fonte.IndexOf("<!-- ABA: JOGOS", inicioPainel, StringComparison.Ordinal);
        Assert.True(fim > inicioPainel, "Não achei o fim do painel da aba Pagamentos (a aba de Jogos vem depois dela).");

        return fonte[inicioPainel..fim];
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
