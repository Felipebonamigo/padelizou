using Xunit;

namespace Padelizou.Tests;

// 10/09/2026 — O RANKING DO PALPITÔMETRO ANTES DO PRIMEIRO JOGO. 🗣️ Felipe, com o 2ª Etapa
// ER PADEL TOUR no ar e 41 jogos já votados: *"acho que o ranking do palpitometro ja tem que
// aparecer"* — e, sobre o palpite que ainda não virou ponto, *"todo jogo pode ser palpitado
// até começar"*.
//
// ⚠️ POR QUE TESTE DE FONTE: a suíte não renderiza Razor. A REGRA (quem entra na lista, o que
// é "em aberto", a ordem) tem trava de comportamento de verdade em PalpiteirosDoTorneioTests;
// o que só existe na VIEW — a coluna, a ausência de posição e o aviso de que a pontuação
// ainda não começou — não tem como um teste de comportamento alcançar.
public class RankingEmAbertoNaTelaTests
{
    [Fact]
    public void A_tabela_tem_a_coluna_EM_ABERTO_e_ela_e_gateada_pelo_dado()
    {
        var tabela = Ler("Views", "Shared", "_TabelaDePalpiteiros.cshtml");

        Assert.Contains("Em aberto", tabela);

        // ⚠️ Some por DADO, nunca por interruptor — mesma régua da coluna "Cravadas": num
        // recorte sem palpite pendente (o hub, um torneio acabado) ela seria uma fileira de
        // zeros ocupando a largura que o celular não tem.
        Assert.Contains("MostrarEmAberto", tabela);
    }

    [Fact]
    public void Sem_ponto_nenhum_a_tabela_nao_inventa_POSICAO()
    {
        var tabela = Ler("Views", "Shared", "_TabelaDePalpiteiros.cshtml");

        // Posicao == 0 é o "ainda não há classificação" que o serviço escreve enquanto nenhum
        // jogo foi apurado. Um "1º" numa tabela de zeros anuncia liderança que não existe.
        Assert.Contains("Posicao == 0", tabela);
    }

    [Fact]
    public void Antes_do_primeiro_resultado_a_tabela_nao_desenha_FILEIRA_DE_ZEROS()
    {
        var tabela = Ler("Views", "Shared", "_TabelaDePalpiteiros.cshtml");

        // ⚠️ Visto no navegador (10/09/2026, 1200px): sem isto a tabela do modo participação
        // mostrava PALPITES 0 · ACERTOS 0 · % 0% em toda linha. É a mesma régua da coluna
        // "Cravadas" — coluna que só sabe dizer zero explica um jeito de pontuar que ainda não
        // aconteceu ali, e faz a conta parecer quebrada.
        Assert.Contains("MostrarApuracao", tabela);

        var ranking = Ler("Views", "Shared", "_RankingDePalpiteiros.cshtml");
        Assert.Contains("!Model.ModoParticipacao", ranking);
    }

    [Fact]
    public void O_ranking_avisa_que_a_pontuacao_ainda_NAO_comecou()
    {
        var ranking = Ler("Views", "Shared", "_RankingDePalpiteiros.cshtml");

        Assert.Contains("ModoParticipacao", ranking);
        // A frase tem que dizer QUANDO começa — "ainda não há pontos" sozinho parece defeito.
        Assert.Contains("primeiro jogo", ranking);
    }

    [Fact]
    public void A_PAGINA_do_torneio_nao_promete_ACERTO_antes_de_existir_jogo_apurado()
    {
        // ⚠️ ATUALIZADO, NÃO APAGADO (10/09/2026). Este teste vigiava a legenda de um BOTÃO
        // "Palpiteiros" no topo da página — botão que saiu por ser duplicata da aba (🗣️ Felipe:
        // *"palpiteiros me parece duplicado, não?"*). A INTENÇÃO não saiu junto: "quem mais
        // acertou" é mentira na véspera, quando nenhum jogo terminou.
        //
        // Sem o botão, o que sobra na página do torneio é a ABA ("Palpiteiros", neutra) e o
        // link do painel ("Abrir em página própria"), também neutro. Então a cobrança agora é
        // sobre a PÁGINA inteira: ela não promete acerto em lugar nenhum. Quem decide a frase é
        // o ranking, e ele tem o `O_ranking_avisa_que_a_pontuacao_ainda_NAO_comecou` logo acima.
        //
        // Sem tirar os comentários isto reprovaria a própria explicação de por que o botão saiu.
        var details = TestInfra.SemComentarios(Ler("Views", "Torneios", "Details.cshtml"));

        Assert.DoesNotContain("acertou", details, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ModoParticipacao", Ler("Views", "Shared", "_RankingDePalpiteiros.cshtml"));
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
