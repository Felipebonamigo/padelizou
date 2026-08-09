using Padelizou.Models;

namespace Padelizou.Services;

// Devolver PARTE do dinheiro de uma cobrança já paga.
//
// Existe separado porque o estorno parcial mexe em três números que precisam continuar
// fechando entre si — o que o jogador pagou, o que o organizador leva e a comissão — e
// porque o gateway devolve o SPLIT junto: estornar R$ 125 de uma cobrança de R$ 250 tira
// metade da fatia de CADA UM, não só a nossa. Tirar do organizador sem esse cálculo seria
// devolver ao jogador um dinheiro que já saiu da conta de outra pessoa.
//
// Nasceu de um caso real (09/08/2026): a inscrição do Felipe no NATA PADEL TOUR foi cobrada
// R$ 250 quando o preço de quem entra sem parceiro virou R$ 125 poucas horas depois. O
// estorno que existia era só TOTAL, e total ali significaria apagar a inscrição junto.
public static class EstornoParcial
{
    // O que sobra da cobrança depois de devolver `aDevolver`.
    public sealed record Sobra(decimal Valor, decimal Repasse, decimal Comissao);

    // Por que não existe "ValorOriginal - somaDosEstornos": depois de um estorno o Pagamento
    // passa a guardar o valor EFETIVO (ver Pagamento.Valor), então o que ainda dá pra devolver
    // é o próprio Valor. Assim todo relatório que já somava Valor/Repasse/Comissao continua
    // certo sem saber que estorno existe.
    public static decimal DisponivelParaEstorno(Pagamento pagamento) =>
        pagamento.Status == "Confirmado" ? pagamento.Valor : 0m;

    // Null = pode estornar esse valor. Texto = o motivo pra recusar, pronto pra tela.
    public static string? ProblemaParaEstornar(Pagamento pagamento, decimal aDevolver)
    {
        if (pagamento.Status != "Confirmado")
            return "Só dá pra devolver parte de uma cobrança que já foi paga.";

        if (aDevolver <= 0)
            return "Informe quanto devolver.";

        if (aDevolver > DisponivelParaEstorno(pagamento))
            return $"Esta cobrança tem {DisponivelParaEstorno(pagamento):C} disponíveis para devolver.";

        return null;
    }

    // Devolver exatamente o que resta É o estorno total — e total tem outra consequência (a
    // inscrição é desfeita), então quem chama precisa saber distinguir os dois.
    public static bool EhTotal(Pagamento pagamento, decimal aDevolver) =>
        aDevolver >= DisponivelParaEstorno(pagamento);

    public static Sobra Calcular(decimal valor, decimal repasse, decimal comissao, decimal aDevolver)
    {
        if (valor <= 0) return new Sobra(0m, 0m, 0m);

        var devolver = Math.Clamp(aDevolver, 0m, valor);

        // A comissão é arredondada e o repasse sai por DIFERENÇA. Arredondar os dois
        // separadamente deixaria repasse + comissão diferente do valor por um centavo, e é
        // esse centavo que faz o extrato do organizador não bater com a fatura.
        var comissaoDevolvida = Math.Round(comissao * (devolver / valor), 2, MidpointRounding.AwayFromZero);

        var novoValor = valor - devolver;
        var novaComissao = Math.Clamp(comissao - comissaoDevolvida, 0m, novoValor);

        return new Sobra(novoValor, novoValor - novaComissao, novaComissao);
    }

    // Quanto da devolução sai da fatia do ORGANIZADOR (o resto sai da comissão).
    //
    // ⚠️ Isto não é informativo: o gateway PRECISA receber este número. Num estorno parcial ele
    // tira o valor todo da fatia da conta principal e recusa se não couber — foi o que
    // aconteceu ao tentar devolver R$ 125 de uma cobrança cuja comissão era R$ 37,50
    // ("Valor da cobrança insuficiente para o estorno solicitado", 09/08/2026). O split só se
    // reverte sozinho no estorno TOTAL.
    public static decimal ParteDoRepasse(Pagamento pagamento, decimal aDevolver)
    {
        var sobra = Calcular(pagamento.Valor, pagamento.ValorRepasse, pagamento.Comissao, aDevolver);
        return pagamento.ValorRepasse - sobra.Repasse;
    }

    // Aplica a sobra no registro e acumula quanto já voltou pro jogador.
    public static void Aplicar(Pagamento pagamento, decimal aDevolver)
    {
        var sobra = Calcular(pagamento.Valor, pagamento.ValorRepasse, pagamento.Comissao, aDevolver);

        pagamento.ValorEstornado += pagamento.Valor - sobra.Valor;
        pagamento.Valor = sobra.Valor;
        pagamento.ValorRepasse = sobra.Repasse;
        pagamento.Comissao = sobra.Comissao;
    }
}
