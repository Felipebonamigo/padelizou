using Padelizou.Models;

namespace Padelizou.Services;

// As regras das contas a pagar e a receber que são conta, não banco.
public static class FinanceiroDoClube
{
    // Em que situação a conta está HOJE. Sai daqui e não de uma coluna no banco porque
    // "vencida" muda sozinha com a passagem do tempo: guardar isso gravado obrigaria alguém
    // a varrer a tabela toda meia-noite só pra manter a verdade em dia.
    public const string Quitado = "Quitado";
    public const string Vencido = "Vencido";
    public const string VenceHoje = "Vence hoje";
    public const string AVencer = "A vencer";

    public static string Situacao(LancamentoFinanceiro l, DateTime hoje)
    {
        if (l.EstaQuitado) return Quitado;

        var vencimento = l.Vencimento.Date;
        var dia = hoje.Date;

        if (vencimento < dia) return Vencido;
        if (vencimento == dia) return VenceHoje;
        return AVencer;
    }

    public static string? ProblemaCom(string? descricao, decimal valor, string? tipo, int repetirMeses)
    {
        if (string.IsNullOrWhiteSpace(descricao)) return "Escreva do que é a conta.";
        if (descricao.Trim().Length > 120) return "A descrição é muito longa (máximo 120 caracteres).";

        // Valor zero não é conta: é lembrete. Se for pra anotar algo sem valor, o lugar é a
        // observação de outro lançamento, não uma linha de R$ 0,00 poluindo o total.
        if (valor <= 0) return "O valor precisa ser maior que zero.";
        if (valor > 10_000_000m) return "Esse valor parece errado — confira.";

        if (tipo != LancamentoFinanceiro.Pagar && tipo != LancamentoFinanceiro.Receber)
            return "Diga se a conta é a pagar ou a receber.";

        if (repetirMeses < 1) return "A repetição precisa ser de pelo menos 1 mês.";
        // 60 meses = 5 anos. Acima disso é quase sempre dedo escorregando, e o estrago é
        // criar centenas de linhas que alguém vai ter que apagar uma a uma.
        if (repetirMeses > 60) return "Repetir por mais de 60 meses não faz sentido — use um prazo menor.";

        return null;
    }

    // As datas de vencimento de uma conta que se repete todo mês.
    //
    // Nascem TODAS de uma vez, gravadas, em vez de uma regra "repete todo dia 10" calculada
    // na hora de mostrar. Motivo: conta que se repete muda — o aluguel reajusta, o contrato
    // acaba, um mês é pago adiantado. Com as linhas gravadas, mexer numa parcela é mexer numa
    // linha; com regra calculada, seria preciso inventar um sistema de exceções.
    //
    // Dia 31 em mês que não tem: cai no último dia do mês, que é como todo boleto se comporta.
    public static List<DateTime> Vencimentos(DateTime primeiro, int repetirMeses)
    {
        var datas = new List<DateTime>();
        int diaDesejado = primeiro.Day;

        for (int i = 0; i < repetirMeses; i++)
        {
            var mes = primeiro.Date.AddDays(1 - primeiro.Day).AddMonths(i);
            int diaValido = Math.Min(diaDesejado, DateTime.DaysInMonth(mes.Year, mes.Month));
            datas.Add(new DateTime(mes.Year, mes.Month, diaValido));
        }

        return datas;
    }

    // O que está em aberto, separado pelo que dói: o que já venceu vem antes do que vence.
    public record Resumo(
        decimal APagarVencido,
        decimal APagarAVencer,
        decimal AReceberVencido,
        decimal AReceberAVencer)
    {
        public decimal TotalAPagar => APagarVencido + APagarAVencer;
        public decimal TotalAReceber => AReceberVencido + AReceberAVencer;

        // Positivo = sobra prevista. Não é saldo de caixa: é o que ainda vai entrar menos o
        // que ainda vai sair, do que está EM ABERTO.
        public decimal Previsto => TotalAReceber - TotalAPagar;
    }

    public static Resumo Resumir(IEnumerable<LancamentoFinanceiro> lancamentos, DateTime hoje)
    {
        decimal pagarVencido = 0, pagarAVencer = 0, receberVencido = 0, receberAVencer = 0;

        foreach (var l in lancamentos)
        {
            if (l.EstaQuitado) continue;

            bool vencido = Situacao(l, hoje) == Vencido;

            if (l.EhPagar)
            {
                if (vencido) pagarVencido += l.Valor; else pagarAVencer += l.Valor;
            }
            else
            {
                if (vencido) receberVencido += l.Valor; else receberAVencer += l.Valor;
            }
        }

        return new Resumo(pagarVencido, pagarAVencer, receberVencido, receberAVencer);
    }
}
