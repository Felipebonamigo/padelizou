using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// QUEM ESTÁ NO CANAL PORQUE PEDIU, e quem está porque a caixinha já nascia marcada.
//
// ⚠️ Esta é a causa raiz da restrição de 04/08/2026, e a única que o resto do conserto não
// tocou. Ritmo, teto e aquecimento tratam VOLUME; a régua que a Meta usa de verdade é outra:
// *mandar pra quem não pediu*. Em 07/08/2026, das 55 contas anteriores àquela data, **54
// estavam no canal por herança** — o campo nascia marcado e ninguém foi perguntado.
//
// O contraste que decidiu fazer isso: das 17 contas criadas DEPOIS (com a preferência nascendo
// desmarcada), 13 marcaram na mão. Perguntando, três em cada quatro dizem sim — então desfazer
// o opt-in herdado não é abrir mão do canal, é trocar 54 pessoas silenciosas por ~40 que
// querem estar lá. E quem quiser voltar volta num toque.
public static class ConsentimentoDoWhatsApp
{
    // O dia em que "Receber por WhatsApp" passou a nascer DESMARCADO no cadastro. Antes disto,
    // estar marcado não diz nada sobre a vontade da pessoa; depois, diz tudo.
    public static readonly DateTime OptInPassouASerObrigatorio = new(2026, 8, 4);

    // ⚠️ `CriadoEm` nulo conta como herdado. É o lado seguro: "não sei quando esta conta
    // nasceu" não pode virar "então ela consentiu".
    public static bool EstaNoCanalSemTerPedido(Jogador j) =>
        j.NotificarWhatsApp && (j.CriadoEm == null || j.CriadoEm < OptInPassouASerObrigatorio);

    public static IQueryable<Jogador> NoCanalSemTerPedido(DbPadelContext context) =>
        context.Jogadores.Where(j => j.ExcluidoEm == null
            && j.NotificarWhatsApp
            && (j.CriadoEm == null || j.CriadoEm < OptInPassouASerObrigatorio));

    public const string TituloDoConvite = "Seus avisos no WhatsApp";

    public const string CorpoDoConvite =
        "Desmarcamos o WhatsApp de quem nunca escolheu receber por lá — agora só continua quem "
        + "pedir. Se você quer os avisos de jogo, inscrição e resultado no WhatsApp, é um toque "
        + "nas suas preferências. O aviso pelo app e por e-mail continua igual.";

    public const string CaminhoDasPreferencias = "/Auth/Preferencias";
}

public record ResultadoDoConsentimento(int Desmarcados, int Convidados);

// A AÇÃO em si, separada da regra porque toca banco e fila. Idempotente de propósito: rodar de
// novo depois de tudo desmarcado não acha ninguém e não convida ninguém — é botão de painel, e
// botão de painel é clicado duas vezes.
public class DesfazerOptInHerdado
{
    private readonly DbPadelContext _context;
    private readonly IPushNotificationService _push;

    public DesfazerOptInHerdado(DbPadelContext context, IPushNotificationService push)
    {
        _context = context;
        _push = push;
    }

    public async Task<ResultadoDoConsentimento> RodarAsync()
    {
        var herdados = await ConsentimentoDoWhatsApp.NoCanalSemTerPedido(_context).ToListAsync();

        if (herdados.Count == 0) return new ResultadoDoConsentimento(0, 0);

        foreach (var jogador in herdados) jogador.NotificarWhatsApp = false;

        // Grava ANTES de convidar. Se a ordem fosse a outra, uma falha no meio deixaria gente
        // convidada a "voltar" pra um canal do qual ela nunca saiu.
        await _context.SaveChangesAsync();

        // O convite sai por push e e-mail (`SoApp` é o padrão do EnviarParaJogadorAsync) e
        // NUNCA por WhatsApp — usar o canal em questão pra avisar que a pessoa saiu dele seria
        // exatamente a mensagem não pedida que causou o problema.
        foreach (var jogador in herdados)
        {
            await _push.EnviarParaJogadorAsync(jogador.Id,
                ConsentimentoDoWhatsApp.TituloDoConvite,
                ConsentimentoDoWhatsApp.CorpoDoConvite,
                ConsentimentoDoWhatsApp.CaminhoDasPreferencias);
        }

        return new ResultadoDoConsentimento(herdados.Count, herdados.Count);
    }
}
