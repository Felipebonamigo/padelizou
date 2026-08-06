namespace Padelizou.Services;

// O Ranking RS (mundodoatleta.com.br) é o ranking de padel gaúcho. A integração serve pra uma
// coisa só: perguntar "esta pessoa pode jogar nesta categoria?" antes de aceitar a inscrição —
// quem já pontuou numa categoria mais forte não desce pra uma mais fraca.
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

    public bool Configurado =>
        !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(BaseUrl);
}
