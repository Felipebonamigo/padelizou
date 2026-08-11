namespace Padelizou.Services;

// Cobrança criada no Asaas: o Id serve pra casar o webhook com o Pagamento no nosso banco,
// e a InvoiceUrl é a página hospedada do gateway.
//
// ⚠️ A InvoiceUrl deixou de ser o destino de quem vai pagar (10/08/2026) — hoje ela é o
// PLANO B, pra cobrança que não tem Pix. Ver Services/LinkDoPagamento.
public record CobrancaAsaas(string PaymentId, string InvoiceUrl);

// O Pix de uma cobrança que já existe lá: o mesmo código que a fatura hospedada mostraria,
// pra ser desenhado dentro do Padelizou.
//
// Só o copia e cola. O gateway manda uma imagem do QR junto, mas ela é DESCARTADA de
// propósito: quem desenha é Services/QrDoPix, a partir desta string — assim o QR da tela e o
// código do botão "copiar" não têm como divergir, e a tela não fica sem QR se a imagem vier
// vazia um dia.
public record PixDaCobranca(string CopiaECola);

// Quanto o jogador paga, quanto vai pro dono do torneio/aula e quanto fica de comissão.
public record RateioComissao(decimal ValorTotal, decimal ValorRepasse, decimal Comissao);

// O que o Asaas exige pra abrir a conta de recebimento de um organizador/professor.
//
// Nome, e-mail, CPF e celular saem do cadastro dele. Endereço e faturamento são pedidos na
// hora e NÃO ficam no nosso banco: atravessam a requisição e acabam ali. Guardar endereço de
// todo organizador seria assumir uma responsabilidade que a gente não precisa ter.
public record DadosDaSubconta(
    string Nome,
    string Email,
    string CpfCnpj,
    string Celular,
    // A documentação NÃO lista a data de nascimento entre os obrigatórios, mas a API recusa
    // sem ela ("É necessário informar a data de nascimento"). Descoberto batendo no sandbox —
    // sem esse teste, todo organizador de verdade teria esbarrado no erro.
    DateTime DataNascimento,
    decimal FaturamentoMensal,
    string Cep,
    string Endereco,
    string Numero,
    string Bairro);

// Só o WalletId volta pra cima.
//
// A resposta do Asaas traz DOIS segredos — `apiKey` e `accessToken` — que dão poder de mover
// o dinheiro da conta do organizador, e vêm uma única vez. Pro split a gente só precisa do
// walletId, então os dois são descartados de propósito: guardar chave assim pra cada usuário
// seria criar um passivo enorme sem ganho nenhum. Se um dia precisar, o Asaas tem endpoint
// pra gerar outra chave.
public record SubcontaCriada(string WalletId);

// Por que a criação falhou, no jeito de falar de quem está lendo a tela.
//
// `JaTemConta` NÃO decide se vale oferecer o caminho manual — esse é sempre oferecido, porque
// colar o código funciona em qualquer cenário de recusa. Serve só pra escolher a frase: quem
// já tem conta lá precisa ouvir "use a que você tem", e não "tente de novo mais tarde".
//
// Antes isso governava a oferta do caminho manual, e havia um cenário previsível em que ele
// sumia: o Período de Avaliação do Asaas deixa a conta-mãe criar no MÁXIMO 10 subcontas nos
// primeiros 60 dias. Na décima primeira, a recusa não fala de conta existente, o texto não
// casava com nada, e o organizador ficava sem saída nenhuma na tela.
public record FalhaAoCriarSubconta(string Motivo, bool JaTemConta);

// Devolver só um PEDAÇO do dinheiro de uma cobrança já paga.
//
// ⚠️ `ValorDoRepasse` existe porque **estorno parcial não reverte o split sozinho**. No estorno
// TOTAL o Asaas devolve a transferência de todo mundo; no parcial, ele tira o valor inteiro da
// fatia da conta PRINCIPAL — que é só a comissão — e recusa com "Valor da cobrança insuficiente
// para o estorno solicitado" se não couber. Foi exatamente o que aconteceu ao tentar devolver
// R$ 125 de uma cobrança de R$ 250 cuja comissão era R$ 37,50 (09/08/2026, em produção).
//
// Quanto sai de cada lado é conta de Services/EstornoParcial — aqui só chega o resultado.
public record DevolucaoParcial(decimal Valor, decimal ValorDoRepasse);

public interface IAsaasService
{
    // Falso quando a ApiKey não está configurada — o chamador cai no fluxo "pago por fora".
    bool Configurado { get; }

    // Calcula o rateio a partir do preço. O percentual sai de tipoOperacao ("Torneio", "Aula"
    // ou "Jogo"); o modo ("Somada"/"Descontada") vem do cadastro do dono do torneio/aula e,
    // em branco, cai no padrão de AsaasSettings.
    RateioComissao CalcularRateio(decimal preco, string tipoOperacao, string? modoComissao = null,
        decimal? percentual = null);

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
        string? walletIdRecebedor,
        string billingType = "UNDEFINED");

    // O QR e o copia e cola de uma cobrança já criada, pra fatura que mora no Padelizou.
    //
    // Null quando o gateway não tem Pix pra ela — o caso normal é a cobrança travada em CARTÃO
    // — e aí o pagador é mandado pra fatura de lá. Não é erro: é a resposta "essa aqui não é
    // de Pix".
    Task<PixDaCobranca?> ObterPixAsync(string asaasPaymentId);

    // Devolve o dinheiro ao jogador. Cobrança ainda não paga é cancelada em vez de estornada —
    // são endpoints diferentes no Asaas.
    //
    // `parcial` null devolve TUDO, e aí o gateway reverte o split sozinho. Ver DevolucaoParcial
    // pro caso de devolver só um pedaço, que não funciona do mesmo jeito.
    Task<bool> EstornarAsync(string asaasPaymentId, bool jaFoiPaga, DevolucaoParcial? parcial = null);

    // Abre a conta de recebimento do organizador sem que ele saia do Padelizou. Devolve o
    // walletId em caso de sucesso, ou o motivo escrito pra tela em caso de recusa.
    Task<(SubcontaCriada? Sucesso, FalhaAoCriarSubconta? Falha)> CriarSubcontaAsync(DadosDaSubconta dados);
}
