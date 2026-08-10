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
        EmTeste,               // 15 dias com condições de assinante, sem mensalidade
        AssinanteEmDia,        // mensalidade quitada (ou dentro da carência)
        AssinanteEmAtraso,     // assinou mas não pagou: taxa de avulso até quitar
        Avulso,                // escolheu ficar sem mensalidade
        TesteVencidoSemEscolha // acabou o teste e não escolheu: tratado como avulso
    }

    public static bool EmTeste(Jogador professor, DateTime agora, PlanoProfessorSettings cfg) =>
        professor.TesteProfessorInicio != null
        && agora <= professor.TesteProfessorInicio.Value.AddDays(cfg.DiasDeTeste);

    public static DateTime? FimDoTeste(Jogador professor, PlanoProfessorSettings cfg) =>
        professor.TesteProfessorInicio?.AddDays(cfg.DiasDeTeste);

    public static Situacao SituacaoDe(Jogador professor, DateTime agora, PlanoProfessorSettings cfg)
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

    // Tem direito à taxa menor AGORA? (teste correndo ou mensalidade em dia)
    public static bool CondicoesDeAssinante(Jogador professor, DateTime agora, PlanoProfessorSettings cfg) =>
        SituacaoDe(professor, agora, cfg) is Situacao.EmTeste or Situacao.AssinanteEmDia;

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
