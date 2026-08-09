namespace Padelizou.Services;

// Cobrança criada no Asaas: o Id serve pra casar o webhook com o Pagamento no nosso banco,
// e a InvoiceUrl é a página hospedada onde o jogador escolhe Pix, cartão ou boleto.
public record CobrancaAsaas(string PaymentId, string InvoiceUrl);

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

    // Devolve o dinheiro ao jogador. Cobrança ainda não paga é cancelada em vez de estornada —
    // são endpoints diferentes no Asaas.
    //
    // valorParcial null devolve tudo. Com valor, o gateway devolve só aquela parte e estorna
    // a mesma proporção do split — ver Services/EstornoParcial pro nosso lado da conta.
    Task<bool> EstornarAsync(string asaasPaymentId, bool jaFoiPaga, decimal? valorParcial = null);

    // Abre a conta de recebimento do organizador sem que ele saia do Padelizou. Devolve o
    // walletId em caso de sucesso, ou o motivo escrito pra tela em caso de recusa.
    Task<(SubcontaCriada? Sucesso, FalhaAoCriarSubconta? Falha)> CriarSubcontaAsync(DadosDaSubconta dados);
}
