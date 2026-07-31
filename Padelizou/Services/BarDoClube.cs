namespace Padelizou.Services;

// As regras do bar que são conta, não banco. Ficam aqui, puras, porque são exatamente as que
// não podem estar erradas: número de comanda, conferência de caixa e o que impede fechar.
public static class BarDoClube
{
    // Como o cliente pagou. É REGISTRO do que aconteceu no balcão, não cobrança: dinheiro e
    // maquininha acontecem fora do sistema (mesma lógica do torneio "por fora"). O Padelizou
    // não confere nada disso — quem confere é a pessoa contando a gaveta no fim do dia.
    public const string Dinheiro = "Dinheiro";
    public const string Cartao = "Cartão";
    public const string Pix = "Pix";
    public const string Cortesia = "Cortesia";

    public static readonly string[] FormasDePagamento = { Dinheiro, Cartao, Pix, Cortesia };

    public static bool FormaValida(string? forma) =>
        !string.IsNullOrWhiteSpace(forma) && FormasDePagamento.Contains(forma);

    // O próximo número livre do dia. Não é "quantas comandas já abriram + 1": comanda
    // cancelada continua ocupando o número dela (é o que o papel do balcão diz), e reusar
    // faria duas comandas diferentes atenderem por "a 7" no mesmo turno.
    public static int ProximoNumero(IEnumerable<int> numerosDoDia)
    {
        int maior = 0;
        foreach (var n in numerosDoDia)
        {
            if (n > maior) maior = n;
        }
        return maior + 1;
    }

    // A única pergunta do fim do dia: **bate?**
    //
    // Esperado = troco de abertura + o que foi vendido EM DINHEIRO. Cartão e Pix não passam
    // pela gaveta, então não entram — somá-los faria o caixa "faltar" todo santo dia.
    // Diferença positiva sobra, negativa falta.
    public static (decimal Esperado, decimal Diferenca) ConferenciaDoCaixa(
        decimal valorAbertura, decimal vendasEmDinheiro, decimal valorContado)
    {
        var esperado = valorAbertura + vendasEmDinheiro;
        return (esperado, valorContado - esperado);
    }

    // Produto sem nome não dá pra vender, e preço negativo é sempre digitação errada. Preço
    // ZERO é válido de propósito: cortesia da casa e brinde existem, e o item precisa
    // aparecer na comanda pra o estoque bater depois.
    public static string? ProblemaComProduto(string? nome, decimal preco)
    {
        if (string.IsNullOrWhiteSpace(nome)) return "Dê um nome ao produto.";
        if (nome.Trim().Length > 80) return "O nome do produto é muito longo (máximo 80 caracteres).";
        if (preco < 0) return "O preço não pode ser negativo.";
        if (preco > 100_000m) return "Esse preço parece errado — confira o valor.";
        return null;
    }

    // Comanda só fecha com forma de pagamento declarada e desconto que faça sentido.
    // Desconto maior que a conta viraria "o bar deve dinheiro ao cliente", que não existe.
    public static string? ProblemaParaFechar(decimal subtotal, decimal desconto, string? forma)
    {
        if (!FormaValida(forma)) return "Escolha como o cliente pagou.";
        if (desconto < 0) return "O desconto não pode ser negativo.";
        if (desconto > subtotal) return "O desconto é maior que o valor da comanda.";
        return null;
    }

    // Quantidade lançada no balcão. O limite não é técnico: é que 999 cervejas numa linha só
    // é dedo escorregando no teclado numérico, e o erro vira cobrança absurda no fechamento.
    public static string? ProblemaComQuantidade(int quantidade)
    {
        if (quantidade < 1) return "A quantidade precisa ser pelo menos 1.";
        if (quantidade > 99) return "Quantidade acima de 99 — lance em linhas separadas.";
        return null;
    }
}
