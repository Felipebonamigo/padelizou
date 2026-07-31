using Padelizou.Models;

namespace Padelizou.Services;

// As regras do estoque do bar que são conta, não banco.
//
// A premissa honesta deste módulo: estoque de bar NÃO é exato, e prometer exatidão é o jeito
// mais rápido de o dono parar de confiar no sistema. Garrafa quebra, funcionário consome,
// cerveja sai sem passar pela comanda no sábado lotado. O que mantém o saldo colado na
// prateleira não é precisão, é a CONTAGEM: dez minutos por semana acertando a diferença.
// Por isso perda e contagem são de primeira classe aqui, e não uma correção escondida.
public static class EstoqueDoBar
{
    // Quantas unidades de venda uma compra representa. Compra-se caixa com 24, vende-se lata:
    // é a conversão que evita o dono digitar 24 e ficar com 24 caixas no sistema.
    public static int UnidadesDaCompra(int quantidadeInformada, int unidadesPorEmbalagem, bool porEmbalagem)
    {
        if (!porEmbalagem) return quantidadeInformada;
        return quantidadeInformada * Math.Max(1, unidadesPorEmbalagem);
    }

    // O saldo é a soma dos movimentos, e nada mais. Ver MovimentoEstoque pra o porquê.
    public static int Saldo(IEnumerable<MovimentoEstoque> movimentos)
    {
        int total = 0;
        foreach (var m in movimentos) total += m.Quantidade;
        return total;
    }

    // O acerto depois de contar a prateleira: a diferença entre o que tem e o que o sistema
    // achava que tinha. Zero significa que bateu — e nesse caso não se grava movimento
    // nenhum, pra não encher o histórico de linhas que não dizem nada.
    public static int AjusteDaContagem(int saldoAtual, int contado) => contado - saldoAtual;

    public static string? ProblemaComEntrada(int quantidade, decimal? custoUnitario)
    {
        if (quantidade < 1) return "A quantidade precisa ser pelo menos 1.";
        if (quantidade > 100_000) return "Essa quantidade parece errada — confira.";
        if (custoUnitario is < 0) return "O custo não pode ser negativo.";
        if (custoUnitario is > 1_000_000m) return "Esse custo parece errado — confira.";
        return null;
    }

    public static string? ProblemaComPerda(int quantidade)
    {
        if (quantidade < 1) return "Quantas unidades se perderam?";
        if (quantidade > 100_000) return "Essa quantidade parece errada — confira.";
        return null;
    }

    public static string? ProblemaComContagem(int contado)
    {
        // Contagem negativa não existe: ou tem, ou não tem.
        if (contado < 0) return "A contagem não pode ser negativa.";
        if (contado > 1_000_000) return "Essa contagem parece errada — confira.";
        return null;
    }

    // Está na hora de comprar? Mínimo zero desliga o aviso — nem todo produto merece um.
    public static bool PrecisaRepor(int saldo, int estoqueMinimo) =>
        estoqueMinimo > 0 && saldo <= estoqueMinimo;

    // Quanto sobra de cada unidade vendida, em reais e em percentual. O custo é o da ÚLTIMA
    // entrada, não uma média: é o preço que o dono vai pagar na próxima compra, e é com ele
    // que a decisão de reajustar é tomada. Média ponderada seria mais "certa" na
    // contabilidade e menos útil na conversa.
    public static (decimal Lucro, decimal? Percentual)? Margem(decimal preco, decimal? ultimoCusto)
    {
        if (ultimoCusto == null) return null;

        var lucro = preco - ultimoCusto.Value;
        // Custo zero (brinde, cortesia) não tem percentual: dividir por zero não é "infinito"
        // numa tela, é uma conta que não faz sentido mostrar.
        decimal? percentual = ultimoCusto.Value > 0
            ? Math.Round(lucro / ultimoCusto.Value * 100m, 1)
            : null;

        return (lucro, percentual);
    }
}
