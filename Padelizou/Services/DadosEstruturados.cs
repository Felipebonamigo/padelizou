using System.Text.Json;
using Padelizou.Models;

namespace Padelizou.Services;

// O bloco <script type="application/ld+json"> que explica ao buscador O QUE é cada página —
// que "Aberto de Verão 2026" não é um texto qualquer, é um evento esportivo com data, local e
// valor de inscrição. É o que permite o resultado sair com data e endereço embaixo do link em
// vez de só três linhas de texto.
//
// Gerado em C# e não escrito à mão na view: nome de torneio tem aspas, & e acento, e um JSON
// quebrado por uma aspa não dá erro em lugar nenhum — o buscador simplesmente descarta o bloco
// inteiro, calado. O serializador escapa `<` e `>` por padrão, que é o que mantém isto seguro
// dentro de um <script>.
public static class DadosEstruturados
{
    // Perfil oficial. Serve pro buscador ligar o site à conta e tratar os dois como a mesma
    // marca — é o que alimenta o painel do lado direito da busca.
    private const string Instagram = "https://www.instagram.com/padelizou/";

    private static string Serializar(object grafo) => JsonSerializer.Serialize(grafo);

    // Data pura quando não há hora marcada, data+hora quando há. Boa parte dos torneios é
    // cadastrada só com o dia, e aí o `T00:00:00` não é a hora do torneio — é a ausência dela.
    // O buscador leria isso como "começa à meia-noite" e mostraria essa hora na ficha.
    //
    // Sem fuso carimbado nos dois casos: o app inteiro fala no horário de Brasília (ver o TZ do
    // serviço), e um "Z" no fim empurraria todo torneio três horas pra frente.
    private static string Quando(DateTime quando)
        => quando.TimeOfDay == TimeSpan.Zero
            ? quando.ToString("yyyy-MM-dd")
            : quando.ToString("yyyy-MM-dd'T'HH:mm:ss");

    public static string Organizacao(string baseUrl) => Serializar(new Dictionary<string, object>
    {
        ["@context"] = "https://schema.org",
        ["@type"] = "Organization",
        ["name"] = "Padelizou",
        ["url"] = baseUrl,
        ["logo"] = $"{baseUrl}/image/icon-512.png",
        ["description"] = MetaDaBusca.DescricaoDoSite,
        ["sameAs"] = new[] { Instagram },
    });

    public static string Site(string baseUrl) => Serializar(new Dictionary<string, object>
    {
        ["@context"] = "https://schema.org",
        ["@type"] = "WebSite",
        ["name"] = "Padelizou",
        ["url"] = baseUrl,
        ["inLanguage"] = "pt-BR",
    });

    // Devolve null quando o torneio não tem data marcada: "startDate" é campo OBRIGATÓRIO de
    // evento pro Google, e bloco incompleto não vira resultado rico — vira aviso de erro no
    // Search Console. Sem data, melhor não afirmar nada.
    //
    // O clube vem por fora, e não por `torneio.Clube`: aquela navegação é obrigatória, então
    // carregá-la por Include arrastaria a página pro 404 quando o clube faltasse (ver o
    // comentário no Details do TorneiosController). Aqui, faltando, só não sai o local.
    public static string? Torneio(Torneio torneio, Clube? clube, string url, string? imagemUrl, string descricao)
    {
        if (torneio.DataInicio == null) return null;

        var grafo = new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "SportsEvent",
            ["name"] = torneio.Nome,
            ["url"] = url,
            ["description"] = descricao,
            ["sport"] = "Padel",
            ["startDate"] = Quando(torneio.DataInicio.Value),
            ["eventAttendanceMode"] = "https://schema.org/OfflineEventAttendanceMode",
            ["eventStatus"] = CancelamentoDoTorneio.EstaCancelado(torneio.Status)
                ? "https://schema.org/EventCancelled"
                : "https://schema.org/EventScheduled",
        };

        if (torneio.DataFim != null)
        {
            grafo["endDate"] = Quando(torneio.DataFim.Value);
        }

        var local = clube?.Nome ?? torneio.LocalTorneio;
        if (local != null)
        {
            var lugar = new Dictionary<string, object>
            {
                ["@type"] = "Place",
                ["name"] = local,
            };

            // A cidade é o que faz este evento responder a "torneio de padel em <cidade>", que
            // é como a busca de verdade é escrita. Sem ela o Place vira um nome solto.
            var cidade = clube?.Cidade?.Nome;
            if (cidade != null)
            {
                lugar["address"] = new Dictionary<string, object>
                {
                    ["@type"] = "PostalAddress",
                    ["addressLocality"] = cidade,
                    ["addressCountry"] = "BR",
                };
            }

            grafo["location"] = lugar;
        }

        if (imagemUrl != null)
        {
            grafo["image"] = imagemUrl;
        }

        if (torneio.PrecoInscricao > 0)
        {
            grafo["offers"] = new Dictionary<string, object>
            {
                ["@type"] = "Offer",
                ["price"] = torneio.PrecoInscricao.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["priceCurrency"] = "BRL",
                ["url"] = url,
            };
        }

        return Serializar(grafo);
    }
}
