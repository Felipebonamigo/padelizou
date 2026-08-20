using Padelizou.Models;

namespace Padelizou.Services;

// "VOCÊ ESTÁ PRESTES A PERDER A TAXA MENOR" — os avisos do plano do professor (Felipe,
// 10/08/2026).
//
// ⚠️ O DEFEITO QUE ISTO CORRIGE É MUDO, E É O PIOR TIPO. A taxa por aula do professor cai de
// 3% pra 10% sozinha em DOIS momentos, e em nenhum dos dois havia uma linha em lugar nenhum —
// nem pra ele, nem pra nós:
//
//   • O TESTE DE 15 DIAS ACABA e ele não escolheu plano nenhum (ou escolheu Assinante e nunca
//     pagou a primeira mensalidade). `SituacaoDe` cai pra Avulso / AssinanteEmAtraso.
//   • A ASSINATURA VENCE e ele não voltou pra gerar a cobrança — a mensalidade **não é
//     recorrente**. Passada a carência de 7 dias, mesma queda.
//
// As duas travas automáticas estão certas (é o que impede assinante inadimplente de pagar taxa
// de assinante); o que faltava era **contar**.
//
// ⚠️ UM SERVIÇO SÓ, E NÃO DOIS, porque é a mesma pergunta feita duas vezes na vida do mesmo
// professor — e dois varredores olhando a mesma pessoa na mesma hora é como nascem dois avisos
// no mesmo tick. A costura entre os dois mundos é uma linha: **enquanto `PagaAte` é nulo, quem
// manda é o TESTE; a partir do primeiro pagamento, quem manda é a ASSINATURA.** Eles nunca se
// sobrepõem.
//
// ⚠️ E AQUI SE AVISA DEPOIS DO PRAZO, ao contrário do LembreteDeInscricaoNaoPaga. Lá, passado
// o prazo, "não é lembrete, é cobrança — e essa conversa é do organizador com a pessoa". Aqui
// o prazo é NOSSO, a consequência é automática e a pessoa não participou dela: calar depois do
// vencimento seria justamente esconder o momento em que ela perdeu alguma coisa.
public static class AvisosDoPlanoDoProfessor
{
    // Os estágios, gravados em `Jogador.UltimoLembreteDeAssinatura`.
    //
    // ⚠️ Duas FAIXAS, e não uma escada só: 1x é o mundo do teste, 2x é o da assinatura. Assim
    // o número no banco diz de qual dos dois ele veio — e, como o teste vem antes na vida do
    // professor, a faixa menor também deixa a comparação monotônica funcionar quando ele passa
    // de um mundo pro outro.
    public const int TesteAcabando = 10;   // ainda dá tempo de escolher sem perder nada
    public const int TesteAcabou = 11;     // acabou; a aula já custa a taxa cheia

    public const int VaiVencer = 20;           // ainda dá tempo de renovar sem perder nada
    public const int VenceuNaCarencia = 21;    // venceu, mas a taxa menor ainda vale
    public const int TaxaVoltouAoCheio = 22;   // a carência acabou e a aula já custa 10%

    // Faixa 3x: o mundo da CORTESIA (20/08/2026). Enquanto ela vale, ninguém é avisado de nada
    // — o professor combinou por fora com o Felipe e cobrar escolha dele seria constrangedor.
    // Este estágio só existe pra DEPOIS: a cortesia acabou e a taxa dele subiu sozinha.
    public const int CortesiaAcabou = 30;

    // Quantos dias antes sai o primeiro aviso, nos dois mundos. Cinco porque o pagamento é
    // manual e pode ser boleto: avisar na véspera é avisar quem já não tem como resolver.
    public const int DiasDeAntecedencia = 5;

    // ⚠️ Depois disto, o aviso de "já caiu" NÃO sai mais. É a régua do "avisar só no NOVO": no
    // dia em que este serviço subir, todo professor que largou o teste ou a mensalidade meses
    // atrás está tecnicamente vencido, e disparar pra todos eles um aviso sobre algo que
    // aconteceu em maio é a definição de spam — sem contar que a conta de e-mail já estourou
    // duas vezes por rajada. Vencimento velho é assunto encerrado.
    public const int JanelaDoAvisoDeQueda = 15;

    // Qual estágio cabe AGORA — nulo quando não há o que dizer.
    public static int? EstagioDevido(Jogador professor, DateTime agora, PlanoProfessorSettings cfg)
    {
        var estagio = QualMundo(professor, agora, cfg);

        if (estagio == null) return null;

        // Monotônico: só sobe. Quando vários estágios se venceram de uma vez (o serviço ficou
        // fora do ar, ou a assinatura já tinha caído quando isto subiu), vale o de AGORA e os
        // anteriores são dados por cumpridos — mandar os três seguidos, num tick só, é como se
        // perde a permissão de notificação de alguém.
        //
        // ⚠️ MAS SÓ DENTRO DA FAIXA, e não na escada inteira — isto mudou quando a cortesia
        // entrou (20/08/2026). Com dois mundos dava pra contar com a ordem da vida: o teste
        // (1x) sempre vem antes da assinatura (2x), então "só sobe" nunca calava nada legítimo.
        // A cortesia (3x) entra NO MEIO da vida e a vida continua depois dela. Com a regra
        // antiga, o 30 gravado calaria os estágios 10, 11, 20, 21 e 22 PARA SEMPRE — e o único
        // outro lugar que zera essa coluna é um pagamento confirmado.
        var ultimo = professor.UltimoLembreteDeAssinatura ?? 0;
        return Faixa(estagio.Value) != Faixa(ultimo) || estagio > ultimo ? estagio : null;
    }

    private static int Faixa(int estagio) => estagio / 10;

    // ⚠️ EXCLUSIVO E PRIMEIRO, nunca `?? DoTeste(...)`. Enquanto a cortesia vale (e durante a
    // janela em que o fim dela ainda é notícia), quem responde é ela — e a resposta durante a
    // vigência é SILÊNCIO. Sem esta exclusividade, o professor de cortesia que nunca escolheu
    // plano cairia no mundo do teste e receberia, por push E por e-mail, "seu período de teste
    // acaba amanhã — escolha um plano": cobrando decisão de quem já combinou tudo com o Felipe.
    private static int? QualMundo(Jogador professor, DateTime agora, PlanoProfessorSettings cfg)
    {
        if (professor.CortesiaProfessorAte is DateTime cortesiaAte
            && agora.Date <= cortesiaAte.Date.AddDays(JanelaDoAvisoDeQueda))
            return DaCortesia(professor, cortesiaAte, agora, cfg);

        return professor.AssinaturaProfessorPagaAte is DateTime pagaAte
            ? DaAssinatura(professor, pagaAte, agora, cfg)
            : DoTeste(professor, agora, cfg);
    }

    // ── Mundo 3: a cortesia ───────────────────────────────────────────────────────────────

    private static int? DaCortesia(Jogador professor, DateTime cortesiaAte, DateTime agora,
        PlanoProfessorSettings cfg)
    {
        // Durante a cortesia, silêncio absoluto: não há prazo pra ele resolver nem escolha a
        // fazer. Quem precisa se lembrar de que isso vence é o Felipe, e ele lê na tela.
        if (agora.Date <= cortesiaAte.Date) return null;

        // ⚠️ E só avisa quem REALMENTE perdeu a taxa menor. Quem tem assinatura paga viva por
        // baixo da cortesia não perdeu nada quando ela acabou — avisá-lo seria inventar um
        // problema que não existe.
        if (PlanoDoProfessor.CondicoesDeAssinante(professor, agora, cfg)) return null;

        return CortesiaAcabou;
    }

    // ── Mundo 1: o teste de 15 dias (ninguém pagou ainda) ─────────────────────────────────

    private static int? DoTeste(Jogador professor, DateTime agora, PlanoProfessorSettings cfg)
    {
        // Sem relógio começado não há teste correndo: ele nunca abriu a tela do plano.
        if (PlanoDoProfessor.FimDoTeste(professor, cfg) is not DateTime fim) return null;

        // ⚠️ Quem ESCOLHEU Avulso não é avisado. O teste acabando não tira nada dele — ele já
        // decidiu pagar a taxa cheia, e lembrá-lo disso é cutucar quem resolveu.
        if (professor.PlanoProfessor == PlanoDoProfessor.Avulso) return null;

        return EstagioPelaData(fim, semTolerancia: true, agora, cfg);
    }

    // ── Mundo 2: a assinatura paga ────────────────────────────────────────────────────────

    private static int? DaAssinatura(Jogador professor, DateTime pagaAte, DateTime agora,
        PlanoProfessorSettings cfg)
    {
        if (professor.PlanoProfessor != PlanoDoProfessor.Assinante) return null;

        var estagio = EstagioPelaData(pagaAte, semTolerancia: false, agora, cfg);

        // Traduz da faixa do teste pra faixa da assinatura: a régua das datas é a mesma, só o
        // vocabulário muda. A do meio (carência) não existe no teste — ver EstagioPelaData.
        return estagio switch
        {
            TesteAcabando => VaiVencer,
            TesteAcabou => TaxaVoltouAoCheio,
            NaCarencia => VenceuNaCarencia,
            _ => null,
        };
    }

    // Valor interno, só pra EstagioPelaData conseguir dizer "está na carência" sem inventar um
    // estágio no mundo do teste, onde carência não existe.
    private const int NaCarencia = 99;

    // A régua das datas, comum aos dois mundos.
    //
    // ⚠️ Tudo em dias de CALENDÁRIO (`.Date`): "acaba hoje" tem que valer o dia inteiro, senão
    // o aviso muda de estágio conforme a hora em que o varredor passou.
    //
    // `semTolerancia` é o que separa os dois: a assinatura tem 7 dias de carência (em que a
    // taxa menor ainda vale, e é sobre isso que o aviso do meio fala); **o teste não tem** —
    // no dia seguinte ao 15º já é taxa cheia.
    private static int? EstagioPelaData(DateTime prazo, bool semTolerancia, DateTime agora,
        PlanoProfessorSettings cfg)
    {
        var fimDaTolerancia = semTolerancia ? prazo : prazo.AddDays(cfg.DiasDeCarencia);

        if (agora.Date > fimDaTolerancia.Date)
        {
            // Já caiu pra taxa cheia. Só avisamos enquanto isso for notícia.
            return agora.Date <= fimDaTolerancia.Date.AddDays(JanelaDoAvisoDeQueda)
                ? TesteAcabou
                : null;
        }

        if (agora.Date > prazo.Date) return NaCarencia;

        // "Acaba hoje" ainda é ACABANDO, e não "acabou": no dia do prazo a condição vale —
        // quem resolve hoje não perdeu nada, e dizer "acabou" o faria pensar que perdeu.
        var faltam = (prazo.Date - agora.Date).Days;
        return faltam <= DiasDeAntecedencia ? TesteAcabando : null;
    }

    // ── O que sai escrito ─────────────────────────────────────────────────────────────────

    public static string Titulo(int estagio) => estagio switch
    {
        TesteAcabando => "Seu teste está acabando",
        TesteAcabou => "Seu período de teste acabou",
        VaiVencer => "Sua assinatura vence em breve",
        VenceuNaCarencia => "Sua assinatura venceu",
        // ⚠️ Precisa vir ANTES do `_`, que devolve o texto da assinatura vencida — esquecer
        // esta linha manda o título errado, sem erro nenhum.
        CortesiaAcabou => "Sua cortesia no Padelizou terminou",
        _ => "Sua taxa por aula voltou ao cheio",
    };

    // ⚠️ As porcentagens vêm da configuração, nunca escritas na frase: são preço de tabela e
    // mudam sem passar por aqui. Um aviso que promete 3% depois de a tabela virar 4% é pior
    // que aviso nenhum.
    public static string Frase(int estagio, Jogador professor, DateTime agora,
        PlanoProfessorSettings cfg)
    {
        var menor = cfg.PercentualAssinantePix.ToString("0.#");
        var cheia = cfg.PercentualAvulso.ToString("0.#");

        if (estagio is TesteAcabando or TesteAcabou)
            return DoTesteEmPalavras(estagio, professor, agora, cfg, menor, cheia);

        // ⚠️ ANTES da linha do `PagaAte!` logo abaixo, e não por organização: na cortesia esse
        // campo é NULO, e o `!.Value` estouraria dentro do `foreach` do serviço de fundo —
        // derrubando o aviso de todo mundo que viesse depois na fila, não só o dele.
        //
        // Sem a palavra "renove" e sem preço: quem pagou com trabalho não recebe cobrança.
        if (estagio == CortesiaAcabou)
        {
            var cortesiaAte = professor.CortesiaProfessorAte!.Value;
            return $"O plano que estava por nossa conta terminou em {cortesiaAte:dd/MM} e a taxa das "
                 + $"suas aulas voltou pros {cheia}%. Vire Assinante quando quiser e ela cai pra {menor}%.";
        }

        var pagaAte = professor.AssinaturaProfessorPagaAte!.Value;
        var fimDaCarencia = pagaAte.AddDays(cfg.DiasDeCarencia);

        if (estagio == VaiVencer)
        {
            return $"Seu plano Assinante {Quando(pagaAte, agora, "vence")} ({pagaAte:dd/MM}). Renove pra "
                 + $"manter a taxa de {menor}% por aula — sem isso ela volta pros {cheia}%.";
        }

        if (estagio == VenceuNaCarencia)
        {
            return $"Seu plano Assinante venceu em {pagaAte:dd/MM}, mas a taxa de {menor}% por aula "
                 + $"ainda vale até {fimDaCarencia:dd/MM}. Depois disso ela volta pros {cheia}%.";
        }

        return $"Seu plano Assinante venceu em {pagaAte:dd/MM} e a taxa das suas aulas voltou pros "
             + $"{cheia}%. Renove quando quiser: a taxa de {menor}% volta na hora.";
    }

    // ⚠️ DUAS SITUAÇÕES BEM DIFERENTES CAEM AQUI, e mandar o mesmo texto pras duas seria dizer
    // a coisa errada pra uma delas:
    //
    //   • quem NÃO ESCOLHEU nada — o teste acabando o joga no Avulso, e o que ele precisa
    //     saber é que existe uma escolha a fazer;
    //   • quem ESCOLHEU ASSINANTE e nunca pagou — a escolha já está feita, o que falta é a
    //     primeira mensalidade. Dizer "escolha um plano" pra essa pessoa é ignorar que ela já
    //     escolheu, e ela ficaria procurando na tela um botão que não é o dela.
    private static string DoTesteEmPalavras(int estagio, Jogador professor, DateTime agora,
        PlanoProfessorSettings cfg, string menor, string cheia)
    {
        var fim = PlanoDoProfessor.FimDoTeste(professor, cfg)!.Value;
        var assinouSemPagar = professor.PlanoProfessor == PlanoDoProfessor.Assinante;

        if (estagio == TesteAcabando)
        {
            var quando = Quando(fim, agora, "acaba");

            return assinouSemPagar
                ? $"Seu período de teste {quando} ({fim:dd/MM}) e sua primeira mensalidade ainda "
                  + $"não entrou. Sem ela, a taxa por aula sai dos {menor}% e vai pros {cheia}%."
                : $"Seu período de teste {quando} ({fim:dd/MM}). Até lá você paga {menor}% por "
                  + $"aula; sem escolher um plano, ela passa pros {cheia}%.";
        }

        return assinouSemPagar
            ? $"Seu período de teste acabou em {fim:dd/MM} e a mensalidade ainda não entrou, então "
              + $"a taxa por aula está nos {cheia}%. Pague e ela volta pros {menor}% na hora."
            : $"Seu período de teste acabou em {fim:dd/MM} e você está no Avulso: {cheia}% por aula. "
              + $"Vire Assinante quando quiser e ela cai pra {menor}%.";
    }

    // "vence em 5 dias" / "acaba amanhã" / "vence hoje" — conta os dias DE VERDADE, nunca o
    // número do marco: quem entra no aviso faltando 1 dia não pode ouvir "faltam 5".
    //
    // O verbo vem de fora porque assinatura VENCE e teste ACABA — a mesma conta, duas palavras.
    private static string Quando(DateTime prazo, DateTime agora, string verbo)
    {
        var faltam = (prazo.Date - agora.Date).Days;
        return faltam <= 0 ? $"{verbo} hoje"
            : faltam == 1 ? $"{verbo} amanhã"
            : $"{verbo} em {faltam} dias";
    }
}
