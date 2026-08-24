using Padelizou.Models;

namespace Padelizou.Services;

// O documento pronto pra virar nota: o que o provedor precisa receber, no vocabulário do
// Padelizou e não no dele. É esta tradução que torna a troca de fornecedor barata.
public sealed record DocumentoParaEmitir(
    string Tipo,                       // NotaFiscal.Nfce ou NotaFiscal.Nfse
    Clube Emitente,                    // quem emite: CNPJ, IE/IM, regime, endereço
    decimal Valor,
    IReadOnlyList<ItemDoDocumento> Itens,
    string? CpfConsumidor = null,
    string? Descricao = null);         // usado na NFS-e (discriminação do serviço)

public sealed record ItemDoDocumento(
    string Descricao,
    int Quantidade,
    decimal PrecoUnitario,
    string? Ncm = null,
    string? Cfop = null,
    string? Cest = null,
    string? Unidade = null,
    int? Origem = null,
    string? CsosnOuCst = null);

// O que voltou do provedor. `Aceita` significa só "ele recebeu e vai processar" — a
// autorização de verdade chega depois, por webhook (ver NotaFiscal).
public sealed record RespostaDaEmissao(
    bool Aceita,
    string? IdNoProvedor = null,
    string? Mensagem = null,
    // ⚠️ Recusa DEFINITIVA (cadastro errado, CNPJ inválido, NCM inexistente) contra recusa
    // TEMPORÁRIA (SEFAZ fora do ar, timeout). A diferença decide se tentar de novo ajuda ou
    // só queima crédito — e como o provedor cobra por requisição, insistir num erro de
    // cadastro é pagar pra repetir o mesmo engano.
    bool ValeTentarDeNovo = true);

// A porta de saída pro mundo fiscal.
//
// ── POR QUE ELA EXISTE ANTES DE EXISTIR PROVEDOR ─────────────────────────────────────────
// Porque já perdemos um. Em 19/08/2026 este projeto recomendava a Nuvem Fiscal como primeira
// escolha; em 24/08 descobrimos que ela havia sido DESATIVADA em 31/07. Se a integração
// tivesse sido escrita direto contra a API dela "pra adiantar", seria trabalho jogado fora.
//
// Com esta interface no meio, trocar de fornecedor é escrever uma classe nova — o resto do
// sistema (comanda, fila, telas) não sabe o nome de ninguém. Não é preciosismo de
// arquitetura: é a defesa de um risco que já se materializou uma vez.
//
// O provedor escolhido hoje é a ACBr API (ver FISCAL.md), mas a implementação dela só nasce
// quando houver clube pagante — até lá vale o EmissorFiscalDesligado.
public interface IEmissorFiscal
{
    // Existe provedor configurado? Falso = o módulo fiscal está desligado e nada é enviado.
    bool Configurado { get; }

    Task<RespostaDaEmissao> EmitirAsync(DocumentoParaEmitir documento, CancellationToken ct = default);

    // Cancelamento tem prazo legal curto (~30 min na NFC-e, na maioria dos estados). Quem
    // sabe se ainda dá tempo é o provedor; aqui só se pede e se registra a resposta.
    Task<RespostaDaEmissao> CancelarAsync(NotaFiscal nota, string motivo, CancellationToken ct = default);
}

// O provedor que não emite nada — e é o que está registrado enquanto o plano Fiscal não
// tiver cliente.
//
// Não é um "stub temporário": é o estado normal do sistema hoje, e é o que garante que ligar
// o cadastro fiscal em produção não dispare nota nenhuma por engano. A fila continua
// funcionando (as notas nascem Pendentes e ficam lá), o que permite MEDIR o volume real de
// um clube piloto antes de existir contrato com provedor — que é exatamente o número que
// falta pra confirmar a franquia do plano.
public class EmissorFiscalDesligado : IEmissorFiscal
{
    public bool Configurado => false;

    public Task<RespostaDaEmissao> EmitirAsync(DocumentoParaEmitir documento, CancellationToken ct = default) =>
        Task.FromResult(new RespostaDaEmissao(false, Mensagem: "Nenhum provedor fiscal configurado."));

    public Task<RespostaDaEmissao> CancelarAsync(NotaFiscal nota, string motivo, CancellationToken ct = default) =>
        Task.FromResult(new RespostaDaEmissao(false, Mensagem: "Nenhum provedor fiscal configurado."));
}
