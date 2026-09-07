using System.Linq;
using System.Text.RegularExpressions;
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
//
// 🔁 REVISTO NO MESMO DIA: a aba nasceu gateada por `PodeVerDinheiro`, o que escondia o
// IMPEDIMENTO (que não é dinheiro) de quem ajuda a organizar sem ver o caixa — pedido do
// Felipe: "a parte de impedimentos do torneio tem que aparecer pro organizador". O servidor
// já aceitava isso (`AlternarPagamentoDupla` e `AlterarImpedimentoOrganizador` conferem
// `EhOrganizadorAsync`, não `PodeVerDinheiro` — ver TorneiosController.Inscricoes.cs); só a
// VIEW escondia mais do que o servidor permitia. Mesma régua já usada em Financeiro.cshtml
// pro ajudante: o VALOR e o link de "Cobrar" (que carrega a quantia e a chave Pix) ficam
// atrás de `PodeVerDinheiro`; o status "Pago"/"Não pago" em palavra, o botão de marcar como
// pago e o impedimento (ver/editar) não.
public class AbaPagamentosNaPaginaDoTorneioTests
{
    [Fact]
    public void A_aba_existe_e_e_gateada_por_PodeGerenciar()
    {
        var fonte = Details();

        var inicioNav = fonte.IndexOf("id=\"pagamentos-tab\"", StringComparison.Ordinal);
        Assert.True(inicioNav >= 0, "Não achei o botão da aba Pagamentos (id=\"pagamentos-tab\").");

        // ⚠️ Ancorado no comentário "Aba PAGAMENTOS", não só em "vem antes de inicioNav" — a
        // aba "Gerenciar Torneio", uma acima, também usa `ViewBag.PodeGerenciar == true`, e um
        // LastIndexOf sem esse recorte pegaria o `@if` DELA, aprovando o teste mesmo se o
        // botão de Pagamentos continuasse atrás de outro gate qualquer (foi falsificado: com o
        // gate ainda em `PodeVerDinheiro`, este teste passava do jeito errado).
        var inicioBloco = fonte.LastIndexOf("Aba PAGAMENTOS", inicioNav, StringComparison.Ordinal);
        Assert.True(inicioBloco >= 0, "Não achei o comentário \"Aba PAGAMENTOS\" que antecede o botão.");

        var gate = fonte.IndexOf("ViewBag.PodeGerenciar == true", inicioBloco, StringComparison.Ordinal);
        Assert.True(gate >= 0 && gate < inicioNav,
            "O botão da aba Pagamentos precisa estar atrás de um `ViewBag.PodeGerenciar == true`.");
    }

    [Fact]
    public void O_painel_da_aba_tambem_e_gateado_por_PodeGerenciar()
    {
        var bloco = BlocoDoPainel();

        // O próprio `@if` que abre o `<div id="pagamentos">` está DENTRO da fatia que
        // começamos a cortar a partir do gate — ver BlocoDoPainel.
        Assert.Contains("id=\"pagamentos\"", bloco);
    }

    // O CORAÇÃO DO PEDIDO (segunda rodada): quem só ajuda a organizar — sem ver dinheiro —
    // continua enxergando e editando impedimento. O botão/formulário de impedimento não pode
    // estar DENTRO de um `if (ViewBag.PodeVerDinheiro ...) { ... }` — e a aba tem vários
    // desses blocos ANTES dele (o aviso pro ajudante, "Cobrar todos", o link "Cobrar"
    // individual), então não basta checar se a palavra aparece antes: tem que checar se,
    // até ali, todo `{` já fechou. Uma chave aberta sobrando é o sinal de que o alvo ainda
    // está dentro de um bloco que devia ter fechado antes dele.
    [Fact]
    public void O_botao_de_impedimento_nao_fica_dentro_de_um_bloco_de_dinheiro()
    {
        var linha = LinhaDeExemplo();

        var alvo = linha.IndexOf("data-bs-target=\"#impedimento-", StringComparison.Ordinal);
        Assert.True(alvo >= 0, "Não achei o botão que abre o impedimento (data-bs-target=\"#impedimento-...\").");

        AssertChavesFechadasAte(linha, alvo, "o botão de Impedimento");
    }

    [Fact]
    public void O_formulario_de_editar_impedimento_nao_fica_dentro_de_um_bloco_de_dinheiro()
    {
        var linha = LinhaDeExemplo();

        var alvo = linha.IndexOf("AlterarImpedimentoOrganizador", StringComparison.Ordinal);
        Assert.True(alvo >= 0, "Não achei o formulário de editar impedimento dentro da linha da dupla.");

        AssertChavesFechadasAte(linha, alvo, "o formulário de editar impedimento");
    }

    // Um `<li>...</li>` sozinho (uma dupla) não tem `foreach`/`else` — só os `@if` da própria
    // linha (badge de pago, link de cobrar, marcar pago, badge/form de impedimento). Contar
    // chaves é seguro aqui porque não sobra nenhum laço estrutural aberto, ao contrário do
    // painel inteiro (que tem o `else`/dois `foreach` em volta de toda dupla).
    //
    // ⚠️ FALSIFICADO E CORRIGIDO: a primeira versão contava chaves no texto CRU, e um `}` de
    // exemplo dentro de um comentário Razor (`@* ... } ... *@`) — coisa que este mesmo arquivo
    // de teste tem no comentário logo abaixo — compensava a chave que o bug real deixava
    // aberta, aprovando o teste mesmo com o botão de impedimento de fato aninhado dentro do
    // `if` de dinheiro. Os comentários são removidos ANTES de contar.
    private static string LinhaDeExemplo()
    {
        var bloco = BlocoDoPainel();
        var inicio = bloco.IndexOf("<li class=\"list-group-item bg-transparent px-0\">", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "Não achei nenhuma linha de dupla (<li class=\"list-group-item...\">) no painel.");
        var fim = bloco.IndexOf("</li>", inicio, StringComparison.Ordinal);
        Assert.True(fim > inicio, "Não achei o fim da linha de dupla (</li>).");
        var linha = bloco[inicio..fim];
        return Regex.Replace(linha, "@\\*.*?\\*@", "", RegexOptions.Singleline);
    }

    // Falsificado: aninhando de propósito o botão de Impedimento dentro do `if` do link
    // "Cobrar" (ou seja, cortando o `}` que fecha aquele bloco antes do botão) faz esta
    // checagem acusar chave aberta sobrando — é exatamente o bug que o pedido do Felipe
    // apontou (o servidor já aceitava, só a tela escondia mais que o servidor permitia).
    private static void AssertChavesFechadasAte(string linha, int indice, string doQue)
    {
        var trecho = linha[..indice];
        var abertas = trecho.Count(c => c == '{');
        var fechadas = trecho.Count(c => c == '}');
        Assert.True(abertas == fechadas,
            $"{doQue} parece estar dentro de um bloco Razor ainda aberto " +
            $"({abertas} chaves abertas, {fechadas} fechadas até ali) — provavelmente dentro " +
            "de um `if` de dinheiro que não fechou antes.");
    }

    // Marcar como pago é trabalho de quem ajuda a organizar (mesma régua do Financeiro.cshtml
    // pro ajudante — ver AcessoAoDinheiroDoTorneio) — não pode depender de ver dinheiro.
    [Fact]
    public void Marcar_como_pago_nao_depende_de_PodeVerDinheiro()
    {
        var bloco = BlocoDoPainel();

        var inicioForm = bloco.IndexOf("AlternarPagamentoDupla", StringComparison.Ordinal);
        Assert.True(inicioForm >= 0, "Não achei o formulário de marcar como pago dentro do painel.");

        var linhaAntesDoForm = bloco.LastIndexOf('\n', inicioForm);
        var trechoAntes = bloco[..linhaAntesDoForm];
        var ultimoIfDeDinheiro = trechoAntes.LastIndexOf("PodeVerDinheiro == true", StringComparison.Ordinal);
        var ultimoIfDePreco = trechoAntes.LastIndexOf("Model.PrecoInscricao > 0", StringComparison.Ordinal);
        Assert.True(ultimoIfDePreco > ultimoIfDeDinheiro,
            "O botão de marcar como pago não pode estar atrás do gate de PodeVerDinheiro (só do preço da inscrição).");
    }

    // O link "Cobrar" de cada linha carrega o VALOR e a chave Pix na mensagem — fica com quem
    // vê dinheiro, mesma régua do link individual em Financeiro.cshtml.
    [Fact]
    public void O_link_de_cobrar_individual_e_gateado_por_PodeVerDinheiro()
    {
        var bloco = BlocoDoPainel();

        // ⚠️ "pdz-cobrar-link" sozinho, sem a aspa, também bate no COMENTÁRIO que menciona
        // ".pdz-cobrar-link" mais acima no mesmo painel — ancorado com a aspa de fechamento
        // da classe CSS pra pegar o uso de verdade, não a prosa.
        var inicioLink = bloco.IndexOf("pdz-cobrar-link\"", StringComparison.Ordinal);
        Assert.True(inicioLink >= 0, "Não achei o link individual de cobrar (classe pdz-cobrar-link).");

        var gate = bloco.LastIndexOf("PodeVerDinheiro == true", inicioLink, StringComparison.Ordinal);
        Assert.True(gate >= 0, "O link de cobrar individual precisa estar atrás de `ViewBag.PodeVerDinheiro == true`.");
    }

    // "Cobrar todos" dispara a MESMA mensagem com valor e Pix — mesmo gate do link individual.
    [Fact]
    public void O_botao_cobrar_todos_e_gateado_por_PodeVerDinheiro()
    {
        var bloco = BlocoDoPainel();

        var inicioBotao = bloco.IndexOf("id=\"pdzCobrarTodos\"", StringComparison.Ordinal);
        Assert.True(inicioBotao >= 0, "Não achei o botão \"Cobrar todos\".");

        var gate = bloco.LastIndexOf("PodeVerDinheiro == true", inicioBotao, StringComparison.Ordinal);
        Assert.True(gate >= 0, "O botão \"Cobrar todos\" precisa estar atrás de `ViewBag.PodeVerDinheiro == true`.");
    }

    // Quem só pode OLHAR a gestão (assistente do sistema, sem ser organizador de verdade DESTE
    // torneio) vê o painel, mas não grava nada — mesmo `<fieldset disabled>` já usado no Painel
    // de Controle (ver Details.cshtml, aba "admin"): o servidor é quem recusa de verdade
    // (`AlternarPagamentoDupla`/`AlterarImpedimentoOrganizador` conferem `EhOrganizadorAsync`),
    // isto é só a tela não prometer o que o servidor não vai aceitar.
    [Fact]
    public void Formularios_do_painel_ficam_desligados_para_quem_so_pode_olhar()
    {
        var bloco = BlocoDoPainel();

        Assert.Contains("fieldset disabled=\"@gestaoSoLeitura\"", bloco);
        Assert.Contains("ViewBag.GestaoSoLeitura == true", bloco);
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
