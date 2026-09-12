using Padelizou.Models;

namespace Padelizou.Services;

// "ESSA DUPLA CHEGOU PRA ESTE JOGO?" — uma régua só, desde 12/09/2026.
//
// A presença é da PESSOA num JOGO (Models/PresencaNoJogo), e "a dupla está completa" é conta
// derivada. Ela é feita em quatro lugares — o selo do cartão de check-in, o contador da tela, a
// lista do dia e a linha da aba Jogos —, e duas cópias dela fariam o contador dizer "32 de 64"
// enquanto o cartão do jogo diz que a dupla está inteira.
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

    // Todos os jogadores dela já apareceram PRA ESTE JOGO? É o que o selo "os dois chegaram", a
    // ordem do horário e o W.O. leem.
    //
    // ⚠️ A CHAVE É (PartidaId, JogadorId) DESDE 12/09/2026. 🗣️ Felipe: *"o checkin ... nao
    // deveria [herdar], tem q ser separado jogo a jogo"*. Perguntar só pela pessoa fazia o jogo
    // das 19:00 herdar o check do jogo das 15:30 — e, com a ordem por presença, subir pro topo do
    // horário sem ninguém em quadra.
    //
    // ⚠️ E O VALOR É A HORA, não um conjunto de ids: a tela escreve "chegou 08:12" do lado de
    // cada nome, e um conjunto obrigaria uma segunda consulta só pra saber a hora — duas fontes
    // pra mesma linha do banco.
    public static bool DuplaCompleta(
        int partidaId, Dupla? dupla, IReadOnlyDictionary<(int PartidaId, int JogadorId), DateTime> chegadas)
    {
        if (dupla == null) return false;

        bool temAlguem = false;
        foreach (var id in IdsDa(dupla))
        {
            if (!chegadas.ContainsKey((partidaId, id))) return false;
            temAlguem = true;
        }
        return temAlguem;
    }

    // A hora em que a pessoa apareceu PRA ESTE JOGO, ou nulo se ainda não. Existe pra view não
    // repetir o `TryGetValue` em quatro lugares — e pra ninguém cair no `chegadas[chave]`, que
    // estoura.
    public static DateTime? ChegouEm(
        int partidaId, int jogadorId, IReadOnlyDictionary<(int PartidaId, int JogadorId), DateTime> chegadas) =>
        chegadas.TryGetValue((partidaId, jogadorId), out var quando) ? quando : null;
}
