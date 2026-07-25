namespace Padelizou.Services;

// Cobrança criada no Asaas: o Id serve pra casar o webhook com o Pagamento no nosso banco,
// e a InvoiceUrl é a página hospedada onde o jogador escolhe Pix, cartão ou boleto.
public record CobrancaAsaas(string PaymentId, string InvoiceUrl);

// Quanto o jogador paga, quanto vai pro dono do torneio/aula e quanto fica de comissão.
public record RateioComissao(decimal ValorTotal, decimal ValorRepasse, decimal Comissao);

public interface IAsaasService
{
    // Falso quando a ApiKey não está configurada — o chamador cai no fluxo "pago por fora".
    bool Configurado { get; }

    // Calcula o rateio a partir do preço. O percentual sai de tipoOperacao ("Torneio", "Aula"
    // ou "Jogo"); o modo ("Somada"/"Descontada") vem do cadastro do dono do torneio/aula e,
    // em branco, cai no padrão de AsaasSettings.
    RateioComissao CalcularRateio(decimal preco, string tipoOperacao, string? modoComissao = null);

    // Reaproveita o cliente já cadastrado pelo CPF; só cria um novo se não existir.
    Task<string?> ObterOuCriarClienteAsync(string nome, string cpf, string? email, string? celular);

    // Cria a cobrança na conta do Padelizou. Quando há walletId, o split manda ValorRepasse
    // direto pro dono e o restante (a comissão) fica na nossa conta.
    Task<CobrancaAsaas?> CriarCobrancaAsync(
        string clienteId,
        RateioComissao rateio,
        string descricao,
        string referenciaExterna,
        DateTime vencimento,
        string? walletIdRecebedor);

    // Devolve o dinheiro ao jogador. Cobrança ainda não paga é cancelada em vez de estornada —
    // são endpoints diferentes no Asaas.
    Task<bool> EstornarAsync(string asaasPaymentId, bool jaFoiPaga);
}
