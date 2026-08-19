namespace Padelizou.Services;

// O que cada item do cardápio é aos olhos de quem emite a nota — e os palpites que poupam o
// dono do clube de abrir a tabela da Receita pra cadastrar uma cerveja.
//
// A pergunta que manda em tudo é a PRIMEIRA, e ela não é fiscal, é de produto: **o que este
// item é?** O cardápio de um bar de clube mistura três naturezas na mesma lista de botões —
// a lata de cerveja foi comprada pronta e revendida, a porção de fritas o próprio bar
// produziu, e o aluguel de raquete não é venda de coisa nenhuma. Sem responder isso, nenhuma
// camada fiscal consegue nem escolher QUAL documento emitir, e é por isso que o tipo vem
// antes de NCM, CFOP e todo o resto.
//
// ⚠️ REGRA DA CASA: aqui se SUGERE, nunca se decide. Quem responde pela tributação é o
// contribuinte — o clube — e o contador dele. Um palpite gravado em silêncio vira autuação
// no nome do cliente, então todo campo sugerido nasce editável e a tela diz de onde veio.
public static class FiscalDoProduto
{
    // Comprou pronto, vende como está: lata, garrafa, barra de cereal. Sai em NFC-e.
    public const string Revenda = "Revenda";

    // O bar produziu: porção, lanche, caipirinha. Também sai em NFC-e, mas com CFOP de
    // produção própria — é operação diferente, ainda que o cliente não veja diferença nenhuma.
    public const string ProducaoPropria = "Producao";

    // Serviço lançado na comanda (aula avulsa, day-use). Sai em NFS-e, na prefeitura — nunca
    // em NFC-e, que é documento estadual de mercadoria.
    public const string Servico = "Servico";

    // Locação de bem móvel: o "aluguel de raquete" que o próprio ProdutoBar cita como exemplo
    // de item do bar. Não é mercadoria (nada muda de dono) e o STF afastou o ISS da locação de
    // bem móvel na Súmula Vinculante 31 — ou seja, não cabe em NFC-e nem em NFS-e. Na prática
    // vira recibo. Está aqui NOMEADO justamente pra não ser emitido por engano como um dos
    // outros dois. ⚠️ Caso clássico de conferir com o contador do clube antes de faturar.
    public const string Locacao = "Locacao";

    public static readonly string[] Tipos = { Revenda, ProducaoPropria, Servico, Locacao };

    public static string RotuloDoTipo(string? tipo) => tipo switch
    {
        Revenda => "Revenda (comprei pronto)",
        ProducaoPropria => "Produção do bar",
        Servico => "Serviço",
        Locacao => "Aluguel",
        _ => "Não definido"
    };

    // Qual documento cabe. É esta função que impede a caipirinha e a raquete de irem parar na
    // mesma nota por descuido.
    public static string DocumentoDoTipo(string? tipo) => tipo switch
    {
        Revenda or ProducaoPropria => "NFC-e",
        Servico => "NFS-e",
        Locacao => "Nenhum",
        _ => "Nenhum"
    };

    public static bool TipoValido(string? tipo) => tipo != null && Tipos.Contains(tipo);

    // 5102 = revenda de mercadoria de terceiros; 5101 = venda de produção do estabelecimento.
    // Ambos dentro do estado, que é o caso de 100% das vendas de um balcão de clube.
    public static string? CfopSugerido(string? tipo) => tipo switch
    {
        Revenda => "5102",
        ProducaoPropria => "5101",
        _ => null
    };

    // O palpite de NCM cobre só o que é 80% dos toques do dia no balcão — a mesma lista que
    // faz o ProdutoBar ter campo de ordem manual. Ir além disso seria fingir uma tabela fiscal
    // completa que envelhece sozinha e erra calada; o resto o clube preenche com o contador,
    // uma vez na vida por produto.
    //
    // ⚠️ AS MARCAS ESTÃO AQUI DE PROPÓSITO, e essa é a parte que quase ficou de fora. A
    // primeira versão procurava só a palavra "cerveja" — e no cardápio de um bar de verdade
    // não existe produto chamado "cerveja": existe "Heineken lata 350ml", "Brahma 600". Um
    // palpite que só acerta o nome genérico é um palpite que nunca dispara, e aí a tela
    // promete ajuda que não entrega. Foi um teste que pegou isso, não a leitura do código.
    public static string? NcmSugerido(string? nomeDoProduto)
    {
        var nome = (nomeDoProduto ?? "").ToLowerInvariant();

        if (Tem(nome, "cerveja", "chopp", "chope", "heineken", "brahma", "skol", "antarctica",
                "budweiser", "corona", "stella", "amstel", "itaipava", "eisenbahn", "spaten",
                "originl", "original", "becks", "patagonia"))
            return "22030000";

        if (Tem(nome, "refrigerante", "refri", "guaran", "coca", "pepsi", "fanta", "sprite",
                "soda", "tônica", "tonica", "schweppes"))
            return "22021000";

        if (Tem(nome, "energ", "red bull", "redbull", "monster"))
            return "22029900";

        if (Tem(nome, "água", "agua"))
            return "22011000";

        return null;
    }

    private static bool Tem(string nome, params string[] pedacos) => pedacos.Any(nome.Contains);

    // A unidade da nota é a mesma unidade de VENDA que o estoque já conta (ver
    // EstoqueDoBar): a lata que o cliente pede, não a caixa que o bar compra.
    public static string UnidadeSugerida(string? nomeDoProduto)
    {
        var nome = (nomeDoProduto ?? "").ToLowerInvariant();
        return nome.Contains("chopp") || nome.Contains("chope") ? "LT" : "UN";
    }

    // ---- Validação do que foi digitado ----
    //
    // Só formato, e de propósito: se o NCM existe na tabela da Receita, se a alíquota bate,
    // se o CEST corresponde àquele NCM — nada disso dá pra responder daqui, e fingir que dá
    // seria a pior das mentiras. O que a tela CONSEGUE garantir é que ninguém grave "1234"
    // num campo de 8 dígitos e descubra na primeira venda com fila.
    public static string? ProblemaComDadosFiscais(string? tipo, string? ncm, string? cfop,
        string? cest, string? codigoBarras, int? origem)
    {
        if (tipo != null && !TipoValido(tipo)) return "Escolha o que este item é.";

        if (!SoDigitosCom(ncm, 8)) return "O NCM tem 8 dígitos.";
        if (!SoDigitosCom(cfop, 4)) return "O CFOP tem 4 dígitos.";
        if (!SoDigitosCom(cest, 7)) return "O CEST tem 7 dígitos.";

        if (!string.IsNullOrWhiteSpace(codigoBarras) && !Documentos.CodigoDeBarrasEhValido(codigoBarras))
            return "Esse código de barras não confere — leia de novo ou deixe em branco.";

        if (origem != null && (origem < 0 || origem > 8)) return "A origem da mercadoria vai de 0 a 8.";

        return null;
    }

    // Vazio passa: o cadastro fiscal é opcional até o clube querer emitir, e recusar campo em
    // branco travaria o cardápio de quem usa o bar só como controle interno.
    private static bool SoDigitosCom(string? valor, int quantos)
    {
        if (string.IsNullOrWhiteSpace(valor)) return true;
        var limpo = Documentos.SomenteDigitos(valor);
        return limpo.Length == quantos && limpo == valor.Trim();
    }

    // O que ainda falta pra ESTE item poder sair numa nota. Lista vazia = pronto.
    //
    // Devolve frases, e não códigos de erro, porque quem lê é o dono do bar na tela do
    // cardápio — "falta o NCM" ele resolve com o contador; `ERR_NCM_MISSING` ele ignora.
    public static IReadOnlyList<string> FaltaParaEmitir(string? tipo, string? ncm, string? cfop, string? unidade)
    {
        var falta = new List<string>();

        if (!TipoValido(tipo))
        {
            falta.Add("dizer o que este item é (revenda, produção, serviço ou aluguel)");
            return falta;   // sem o tipo, cobrar NCM é cobrar o campo errado
        }

        // Aluguel não emite nada — e por isso não falta nada nele. Dizer "falta o NCM" aqui
        // mandaria o clube caçar uma classificação que não existe pro caso dele.
        if (tipo == Locacao) return falta;

        if (tipo is Revenda or ProducaoPropria)
        {
            if (string.IsNullOrWhiteSpace(ncm)) falta.Add("o NCM (classificação da mercadoria)");
            if (string.IsNullOrWhiteSpace(cfop)) falta.Add("o CFOP (natureza da operação)");
            if (string.IsNullOrWhiteSpace(unidade)) falta.Add("a unidade (UN, LT, KG...)");
        }

        return falta;
    }
}
