namespace Padelizou.Services;

// Mensal ou anual — a régua de tempo que vale pra QUALQUER assinatura do Padelizou.
//
// Nasceu extraída do PlanoDoProfessor no dia em que o clube ganhou plano próprio. A conta é
// exatamente a mesma nos dois ("pagar adiantado soma no fim, pagar atrasado conta de hoje"),
// e é justamente por ser a mesma que ela não podia ser copiada: duas cópias de uma regra de
// dinheiro divergem no dia em que só uma delas é corrigida — e aqui a divergência seria
// alguém pagando um mês e ganhando doze, ou o contrário.
//
// O PlanoDoProfessor continua expondo os mesmos nomes de antes (chamam daqui), porque renomear
// a API dele custaria uma varredura em tela, controller e teste pra não mudar comportamento
// nenhum.
public static class CicloDeAssinatura
{
    public const string Mensal = "Mensal";
    public const string Anual = "Anual";

    // O que veio da tela, normalizado. Só existem dois ciclos; qualquer outra coisa é mensal.
    public static string Valido(string? ciclo) => ciclo == Anual ? Anual : Mensal;

    // ⚠️ Ciclo AUSENTE é mensal de propósito: toda cobrança criada antes do plano anual
    // existir tem `{"ProfessorId":N}` no banco, sem ciclo nenhum — e ela comprou um mês.
    // Tratar o null como "não sei" travaria a confirmação de quem já tem cobrança em aberto.
    public static int MesesDe(string? ciclo) => ciclo == Anual ? 12 : 1;

    // O pagamento estende a assinatura a partir de onde ela estiver: pagar adiantado soma no
    // fim; pagar atrasado conta a partir de hoje (atraso não vira crédito). O ciclo decide
    // quantos meses entram — 1 no mensal, 12 no anual.
    public static DateTime NovaDataPagaAte(DateTime? pagaAteAtual, DateTime agora, string? ciclo = null) =>
        (pagaAteAtual != null && pagaAteAtual.Value > agora ? pagaAteAtual.Value : agora)
            .AddMonths(MesesDe(ciclo));

    // Quanto o anual economiza contra 12 mensalidades — o número que decide a compra, e que a
    // tela não pode chutar: com preço vindo de configuração, "2 meses grátis" vira mentira no
    // dia em que um dos dois valores mudar.
    public static decimal EconomiaDoAnual(decimal mensal, decimal anual) => mensal * 12 - anual;
}
