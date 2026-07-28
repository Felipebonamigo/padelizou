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

    // Nosso custo com a equipe. Não aparece pro organizador — é o piso pra quem responde a
    // solicitação saber por quanto NÃO vale a pena aceitar.
    public static decimal CustoEstimado(int pessoas, int dias, decimal custoPorPessoaPorDia) =>
        pessoas * dias * custoPorPessoaPorDia;

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

    // Quantas quadras uma pessoa consegue acompanhar sozinha.
    public int QuadrasPorPessoa { get; set; } = 2;

    // O que pagamos a cada pessoa por dia de torneio. R$ 10 é a referência do mercado — é o
    // que o sistema concorrente paga a quem vai registrar. Só aparece no painel do admin.
    public decimal CustoPorPessoaPorDia { get; set; } = 10m;

    // Antecedência mínima pra conseguir montar equipe.
    public int AntecedenciaMinimaDias { get; set; } = 7;
}
