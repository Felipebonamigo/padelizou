namespace Padelizou.Services;

// "há 3 min", "ontem", "12/08". Data cheia numa lista obriga a pessoa a fazer a conta de
// quanto tempo faz — que é a única coisa que ela quer saber ali.
//
// Nasceu como uma função local dentro da tela de Notificações. Virou classe quando a segunda
// tela precisou do mesmo texto: duas cópias da mesma régua é como as telas começam a discordar
// entre si (a lição que este projeto já pagou caro algumas vezes).
public static class TempoRelativo
{
    // Pro que ACONTECEU: um aviso que chegou, um comentário que alguém escreveu.
    public static string Curto(DateTime quando, DateTime? agoraOpcional = null)
    {
        var faz = (agoraOpcional ?? DateTime.Now) - quando;

        // Relógio do servidor e relógio de quem escreveu podem discordar por segundos, e
        // "há -1 min" é o tipo de coisa que faz a pessoa desconfiar do resto da tela.
        if (faz.TotalMinutes < 1) return "agora";
        if (faz.TotalMinutes < 60) return $"há {(int)faz.TotalMinutes} min";
        if (faz.TotalHours < 24) return $"há {(int)faz.TotalHours}h";
        if (faz.TotalDays < 2) return "ontem";
        if (faz.TotalDays < 7) return $"há {(int)faz.TotalDays} dias";
        return quando.ToString("dd/MM");
    }

    // Pro que COMEÇOU e continua valendo: desde quando alguém te segue. Aqui o mês seco
    // ("08/2026") diz mais que o dia exato — ninguém guarda o dia em que ganhou um seguidor.
    public static string Desde(DateTime quando, DateTime? agoraOpcional = null)
    {
        var faz = (agoraOpcional ?? DateTime.Now) - quando;

        if (faz.TotalDays < 1) return "desde hoje";
        if (faz.TotalDays < 2) return "desde ontem";
        if (faz.TotalDays < 30) return $"há {(int)faz.TotalDays} dias";
        return $"desde {quando:MM/yyyy}";
    }
}
