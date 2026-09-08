using Xunit;

namespace Padelizou.Tests;

// O "COBRAR TODOS" PROMETIA MAIS DO QUE FAZ (08/09/2026).
//
// Pergunta do Felipe, olhando a tela: *"o que esse botão faz? ele envia pro whats? pq dá a
// impressão que sim, mas parece que não está enviando"*.
//
// Ele nunca enviou — abre a conversa de cada um no WhatsApp DELE, com o texto pronto, e quem
// aperta enviar é ele. Isso é decisão, não limitação: mandar 54 mensagens automáticas do mesmo
// número queimaria o chip, como aconteceu em 03/08/2026 quando a Meta restringiu o número
// depois de uma rajada de avisos.
//
// O defeito era a TELA, em três pontos:
//
//   1. "Cobrar todos (54)" lê como disparo em massa. O nome precisa dizer que abre UMA.
//   2. Pop-up bloqueado falhava calado — o organizador clicava e não acontecia nada, sem
//      nenhuma pista de por quê. Foi exatamente essa a dúvida dele.
//   3. Recarregar a página zerava a contagem, e não havia como saber quem já tinha sido
//      cobrado — na segunda passada ele recomeçaria do primeiro.
//
// ⚠️ POR QUE TESTE DE FONTE: a suíte não roda JavaScript nem renderiza Razor. Estes testes
// seguram que a mudança ESTÁ no arquivo; o comportamento em si foi conferido no navegador.
// É a mesma régua já assumida no AbaPagamentosNaPaginaDoTorneioTests.
public class CobrarTodosHonestoTests
{
    [Fact]
    public void O_botao_nao_promete_envio_em_massa()
    {
        var bloco = Painel();

        // ⚠️ O texto antigo era "Cobrar todos (N)". Ele sugere que N mensagens saem de uma vez,
        // que é justamente o que NÃO acontece — e foi o que fez o organizador achar que estava
        // quebrado quando só uma conversa abriu.
        Assert.DoesNotContain("Cobrar todos (", bloco);
        Assert.Contains("Abrir cobrança", bloco);
    }

    [Fact]
    public void A_tela_diz_que_quem_aperta_enviar_e_o_organizador()
    {
        // A explicação de uma linha embaixo do título. Sem ela, "abrir" e "enviar" viram a
        // mesma coisa na cabeça de quem clica.
        Assert.Contains("você aperta enviar", Painel());
    }

    [Fact]
    public void O_script_avisa_quando_o_navegador_bloqueia_o_popup()
    {
        var js = Script();

        // `window.open` devolve null quando o pop-up é bloqueado. Sem olhar o retorno, o
        // clique some sem deixar rastro — que era o sintoma relatado.
        Assert.Contains("pdzCobrarAviso", js);
        Assert.Contains("bloqueado", js);
    }

    [Fact]
    public void Popup_bloqueado_NAO_conta_como_cobrado()
    {
        var js = Script();

        // ⚠️ A regra que faz o aviso valer alguma coisa: se a aba não abriu, a pessoa não foi
        // cobrada, então a fila não pode andar. Avançar aqui pularia alguém em silêncio — pior
        // que o defeito original, porque o organizador acharia que cobrou.
        var abriu = js.IndexOf("var aba = window.open", System.StringComparison.Ordinal);
        Assert.True(abriu >= 0, "Não achei a captura do retorno do window.open.");

        var guarda = js.IndexOf("if (!aba)", abriu, System.StringComparison.Ordinal);
        var marca = js.IndexOf("cobrados.push", abriu, System.StringComparison.Ordinal);

        Assert.True(guarda >= 0, "Não achei a guarda do pop-up bloqueado.");
        Assert.True(marca >= 0, "Não achei onde a pessoa é marcada como cobrada.");
        Assert.True(guarda < marca, "A pessoa é marcada como cobrada ANTES de conferir se a aba abriu.");
    }

    [Fact]
    public void Quem_ja_foi_cobrado_e_lembrado_entre_recarregamentos()
    {
        var js = Script();

        // localStorage, e não servidor: é conveniência de quem está com a tela aberta, não
        // estado compartilhado do torneio. Ninguém mais precisa saber disso.
        Assert.Contains("localStorage", js);

        // A memória é POR TORNEIO. Uma chave só faria a cobrança de um torneio marcar como
        // cobrado o inscrito de outro.
        Assert.Contains("pdz-cobrados-", js);
    }

    [Fact]
    public void Da_pra_recomecar_a_cobranca()
    {
        // ⚠️ SEM ISTO A MEMÓRIA VIRA ARMADILHA. Segunda leva de cobrança — dias depois, com
        // metade ainda devendo — encontraria o botão desabilitado dizendo "todo mundo foi
        // cobrado", e não haveria caminho de volta pela tela.
        Assert.Contains("pdzCobrarRecomecar", Painel());
        Assert.Contains("pdzCobrarRecomecar", Script());
    }

    [Fact]
    public void Cada_linha_leva_a_identidade_da_dupla()
    {
        // É o que a memória guarda. Guardar a POSIÇÃO na lista não serviria: alguém paga, some
        // da lista, e as posições seguintes andam — a memória passaria a apontar pra outra
        // pessoa.
        Assert.Contains("data-pdz-dupla=", Painel());
        Assert.Contains("data-pdz-dupla", Script());
    }

    private static string Painel()
    {
        var fonte = Details();

        var inicio = fonte.IndexOf("id=\"pdzCobrarTodos\"", System.StringComparison.Ordinal);
        Assert.True(inicio >= 0, "Não achei o botão de cobrar em lote na página do torneio.");

        var inicioPainel = fonte.LastIndexOf("id=\"pagamentos\" role=\"tabpanel\"", inicio, System.StringComparison.Ordinal);
        var fim = fonte.IndexOf("<!-- ABA: JOGOS", inicioPainel, System.StringComparison.Ordinal);

        return fonte[inicioPainel..fim];
    }

    private static string Details() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Details.cshtml"));

    private static string Script() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "wwwroot", "js", "cobrar-todos.js"));

    // Mesma busca do AbaPagamentosNaPaginaDoTorneioTests: sobe do bin ate achar a pasta do
    // projeto web. O alvo e "Padelizou/Views" — a raiz do repo nao tem "Views" solto.
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
            "Nao achei a pasta do projeto web subindo a partir de " + AppContext.BaseDirectory);
    }
}
