using System.Globalization;

namespace Padelizou.Services;

// O que o organizador precisa saber ANTES de escolher como vai receber.
//
// Números da conta do Padelizou no Asaas (GET /myAccount/fees, conferido em 27/07/2026),
// já com o desconto promocional vigente. Ficam em configuração porque o Asaas renegocia
// taxa por volume e o desconto tem validade — quando virar, é trocar aqui.
//
// Duas coisas dominam a decisão e por isso aparecem destacadas na tela:
//   1. Cartão de crédito só cai 32 dias depois. Quadra, bola e premiação vencem antes.
//   2. Os 100 primeiros Pix do mês não pagam taxa — num torneio de 32 duplas, o Pix sai
//      literalmente de graça, enquanto o cartão parcelado come até 3,29% + R$ 0,49.
public class TaxasExibicao
{
    public FormaRecebimento Pix { get; set; } =
        new("Pix", 0m, 0.99m, 0, "os 100 primeiros do mês são sem taxa");
    public FormaRecebimento Boleto { get; set; } =
        new("Boleto", 0m, 0.99m, 1);
    public FormaRecebimento CartaoDebito { get; set; } =
        new("Cartão de débito", 1.89m, 0.35m, 3);
    public FormaRecebimento CreditoAVista { get; set; } =
        new("Crédito à vista", 1.99m, 0.49m, 32);
    public FormaRecebimento CreditoAte6x { get; set; } =
        new("Crédito em até 6x", 2.49m, 0.49m, 32);
    public FormaRecebimento CreditoAte12x { get; set; } =
        new("Crédito em até 12x", 2.99m, 0.49m, 32);
    public FormaRecebimento CreditoAte21x { get; set; } =
        new("Crédito em até 21x", 3.29m, 0.49m, 32);

    // As taxas acima são promocionais. Depois desta data sobem (crédito à vista vai a 2,99%,
    // 21x a 4,29%, Pix e boleto a R$ 1,99).
    public DateTime? DescontoValidoAte { get; set; } = new DateTime(2026, 10, 27);

    // ── Taxa do Padelizou por forma de recebimento (só torneios) ───────────────────────
    // Cada faixa acompanha o custo e o risco que a plataforma assume. Quem escolhe é o
    // ORGANIZADOR, na criação — nunca o jogador no checkout, senão o organizador não
    // saberia quanto vai receber antes de anunciar o preço.

    // Não tocamos no dinheiro: sem custo de meio de pagamento, sem risco, sem capital parado.
    public decimal ComissaoPercentualExterno { get; set; } = 5m;

    // Pix: cai na hora e custa centavos.
    public decimal ComissaoPercentualSomentePix { get; set; } = 10m;

    // Todas as formas: o jogador pode parcelar no crédito, que custa até 3,29% + R$ 0,49
    // e só é repassado 32 dias depois.
    public decimal ComissaoPercentualTodasFormas { get; set; } = 15m;

    // A taxa de um torneio, pela escolha que o organizador fez.
    public decimal PercentualDoTorneio(string formaPagamento) => formaPagamento switch
    {
        "Externo" => ComissaoPercentualExterno,
        "OnlinePix" => ComissaoPercentualSomentePix,
        _ => ComissaoPercentualTodasFormas,
    };

    public IEnumerable<FormaRecebimento> Todas => new[]
    {
        Pix, Boleto, CartaoDebito, CreditoAVista, CreditoAte6x, CreditoAte12x, CreditoAte21x
    };

    public record FormaRecebimento(
        string Nome, decimal Percentual, decimal ValorFixo, int DiasParaReceber, string? Observacao = null)
    {
        public decimal Custo(decimal valor) => Math.Round(valor * Percentual / 100m + ValorFixo, 2);

        public decimal Liquido(decimal valor) => Math.Round(valor - Custo(valor), 2);

        public string TaxaEscrita => (Percentual, ValorFixo) switch
        {
            (0, var f) => f.ToString("C", Cultura),
            (var p, 0) => p.ToString("0.##", Cultura) + "%",
            var (p, f) => $"{p.ToString("0.##", Cultura)}% + {f.ToString("C", Cultura)}",
        };

        public string PrazoEscrito => DiasParaReceber switch
        {
            0 => "na hora",
            1 => "1 dia útil",
            _ => $"{DiasParaReceber} dias",
        };

        // O que merece destaque vermelho na tela de criar torneio.
        public bool Demora => DiasParaReceber >= 30;

        private static readonly CultureInfo Cultura = new("pt-BR");
    }
}
