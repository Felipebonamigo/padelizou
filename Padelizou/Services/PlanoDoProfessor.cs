using Padelizou.Models;

namespace Padelizou.Services;

// Os números do plano do professor. Ficam em configuração porque são preço de tabela —
// renegociar não pode exigir republicar o site.
public class PlanoProfessorSettings
{
    public decimal MensalidadeAssinante { get; set; } = 49.90m;

    // Doze meses de uma vez, por menos que doze mensalidades. Mesmo motivo de estar em
    // configuração: é preço de tabela, e renegociar não pode exigir republicar o site.
    public decimal AnuidadeAssinante { get; set; } = 499.90m;

    // Taxa por aula do ASSINANTE, pela forma que o aluno paga: Pix e boleto custam centavos
    // pro gateway, cartão custa caro e demora — mesma régua dos torneios.
    public decimal PercentualAssinantePix { get; set; } = 3m;
    public decimal PercentualAssinanteCartao { get; set; } = 6m;

    // Sem assinatura: taxa cheia por aula, qualquer forma.
    public decimal PercentualAvulso { get; set; } = 10m;

    public int DiasDeTeste { get; set; } = 15;

    // Dias de atraso da mensalidade que ainda seguram as condições de assinante. Cair pra
    // taxa cheia no primeiro minuto de atraso puniria quem só esqueceu o boleto no feriado.
    public int DiasDeCarencia { get; set; } = 7;
}

// A regra do plano, pura: quem está em teste, quem está em dia, quem caiu pro avulso — e
// qual taxa cada aula paga. Existe porque a resposta é consumida em três lugares (a tela do
// plano, o checkout do aluno e a cobrança em si) e os três TÊM que concordar.
public static class PlanoDoProfessor
{
    public const string Assinante = "Assinante";
    public const string Avulso = "Avulso";

    // ── Ciclo de pagamento ────────────────────────────────────────────────────────────────
    // O ciclo é do PAGAMENTO, não do professor: quem assina continua sendo "Assinante", e o
    // que muda é quanto tempo cada cobrança compra. Por isso ele mora no JSON da cobrança
    // (DadosAssinaturaProfessor) e não numa coluna do Jogador — pagar 12 meses agora e um mês
    // no ano que vem não é "trocar de plano", e uma coluna diria que é.
    public const string CicloMensal = "Mensal";
    public const string CicloAnual = "Anual";

    // O que veio da tela, normalizado. Só existem dois ciclos; qualquer outra coisa é mensal.
    public static string CicloValido(string? ciclo) =>
        ciclo == CicloAnual ? CicloAnual : CicloMensal;

    // ⚠️ Ciclo AUSENTE é mensal de propósito: toda cobrança criada antes do plano anual
    // existir tem `{"ProfessorId":N}` no banco, sem ciclo nenhum — e ela comprou um mês.
    // Tratar o null como "não sei" travaria a confirmação de quem já tem cobrança em aberto.
    public static int MesesDo(string? ciclo) => ciclo == CicloAnual ? 12 : 1;

    public static decimal ValorDo(string? ciclo, PlanoProfessorSettings cfg) =>
        ciclo == CicloAnual ? cfg.AnuidadeAssinante : cfg.MensalidadeAssinante;

    // Quanto o anual economiza contra 12 mensalidades — o número que decide a compra, e que
    // a tela não pode chutar: com preço vindo de configuração, "2 meses grátis" vira mentira
    // no dia em que um dos dois valores mudar.
    public static decimal EconomiaDoAnual(PlanoProfessorSettings cfg) =>
        cfg.MensalidadeAssinante * 12 - cfg.AnuidadeAssinante;

    public enum Situacao
    {
        EmTeste,                // 15 dias com condições de assinante, sem mensalidade
        AssinanteEmDia,         // mensalidade quitada (ou dentro da carência)
        AssinanteEmAtraso,      // assinou mas não pagou: taxa de avulso até quitar
        Avulso,                 // escolheu ficar sem mensalidade
        TesteVencidoSemEscolha, // acabou o teste e não escolheu: tratado como avulso
        Cortesia                // condições de assinante por combinação nossa, sem dinheiro
    }

    // ⚠️ DIA DE CALENDÁRIO E SEM CARÊNCIA, ao contrário da assinatura paga. Os 7 dias de
    // `DiasDeCarencia` existem pra quem esqueceu o boleto no feriado — cortesia não tem boleto.
    // E "vale até 20/08" vale o dia 20 INTEIRO: comparar com a hora faria a cortesia morrer à
    // meia-noite e um minuto do dia que a tela promete.
    public static bool EmCortesia(Jogador professor, DateTime agora) =>
        professor.CortesiaProfessorAte != null
        && agora.Date <= professor.CortesiaProfessorAte.Value.Date;

    public const int MesesMaximosDeCortesia = 24;

    // O que impede de gravar uma cortesia. Mora aqui, e não no controller, pelo mesmo motivo de
    // FinanceiroDoPadelizou.ProblemaParaRegistrar existir: é regra, não tela.
    public static string? ProblemaParaDarCortesia(DateTime? ate, string? motivo, DateTime agora)
    {
        if (ate == null)
            return "Escolha até quando vale a cortesia.";
        if (ate.Value.Date < agora.Date)
            return "A cortesia não pode terminar no passado.";
        // ⚠️ Um `<input type="date">` aceita o ano 20270 num dedo torto, e isso seria
        // "assinante em dia pra sempre" — sem nada na tela denunciando.
        if (ate.Value.Date > agora.Date.AddMonths(MesesMaximosDeCortesia))
            return $"A cortesia vale no máximo {MesesMaximosDeCortesia} meses de cada vez.";
        if (string.IsNullOrWhiteSpace(motivo))
            return "Diga o motivo da cortesia — \"permuta de serviços\", por exemplo. "
                 + "Daqui a um ano é isso que decide se renova.";
        return null;
    }

    public static bool EmTeste(Jogador professor, DateTime agora, PlanoProfessorSettings cfg) =>
        professor.TesteProfessorInicio != null
        && agora <= professor.TesteProfessorInicio.Value.AddDays(cfg.DiasDeTeste);

    public static DateTime? FimDoTeste(Jogador professor, PlanoProfessorSettings cfg) =>
        professor.TesteProfessorInicio?.AddDays(cfg.DiasDeTeste);

    // ⚠️ A CORTESIA É PISO, NÃO TETO — e a ordem destas três linhas é a decisão inteira.
    //
    // Ela GANHA de tudo que deixaria o professor na taxa cheia (avulso escolhido por ele, teste
    // vencido, assinante em atraso): é pra isso que a cortesia existe. Mas PERDE do assinante
    // pagante em dia, por dois motivos que só aparecem meses depois:
    //   1. O clique dele em "Ficar no Avulso" na tela dele não pode apagar o presente que a
    //      gente deu — e não apaga, porque a cortesia continua vencendo o Avulso.
    //   2. O contador "assinantes em dia" do /Admin/Professores é lido como proxy de RECEITA.
    //      Se a cortesia ganhasse do pagante, um assinante que paga sumiria do verde e iria pro
    //      azul — o dinheiro dele continuaria em "Já pagou" e a conta pararia de fechar.
    public static Situacao SituacaoDe(Jogador professor, DateTime agora, PlanoProfessorSettings cfg)
    {
        var dele = SemCortesia(professor, agora, cfg);
        if (dele == Situacao.AssinanteEmDia) return dele;
        return EmCortesia(professor, agora) ? Situacao.Cortesia : dele;
    }

    // A situação que o professor teria por conta própria, ignorando qualquer cortesia.
    private static Situacao SemCortesia(Jogador professor, DateTime agora, PlanoProfessorSettings cfg)
    {
        if (professor.PlanoProfessor == Assinante)
        {
            if (professor.AssinaturaProfessorPagaAte != null
                && agora <= professor.AssinaturaProfessorPagaAte.Value.AddDays(cfg.DiasDeCarencia))
                return Situacao.AssinanteEmDia;

            // Assinou durante o teste e a primeira mensalidade ainda não venceu: o teste
            // segura as condições até o fim dos 15 dias.
            if (EmTeste(professor, agora, cfg)) return Situacao.EmTeste;

            return Situacao.AssinanteEmAtraso;
        }

        if (professor.PlanoProfessor == Avulso) return Situacao.Avulso;

        return EmTeste(professor, agora, cfg) ? Situacao.EmTeste : Situacao.TesteVencidoSemEscolha;
    }

    // Tem direito à taxa menor AGORA? (teste correndo, mensalidade em dia ou cortesia)
    //
    // ⚠️ ESTA LINHA É A COBRANÇA INTEIRA. Acrescentar `Cortesia` aqui é o que faz a aula do
    // professor de cortesia sair a 3%/6% em vez de 10% — CobrancaDaAula, JogoAulaController,
    // PagamentoInscricaoService e AulasController todos passam por aqui e não mudam uma letra.
    public static bool CondicoesDeAssinante(Jogador professor, DateTime agora, PlanoProfessorSettings cfg) =>
        SituacaoDe(professor, agora, cfg) is Situacao.EmTeste or Situacao.AssinanteEmDia or Situacao.Cortesia;

    // O que a cobrança da aula trava no gateway e que taxa fica — o par nasce junto, igual
    // ao CobrancaDoTorneio, porque taxa e forma não podem se contradizer.
    public static CobrancaDoTorneio.Cobranca CobrancaDaAula(
        Jogador professor, string? escolhaDoAluno, DateTime agora, PlanoProfessorSettings cfg)
    {
        if (!CondicoesDeAssinante(professor, agora, cfg))
        {
            // Avulso: a taxa não depende da forma, então o aluno escolhe no gateway mesmo.
            return new CobrancaDoTorneio.Cobranca("UNDEFINED", cfg.PercentualAvulso);
        }

        return (escolhaDoAluno ?? "") switch
        {
            CobrancaDoTorneio.EscolhaPix => new CobrancaDoTorneio.Cobranca("PIX", cfg.PercentualAssinantePix),
            CobrancaDoTorneio.EscolhaCartao => new CobrancaDoTorneio.Cobranca("CREDIT_CARD", cfg.PercentualAssinanteCartao),

            // Escolha ausente: forma aberta com a taxa de cartão — errar pra cá nunca dá
            // prejuízo silencioso (o professor assinante recebe o combinado nos dois modos).
            _ => new CobrancaDoTorneio.Cobranca("UNDEFINED", cfg.PercentualAssinanteCartao),
        };
    }

    // O pagamento estende a assinatura a partir de onde ela estiver: pagar adiantado soma no
    // fim; pagar atrasado conta a partir de hoje (atraso não vira crédito). O ciclo decide
    // quantos meses entram — 1 no mensal, 12 no anual.
    public static DateTime NovaDataPagaAte(DateTime? pagaAteAtual, DateTime agora, string? ciclo = null) =>
        (pagaAteAtual != null && pagaAteAtual.Value > agora ? pagaAteAtual.Value : agora)
            .AddMonths(MesesDo(ciclo));
}
