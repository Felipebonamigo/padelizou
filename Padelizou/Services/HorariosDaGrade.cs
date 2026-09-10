using Padelizou.Models;

namespace Padelizou.Services;

// OS HORÁRIOS QUE ESTE TORNEIO USA — a lista que o "Definir horário" oferece em vez de um
// relógio em branco.
//
// 🗣️ Felipe, 10/09/2026, com o seletor do navegador aberto na tela do Er: *"ao alterar o
// horario, deixe para que fique mais facil seguindo a ordem padrão do jogo (nesse torneio é de
// 50 em 50 min)"*.
//
// 🕳️ O campo era um `datetime-local` livre. Levar um jogo das 19:40 pras 20:30 exigia rolar a
// roda de minutos passando por 41, 42, 43… e saber o passo da grade de cabeça — e errar por um
// minuto não dá erro nenhum: dá um jogo às 20:31, desalinhado do resto do torneio, que só
// aparece depois no Conferir grade. Escolher numa lista dos horários que já existem não tem
// como errar o passo.
//
// ⚠️ O PASSO NÃO É UM NÚMERO ESCRITO AQUI. Ele é a duração da partida do torneio, e a virada do
// dia é a do expediente dele — os dois saem de `GradeDeJogos.Horarios`, o mesmo motor que monta
// a grade de verdade. Com UMA quadra, aquela função é exatamente a sequência de slots: cada
// jogo empurra o relógio uma partida. Uma segunda conta aqui divergiria da grade no primeiro
// torneio com expediente diferente, e a lista ofereceria horários que a grade nunca usa.
public static class HorariosDaGrade
{
    // Quantos slots a lista segue DEPOIS do último jogo. Atrasar é metade do que o organizador
    // faz com este botão no dia de jogo, e uma lista que para no último jogo não tem pra onde
    // empurrar. Seis são cinco horas num torneio de 50 minutos.
    public const int SlotsDeFolga = 6;

    /// <summary>Um horário da grade, com o que já está marcado nele.</summary>
    /// <param name="Quadras">Quantas quadras recebem jogo NESTE horário — as abertas, quando o
    /// torneio tem local com janela. Zero é o torneio que não cadastrou quadra: aí não há
    /// capacidade a informar, e a tela só mostra a hora.</param>
    public sealed record Slot(DateTime Horario, int Ocupadas, int Quadras)
    {
        public int Livres => Math.Max(0, Quadras - Ocupadas);
        public bool Lotado => Quadras > 0 && Livres == 0;
    }

    /// <summary>
    /// Os horários da grade deste torneio, do primeiro ao último, com quantas quadras estão
    /// tomadas em cada um.
    /// </summary>
    /// <param name="tambem">Horários que precisam estar na lista mesmo não sendo da grade — os
    /// das eliminatórias PREVISTAS (Services/ProximasFasesDaChave), que têm hora e são
    /// remarcáveis, mas não são jogo no banco e por isso não aparecem em <paramref name="jogos"/>.</param>
    public static List<Slot> Montar(Torneio torneio, IEnumerable<Partida> jogos,
        SedesDoTorneio? sedes = null, IEnumerable<DateTime?>? tambem = null)
    {
        var ocupadas = new Dictionary<DateTime, int>();
        foreach (var jogo in jogos)
            if (jogo.HorarioPrevisto is DateTime quando)
                ocupadas[quando] = ocupadas.GetValueOrDefault(quando) + 1;

        // ⚠️ O QUE JÁ EXISTE ENTRA MESMO FORA DO PASSO. Um jogo mexido na mão pras 20:13 não é
        // slot da grade — e sem ele na lista o seletor abriria sem nada marcado, dizendo por
        // omissão que o jogo não tem hora. Vale igual pro horário de uma prévia (`tambem`).
        var usados = ocupadas.Keys
            .Concat((tambem ?? Enumerable.Empty<DateTime?>()).Where(h => h != null).Select(h => h!.Value))
            .ToHashSet();

        var abertura = torneio.AberturaDaGrade;
        var ate = usados.DefaultIfEmpty(abertura).Max();
        if (torneio.DataFim is DateTime prazo && prazo.Date.Add(torneio.HoraFimDoDia) > ate)
            ate = prazo.Date.Add(torneio.HoraFimDoDia);

        // Teto de 14 dias de grade: existe só pra que uma configuração torta (fim do dia antes
        // da abertura, prazo em outro ano) não vire uma lista sem fim numa página.
        int duracao = VagasDaGrade.Duracao(torneio);
        int teto = 14 * 24 * 60 / duracao;

        var daGrade = new List<DateTime>();
        int depoisDoFim = 0;
        foreach (var horario in GradeDeJogos.Horarios(abertura, torneio.HoraFimDoDia,
                     quadras: 1, duracao, teto, aberturaDiasSeguintes: torneio.HoraInicioDiasSeguintes))
        {
            daGrade.Add(horario);
            if (horario >= ate && ++depoisDoFim > SlotsDeFolga) break;
        }

        return daGrade
            .Concat(usados)
            .Distinct()
            .OrderBy(horario => horario)
            .Select(horario => new Slot(
                horario,
                ocupadas.GetValueOrDefault(horario),
                sedes?.QuadrasAbertasEm(horario) ?? torneio.QuantidadeQuadras))
            .ToList();
    }
}
