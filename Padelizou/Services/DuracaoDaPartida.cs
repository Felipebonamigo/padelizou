using Padelizou.Models;

namespace Padelizou.Services;

// QUANTO DURA UM JOGO, pela fase dele (19/11/2026).
//
// 🗣️ Rafael (Wallau): *"coloquei finais de 2 sets de 6 [...] não consigo mudar o horário só das
// finais"*. Felipe: *"por fases, duração de jogo de chave, mata mata, semi final e final e ter a
// opção de deixar todos iguais"*.
//
// ⚠️ A GRADE CONTINUA COM VAGA DO TAMANHO DOS GRUPOS (`VagasDaGrade.Duracao`), pra que a grade
// dos grupos saia idêntica à de antes. Jogo mais longo OCUPA mais de uma vaga da mesma quadra
// (`Vagas`); jogo mais curto ocupa uma — atalho: o resto da vaga fica vazio; encaixar jogo curto
// no meio da vaga mudaria o tamanho da vaga de toda a grade.
//
// Todo lugar que mede um jogo pergunta AQUI — a coluna crua dá a duração errada pras eliminatórias.
public static class DuracaoDaPartida
{
    // O teto do campo de duração na gestão (o dos grupos vai até 240).
    public const int MaximoDeMinutos = 240;

    public static int Minutos(Torneio torneio, string? fase)
    {
        int propria = fase switch
        {
            "Final" => torneio.TempoFinalMinutos,
            "Semifinal" => torneio.TempoSemifinalMinutos,
            ChaveamentoMataMata.PrimeiraRodada or "Oitavas de Final" or "Quartas de Final" => torneio.TempoMataMataMinutos,
            _ => 0,
        };
        return propria > 0 ? propria : VagasDaGrade.Duracao(torneio);
    }

    public static TimeSpan Tempo(Torneio torneio, string? fase) => TimeSpan.FromMinutes(Minutos(torneio, fase));

    // Quantas vagas seguidas da mesma quadra o jogo ocupa. Nunca menos de uma.
    public static int Vagas(Torneio torneio, string? fase)
    {
        int vaga = VagasDaGrade.Duracao(torneio);
        return Math.Max(1, (Minutos(torneio, fase) + vaga - 1) / vaga);
    }

    // A caixa "Mesma duração em todas as fases" da tela: derivada, não gravada.
    public static bool TodasIguais(Torneio torneio) =>
        torneio.TempoMataMataMinutos <= 0 && torneio.TempoSemifinalMinutos <= 0 && torneio.TempoFinalMinutos <= 0;
}
