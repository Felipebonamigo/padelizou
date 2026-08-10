using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Padelizou.Models;
using WebPush;

namespace Padelizou.Services;

// Um registro FANTASMA é uma linha da PushSubscriptionJogador que o servidor de push continua
// aceitando (2xx) e que não entrega nada em aparelho nenhum. Aconteceu de verdade em
// 10/08/2026: um registro Apple de 30/07 respondeu 2xx por ONZE DIAS depois de o app sair da
// tela de início do iPhone, e a Apple nunca mandou 410.
//
// Isso importa porque a limpeza que já existe (no PushNotificationService) só apaga em
// 410/404. Enquanto o servidor de push mentir 2xx, o fantasma fica na tabela pra sempre — e
// como a contagem de aparelhos é o número que a gente usa pra saber o alcance do canal, ela
// infla devagar e ninguém percebe.
//
// ⚠️ NÃO EXISTE CONSULTA QUE SEPARE VIVO DE FANTASMA. Só o envio separa, e mesmo assim só
// quando o servidor de push finalmente admite o 410. Por isso esta varredura é uma SONDA, o
// resultado dela tem três estados (e não dois), e "ainda responde 2xx" significa
// **inconclusivo**, nunca "vivo".
public enum ResultadoDaSonda
{
    // 410/404: o servidor de push admitiu que o registro morreu. É a única prova que existe.
    Morto,

    // 2xx: aceitou. NÃO prova que chegou no aparelho — foi exatamente assim que o fantasma
    // do dia 10/08 passou onze dias despistando.
    Inconclusivo,

    // Qualquer outra recusa (rede fora, 5xx, chave errada). Não é motivo pra apagar nada.
    Erro,
}

public record RelatorioDaVarredura
{
    public int Suspeitos { get; init; }
    public int Apagados { get; init; }
    public int Inconclusivos { get; init; }
    public int ComErro { get; init; }

    // Quantos registros a tabela tem no total, pra dar tamanho ao que foi mexido.
    public int TotalDeRegistros { get; init; }
}

// A sonda de verdade, isolada num contrato próprio pra que a REGRA DE APAGAR possa ser testada
// sem rede. É a regra que precisa de teste: apagar um registro vivo por engano tira do canal
// alguém que estava perfeitamente alcançável, e nada na tela denuncia isso.
public interface ISondaDePush
{
    Task<ResultadoDaSonda> SondarAsync(PushSubscriptionJogador registro);
}

public class SondaDePushWebPush : ISondaDePush
{
    private readonly VapidDetails _vapid;
    private readonly ILogger<SondaDePushWebPush> _logger;

    public SondaDePushWebPush(IOptions<VapidSettings> vapidOptions, ILogger<SondaDePushWebPush> logger)
    {
        var s = vapidOptions.Value;
        _vapid = new VapidDetails(s.Subject, s.PublicKey, s.PrivateKey);
        _logger = logger;
    }

    public async Task<ResultadoDaSonda> SondarAsync(PushSubscriptionJogador registro)
    {
        // `tipo: "sonda"` é o combinado com o sw.js: ao ver isso, ele NÃO mostra notificação.
        //
        // ⚠️ As inscrições nascem com `userVisibleOnly: true`, então o navegador pode decidir
        // mostrar um aviso genérico dele mesmo quando o service worker não mostra nada. Não dá
        // pra garantir silêncio — dá pra garantir que a gente só sonda quem já é suspeito.
        var payload = System.Text.Json.JsonSerializer.Serialize(new { tipo = "sonda" });

        try
        {
            await new WebPushClient().SendNotificationAsync(
                new PushSubscription(registro.Endpoint, registro.P256dh, registro.Auth), payload, _vapid);
            return ResultadoDaSonda.Inconclusivo;
        }
        catch (WebPushException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Gone
                                                        or System.Net.HttpStatusCode.NotFound)
        {
            return ResultadoDaSonda.Morto;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sonda de push falhou no registro {Id}.", registro.Id);
            return ResultadoDaSonda.Erro;
        }
    }
}

public class VarreduraDeFantasmas
{
    private readonly DbPadelContext _context;
    private readonly ISondaDePush _sonda;

    public VarreduraDeFantasmas(DbPadelContext context, ISondaDePush sonda)
    {
        _context = context;
        _sonda = sonda;
    }

    // Os SUSPEITOS, e por que a varredura não sonda a tabela inteira.
    //
    // Um navegador guarda UMA inscrição por aparelho e por site. Então, quando o mesmo jogador
    // tem duas linhas no MESMO servidor de push, a mais velha é quase sempre o mesmo aparelho
    // que se reinscreveu e deixou a anterior pra trás — foi exatamente o desenho do caso real
    // (Apple de 30/07 morta, Apple de 10/08 viva, mesma pessoa).
    //
    // ⚠️ "Quase sempre" e não "sempre": iPhone + iPad da mesma pessoa também caem aqui, e nesse
    // caso a sonda vai bater num aparelho VIVO. É por isso que ela nunca apaga por suspeita —
    // só apaga com 410/404 na mão. O custo do falso positivo é, no máximo, um aviso que o
    // navegador do dono decidir mostrar.
    //
    // Sondar a tabela inteira seria pior de um jeito que não dá pra desfazer: com
    // `userVisibleOnly`, cada aparelho vivo receberia uma notificação vinda do nada. Mensagem
    // que ninguém pediu é o que faz a pessoa desligar o canal — e desse clique não se volta.
    public async Task<List<PushSubscriptionJogador>> SuspeitosAsync()
    {
        var registros = await _context.Set<PushSubscriptionJogador>().ToListAsync();

        return registros
            .Where(r => registros.Any(outro =>
                outro.JogadorId == r.JogadorId &&
                outro.Id != r.Id &&
                ServidorDe(outro.Endpoint) == ServidorDe(r.Endpoint) &&
                outro.CriadoEm > r.CriadoEm))
            .OrderBy(r => r.JogadorId).ThenBy(r => r.CriadoEm)
            .ToList();
    }

    public async Task<RelatorioDaVarredura> RodarAsync()
    {
        var suspeitos = await SuspeitosAsync();
        int apagados = 0, inconclusivos = 0, erros = 0;

        foreach (var registro in suspeitos)
        {
            switch (await _sonda.SondarAsync(registro))
            {
                case ResultadoDaSonda.Morto:
                    _context.Remove(registro);
                    apagados++;
                    break;
                case ResultadoDaSonda.Inconclusivo:
                    inconclusivos++;
                    break;
                default:
                    erros++;
                    break;
            }
        }

        if (apagados > 0) await _context.SaveChangesAsync();

        return new RelatorioDaVarredura
        {
            Suspeitos = suspeitos.Count,
            Apagados = apagados,
            Inconclusivos = inconclusivos,
            ComErro = erros,
            TotalDeRegistros = await _context.Set<PushSubscriptionJogador>().CountAsync(),
        };
    }

    // Endpoint mal formado não pode derrubar a varredura inteira: ele simplesmente não casa
    // com ninguém e deixa de ser suspeito.
    private static string ServidorDe(string endpoint) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ? uri.Host : endpoint;
}
