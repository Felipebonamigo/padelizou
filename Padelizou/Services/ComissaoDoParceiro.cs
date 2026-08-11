using Padelizou.Models;

namespace Padelizou.Services;

// Quanto o parceiro comercial tem a receber. A régua está em PARCEIROS.md e é a mesma pros
// dois produtos que existem: **20% da PRIMEIRA venda, 10% do que vier depois, por 12 MESES** —
// nada vitalício. No torneio a primeira venda é a primeira EDIÇÃO; no professor é a primeira
// MENSALIDADE.
//
// Fica aqui, e não no controller, porque a mesma conta vai ser lida por dois olhos diferentes
// (o Felipe vendo todo mundo, o parceiro vendo só ele) e uma segunda cópia da regra é a causa
// número um dos bugs graves deste sistema.
public static class ComissaoDoParceiro
{
    public const int MesesDeComissao = 12;

    // 20%, e não 30%, por decisão do Felipe em 11/08/2026. O que derrubou o 30 não foi o
    // percentual: foi o TAMANHO REAL do torneio. A régua tinha sido calibrada em cima de um
    // torneio de 32 duplas, que praticamente não existe — os de verdade têm 45 a 110. Nesse
    // tamanho, 20% da estreia dá R$ 135 a R$ 330, e o parceiro segue bem pago.
    public const decimal PercentualDaPrimeiraVenda = 20m;

    public const decimal PercentualRecorrente = 10m;

    // Só o que virou dinheiro conta. "Pendente" é promessa e "Estornado" é dinheiro que voltou.
    public const string StatusQueConta = "Confirmado";

    // As duas cobranças que são 100% nossas e onde o cliente é quem PAGA, não quem recebe.
    public static readonly string[] TiposDeMensalidade = { "AssinaturaProfessor" };

    // ⚠️ O PARCEIRO GANHA SÓ DA FRENTE QUE ELE VENDEU (decisão do Felipe, 11/08/2026). Quem
    // vendeu um TORNEIO não ganha da mensalidade de professor da mesma pessoa: aquela venda
    // não foi dele. Sem este filtro, um lead de torneio rendia sobre tudo que o cliente
    // gerasse pra sempre — inclusive frentes que outro parceiro (ou o próprio Felipe) vendeu.
    //
    // Torneio é reconhecido pelo `TorneioId`, e não por lista de tipo: toda cobrança de
    // torneio carrega o id (inscrição de dupla, individual, "pagar depois" e a taxa do
    // externo), e uma lista de strings esqueceria a próxima frente de torneio que nascer.
    public static readonly string[] TiposDoProfessor = { "AssinaturaProfessor", "Aula" };
    public static readonly string[] TiposDoClube = { "Jogo", "JogoVarios" };

    public static bool EhDaFrente(string tipoDoLead, Pagamento p) => tipoDoLead switch
    {
        "Torneio" => p.TorneioId != null,
        "Professor" => TiposDoProfessor.Contains(p.Tipo),
        "Clube" => TiposDoClube.Contains(p.Tipo) || TiposDeMensalidade.Contains(p.Tipo),
        _ => false,
    };

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
        // Três filtros, e cada um é uma frase da régua:
        //   "do que ele vendeu"  → EhDaFrente
        //   "e o cliente usou"   → Confirmado, e ComissaoLiquida já desconta estorno
        var pagos = pagamentosDoCliente
            .Where(p => EhDaFrente(tipoDoLead, p))
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

        // A régua é UMA — 20% na entrada, 10% no resto. O que muda por produto é só o que
        // conta como "a primeira venda": no torneio é a primeira EDIÇÃO (não a primeira
        // cobrança), no professor e no clube é a primeira MENSALIDADE.
        var parcelas = tipoDoLead == "Torneio"
            ? PorEdicaoDeTorneio(dentro)
            : PelaPrimeiraMensalidade(dentro);

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

    // Professor e clube: a "entrada" é a PRIMEIRA MENSALIDADE, e o resto é 10%.
    //
    // ⚠️ A ENTRADA É A MENSALIDADE, E NÃO O PRIMEIRO PAGAMENTO QUALQUER. Um professor pode
    // pagar taxa de aula antes de assinar (é o plano Avulso), e nesse caso a primeira cobrança
    // dele é uma aula de R$ 100 — 20% dali seriam R$ 2 pelo trabalho de trazer um cliente
    // novo. A venda que o parceiro fez é a ASSINATURA; é ela que paga a entrada.
    //
    // ⚠️ Consequência assumida: professor que fica no Avulso pra sempre **nunca gera entrada**
    // — tudo dele é 10%. Era assim também com o bônus fixo de R$ 50 que existia antes.
    //
    // Pro CLUBE isso hoje nunca dispara, e é de propósito: não existe plano de clube no código,
    // então um clube só gera pagamento de reserva, que é recorrente. Quando a mensalidade do
    // clube nascer, o tipo dela entra em `TiposDeMensalidade` e a entrada passa a ser paga
    // sozinha, nos mesmos 20%.
    private static List<Parcela> PelaPrimeiraMensalidade(List<Pagamento> pagos)
    {
        var entrada = pagos.FirstOrDefault(EhMensalidade);

        return pagos.Select(p =>
        {
            var percentual = p == entrada ? PercentualDaPrimeiraVenda : PercentualRecorrente;
            var motivo = p == entrada ? "1ª mensalidade" : "recorrente";
            return new Parcela(p, percentual,
                Math.Round(ComissaoLiquida(p) * percentual / 100m, 2), motivo);
        }).ToList();
    }

    // O mês em que cada parcela caiu — é assim que o repasse é fechado (dia 30) e pago (dia 10
    // do mês seguinte). O mês corrente aparece separado porque ainda pode crescer.
    public static bool EhDoMesCorrente(Parcela parcela, DateTime agora)
    {
        var quando = parcela.Pagamento.ConfirmadoEm;
        return quando != null && quando.Value.Year == agora.Year && quando.Value.Month == agora.Month;
    }

    // ── O ACERTO: quanto o parceiro já ganhou × quanto já recebeu ─────────────────────────
    //
    // ⚠️ É um SALDO, não uma marcação por pagamento. A comissão pinga o mês inteiro, cliente a
    // cliente; o repasse cobre "tudo que fechou até agora". Marcar pagamento por pagamento
    // obrigaria a escolher quais entram em cada Pix e criaria uma segunda contabilidade.
    public record Acerto(decimal TotalGanho, decimal JaRepassado, decimal DoMesCorrente)
    {
        public decimal Saldo => TotalGanho - JaRepassado;

        // ⚠️ O QUE JÁ DÁ PRA PAGAR HOJE — e não é o saldo. O mês corrente ainda pode crescer
        // (ou ENCOLHER, se um estorno entrar antes do fim do mês), e pagá-lo adiantado é como
        // se cria um repasse a mais que ninguém consegue explicar depois. `Max(0)` porque
        // adiantamento não vira dívida do parceiro.
        public decimal PagavelAgora => Math.Max(0m, Saldo - DoMesCorrente);

        // Pagou mais do que devia (adiantou, ou um estorno derrubou a comissão depois do Pix).
        // Aparece como crédito, não como número negativo: negativo faz o Felipe achar que
        // ainda deve.
        public decimal Adiantado => Math.Max(0m, JaRepassado - TotalGanho);
    }
}
