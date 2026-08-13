using System.Security.Cryptography;
using System.Text;

namespace Padelizou.Services;

// O Ranking RS (mundodoatleta.com.br) é o ranking de padel gaúcho. A integração tem DUAS mãos,
// e cada uma tem a sua chave (ver `ChaveDoParceiro`, mais abaixo):
//   • a nossa pergunta a eles — "esta pessoa pode jogar nesta categoria?", antes de aceitar a
//     inscrição, porque quem já pontuou numa categoria mais forte não desce pra uma mais fraca;
//   • a pergunta deles a nós — "quais torneios estão com inscrição aberta pelo ranking?"
//     (Controllers/TorneiosDoRankingApiController).
//
// `ApiKey` vazio = integração DESLIGADA, e esse é o padrão de propósito. No localhost e nos
// testes não existe chave nenhuma, e sem isto todo teste de inscrição sairia batendo num
// servidor de verdade lá fora.
//
// ⚠️ A CHAVE É SEGREDO e mora só em:
//   • `appsettings.json` (que é git-ignored) na máquina local;
//   • `Environment=RankingRs__ApiKey=...` no systemd unit, em prod e dev.
// Nunca em `appsettings.Development.json` — esse é versionado.
public class RankingRsSettings
{
    public string BaseUrl { get; set; } = "https://mundodoatleta.com.br/api";

    public string ApiKey { get; set; } = "";

    // A consulta acontece DENTRO do POST da inscrição, com a pessoa olhando pra tela. Se o
    // servidor deles pendurar, é melhor desistir da checagem do que segurar a inscrição —
    // ver RankingRsService, que nunca lança e devolve "não consultado".
    public int TimeoutSegundos { get; set; } = 6;

    // Quanto o Padelizou paga a eles por INSCRITO — pessoa, não inscrição (ver
    // Services/AcertoComORankingRs). Fica em configuração porque é preço combinado:
    // renegociar não pode exigir republicar o site.
    public decimal CustoPorInscrito { get; set; } = 1m;

    public bool Configurado =>
        !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(BaseUrl);

    // ---- A MÃO CONTRÁRIA: a chave que ELES mandam pra ler os nossos torneios ----
    //
    // ⚠️ NÃO É A `ApiKey` ACIMA, e trocar uma pela outra é o erro caro deste arquivo: a `ApiKey`
    // é segredo DELES, que a gente guarda pra conseguir perguntar; esta aqui é segredo NOSSO,
    // que a gente entrega a eles pra que consigam pedir a lista. Se a nossa API conferisse a
    // `ApiKey`, bastaria chamar a nossa porta com a chave deles pra descobrir que ela é válida.
    //
    // Vive no mesmo lugar que a outra: `Environment=RankingRs__ChaveDoParceiro=...` no systemd,
    // ou no `appsettings.json` git-ignored da máquina local. Gerar com `openssl rand -hex 32`.
    //
    // ⚠️ SÓ EM PRODUÇÃO, por decisão do Felipe (12/08/2026). O dev existe e sobe o mesmo
    // binário, mas fica SEM chave — e sem chave a porta responde 503, que é o comportamento
    // certo pra um ambiente que ninguém combinou de usar. Se um dia o parceiro precisar testar
    // sem tocar na produção, é uma linha no systemd do dev, com valor DIFERENTE: a mesma chave
    // nos dois faria a chave de teste abrir a produção.
    public string ChaveDoParceiro { get; set; } = "";

    // Chave curta é chave adivinhável, e esta abre uma porta que responde sem login nenhum.
    // Vinte caracteres não é rigor de segurança — é o piso que impede a integração inteira ficar
    // protegida por um "padelizou123" digitado com pressa numa sexta à noite.
    public const int TamanhoMinimoDaChave = 20;

    // Nasce DESLIGADA, igual à outra ponta: sem chave configurada a API não responde lista
    // nenhuma (devolve 503). O padrão seguro é a porta fechada — uma API de torneios que nasce
    // aberta porque ninguém configurou nada é o tipo de coisa que só se descobre depois.
    public bool ApiDoParceiroLigada => ChaveDoParceiro.Trim().Length >= TamanhoMinimoDaChave;

    // ⚠️ Comparação de tempo FIXO, não `==`. A comparação normal de string para no primeiro
    // caractere diferente, e a diferença de microssegundos entre "errou na 1ª letra" e "errou
    // na 30ª" é medível pela rede — dá pra descobrir a chave um caractere por vez.
    //
    // (O webhook do Asaas ainda compara o token dele com `==`, em
    // PagamentosController.TokenValido. Não é o assunto deste trabalho, mas é o mesmo buraco —
    // quem for mexer lá, o remédio é esta linha.)
    public bool ChaveDoParceiroConfere(string? enviada)
    {
        if (!ApiDoParceiroLigada || string.IsNullOrWhiteSpace(enviada)) return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(enviada.Trim()),
            Encoding.UTF8.GetBytes(ChaveDoParceiro.Trim()));
    }
}
