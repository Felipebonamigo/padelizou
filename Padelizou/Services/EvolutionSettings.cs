namespace Padelizou.Services;

// A Evolution API roda ao lado do app, no mesmo VPS, atrás do 127.0.0.1 (ver
// /opt/evolution/docker-compose.yml). É ela que fala com o WhatsApp por um chip de verdade.
//
// `BaseUrl` vazio = canal DESLIGADO, e esse é o padrão de propósito: no localhost e no
// ambiente de teste não existe container nenhum, e um envio de verdade saindo de um teste
// chegaria no celular de gente de verdade.
public class EvolutionSettings
{
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";

    // A instância é o chip conectado. Uma só hoje ("padelizou").
    public string Instancia { get; set; } = "padelizou";

    public bool Configurado =>
        !string.IsNullOrWhiteSpace(BaseUrl)
        && !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(Instancia);
}
