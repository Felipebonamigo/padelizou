using Padelizou.Models;

namespace Padelizou.Services;

// Regras do pacote "nós registramos os resultados para você".
//
// O organizador contrata o Padelizou pra mandar gente lançar os jogos durante o torneio.
// Duas decisões moldam tudo:
//
//   1. É SOLICITAÇÃO, não compra. O botão diz "verificar disponibilidade" porque pode não
//      haver ninguém livre naquela data e naquela cidade. Vender antes de saber seria
//      prometer o que não temos.
//
//   2. O VALOR não aparece antes da resposta. Quanto custa depende de quantas pessoas
//      conseguimos, de onde elas vêm e de quantos dias — coisas que só sabemos ao checar.
//      Um preço estimado na tela viraria promessa na cabeça do organizador. O que a tela
//      mostra antes é o que é fato: quantas pessoas o torneio pede e por quantos dias.
public static class RegistroDeResultados
{
    // Uma pessoa dá conta de duas quadras: ela alterna entre as duas anotando cada game.
    // Acima disso começa a perder jogo, que é justamente o que o organizador está pagando
    // pra não acontecer.
    public static int PessoasSugeridas(int quadras, int quadrasPorPessoa)
    {
        if (quadrasPorPessoa < 1) quadrasPorPessoa = 2;
        if (quadras < 1) quadras = 1;

        return (int)Math.Ceiling(quadras / (double)quadrasPorPessoa);
    }

    // Torneio de um dia só tem DataFim nula ou igual ao início — em qualquer caso, 1 dia.
    public static int DiasDoTorneio(DateTime? inicio, DateTime? fim)
    {
        if (inicio == null) return 1;
        if (fim == null || fim.Value.Date <= inicio.Value.Date) return 1;

        return (int)(fim.Value.Date - inicio.Value.Date).TotalDays + 1;
    }

    // Quantos jogos o torneio inteiro vai ter — a soma das categorias.
    //
    // Desde 20/08/2026 o número manda só no CUSTO, não mais no preço: quem vai registrar
    // ganha por jogo lançado, então quem responde precisa dele pra saber por quanto não
    // vale a pena aceitar. Um Americano de um dia pode ter mais jogos que um torneio de
    // duplas de três — é por isso que o custo nunca foi por dia.
    public static int JogosPrevistos(IEnumerable<int> duplasPorCategoria) =>
        duplasPorCategoria.Where(d => d >= 2).Sum(PrevisaoDoTorneio.TotalDeJogos);

    // Nosso custo com a equipe. Não aparece pro organizador — é o piso pra quem responde a
    // solicitação saber por quanto NÃO vale a pena aceitar.
    public static decimal CustoEstimado(int jogos, decimal custoPorJogo) =>
        Math.Max(0, jogos) * custoPorJogo;

    // O preço pela regra publicada (20/08/2026): percentual SOBRE o valor das inscrições,
    // a mais da taxa da forma de recebimento. Mesma régua da taxa do Externo — pessoas ×
    // preço por pessoa (Services/TaxaDoTorneioExterno.PessoasInscritas) — de propósito: o
    // organizador compara com o concorrente em percentual, e duas bases diferentes pra
    // "valor das inscrições" seria a conta que ninguém confere.
    //
    // O mínimo continua: mandar alguém passar o dia custa o dia inteiro, tendo 10 ou 40
    // jogos — sem ele, torneio pequeno (ou gratuito) sairia no prejuízo.
    public static decimal PrecoSugerido(
        int pessoasInscritas, decimal precoPorPessoa, decimal percentual, decimal valorMinimo) =>
        Math.Max(
            Math.Round(Math.Max(0, pessoasInscritas) * precoPorPessoa * percentual / 100m, 2),
            valorMinimo);

    // A régua ANTIGA (R$ por jogo), viva só pros pedidos feitos antes de 20/08/2026: a
    // cotação foi congelada no pedido, e quem pediu por ela continua valendo o que leu.
    public static decimal PrecoSugeridoPorJogo(int jogos, decimal precoPorJogo, decimal valorMinimo) =>
        Math.Max(Math.Max(0, jogos) * precoPorJogo, valorMinimo);

    // A partir de quanto de inscrição (pessoas × preço) o percentual passa o mínimo. Abaixo
    // disso todo torneio paga o mesmo — e quem responde precisa saber disso pra não achar
    // que errou a conta quando dois pedidos diferentes dão o mesmo valor.
    public static decimal InscricoesParaSairDoMinimo(decimal percentual, decimal valorMinimo) =>
        percentual <= 0 ? 0 : Math.Round(valorMinimo * 100m / percentual, 2);

    public static string? ProblemaParaSolicitar(
        bool servicoHabilitado, bool jaTemSolicitacaoAberta,
        DateTime? dataInicio, DateTime hoje, int antecedenciaMinimaDias)
    {
        if (!servicoHabilitado)
            return "Este serviço está indisponível no momento.";

        if (jaTemSolicitacaoAberta)
            return "Já existe um pedido em aberto para este torneio.";

        if (dataInicio == null)
            return "Defina a data de início do torneio para pedirmos a equipe.";

        // Não dá pra achar, combinar e deslocar gente pra depois de amanhã. Prometer que dá
        // e falhar na véspera é pior que dizer não agora.
        if (dataInicio.Value.Date < hoje.Date.AddDays(antecedenciaMinimaDias))
            return $"Precisamos de pelo menos {antecedenciaMinimaDias} dias de antecedência "
                 + "para organizar a equipe. Fale com a gente pelo canal de suporte.";

        return null;
    }

    // Só pedido em aberto pode ser respondido: responder duas vezes deixaria o organizador
    // com duas versões do combinado, sem saber qual vale.
    public static string? ProblemaParaResponder(string statusAtual) =>
        statusAtual == SolicitacaoRegistroResultados.Solicitada
            ? null
            : $"Este pedido já foi respondido (está como \"{statusAtual}\").";

    public static string? ProblemaParaCancelar(string statusAtual) =>
        statusAtual is SolicitacaoRegistroResultados.Solicitada
                    or SolicitacaoRegistroResultados.Confirmada
            ? null
            : $"Não dá pra cancelar um pedido que está como \"{statusAtual}\".";

    public static string CorDoStatus(string status) => status switch
    {
        SolicitacaoRegistroResultados.Confirmada => "success",
        SolicitacaoRegistroResultados.Solicitada => "warning",
        SolicitacaoRegistroResultados.Concluida => "primary",
        _ => "secondary",
    };
}

// Configuração do serviço. Fica em appsettings pra dar pra DESLIGAR a oferta num aperto:
// se a equipe está toda ocupada num fim de semana, melhor sumir com o botão do que receber
// pedidos que vão todos virar "sem disponibilidade".
public class RegistroResultadosSettings
{
    public bool Habilitado { get; set; } = true;

    // Quantas quadras uma pessoa consegue acompanhar sozinha. Não entra no preço — serve só
    // pra saber quanta gente mandar.
    public int QuadrasPorPessoa { get; set; } = 2;

    // O que pagamos POR JOGO registrado. R$ 10 é a referência do mercado — é o que o sistema
    // concorrente paga a quem vai lançar os resultados. Só aparece no painel do admin.
    public decimal CustoPorJogo { get; set; } = 10m;

    // O que cobramos do organizador: percentual sobre o valor das inscrições (pessoas ×
    // preço por pessoa), A MAIS da taxa da forma de recebimento. Trocou o R$ 12 por jogo em
    // 20/08/2026: o concorrente que marca placar cobra percentual, e por jogo a comparação
    // saía mais cara justamente nos torneios grandes. ⚠️ O custo continua por jogo — em
    // inscrição barata com muitos jogos o percentual pode não cobrir o custo; o mínimo
    // segura parte disso, e o resto é decisão de quem responde (o valor é ajustável).
    public decimal PercentualDasInscricoes { get; set; } = 5m;

    // Piso do serviço. Mandar alguém passar o dia custa o dia inteiro, tendo 10 ou 40 jogos.
    // Também é o amortecedor de distância: clube longe encarece, e quem responde ajusta o
    // valor final no painel.
    public decimal ValorMinimo { get; set; } = 500m;

    // Antecedência mínima pra conseguir montar equipe.
    public int AntecedenciaMinimaDias { get; set; } = 7;
}
