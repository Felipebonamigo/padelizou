using Padelizou.Models;
using padelizou.Models;

namespace Padelizou.Services;

// PAGO / NÃO PAGO DE CADA JOGADOR DE UMA DUPLA (11/11/2026).
//
// 🗣️ Felipe: "pôr o pago/não pago e cobrar para jogador individualmente também e a opção a
// dupla pagou" · "normalmente a cobrança é individual" · "o valor da inscrição é colocado em
// todo torneio".
//
// ⚠️ É LEITURA DERIVADA, e não um campo novo por posição: o jogador está pago quando (a) a
// inscrição inteira está paga (`Dupla.Pago` — o "Dupla pagou", o webhook que quitou, a junção
// de inscrições), (b) o organizador o marcou (Models/PagamentoManualDoJogador) ou (c) entrou
// pagamento CONFIRMADO dele nesta inscrição pelo app ("pagar só a minha parte"). Assim nenhum
// dos lugares que já mexem em `Dupla.Pago` precisou mudar.
public static class PagamentoPorJogador
{
    public static bool Pagou(Dupla dupla, int jogadorId, IEnumerable<PagamentoManualDoJogador> manuais,
        IEnumerable<Pagamento> confirmadosDaDupla) =>
        dupla.Pago
        || manuais.Any(m => m.DuplaId == dupla.Id && m.JogadorId == jogadorId)
        || confirmadosDaDupla.Any(p => p.JogadorId == jogadorId && p.Status == "Confirmado"
                                       && p.ReferenciaId == dupla.Id);

    // Todos os jogadores ATUAIS da inscrição pagos? (sem parceiro: só o que existe)
    public static bool TodosPagaram(Dupla dupla, IEnumerable<PagamentoManualDoJogador> manuais,
        IEnumerable<Pagamento> confirmadosDaDupla) =>
        Pagou(dupla, dupla.Jogador1Id, manuais, confirmadosDaDupla)
        && (dupla.Jogador2Id is not int j2 || Pagou(dupla, j2, manuais, confirmadosDaDupla));

    // QUANTO É A PARTE DE CADA UM — o valor de inscrição que o ORGANIZADOR pôs no torneio, por
    // pessoa (com o preço de segunda inscrição pra quem já está em outra categoria, como na
    // inscrição). O que sobra até o total gravado da inscrição (taxa de impedimento, que é da
    // dupla, ou preço que mudou depois) é dividido ao meio; o centavo ímpar fica com o
    // primeiro. A soma das duas partes é SEMPRE o total — a cobrança de cada um nunca deixa
    // dinheiro de fora nem cobra a mais.
    public static (decimal Jogador1, decimal Jogador2) Partes(Torneio torneio, Dupla dupla,
        bool jogador1JaNoTorneio, bool jogador2JaNoTorneio)
    {
        decimal total = PrecoDaInscricao.DaDupla(torneio, dupla);
        if (dupla.Jogador2Id == null) return (total, 0m);

        decimal p1 = PrecoDaInscricao.PorPessoa(torneio, jogador1JaNoTorneio);
        decimal p2 = PrecoDaInscricao.PorPessoa(torneio, jogador2JaNoTorneio);
        decimal metadeDoResto = Math.Floor((total - p1 - p2) / 2m * 100m) / 100m;

        decimal parte2 = p2 + metadeDoResto;
        return (total - parte2, parte2);
    }

    public sealed record Situacao(bool Jogador1Pago, bool Jogador2Pago, decimal Parte1, decimal Parte2);

    // O que a aba de inscritos precisa de cada dupla: quem pagou e a parte de cada um.
    //
    // "Já estava no torneio" = o jogador tem outra inscrição DESTE torneio criada antes desta
    // (Id menor) — a mesma pergunta que a inscrição fez pra decidir o preço de segunda.
    public static Dictionary<int, Situacao> Retrato(Torneio torneio, IEnumerable<Dupla> duplas,
        IEnumerable<PagamentoManualDoJogador> manuais, IEnumerable<Pagamento> confirmados)
    {
        var lista = duplas.ToList();
        var marcas = manuais.ToList();
        var porDupla = confirmados.Where(p => p.ReferenciaId != null).ToLookup(p => p.ReferenciaId!.Value);

        bool JaEstava(int jogadorId, int duplaId) =>
            lista.Any(d => d.Id < duplaId && (d.Jogador1Id == jogadorId || d.Jogador2Id == jogadorId));

        var retrato = new Dictionary<int, Situacao>();
        foreach (var d in lista)
        {
            var dela = porDupla[d.Id].ToList();
            var (p1, p2) = Partes(torneio, d, JaEstava(d.Jogador1Id, d.Id),
                d.Jogador2Id is int j2 && JaEstava(j2, d.Id));
            retrato[d.Id] = new Situacao(
                Pagou(d, d.Jogador1Id, marcas, dela),
                d.Jogador2Id is int outro && Pagou(d, outro, marcas, dela),
                p1, p2);
        }
        return retrato;
    }
}
