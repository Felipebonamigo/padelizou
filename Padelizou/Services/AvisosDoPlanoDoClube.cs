using Padelizou.Models;

namespace Padelizou.Services;

// "O BAR VAI FECHAR" — os avisos do plano do clube.
//
// ⚠️ O DEFEITO QUE ISTO EVITA É MUDO, e é o mesmo do plano do professor com uma diferença que
// pesa muito mais: quando a assinatura do clube vence, não é uma taxa que sobe — é o BALCÃO
// QUE PARA. Comanda não abre, caixa não fecha, e quem descobre é a pessoa do bar num sábado à
// noite, com fila na frente. Um corte de acesso sem aviso não é regra cumprida: é cliente
// perdido, e com razão.
//
// A costura entre os dois mundos é a mesma do professor: **enquanto `PagaAte` é nulo quem
// manda é o TESTE; a partir do primeiro pagamento, quem manda é a ASSINATURA.** Nunca se
// sobrepõem.
public static class AvisosDoPlanoDoClube
{
    // Os estágios, gravados em `Clube.UltimoLembreteDoPlano`. Duas faixas como no professor:
    // 1x é o mundo do teste, 2x o da assinatura — assim o número no banco diz de qual dos dois
    // ele veio, e a comparação continua monotônica quando o clube passa de um pro outro.
    public const int TesteAcabando = 10;
    public const int TesteAcabou = 11;

    public const int VaiVencer = 20;
    public const int VenceuNaCarencia = 21;
    public const int AcessoFechou = 22;

    // Quantos dias antes sai o primeiro aviso. SETE, e não cinco como no professor: aqui quem
    // paga costuma ser o financeiro do clube e não o dono, e essa conversa não acontece em
    // dois dias. Errar pra mais custa um aviso a mais; errar pra menos custa o balcão.
    public const int DiasDeAntecedencia = 7;

    // Depois disto o aviso de "já fechou" não sai mais. Mesma régua do professor: no dia em
    // que este serviço subir, todo clube que largou o teste meses atrás está tecnicamente
    // vencido, e disparar pra todos eles um aviso sobre algo de maio é spam.
    public const int JanelaDoAvisoDeQueda = 15;

    // Qual estágio cabe AGORA — nulo quando não há o que dizer.
    public static int? EstagioDevido(Clube clube, DateTime agora, PlanoClubeSettings cfg)
    {
        var estagio = clube.AssinaturaClubePagaAte is DateTime pagaAte
            ? DaAssinatura(clube, pagaAte, agora, cfg)
            : DoTeste(clube, agora, cfg);

        if (estagio == null) return null;

        // Monotônico: só sobe. Quando vários estágios se venceram de uma vez (o serviço ficou
        // fora do ar, ou a assinatura já tinha caído quando isto subiu), vale o de AGORA — os
        // anteriores são dados por cumpridos. Mandar os três seguidos num tick só é como se
        // perde a permissão de notificação de alguém.
        var ultimo = clube.UltimoLembreteDoPlano ?? 0;
        return estagio > ultimo ? estagio : null;
    }

    private static int? DoTeste(Clube clube, DateTime agora, PlanoClubeSettings cfg)
    {
        // Sem relógio começado não há teste correndo: ninguém do clube abriu a tela do plano.
        if (PlanoDoClube.FimDoTeste(clube, cfg) is not DateTime fim) return null;

        return EstagioPelaData(fim, semTolerancia: true, agora, cfg) switch
        {
            Acabando => TesteAcabando,
            Acabou => TesteAcabou,
            _ => null,
        };
    }

    private static int? DaAssinatura(Clube clube, DateTime pagaAte, DateTime agora,
        PlanoClubeSettings cfg)
    {
        if (!PlanoDoClube.PlanoValido(clube.PlanoDoClube)) return null;

        return EstagioPelaData(pagaAte, semTolerancia: false, agora, cfg) switch
        {
            Acabando => VaiVencer,
            NaCarencia => VenceuNaCarencia,
            Acabou => AcessoFechou,
            _ => null,
        };
    }

    // Valores internos da régua de datas — traduzidos pra faixa de cada mundo lá em cima.
    private const int Acabando = 1;
    private const int NaCarencia = 2;
    private const int Acabou = 3;

    // ⚠️ Tudo em dias de CALENDÁRIO (`.Date`): "vence hoje" tem que valer o dia inteiro, senão
    // o aviso muda de estágio conforme a hora em que o varredor passou.
    //
    // `semTolerancia` separa os dois mundos: a assinatura tem carência (7 dias em que o bar
    // continua aberto); o teste não tem — no dia seguinte ao 15º a porta fecha.
    private static int? EstagioPelaData(DateTime prazo, bool semTolerancia, DateTime agora,
        PlanoClubeSettings cfg)
    {
        var fimDaTolerancia = semTolerancia ? prazo : prazo.AddDays(cfg.DiasDeCarencia);

        if (agora.Date > fimDaTolerancia.Date)
        {
            return agora.Date <= fimDaTolerancia.Date.AddDays(JanelaDoAvisoDeQueda) ? Acabou : null;
        }

        if (agora.Date > prazo.Date) return NaCarencia;

        // "Vence hoje" ainda é VENCENDO, e não "venceu": no dia do prazo o acesso vale, e quem
        // resolve hoje não perdeu nada.
        return (prazo.Date - agora.Date).Days <= DiasDeAntecedencia ? Acabando : null;
    }

    public static string Titulo(int estagio) => estagio switch
    {
        TesteAcabando => "Seu teste está acabando",
        TesteAcabou => "O teste do seu clube acabou",
        VaiVencer => "A assinatura do clube vence em breve",
        VenceuNaCarencia => "A assinatura do clube venceu",
        _ => "O bar e as contas do clube foram fechados",
    };

    // ⚠️ Os valores vêm da configuração, nunca escritos na frase: são preço de tabela e mudam
    // sem passar por aqui. Um aviso que promete R$ 99 depois de a tabela virar R$ 129 é pior
    // que aviso nenhum.
    public static string Frase(int estagio, Clube clube, DateTime agora, PlanoClubeSettings cfg)
    {
        var plano = PlanoDoClube.RotuloDoPlano(clube.PlanoDoClube);

        if (estagio is TesteAcabando or TesteAcabou)
        {
            var fim = PlanoDoClube.FimDoTeste(clube, cfg)!.Value;
            var escolheu = PlanoDoClube.PlanoValido(clube.PlanoDoClube);

            if (estagio == TesteAcabando)
            {
                // Duas situações diferentes caem aqui, e o mesmo texto diria a coisa errada
                // pra uma delas: quem já escolheu o plano só precisa PAGAR; quem não escolheu
                // precisa saber que existe uma escolha a fazer.
                return escolheu
                    ? $"O teste do {clube.Nome} {Quando(fim, agora)} ({fim:dd/MM}). Gere a cobrança do "
                      + $"{plano} pra não perder o bar, o estoque e as contas."
                    : $"O teste do {clube.Nome} {Quando(fim, agora)} ({fim:dd/MM}). Escolha um plano pra "
                      + "continuar com o bar, o estoque e as contas do clube.";
            }

            return $"O teste do {clube.Nome} acabou em {fim:dd/MM} e o bar foi fechado. "
                 + "Assine quando quiser: tudo o que já foi registrado continua lá, intacto.";
        }

        var pagaAte = clube.AssinaturaClubePagaAte!.Value;
        var fimDaCarencia = pagaAte.AddDays(cfg.DiasDeCarencia);

        if (estagio == VaiVencer)
        {
            return $"O {plano} do {clube.Nome} {Quando(pagaAte, agora)} ({pagaAte:dd/MM}). "
                 + "Renove pra manter o bar e as contas abertos.";
        }

        if (estagio == VenceuNaCarencia)
        {
            return $"O {plano} do {clube.Nome} venceu em {pagaAte:dd/MM}, mas o bar segue aberto até "
                 + $"{fimDaCarencia:dd/MM}. Depois disso o balcão para.";
        }

        return $"O {plano} do {clube.Nome} venceu em {pagaAte:dd/MM} e o bar foi fechado. "
             + "Renove quando quiser: nada do que já foi registrado se perde, e volta na hora.";
    }

    // "vence hoje" / "vence amanhã" / "vence em 5 dias" — a data seca ao lado de "em 1 dias"
    // é o tipo de detalhe que faz um aviso parecer robô.
    private static string Quando(DateTime prazo, DateTime agora)
    {
        var dias = (prazo.Date - agora.Date).Days;

        return dias switch
        {
            <= 0 => "vence hoje",
            1 => "vence amanhã",
            _ => $"vence em {dias} dias"
        };
    }
}
