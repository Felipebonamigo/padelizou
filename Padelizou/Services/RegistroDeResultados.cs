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
// Uma categoria, do jeito que a CONTAGEM DE JOGOS precisa dela: quantos entraram no sorteio
// e se ela é chave direta. O formato é do torneio inteiro, por isso não está aqui.
public readonly record struct CategoriaParaContar(bool ChaveDireta, int Inscritos);

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

    // Quantos jogos o torneio inteiro vai ter — a soma das categorias, cada uma pela régua
    // do sorteio dela (PrevisaoDoTorneio.JogosDaCategoria).
    //
    // ⚠️ DESDE 23/09/2026 ESTE NÚMERO É O PREÇO, e não mais só o custo. Enquanto foi custo,
    // ele vivia num painel que só o raiz abre e errar era uma linha torta pra quem responde.
    // Agora ele multiplica por R$ 12 e vira a conta do organizador — foi por isso que os dois
    // buracos da contagem (Americano contando zero, chave direta contando grupos que não
    // existem) deixaram de ser cosméticos e viraram trabalho.
    public static int JogosPrevistos(string? formato, IEnumerable<CategoriaParaContar> categorias) =>
        categorias.Sum(c => PrevisaoDoTorneio.JogosDaCategoria(formato, c.ChaveDireta, c.Inscritos));

    // Nosso custo com a equipe. Não aparece pro organizador — é o piso pra quem responde a
    // solicitação saber por quanto NÃO vale a pena aceitar.
    public static decimal CustoEstimado(int jogos, decimal custoPorJogo) =>
        Math.Max(0, jogos) * custoPorJogo;

    // O preço pela regra publicada (23/09/2026): R$ por JOGO lançado, a mais da taxa da
    // forma de recebimento. 🗣️ Felipe: *"mude o sistema, para que seja 12 reais por jogo, no
    // lugar de 10% para marcarmos os placares"*.
    //
    // A unidade é a mesma do CUSTO, e é esse o ganho: pelo percentual o preço seguia as
    // inscrições enquanto o custo seguia os jogos, e inscrição barata com muitos jogos saía
    // abaixo do custo — o risco estava escrito como "aceito" no teste desta régua.
    //
    // O mínimo continua: mandar alguém passar o dia custa o dia inteiro, tendo 10 ou 40
    // jogos — sem ele, torneio pequeno (ou gratuito) sairia no prejuízo.
    public static decimal PrecoSugeridoPorJogo(int jogos, decimal precoPorJogo, decimal valorMinimo) =>
        Math.Max(Math.Max(0, jogos) * precoPorJogo, valorMinimo);

    // A régua PERCENTUAL (20/08 a 23/09/2026), viva só pros pedidos cotados nela: a cotação
    // congela no pedido (SolicitacaoRegistroResultados.PercentualCotado), e quem pediu a 5%
    // ou a 10% continua valendo o que leu na tela. A base é a mesma da taxa do Externo —
    // pessoas × preço por pessoa (Services/TaxaDoTorneioExterno.PessoasInscritas).
    public static decimal PrecoSugerido(
        int pessoasInscritas, decimal precoPorPessoa, decimal percentual, decimal valorMinimo) =>
        Math.Max(
            Math.Round(Math.Max(0, pessoasInscritas) * precoPorPessoa * percentual / 100m, 2),
            valorMinimo);

    // A partir de quantos jogos o preço passa o mínimo. Abaixo disso todo torneio paga o
    // mesmo — e quem responde precisa saber disso pra não achar que errou a conta quando dois
    // pedidos de tamanhos diferentes dão o mesmo valor.
    public static int JogosParaSairDoMinimo(decimal precoPorJogo, decimal valorMinimo) =>
        precoPorJogo <= 0 ? 0 : (int)Math.Ceiling(valorMinimo / precoPorJogo);

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

    // O que cobramos do organizador POR JOGO lançado, a mais da taxa da forma de recebimento.
    //
    // 23/09/2026 (Felipe): voltou a ser por jogo, desfazendo o percentual que valeu de 20/08
    // (5%) a 26/08 (10%). Com a inscrição média em R$ 150, os 10% cobravam R$ 15 por pessoa
    // contra ~R$ 8 por jogo — e, mais que o número, o percentual andava solto do custo, que
    // sempre foi por jogo. ⚠️ A margem é de R$ 2 por jogo (CustoPorJogo = 10): quem responde
    // ajusta o valor final no painel, e é o mínimo que segura clube longe e torneio pequeno.
    //
    // ⚠️ Pedido cotado em percentual NÃO é recalculado — a cotação congela no pedido
    // (SolicitacaoRegistroResultados.PercentualCotado), que é a mesma promessa que protegeu
    // quem tinha pedido por jogo quando o percentual entrou.
    public decimal PrecoPorJogo { get; set; } = 12m;

    // Piso do serviço. Mandar alguém passar o dia custa o dia inteiro, tendo 10 ou 40 jogos.
    // Também é o amortecedor de distância: clube longe encarece, e quem responde ajusta o
    // valor final no painel.
    public decimal ValorMinimo { get; set; } = 500m;

    // Antecedência mínima pra conseguir montar equipe.
    public int AntecedenciaMinimaDias { get; set; } = 7;
}
