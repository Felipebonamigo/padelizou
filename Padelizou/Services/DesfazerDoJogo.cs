using Padelizou.Models;

namespace Padelizou.Services;

// Desfazer o clique errado na Mesa: "Ao Vivo" que volta pra "Agendada", "Finalizada" que
// volta pra "Ao Vivo".
//
// No balcão do clube, de celular e com fila esperando, tocar no play do jogo de baixo ou
// finalizar a partida errada acontece — e até agora não tinha volta: quem errava ficava com
// um jogo "ao vivo" que não estava rolando, ou pior, com um resultado que não existiu
// gerando a fase seguinte.
//
// ⚠️ Reabrir uma partida finalizada é MUITO mais que trocar o status. A finalização dispara
// uma cascata: carimba a fase do perdedor, move o Padelímetro dos 4 jogadores, e — quando é
// o último jogo da fase — MANDA O ROBÔ CRIAR A FASE SEGUINTE. Voltar sem desfazer isso
// deixaria a categoria com uma semifinal montada a partir de um resultado que foi apagado.
//
// Por isso a regra central aqui é uma recusa: se algo que veio DEPOIS já começou, não se
// reabre. Nesse ponto a correção deixou de ser um clique e virou decisão de quem organiza.
public static class DesfazerDoJogo
{
    // A ordem das fases dentro de uma categoria. Grupo é 0; o que não se reconhece (Americano,
    // fase de seed antigo) fica em -1 e nunca é tratado como "depois" de ninguém — apagar por
    // engano um jogo que não faz parte da corrente seria bem pior que não apagar.
    private static readonly string[] Corrente =
        { ChaveamentoMataMata.PrimeiraRodada, "Oitavas de Final", "Quartas de Final", "Semifinal", "Final" };

    public static int OrdemDaFase(string? fase)
    {
        if (string.IsNullOrEmpty(fase)) return -1;
        if (FasesTorneio.EhFaseDeGrupos(fase)) return 0;

        var i = Array.IndexOf(Corrente, fase);
        return i < 0 ? -1 : i + 1;
    }

    // ---- Ao Vivo → Agendada ----

    // Null = pode voltar. Texto = o motivo, na língua de quem organiza.
    public static string? MotivoParaNaoVoltarParaAgendado(Partida? jogo)
    {
        if (jogo == null) return "Não encontrei o jogo.";
        if (jogo.Status == "Agendada") return $"O jogo {jogo.Codigo} já está agendado.";
        if (jogo.Status != "AoVivo")
            return $"Só dá pra voltar pra agendado um jogo que está EM QUADRA — o {jogo.Codigo} está {jogo.Status}.";

        return null;
    }

    // O jogo volta pra fila como se nunca tivesse sido chamado: sem hora de início e sem
    // placar. Zerar é o certo — placar de um jogo que não aconteceu é o que enche a Mesa de
    // número que ninguém sabe de onde veio.
    public static void VoltarParaAgendado(Partida jogo)
    {
        jogo.Status = "Agendada";
        jogo.HorarioInicioReal = null;
        jogo.HorarioFimReal = null;
        jogo.SendoTransmitida = false;
        jogo.GamesDupla1 = null;
        jogo.GamesDupla2 = null;
        jogo.SetsDupla1 = null;
        jogo.SetsDupla2 = null;
        jogo.VencedorId = null;
    }

    // ---- Finalizada → Ao Vivo ----

    // O que a finalização deste jogo pode ter GERADO: os jogos da mesma categoria em fases
    // posteriores. Numa fase de grupos é o mata-mata inteiro; numa semifinal, a final.
    public static List<Partida> GeradosDepois(Partida jogo, IEnumerable<Partida> daCategoria)
    {
        int minha = OrdemDaFase(jogo.Fase);
        if (minha < 0) return new List<Partida>();

        return daCategoria
            .Where(p => p.Id != jogo.Id && OrdemDaFase(p.Fase) > minha)
            .ToList();
    }

    // Null = pode reabrir.
    public static string? MotivoParaNaoReabrir(Partida? jogo, IEnumerable<Partida> daCategoria)
    {
        if (jogo == null) return "Não encontrei o jogo.";
        if (jogo.Status == "AoVivo") return $"O jogo {jogo.Codigo} já está em quadra.";
        if (jogo.Status != "Finalizada")
            return $"Só dá pra reabrir um jogo FINALIZADO — o {jogo.Codigo} está {jogo.Status}.";

        // A trava que importa: o que veio depois não pode ter saído do papel. Reabrir aqui
        // apagaria uma semifinal que já está rolando, com gente na quadra.
        var depois = GeradosDepois(jogo, daCategoria);
        if (depois.FirstOrDefault(p => p.Status != "Agendada") is { } jaRolou)
        {
            return $"Não dá pra reabrir o {jogo.Codigo}: a fase seguinte já começou " +
                   $"(o jogo {jaRolou.Codigo}, da {jaRolou.Fase}, está {jaRolou.Status}). " +
                   "Corrija o placar em vez de reabrir.";
        }

        return null;
    }
}
