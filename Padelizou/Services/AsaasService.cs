using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Padelizou.Services;

// Integração com o gateway Asaas. A cobrança nasce na conta do Padelizou e o split manda a
// fatia do organizador/professor direto pra wallet dele — assim só a comissão entra como
// faturamento nosso (o que mantém o MEI dentro do limite anual). Mesmo contrato do
// WhatsAppApiService: falha nunca derruba o fluxo do usuário, só loga e devolve null pro
// chamador decidir.
public class AsaasService : IAsaasService
{
    private readonly HttpClient _httpClient;
    private readonly AsaasSettings _settings;
    private readonly ILogger<AsaasService> _logger;

    public AsaasService(HttpClient httpClient, IOptions<AsaasSettings> settings, ILogger<AsaasService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public bool Configurado => !string.IsNullOrWhiteSpace(_settings.ApiKey);

    // percentual != null sobrepõe a tabela por tipo — é assim que cada torneio cobra a taxa
    // da forma de recebimento que o organizador escolheu.
    public RateioComissao CalcularRateio(decimal preco, string tipoOperacao, string? modoComissao = null,
        decimal? percentual = null)
    {
        var comissao = Math.Max(
            Math.Round(preco * (percentual ?? PercentualDe(tipoOperacao)) / 100m, 2),
            MinimaDe(tipoOperacao));

        var modo = string.IsNullOrWhiteSpace(modoComissao) ? _settings.ModoComissaoPadrao : modoComissao;

        if (modo.Equals("Descontada", StringComparison.OrdinalIgnoreCase))
        {
            // Nunca deixar o repasse negativo num preço mais barato que a comissão mínima.
            comissao = Math.Min(comissao, preco);
            return new RateioComissao(preco, preco - comissao, comissao);
        }

        // "Somada": o jogador paga o preço mais a taxa de serviço e o dono recebe cheio.
        return new RateioComissao(preco + comissao, preco, comissao);
    }

    // Comparação frouxa de propósito: o percentual vem do appsettings, onde a chave pode vir
    // com outra caixa ("torneio", "Torneio") sem que isso deva zerar a comissão.
    private decimal PercentualDe(string tipoOperacao)
    {
        foreach (var par in _settings.ComissaoPercentualPorTipo)
        {
            if (string.Equals(par.Key, tipoOperacao, StringComparison.OrdinalIgnoreCase)) return par.Value;
        }
        return _settings.ComissaoPercentualPadrao;
    }

    private decimal MinimaDe(string tipoOperacao)
    {
        foreach (var par in _settings.ComissaoMinimaPorTipo)
        {
            if (string.Equals(par.Key, tipoOperacao, StringComparison.OrdinalIgnoreCase)) return par.Value;
        }
        return _settings.ComissaoMinima;
    }

    public async Task<string?> ObterOuCriarClienteAsync(string nome, string cpf, string? email, string? celular)
    {
        var documento = new string((cpf ?? "").Where(char.IsDigit).ToArray());
        if (documento.Length == 0)
        {
            _logger.LogWarning("Jogador sem CPF válido — não dá pra criar cliente no Asaas.");
            return null;
        }

        try
        {
            // Reaproveita o cliente já existente: recriar pelo mesmo CPF gera duplicata no Asaas.
            using var busca = Requisicao(HttpMethod.Get, $"customers?cpfCnpj={documento}");
            var respostaBusca = await _httpClient.SendAsync(busca);
            if (respostaBusca.IsSuccessStatusCode)
            {
                using var jsonBusca = JsonDocument.Parse(await respostaBusca.Content.ReadAsStringAsync());
                if (jsonBusca.RootElement.TryGetProperty("data", out var lista) && lista.GetArrayLength() > 0)
                {
                    return lista[0].GetProperty("id").GetString();
                }
            }

            // Telefone e e-mail são acessórios pro Asaas: se vierem tortos (máscara, número
            // incompleto, e-mail digitado errado), ele recusa o cliente inteiro com
            // invalid_mobilePhone/invalid_email e a inscrição morre. Como nenhum dos dois é
            // necessário pra cobrar, o que não estiver válido é omitido em vez de derrubar
            // o pagamento.
            var corpoCliente = new Dictionary<string, object?>
            {
                ["name"] = nome,
                ["cpfCnpj"] = documento
            };

            var telefone = NormalizarCelular(celular);
            if (telefone != null) corpoCliente["mobilePhone"] = telefone;

            if (!string.IsNullOrWhiteSpace(email) && email.Contains('@') && email.Contains('.'))
            {
                corpoCliente["email"] = email.Trim();
            }

            using var criacao = Requisicao(HttpMethod.Post, "customers", corpoCliente);

            var resposta = await _httpClient.SendAsync(criacao);
            var conteudo = await resposta.Content.ReadAsStringAsync();
            if (!resposta.IsSuccessStatusCode)
            {
                _logger.LogWarning("Asaas retornou {Status} ao criar cliente: {Corpo}", resposta.StatusCode, conteudo);
                return null;
            }

            using var json = JsonDocument.Parse(conteudo);
            return json.RootElement.GetProperty("id").GetString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao obter/criar cliente no Asaas.");
            return null;
        }
    }

    // Devolve o número só com dígitos (DDD + 8 ou 9), ou null se não der pra aproveitar.
    // O app guarda celular em formato livre — "(51) 99999-9999", "+55 51 9999-9999" —, mas o
    // Asaas só aceita dígitos.
    private static string? NormalizarCelular(string? celular)
    {
        var digitos = new string((celular ?? "").Where(char.IsDigit).ToArray());

        // Tira o código do país quando o jogador digitou o número internacional completo.
        if (digitos.Length is 12 or 13 && digitos.StartsWith("55")) digitos = digitos[2..];

        return digitos.Length is 10 or 11 ? digitos : null;
    }

    public async Task<CobrancaAsaas?> CriarCobrancaAsync(
        string clienteId,
        RateioComissao rateio,
        string descricao,
        string referenciaExterna,
        DateTime vencimento,
        string? walletIdRecebedor,
        string billingType = "UNDEFINED")
    {
        try
        {
            var corpo = new Dictionary<string, object?>
            {
                ["customer"] = clienteId,
                // UNDEFINED deixa o pagador escolher entre as formas habilitadas na conta.
                // "PIX" trava a cobrança em Pix — usado quando o organizador escolheu só Pix,
                // porque não existe forma de oferecer um subconjunto (Pix+boleto, sem cartão).
                ["billingType"] = billingType,
                ["value"] = rateio.ValorTotal,
                ["dueDate"] = vencimento.ToString("yyyy-MM-dd"),
                ["description"] = descricao,
                ["externalReference"] = referenciaExterna
            };

            // O Asaas recusa split para a carteira de origem ("Não é permitido split para sua
            // própria carteira"), o que acontece sempre que o dono do torneio/aula é o próprio
            // Padelizou. Nesse caso o dinheiro já cai na conta certa e não há repasse a fazer.
            bool ehACarteiraDaPlataforma = !string.IsNullOrWhiteSpace(_settings.WalletIdPlataforma)
                && string.Equals(walletIdRecebedor, _settings.WalletIdPlataforma, StringComparison.OrdinalIgnoreCase);

            // Sem wallet o dono ainda não conectou a conta dele: a cobrança inteira cai no
            // Padelizou e o repasse fica combinado por fora.
            if (!string.IsNullOrWhiteSpace(walletIdRecebedor) && !ehACarteiraDaPlataforma && rateio.ValorRepasse > 0)
            {
                corpo["split"] = new[]
                {
                    new { walletId = walletIdRecebedor, fixedValue = rateio.ValorRepasse }
                };
            }

            using var request = Requisicao(HttpMethod.Post, "payments", corpo);
            var resposta = await _httpClient.SendAsync(request);
            var conteudo = await resposta.Content.ReadAsStringAsync();

            if (!resposta.IsSuccessStatusCode)
            {
                _logger.LogWarning("Asaas retornou {Status} ao criar cobrança {Referencia}: {Corpo}",
                    resposta.StatusCode, referenciaExterna, conteudo);
                return null;
            }

            using var json = JsonDocument.Parse(conteudo);
            var id = json.RootElement.GetProperty("id").GetString();
            var url = json.RootElement.TryGetProperty("invoiceUrl", out var invoice) ? invoice.GetString() : null;

            if (id == null || url == null)
            {
                _logger.LogWarning("Cobrança criada no Asaas sem id/invoiceUrl: {Corpo}", conteudo);
                return null;
            }

            return new CobrancaAsaas(id, url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao criar cobrança no Asaas para {Referencia}.", referenciaExterna);
            return null;
        }
    }

    public async Task<bool> EstornarAsync(string asaasPaymentId, bool jaFoiPaga)
    {
        try
        {
            // Paga -> POST /refund devolve o valor. Ainda pendente -> DELETE remove a cobrança:
            // pedir refund de algo não pago o Asaas recusa.
            using var request = jaFoiPaga
                ? Requisicao(HttpMethod.Post, $"payments/{asaasPaymentId}/refund")
                : Requisicao(HttpMethod.Delete, $"payments/{asaasPaymentId}");

            var resposta = await _httpClient.SendAsync(request);
            if (!resposta.IsSuccessStatusCode)
            {
                var corpo = await resposta.Content.ReadAsStringAsync();
                _logger.LogWarning("Asaas retornou {Status} ao estornar {PaymentId}: {Corpo}",
                    resposta.StatusCode, asaasPaymentId, corpo);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao estornar a cobrança {PaymentId} no Asaas.", asaasPaymentId);
            return false;
        }
    }

    public async Task<(SubcontaCriada?, FalhaAoCriarSubconta?)> CriarSubcontaAsync(DadosDaSubconta dados)
    {
        try
        {
            var corpo = new Dictionary<string, object?>
            {
                ["name"] = dados.Nome,
                ["email"] = dados.Email,
                ["cpfCnpj"] = SomenteDigitos(dados.CpfCnpj),
                ["mobilePhone"] = SomenteDigitos(dados.Celular),
                ["birthDate"] = dados.DataNascimento.ToString("yyyy-MM-dd"),
                ["incomeValue"] = dados.FaturamentoMensal,
                ["address"] = dados.Endereco,
                ["addressNumber"] = dados.Numero,
                ["province"] = dados.Bairro,
                ["postalCode"] = SomenteDigitos(dados.Cep),
            };

            using var request = Requisicao(HttpMethod.Post, "accounts", corpo);
            var resposta = await _httpClient.SendAsync(request);
            var conteudo = await resposta.Content.ReadAsStringAsync();

            if (!resposta.IsSuccessStatusCode)
            {
                // NUNCA logar o conteúdo inteiro aqui: o corpo enviado leva endereço e CPF, e
                // o Asaas devolve trechos dele no erro. Só o código e a descrição do problema.
                var (motivo, jaTemConta) = LerRecusa(conteudo);
                _logger.LogWarning("Asaas recusou a criação de subconta ({Status}): {Motivo}",
                    resposta.StatusCode, motivo);
                return (null, new FalhaAoCriarSubconta(motivo, PodeTentarManual: jaTemConta));
            }

            using var json = JsonDocument.Parse(conteudo);
            var walletId = json.RootElement.TryGetProperty("walletId", out var w) ? w.GetString() : null;

            if (string.IsNullOrWhiteSpace(walletId))
            {
                _logger.LogError("Asaas criou a subconta mas não devolveu walletId.");
                return (null, new FalhaAoCriarSubconta(
                    "A conta foi criada, mas não recebemos o identificador dela. Fale com a gente.",
                    PodeTentarManual: true));
            }

            // A apiKey que veio junto morre aqui, junto com `conteudo`: ela move dinheiro da
            // conta dele e o split não precisa dela. Ver o comentário em SubcontaCriada.
            return (new SubcontaCriada(walletId), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao criar subconta no Asaas.");
            return (null, new FalhaAoCriarSubconta(
                "Não conseguimos falar com o meio de pagamento agora. Tente de novo em instantes.",
                PodeTentarManual: true));
        }
    }

    // O Asaas devolve {"errors":[{"code":"...","description":"..."}]}. A descrição dele já vem
    // em português e é mais específica do que qualquer texto genérico nosso — vale repassar.
    private static (string Motivo, bool JaTemConta) LerRecusa(string conteudo)
    {
        try
        {
            using var json = JsonDocument.Parse(conteudo);
            if (json.RootElement.TryGetProperty("errors", out var erros) && erros.GetArrayLength() > 0)
            {
                var primeiro = erros[0];
                var descricao = primeiro.TryGetProperty("description", out var d) ? d.GetString() : null;
                var codigo = primeiro.TryGetProperty("code", out var c) ? c.GetString() : null;

                // CPF que já tem conta lá é o caso comum, e não é erro nenhum: essa pessoa
                // só precisa colar o código da conta que ela já tem.
                bool jaTemConta = (codigo ?? "").Contains("already", StringComparison.OrdinalIgnoreCase)
                    || (descricao ?? "").Contains("já", StringComparison.OrdinalIgnoreCase)
                    || (descricao ?? "").Contains("existe", StringComparison.OrdinalIgnoreCase);

                return (descricao ?? "O meio de pagamento recusou os dados informados.", jaTemConta);
            }
        }
        catch (JsonException) { /* corpo não-JSON: cai no texto genérico */ }

        return ("O meio de pagamento recusou os dados informados.", false);
    }

    private static string SomenteDigitos(string? valor) =>
        new((valor ?? "").Where(char.IsDigit).ToArray());

    private HttpRequestMessage Requisicao(HttpMethod metodo, string caminho, object? corpo = null)
    {
        var request = new HttpRequestMessage(metodo, $"{_settings.BaseUrl.TrimEnd('/')}/{caminho.TrimStart('/')}");
        request.Headers.Add("access_token", _settings.ApiKey);
        if (corpo != null)
        {
            request.Content = JsonContent.Create(corpo);
        }
        return request;
    }
}
