using Padelizou.Models;

namespace Padelizou.Services;

// Quanto o parceiro comercial tem a receber. A régua está em PARCEIROS.md e é curta:
// 30% da comissão da PRIMEIRA venda, 10% do que vier depois, por 12 MESES — nada vitalício.
//
// Fica aqui, e não no controller, porque a mesma conta vai ser lida por dois olhos diferentes
// (o Felipe vendo todo mundo, o parceiro vendo só ele) e uma segunda cópia da regra é a causa
// número um dos bugs graves deste sistema.
public static class ComissaoDoParceiro
{
    public const int MesesDeComissao = 12;
    public const decimal PercentualDaPrimeiraVenda = 30m;
    public const decimal PercentualRecorrente = 10m;

    // No professor o bônus de estreia é um valor fixo, e não um percentual: 10% de R$ 49,90
    // seriam R$ 4,99 pelo trabalho inteiro de trazer um cliente novo.
    public const decimal BonusDeEstreiaDoProfessor = 50m;

    // Só o que virou dinheiro conta. "Pendente" é promessa e "Estornado" é dinheiro que voltou.
    public const string StatusQueConta = "Confirmado";

    // As duas cobranças que são 100% nossas e onde o cliente é quem PAGA, não quem recebe.
    public static readonly string[] TiposDeMensalidade = { "AssinaturaProfessor" };

    // ⚠️ DE QUEM É O CLIENTE NESTE PAGAMENTO — a pergunta que o cálculo inteiro depende, e ela
    // tem DUAS respostas no banco:
    //
    // - Inscrição, aula e quadra: quem nos interessa é o `RecebedorId` (o organizador, o
    //   professor, o dono do clube). O `JogadorId` ali é o jogador que se inscreveu, e
    //   atribuir por ele daria a comissão do parceiro por causa de um cliente do cliente.
    // - Mensalidade do professor e taxa do torneio externo: `RecebedorId` é NULO (o valor
    //   inteiro é nosso, não há repasse) e quem paga É o cliente.
    //
    // A troca é segura porque os únicos pagamentos com recebedor nulo são esses dois — está
    // dito no próprio PagamentoInscricaoService ("Só mensalidade e taxa do externo, 100%
    // receita nossa"). Se algum dia uma inscrição nascer sem recebedor, esta linha passa a
    // premiar a pessoa errada em silêncio.
    public static int ClienteDoPagamento(Pagamento p) => p.RecebedorId ?? p.JogadorId;

    // O estorno tem que derrubar a comissão junto, senão o parceiro é pago por uma venda que
    // foi desfeita. Parcial reduz na proporção do que voltou.
    public static decimal ComissaoLiquida(Pagamento p)
    {
        if (p.Status != StatusQueConta) return 0m;
        if (p.Valor <= 0) return 0m;

        var sobrou = p.Valor - p.ValorEstornado;
        if (sobrou <= 0) return 0m;

        return Math.Round(p.Comissao * (sobrou / p.Valor), 2);
    }

    public static bool EhMensalidade(Pagamento p) => TiposDeMensalidade.Contains(p.Tipo);

    public record Parcela(Pagamento Pagamento, decimal Percentual, decimal Valor, string Motivo);

    public record Conta(
        DateTime? PrimeiroPagamento,
        DateTime? FimDaJanela,
        decimal Total,
        List<Parcela> Parcelas)
    {
        public static readonly Conta Vazia = new(null, null, 0m, new List<Parcela>());

        // O que ainda dá pra ganhar com este cliente. Negativo nunca — zero quer dizer
        // "acabou", e é o que a tela precisa dizer com todas as letras.
        public int DiasQueRestam(DateTime agora) =>
            FimDaJanela == null ? 0 : Math.Max(0, (int)Math.Ceiling((FimDaJanela.Value - agora).TotalDays));

        public bool AindaRende(DateTime agora) => FimDaJanela != null && FimDaJanela.Value > agora;
    }

    // `pagamentosDoCliente` já vem filtrado por cliente. A ordem não importa: a primeira venda
    // é decidida aqui, pela data de confirmação.
    public static Conta Calcular(string tipoDoLead, IEnumerable<Pagamento> pagamentosDoCliente)
    {
        var pagos = pagamentosDoCliente
            .Where(p => p.Status == StatusQueConta && p.ConfirmadoEm != null && ComissaoLiquida(p) > 0)
            .OrderBy(p => p.ConfirmadoEm!.Value)
            .ToList();

        if (pagos.Count == 0) return Conta.Vazia;

        // ⚠️ A JANELA CONTA DO PRIMEIRO PAGAMENTO CONFIRMADO, e não da data em que o contato
        // foi fechado na tela: quem fecha o lead é o Felipe, quando lembra. Amarrar o relógio
        // do dinheiro a um clique manual faria o parceiro ganhar ou perder meses conforme a
        // agenda de outra pessoa.
        var primeiro = pagos[0].ConfirmadoEm!.Value;
        var fim = primeiro.AddMonths(MesesDeComissao);

        var dentro = pagos.Where(p => p.ConfirmadoEm!.Value < fim).ToList();

        var parcelas = tipoDoLead switch
        {
            "Torneio" => PorEdicaoDeTorneio(dentro),
            "Professor" => ComBonusDeEstreia(dentro),
            _ => ComPrimeiraMensalidadeInteira(dentro),
        };

        return new Conta(primeiro, fim, parcelas.Sum(p => p.Valor), parcelas);
    }

    // Torneio: a PRIMEIRA EDIÇÃO paga 30%, as seguintes 10%. "Edição" é o torneio, não o
    // pagamento — um torneio de 32 duplas são 32 cobranças e uma edição só.
    private static List<Parcela> PorEdicaoDeTorneio(List<Pagamento> pagos)
    {
        // O torneio da primeira cobrança confirmada é a estreia. Pagamento sem torneio (não
        // deveria existir num cliente de torneio) cai no recorrente, que é o lado seguro:
        // errar pra menos aqui devolve dinheiro ao Padelizou, errar pra mais sai do caixa.
        var primeiroTorneio = pagos.FirstOrDefault(p => p.TorneioId != null)?.TorneioId;

        return pagos.Select(p =>
        {
            var estreia = primeiroTorneio != null && p.TorneioId == primeiroTorneio;
            var percentual = estreia ? PercentualDaPrimeiraVenda : PercentualRecorrente;
            var motivo = estreia ? "1ª edição" : "edição seguinte";
            return new Parcela(p, percentual, Math.Round(ComissaoLiquida(p) * percentual / 100m, 2), motivo);
        }).ToList();
    }

    // Professor: 10% de tudo, mais R$ 50 fixos quando a primeira mensalidade é paga.
    private static List<Parcela> ComBonusDeEstreia(List<Pagamento> pagos)
    {
        var primeiraMensalidade = pagos.FirstOrDefault(EhMensalidade);

        return pagos.Select(p =>
        {
            var valor = Math.Round(ComissaoLiquida(p) * PercentualRecorrente / 100m, 2);
            if (p == primeiraMensalidade)
                return new Parcela(p, PercentualRecorrente,
                    valor + BonusDeEstreiaDoProfessor, $"1ª mensalidade — 10% + R$ {BonusDeEstreiaDoProfessor:N0} de estreia");

            return new Parcela(p, PercentualRecorrente, valor, "recorrente");
        }).ToList();
    }

    // Clube: a primeira MENSALIDADE vem inteira, o resto é 10%.
    //
    // ⚠️ Hoje isso nunca dispara, e é de propósito: **não existe plano de clube no código**
    // (o preço nem foi fechado — ver PARCEIROS.md). Um clube só gera pagamento de reserva, que
    // é recorrente. Quando a mensalidade do clube nascer, basta o tipo dela entrar em
    // `TiposDeMensalidade` e a estreia passa a ser paga sozinha.
    private static List<Parcela> ComPrimeiraMensalidadeInteira(List<Pagamento> pagos)
    {
        var primeiraMensalidade = pagos.FirstOrDefault(EhMensalidade);

        return pagos.Select(p =>
        {
            if (p == primeiraMensalidade)
                return new Parcela(p, 100m, ComissaoLiquida(p), "1ª mensalidade, inteira");

            return new Parcela(p, PercentualRecorrente,
                Math.Round(ComissaoLiquida(p) * PercentualRecorrente / 100m, 2), "recorrente");
        }).ToList();
    }

    // O mês em que cada parcela caiu — é assim que o repasse é fechado (dia 30) e pago (dia 10
    // do mês seguinte). O mês corrente aparece separado porque ainda pode crescer.
    public static bool EhDoMesCorrente(Parcela parcela, DateTime agora)
    {
        var quando = parcela.Pagamento.ConfirmadoEm;
        return quando != null && quando.Value.Year == agora.Year && quando.Value.Month == agora.Month;
    }
}
