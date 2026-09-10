using Xunit;

namespace Padelizou.Tests;

// 10/09/2026 — O RANKING DO PALPITRÔMETRO ANTES DO PRIMEIRO JOGO. 🗣️ Felipe, com o 2ª Etapa
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
    public void O_ranking_avisa_que_a_pontuacao_ainda_NAO_comecou()
    {
        var ranking = Ler("Views", "Shared", "_RankingDePalpiteiros.cshtml");

        Assert.Contains("ModoParticipacao", ranking);
        // A frase tem que dizer QUANDO começa — "ainda não há pontos" sozinho parece defeito.
        Assert.Contains("primeiro jogo", ranking);
    }

    [Fact]
    public void O_botao_do_torneio_nao_promete_ACERTO_antes_de_existir_jogo_apurado()
    {
        var details = Ler("Views", "Torneios", "Details.cshtml");

        var inicio = details.IndexOf("asp-action=\"Palpiteiros\"", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "Não achei o botão Palpiteiros na página do torneio.");

        // "Quem mais acertou no Palpitrômetro" é mentira na véspera: ninguém acertou nada
        // ainda. O texto tem que perguntar ao ranking em que fase ele está.
        var trecho = details[inicio..Math.Min(details.Length, inicio + 700)];
        Assert.Contains("ModoParticipacao", trecho);
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
