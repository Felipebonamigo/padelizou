using Padelizou.Models;

namespace Padelizou.Services;

// O caixa do PADELIZOU — não confundir com o Financeiro do organizador (Pagamentos/Meus).
// Responde três perguntas, separadas porque o Felipe fecha o mês com elas:
//
//   1. Quanto entrou POR FORA do gateway (Pix direto + registro manual)?
//   2. Quanto entrou PELO gateway (a comissão dos splits, bruta)?
//   3. Quanto SAIU (a fatura do gateway, VPS... — despesas lançadas à mão)?
//
// ⚠️ A número 3 é REGISTRO, não cálculo, e isso é decisão: o custo exato de cada transação
// do gateway não existe no nosso banco (a taxa sai por dentro e nem guardamos a forma que o
// pagador escolheu). Estimar seria inventar contabilidade — o número certo está no extrato
// deles, e o admin copia de lá.
public static class FinanceiroDoPadelizou
{
    // Vai em Pagamento.MetodoPagamento: dinheiro que entrou por fora de TUDO (Pix combinado
    // no WhatsApp, dinheiro na mão) e que o admin registra pra contabilidade fechar.
    public const string MetodoManual = "Manual";

    public record Resumo(
        decimal RecebidoPorFora,     // Pix direto + manual, confirmados
        decimal RecebidoPeloGateway, // comissão dos splits confirmados (bruta)
        decimal Despesas,            // o que foi lançado como saída
        int QtdPorFora,
        int QtdPeloGateway,
        int QtdDespesas)
    {
        public decimal TotalRecebido => RecebidoPorFora + RecebidoPeloGateway;
        public decimal Liquido => TotalRecebido - Despesas;
    }

    // Soma em memória, recebendo listas já recortadas pelo período — quem consulta é o
    // controller, e a conta fica testável sem banco.
    public static Resumo Somar(IEnumerable<Pagamento> confirmados, IEnumerable<DespesaRegistrada> despesas)
    {
        decimal porFora = 0m, peloGateway = 0m;
        int qtdFora = 0, qtdGateway = 0;

        foreach (var p in confirmados)
        {
            // A régua é a COMISSÃO, não o valor cheio: no split o repasse nunca foi nosso.
            // Nos tipos 100% próprios (mensalidade, taxa) a comissão JÁ É o valor cheio.
            if (p.Status != "Confirmado") continue;

            if (p.MetodoPagamento == PixDireto.Metodo || p.MetodoPagamento == MetodoManual)
            {
                porFora += p.Comissao;
                qtdFora++;
            }
            else
            {
                peloGateway += p.Comissao;
                qtdGateway++;
            }
        }

        var listaDespesas = despesas.ToList();
        return new Resumo(porFora, peloGateway, listaDespesas.Sum(d => d.Valor),
            qtdFora, qtdGateway, listaDespesas.Count);
    }

    // ── Registro manual de pagamento ──────────────────────────────────────────────────────
    // O que pode ser registrado à mão é EXATAMENTE o que pode ser Pix direto: só o que é
    // 100% receita do Padelizou. Inscrição/aula/quadra "por fora" é dinheiro do organizador
    // — não passa por nós e não há o que registrar como receita nossa.

    public static string? ProblemaParaRegistrar(string? tipo, decimal valor)
    {
        if (!PixDireto.AceitaPix(tipo))
            return "Só mensalidade de professor, assinatura de clube e taxa de torneio podem ser "
                + "registradas — o resto tem repasse a terceiro.";
        if (valor <= 0)
            return "Informe o valor recebido.";
        return null;
    }

    // A mensalidade manual exige um PROFESSOR: efetivar estende a assinatura, e estender a
    // assinatura de quem não dá aula é registro que mente.
    public static string? ProblemaComOPagador(string? tipo, Jogador? pagador)
    {
        if (pagador == null)
            return "Ninguém encontrado com esse login, e-mail ou CPF.";
        if (tipo == PixDireto.TipoAssinatura && !pagador.IsProfessor)
            return $"{pagador.Nome} não é professor — a mensalidade é do plano de professor.";
        return null;
    }

    public static string? ProblemaComOTorneio(Torneio? torneio)
    {
        if (torneio == null)
            return "Escolha o torneio da taxa.";
        if (torneio.FormaPagamento != "Externo")
            return "Este torneio não é \"por fora\" — a taxa dele entra pelo gateway, no split.";
        if (torneio.TaxaExternoPagaEm != null)
            return "A taxa deste torneio já está paga.";
        return null;
    }

    public static string? ProblemaComADespesa(string? descricao, decimal valor)
    {
        if (string.IsNullOrWhiteSpace(descricao))
            return "Descreva a despesa — \"fatura do gateway de julho\", por exemplo.";
        if (valor <= 0)
            return "Informe o valor pago.";
        return null;
    }
}
