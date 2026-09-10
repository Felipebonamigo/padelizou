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
            .Where(p => MesmoLugar(p, terminada))
            .OrderBy(p => p.HorarioPrevisto)
            .FirstOrDefault();
    }

    // Com quadra dos dois lados, é a quadra. Sem quadra em um deles — o "por ordem de
    // liberação", onde a quadra é do balcão e o que se sabe do jogo é o CLUBE carimbado
    // (Partida.ClubeId) —, é o clube: nulo casa com nulo, que é o torneio pequeno de uma sede.
    //
    // ⚠️ Até 10/09/2026 quadra nula casava com qualquer quadra nula, e no Er (duas sedes, toda
    // quadra nula) o aviso ia pro jogo mais cedo do torneio INTEIRO — as jogadoras do Radar
    // recebiam "a quadra vagou" por um jogo do Er Padel, e consumiam o "avisa uma vez só" do
    // jogo certo (revisão adversarial do ensaio).
    private static bool MesmoLugar(Partida a, Partida b) =>
        !string.IsNullOrWhiteSpace(a.NomeQuadra) && !string.IsNullOrWhiteSpace(b.NomeQuadra)
            ? string.Equals(a.NomeQuadra.Trim(), b.NomeQuadra.Trim(), StringComparison.OrdinalIgnoreCase)
            : a.ClubeId == b.ClubeId;

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

    // ⚠️ ESTE AVISO MANDA A PESSOA SE LEVANTAR E ANDAR, então ele precisa dizer PRA ONDE.
    //
    // Até 21/08/2026 ele citava só a quadra, e bastava — o torneio era num lugar só. Num torneio
    // de duas sedes, "A Quadra 2 vagou, fique por perto" é instrução ativamente errada pra quem
    // está no clube errado: perto do quê? Com sede, o clube entra no texto. Sai por app, e-mail
    // e WhatsApp de uma vez (ver Services/EncerramentoDaPartida), então errar aqui erra em três
    // canais ao mesmo tempo.
    //
    // Sem quadra (o "por ordem"), diz pelo menos o PRÉDIO, pelo carimbo: "vagou uma quadra no
    // Radar" ainda manda a pessoa pro lado certo.
    public static string CorpoDoProximo(Partida partida, SedesDoTorneio? sedes = null) =>
        LugarDoJogo.EmTextoCorrido(sedes, partida.NomeQuadra) is { } onde
            ? $"A {onde} vagou — seu jogo é o próximo. Fique por perto."
            : partida.ClubeId is { } carimbo && sedes?.NomeDoClube(carimbo) is { } clube
            ? $"Vagou uma quadra no {clube} — seu jogo é o próximo. Fique por perto."
            : "A quadra vagou — seu jogo é o próximo. Fique por perto.";

    // "Chaves publicadas": o valor está em dizer QUANDO a pessoa joga, não que existe uma
    // tabela em algum lugar. Sem horário, o aviso genérico ainda serve de chamado.
    public static string CorpoDasChaves(DateTime? primeiroJogo) =>
        primeiroJogo == null
            ? "As chaves saíram. Veja contra quem você joga."
            : $"Seu primeiro jogo é {primeiroJogo.Value:dd/MM} às {primeiroJogo.Value:HH:mm}.";
}
