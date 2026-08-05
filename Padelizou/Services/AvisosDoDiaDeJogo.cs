using Padelizou.Models;

namespace Padelizou.Services;

// Os dois avisos que faltavam no dia do torneio: "as chaves saíram" e "seu jogo é o próximo".
//
// O "próximo" é disparado pelo FIM do jogo anterior, não por relógio. Torneio atrasa — foi
// pra isso que a grade existe e mesmo assim escorrega — e um aviso preso ao horário previsto
// chegaria com o jogador ainda almoçando, ou depois de ele já ter jogado. Quem sabe de
// verdade que a quadra vagou é a partida que acabou de terminar nela.
public static class AvisosDoDiaDeJogo
{
    // Qual partida avisar quando `terminada` acaba.
    //
    // Regra: a próxima agendada NA MESMA QUADRA. Quadra sem nome também casa com quadra sem
    // nome — torneio pequeno costuma não nomear quadra nenhuma, e sem isso o aviso
    // simplesmente nunca sairia pra eles.
    public static Partida? ProximaAposTerminar(Partida terminada, IEnumerable<Partida> candidatas) =>
        ProximaNaQuadra(terminada, candidatas.Where(p => p.AvisoProximoEnviadoEm == null));

    // A mesma regra SEM a trava de "avisa uma vez só" — porque a tela não é um aviso.
    //
    // Quando a quadra vaga, o organizador precisa da sugestão do próximo jogo dela pra chamar
    // com um toque, e ele pode voltar nessa tela quantas vezes quiser. O push é que não pode
    // repetir: o jogador receberia "seu jogo é o próximo" duas vezes e viria correndo à toa.
    public static Partida? ProximaNaQuadra(Partida terminada, IEnumerable<Partida> candidatas)
    {
        return candidatas
            .Where(p => p.Id != terminada.Id)
            .Where(p => p.Status == "Agendada")
            .Where(p => p.HorarioPrevisto != null)
            .Where(p => MesmaQuadra(p.NomeQuadra, terminada.NomeQuadra))
            .OrderBy(p => p.HorarioPrevisto)
            .FirstOrDefault();
    }

    private static bool MesmaQuadra(string? a, string? b) =>
        string.IsNullOrWhiteSpace(a) && string.IsNullOrWhiteSpace(b)
        || string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

    // Os jogadores de uma partida, sem repetir e sem nulo (dupla pode estar sem parceiro).
    // Dupla-TIME não tem jogador pra avisar: o Jogador1Id dela é o organizador que cadastrou
    // o time, e "seu jogo é o próximo" no celular dele seria mentira.
    public static List<int> JogadoresDa(Partida partida)
    {
        var ids = new List<int?>();
        if (partida.Dupla1 is { NomeTime: null } d1) { ids.Add(d1.Jogador1Id); ids.Add(d1.Jogador2Id); }
        if (partida.Dupla2 is { NomeTime: null } d2) { ids.Add(d2.Jogador1Id); ids.Add(d2.Jogador2Id); }

        return ids.Where(id => id != null).Select(id => id!.Value).Distinct().ToList();
    }

    public static string CorpoDoProximo(Partida partida) =>
        string.IsNullOrWhiteSpace(partida.NomeQuadra)
            ? "A quadra vagou — seu jogo é o próximo. Fique por perto."
            : $"A {partida.NomeQuadra} vagou — seu jogo é o próximo. Fique por perto.";

    // "Chaves publicadas": o valor está em dizer QUANDO a pessoa joga, não que existe uma
    // tabela em algum lugar. Sem horário, o aviso genérico ainda serve de chamado.
    public static string CorpoDasChaves(DateTime? primeiroJogo) =>
        primeiroJogo == null
            ? "As chaves saíram. Veja contra quem você joga."
            : $"Seu primeiro jogo é {primeiroJogo.Value:dd/MM} às {primeiroJogo.Value:HH:mm}.";
}
