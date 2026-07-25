namespace Padelizou.Services;

public class AsaasSettings
{
    // Chave da API do Asaas. Em branco = integração desligada: o app continua funcionando
    // normalmente e toda inscrição segue o fluxo antigo ("pago por fora").
    public string ApiKey { get; set; } = "";

    // Sandbox: https://sandbox.asaas.com/api/v3  |  Produção: https://api.asaas.com/v3
    public string BaseUrl { get; set; } = "https://sandbox.asaas.com/api/v3";

    // Token secreto conferido no header do webhook (definido junto com a URL no painel do
    // Asaas). Sem ele qualquer um poderia forjar uma confirmação de pagamento.
    public string WebhookToken { get; set; } = "";

    // Minutos que a cobrança pendente segura a vaga do jogador antes de expirar.
    public int MinutosParaPagar { get; set; } = 60;

    // Wallet da própria conta do Padelizou (veja em GET /wallets). Quando o dono do torneio
    // ou da aula é o próprio Padelizou, o split é omitido: o Asaas recusa split para a
    // carteira de origem, e não há repasse nenhum a fazer nesse caso.
    public string WalletIdPlataforma { get; set; } = "";

    // ---- Comissão do Padelizou — tudo ajustável por configuração, sem mexer em código ----

    // Percentual por tipo de operação — dá pra cobrar diferente em cada frente do app.
    // Chaves usadas hoje: "Torneio", "Aula", "Jogo" (marcação de jogo no site).
    public Dictionary<string, decimal> ComissaoPercentualPorTipo { get; set; } = new()
    {
        ["Torneio"] = 15m,
        ["Aula"] = 10m,
        ["Jogo"] = 10m
    };

    // Vale quando o tipo não está no dicionário acima (frente nova ainda sem percentual).
    public decimal ComissaoPercentualPadrao { get; set; } = 10m;

    // Piso em reais. A taxa do Asaas no Pix é fixa (R$ 1,99), então sem um mínimo a comissão
    // de uma inscrição barata não se paga.
    public decimal ComissaoMinima { get; set; } = 4m;

    // Quem decide como a comissão é cobrada é o dono do torneio/aula — a escolha fica gravada
    // no cadastro dele. Isto aqui é só o padrão de quem ainda não escolheu.
    // "Somada"     -> modelo Sympla: o jogador paga inscrição + taxa e o dono recebe cheio.
    // "Descontada" -> a comissão sai da fatia do dono (o jogador paga só a inscrição).
    public string ModoComissaoPadrao { get; set; } = "Somada";
}
