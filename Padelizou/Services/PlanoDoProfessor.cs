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
    //
    // ⚠️ A CONTA DE TEMPO NÃO MORA MAIS AQUI: ela virou Services/CicloDeAssinatura quando o
    // clube ganhou plano próprio. Estes membros continuam existindo com o mesmo nome porque
    // tela, controller e teste chamam por eles — o que mudou é que agora só repassam.
    public const string CicloMensal = CicloDeAssinatura.Mensal;
    public const string CicloAnual = CicloDeAssinatura.Anual;

    public static string CicloValido(string? ciclo) => CicloDeAssinatura.Valido(ciclo);

    public static int MesesDo(string? ciclo) => CicloDeAssinatura.MesesDe(ciclo);

    public static decimal ValorDo(string? ciclo, PlanoProfessorSettings cfg) =>
        ciclo == CicloAnual ? cfg.AnuidadeAssinante : cfg.MensalidadeAssinante;

    public static decimal EconomiaDoAnual(PlanoProfessorSettings cfg) =>
        CicloDeAssinatura.EconomiaDoAnual(cfg.MensalidadeAssinante, cfg.AnuidadeAssinante);

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

    public static DateTime NovaDataPagaAte(DateTime? pagaAteAtual, DateTime agora, string? ciclo = null) =>
        CicloDeAssinatura.NovaDataPagaAte(pagaAteAtual, agora, ciclo);
}
