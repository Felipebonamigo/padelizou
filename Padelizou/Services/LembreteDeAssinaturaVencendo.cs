using Padelizou.Models;

namespace Padelizou.Services;

// "SUA ASSINATURA VAI VENCER" — o aviso que faltava pro professor assinante (Felipe,
// 10/08/2026).
//
// ⚠️ O DEFEITO QUE ISTO CORRIGE É MUDO, E É O PIOR TIPO. A mensalidade não é recorrente: quem
// paga tem que voltar na tela e gerar a cobrança de novo. Quando não volta, `SituacaoDe` cai
// sozinha pra `AssinanteEmAtraso` depois da carência e a taxa das aulas dele **sobe de 3% pra
// 10% sem uma linha em lugar nenhum** — nem pra ele, nem pra nós. Ele descobre no extrato, se
// descobrir. A trava automática está certa (ver PlanoDoProfessor); o que faltava era contar.
//
// ⚠️ E AQUI SE AVISA DEPOIS DO PRAZO, AO CONTRÁRIO DO LembreteDeInscricaoNaoPaga. Lá, passado
// o prazo, "não é lembrete, é cobrança — e essa conversa é do organizador com a pessoa". Aqui
// o prazo é NOSSO, a consequência é automática e a pessoa não participou dela: calar depois do
// vencimento seria justamente esconder o momento em que ela perdeu alguma coisa.
public static class LembreteDeAssinaturaVencendo
{
    // Os três momentos, em ordem. O número gravado é o ESTÁGIO, não uma contagem de dias:
    // o último acontece DEPOIS do vencimento, e "quantos dias faltam" não sabe falar de um
    // prazo que já passou.
    public const int VaiVencer = 1;            // ainda dá tempo de renovar sem perder nada
    public const int VenceuNaCarencia = 2;     // venceu, mas a taxa menor ainda vale
    public const int TaxaVoltouAoCheio = 3;    // a carência acabou e a aula já custa 10%

    // Quantos dias antes do vencimento sai o primeiro aviso. Cinco porque o pagamento é
    // manual e pode ser boleto: avisar na véspera é avisar quem já não tem como resolver.
    public const int DiasDeAntecedencia = 5;

    // ⚠️ Depois disto, o "sua taxa voltou pros 10%" NÃO sai mais. É a régua do "avisar só no
    // NOVO": no dia em que este serviço subir, todo assinante que largou a mensalidade meses
    // atrás está tecnicamente vencido, e disparar pra todos eles um aviso sobre algo que
    // aconteceu em maio é a definição de spam — sem contar que a conta de e-mail já estourou
    // duas vezes por rajada. Vencimento velho é assunto encerrado.
    public const int JanelaDoAvisoDeQueda = 15;

    // Qual estágio cabe AGORA — nulo quando não há o que dizer.
    //
    // ⚠️ Só quem TEM assinatura paga entra: sem `AssinaturaProfessorPagaAte` não existe
    // vencimento nenhum a lembrar. Quem está no teste de 15 dias e quem escolheu Avulso ficam
    // de fora de propósito — o teste acabando é outro aviso, com outro texto e outro dono.
    public static int? EstagioDevido(Jogador professor, DateTime agora, PlanoProfessorSettings cfg)
    {
        if (professor.PlanoProfessor != PlanoDoProfessor.Assinante) return null;
        if (professor.AssinaturaProfessorPagaAte is not DateTime pagaAte) return null;

        var estagio = EstagioDaData(pagaAte, agora, cfg);
        if (estagio == null) return null;

        // Monotônico: só sobe. Quando vários estágios se venceram de uma vez (o serviço ficou
        // fora do ar, ou a assinatura já tinha caído quando isto subiu), vale o de AGORA e os
        // anteriores são dados por cumpridos — mandar os três seguidos, num tick só, é como se
        // perde a permissão de notificação de alguém.
        var ultimo = professor.UltimoLembreteDeAssinatura ?? 0;
        return estagio > ultimo ? estagio : null;
    }

    // O estágio pela data, sem olhar o que já foi enviado. Separado porque é o que o texto
    // precisa saber, e porque é onde mora a única conta difícil daqui.
    //
    // ⚠️ Tudo em dias de CALENDÁRIO (`.Date`): "vence hoje" tem que valer o dia inteiro, senão
    // o aviso muda de estágio conforme a hora em que o varredor passou.
    public static int? EstagioDaData(DateTime pagaAte, DateTime agora, PlanoProfessorSettings cfg)
    {
        var fimDaCarencia = pagaAte.AddDays(cfg.DiasDeCarencia);

        if (agora.Date > fimDaCarencia.Date)
        {
            // Caiu pra taxa cheia. Só avisamos enquanto isso for notícia.
            return agora.Date <= fimDaCarencia.Date.AddDays(JanelaDoAvisoDeQueda)
                ? TaxaVoltouAoCheio
                : null;
        }

        if (agora.Date > pagaAte.Date) return VenceuNaCarencia;

        // "Vence hoje" ainda é VaiVencer, e não "venceu": no dia do vencimento a assinatura
        // vale — quem paga hoje não perdeu nada, e dizer "venceu" o faria pensar que perdeu.
        var faltam = (pagaAte.Date - agora.Date).Days;
        return faltam <= DiasDeAntecedencia ? VaiVencer : null;
    }

    public static string Titulo(int estagio) => estagio switch
    {
        VaiVencer => "Sua assinatura vence em breve",
        VenceuNaCarencia => "Sua assinatura venceu",
        _ => "Sua taxa por aula voltou ao cheio",
    };

    // ⚠️ As porcentagens vêm da configuração, nunca escritas na frase: são preço de tabela e
    // mudam sem passar por aqui. Um aviso que promete 3% depois de a tabela virar 4% é pior
    // que aviso nenhum.
    public static string Frase(int estagio, DateTime pagaAte, DateTime agora, PlanoProfessorSettings cfg)
    {
        var menor = cfg.PercentualAssinantePix.ToString("0.#");
        var cheia = cfg.PercentualAvulso.ToString("0.#");
        var fimDaCarencia = pagaAte.AddDays(cfg.DiasDeCarencia);

        if (estagio == VaiVencer)
        {
            var faltam = (pagaAte.Date - agora.Date).Days;
            var quando = faltam <= 0 ? "vence hoje"
                : faltam == 1 ? "vence amanhã"
                : $"vence em {faltam} dias";

            return $"Seu plano Assinante {quando} ({pagaAte:dd/MM}). Renove pra manter a taxa de "
                 + $"{menor}% por aula — sem isso ela volta pros {cheia}%.";
        }

        if (estagio == VenceuNaCarencia)
        {
            return $"Seu plano Assinante venceu em {pagaAte:dd/MM}, mas a taxa de {menor}% por aula "
                 + $"ainda vale até {fimDaCarencia:dd/MM}. Depois disso ela volta pros {cheia}%.";
        }

        return $"Seu plano Assinante venceu em {pagaAte:dd/MM} e a taxa das suas aulas voltou pros "
             + $"{cheia}%. Renove quando quiser: a taxa de {menor}% volta na hora.";
    }
}
