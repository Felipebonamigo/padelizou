using Padelizou.Models;

namespace Padelizou.Services;

// "ESSA DUPLA CHEGOU?" — uma régua só, desde 12/09/2026.
//
// A presença passou a ser da PESSOA (Models/PresencaNoTorneio), e "a dupla está completa" virou
// conta derivada. Ela é feita em quatro lugares — o selo do cartão de check-in, o contador da
// tela, a lista por categoria e a linha da aba Jogos —, e duas cópias dela fariam o contador
// dizer "32 de 64" enquanto o cartão do jogo diz que a dupla está inteira.
//
// ⚠️ INSCRIÇÃO SEM PARCEIRO CONTA COM UMA PESSOA SÓ. A vaga sozinha entra na chave desde 09/09;
// exigir dois checks deixaria essa dupla eternamente "faltando alguém" — e o W.O. sairia contra
// quem estava em quadra.
public static class PresencaNoDia
{
    // Os jogadores que uma dupla põe em quadra: dois, ou um na inscrição sem parceiro.
    // Dupla nula existe de verdade nesta lista (a prévia do mata-mata tem lado sem dono).
    public static IEnumerable<Jogador> JogadoresDa(Dupla? dupla)
    {
        if (dupla == null) yield break;
        if (dupla.Jogador1 != null) yield return dupla.Jogador1;
        if (dupla.Jogador2 != null) yield return dupla.Jogador2;
    }

    // Os Ids, pra quem não carregou as navegações (o contador da tela sai de uma projeção).
    public static IEnumerable<int> IdsDa(Dupla? dupla)
    {
        if (dupla == null) yield break;
        yield return dupla.Jogador1Id;
        if (dupla.Jogador2Id is int segundo) yield return segundo;
    }

    // Todos os jogadores dela já apareceram? É o que o selo "os dois chegaram" e o W.O. leem.
    //
    // ⚠️ O DICIONÁRIO É jogadorId → HORA DA CHEGADA, e não um conjunto de ids: a tela escreve
    // "chegou 08:12" do lado de cada nome, e um conjunto obrigaria uma segunda consulta (ou uma
    // segunda estrutura) só pra saber a hora — duas fontes pra mesma linha do banco.
    public static bool DuplaCompleta(Dupla? dupla, IReadOnlyDictionary<int, DateTime> chegadas)
    {
        if (dupla == null) return false;

        bool temAlguem = false;
        foreach (var id in IdsDa(dupla))
        {
            if (!chegadas.ContainsKey(id)) return false;
            temAlguem = true;
        }
        return temAlguem;
    }

    // A hora em que a pessoa apareceu, ou nulo se ainda não. Existe pra view não repetir o
    // `TryGetValue` em quatro lugares — e pra ninguém cair no `chegadas[id]` que estoura.
    public static DateTime? ChegouEm(int jogadorId, IReadOnlyDictionary<int, DateTime> chegadas) =>
        chegadas.TryGetValue(jogadorId, out var quando) ? quando : null;
}
