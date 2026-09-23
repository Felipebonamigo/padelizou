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

    // Faixa 4x: A AGENDA VAI FECHAR (22/09/2026). Assunto diferente das faixas de cima — elas
    // falam de quanto a aula CUSTA, estas de o professor parar de marcar aula. 🗣️ Felipe:
    // "coloque avisos regulares de que vai vencer em 1 semana, 1 dia, 1 hora".
    public const int BloqueioEmUmaSemana = 40;
    public const int BloqueioAmanha = 41;
    public const int BloqueioEmUmaHora = 42;

    // 50 e acima: JÁ BLOQUEADO, um por dia — `PrimeiroDiaBloqueado + dias`. 🗣️ Felipe: "o
    // professor vai avisando todo dia por email e push q venceu".
    //
    // ⚠️ O DIA VIRA O PRÓPRIO ESTÁGIO, e é isso que dispensa coluna nova. A escada já é
    // monotônica; um número que cresce com o calendário responde "já avisei hoje?" de graça, e
    // a varredura de hora em hora para de mandar doze avisos por dia sem nenhuma migration.
    public const int PrimeiroDiaBloqueado = 50;

    // Quantos dias antes do bloqueio o primeiro aviso sai.
    public const int DiasDeAntecedenciaDoBloqueio = 7;

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
        var ultimoAviso = professor.UltimoLembreteDeAssinatura ?? 0;

        // ⚠️ O BLOQUEIO FALA PRIMEIRO quando está à vista. É a consequência maior e a mais
        // tardia: com ele no horizonte, "sua taxa voltou ao cheio" virou detalhe do que está
        // acontecendo. A escada dele é monotônica na escala inteira — nunca desce.
        if (DoBloqueio(professor, agora, cfg) is int doBloqueio)
            return doBloqueio > ultimoAviso ? doBloqueio : null;

        // ⚠️ E QUEM JÁ ENTROU NELA NÃO VOLTA a ouvir sobre taxa: "sua agenda fecha amanhã"
        // seguido de "sua taxa voltou ao cheio" é ordem DECRESCENTE de urgência, e quem lê o
        // segundo conclui que o primeiro se resolveu.
        if (ultimoAviso >= BloqueioEmUmaSemana) return null;

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

    // ── O mundo do BLOQUEIO ───────────────────────────────────────────────────────────────

    private static int? DoBloqueio(Jogador professor, DateTime agora, PlanoProfessorSettings cfg)
    {
        // Dormente, ou relógio que nunca começou: não há bloqueio pra anunciar.
        if (BloqueioDoProfessor.BloqueiaEm(professor, cfg) is not DateTime bloqueia) return null;

        // ⚠️ QUEM DECIDE SE ESTÁ FECHADA É `EstaBloqueado`, e não uma comparação de datas aqui:
        // duas contas da mesma coisa em arquivos diferentes é como elas passam a discordar.
        if (!BloqueioDoProfessor.EstaBloqueado(professor, agora, cfg))
        {
            // ⚠️ E NÃO HÁ GUARDA POR `CondicoesDeAssinante` AQUI — tinha, e estava errada. O
            // aviso de "uma semana" cai três dias DEPOIS do vencimento, ou seja, DENTRO da
            // carência, quando o professor ainda é "assinante em dia". É exatamente aí que o
            // aviso serve: ele ainda tem a taxa menor e ainda dá pra resolver sem perder nada.
            // Com a guarda, o primeiro aviso da escada nunca saía.
            //
            // Quem está de fato em dia não entra por aritmética: `BloqueiaEm` nasce do fim do
            // último direito, então um pagamento empurra a data pra fora da janela dos 7 dias.
            if (agora >= bloqueia) return null;

            var faltam = (bloqueia.Date - agora.Date).Days;

            // "Falta 1 hora" é o MESMO DIA do bloqueio, antes dele. Só existe porque o bloqueio
            // cai às 10h e a varredura das 9h ainda pega o professor solto — ver
            // BloqueioDoProfessor.HoraDoBloqueio. Varredura que só rodar depois das 10h pula
            // este estágio e manda o do dia 0; é o desenho, não um furo.
            if (faltam <= 0) return BloqueioEmUmaHora;
            if (faltam == 1) return BloqueioAmanha;
            return faltam <= DiasDeAntecedenciaDoBloqueio ? BloqueioEmUmaSemana : null;
        }

        // Já bloqueado: um aviso por dia até as aulas caírem. Dali em diante quem fala é o
        // cancelamento — repetir "seu plano venceu" sobre fato consumado é só ruído.
        // ⚠️ `>=`, e não `>`: NO dia do cancelamento quem fala é o próprio cancelamento, com o
        // que de fato aconteceu. Os dois no mesmo dia seriam "suas aulas caem hoje" seguido de
        // "suas aulas caíram" — o segundo torna o primeiro ruído.
        if (BloqueioDoProfessor.CancelaAulasEm(professor, cfg) is DateTime cancela
            && agora.Date >= cancela.Date)
            return null;

        return PrimeiroDiaBloqueado + (agora.Date - bloqueia.Date).Days;
    }

    // POR ONDE O AVISO SAI — e esta função existe por causa da CONTA DE E-MAIL.
    //
    // ⚠️ `EnviarParaJogadorAsync` manda push E E-MAIL no mesmo funil: os avisos do plano sempre
    // mandaram e-mail, sem ninguém pedir. Diário × ~20 dias × N professores seria, com 10
    // bloqueados, 200 e-mails contra um volume mensal do sistema inteiro de ~300 a 500
    // (EMAIL.md). A cota do Gmail já estourou duas vezes; na segunda, 130 e-mails morreram
    // calados — duas recuperações de senha entre eles.
    //
    // Então o diário vai por `AppSemEmail` (push + caixa de entrada), que JÁ EXISTE e nasceu
    // desse mesmo estouro, e o e-mail sai em TRÊS marcos: o dia do bloqueio, a metade do prazo
    // e a véspera do cancelamento.
    public static AlcanceDoAviso AlcanceDe(int estagio, Jogador professor, PlanoProfessorSettings cfg)
    {
        if (estagio < PrimeiroDiaBloqueado) return AlcanceDoAviso.SoApp;

        var dia = estagio - PrimeiroDiaBloqueado;

        // O prazo do diário: do bloqueio até o cancelamento.
        var prazo = cfg.DiasAteCancelarAsAulas - cfg.DiasAteOBloqueio;

        return dia == 0 || dia == prazo / 2 || dia == prazo - 1
            ? AlcanceDoAviso.SoApp
            : AlcanceDoAviso.AppSemEmail;
    }

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
        BloqueioEmUmaSemana or BloqueioAmanha or BloqueioEmUmaHora => "Sua agenda vai fechar",
        // ⚠️ ANTES do `_`, como a cortesia: o `_` devolve o texto da assinatura vencida, e um
        // estágio novo sem linha própria manda o título errado SEM ERRO NENHUM.
        >= PrimeiroDiaBloqueado => "Sua agenda está fechada",
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

        if (estagio >= BloqueioEmUmaSemana)
            return DoBloqueioEmPalavras(estagio, professor, agora, cfg);

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

    // ⚠️ NENHUMA DESTAS FRASES PROMETE QUE A AULA MARCADA SOME. Ela não some: o bloqueio para
    // de aceitar novidade e a agenda continua à vista. Quem cancela é o prazo de um mês, e é
    // ELE que a frase do fim anuncia — trocar isso assustaria o professor com uma perda que não
    // aconteceu, no dia em que ele mais precisa entender o que dá pra fazer.
    private static string DoBloqueioEmPalavras(int estagio, Jogador professor, DateTime agora,
        PlanoProfessorSettings cfg)
    {
        if (estagio < PrimeiroDiaBloqueado)
        {
            var bloqueia = BloqueioDoProfessor.BloqueiaEm(professor, cfg)!.Value;
            var quando = estagio == BloqueioEmUmaHora ? "fecha hoje"
                       : estagio == BloqueioAmanha ? "fecha amanhã"
                       : Quando(bloqueia, agora, "fecha");

            return $"Sua agenda {quando} ({bloqueia:dd/MM}) e você para de marcar aula nova. "
                 + "O que já está marcado continua aparecendo. Assine e ela reabre na hora.";
        }

        // Já fechada. O que muda por dia é a conta regressiva até as aulas caírem — é a única
        // informação nova que ele tem a cada manhã, e é a que decide se ele resolve hoje.
        if (BloqueioDoProfessor.CancelaAulasEm(professor, cfg) is not DateTime cancela)
            return "Sua agenda está fechada pra aula nova. Assine e ela reabre na hora.";

        var faltam = (cancela.Date - agora.Date).Days;
        var prazo = faltam <= 0 ? "hoje" : faltam == 1 ? "amanhã" : $"em {faltam} dias";

        return $"Sua agenda está fechada pra aula nova. Suas aulas já marcadas continuam de pé, "
             + $"mas são canceladas {prazo} ({cancela:dd/MM}) se o plano não voltar. "
             + "Assine e tudo reabre na hora.";
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
