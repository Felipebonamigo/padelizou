using Microsoft.Extensions.Options;

namespace Padelizou.Services;

// O porteiro da SAÍDA: decide se uma mensagem tem o direito de deixar este ambiente.
//
// Um lugar só, e os três canais perguntam a ele — e-mail (`EmailService`), WhatsApp
// (`EvolutionApiService`) e push (`PushNotificationService`). Espalhar a pergunta pelos ~30
// pontos que GERAM aviso seria garantir que o próximo aviso nasça esquecendo um canal; é
// exatamente por isso que a entrega já mora num lugar só (ver `PushNotificationService`).
//
// ⚠️ Ele NÃO substitui as preferências do jogador (`NotificarEmail`, `NotificarWhatsApp`) nem
// o `AlcanceDoAviso`. Aquelas respondem se a pessoa QUER; este responde se o ambiente PODE. Um
// ensaio no dev não é motivo pra alguém ser incomodado, mesmo tendo dito que sim.
//
// ⚠️ A CAIXA DE ENTRADA DO APP FICA DE FORA, de propósito. Ela não sai daqui: é uma linha num
// banco de teste, não um toque no celular de ninguém. Barrá-la também tiraria do ensaio a
// única tela onde se confere que o aviso chegou a ser gerado.
public class PorteiroDaSaida
{
    private readonly HashSet<string> _permitidos;

    public PorteiroDaSaida(IOptions<EntregaSettings> settings, ILogger<PorteiroDaSaida> logger)
    {
        _permitidos = (settings.Value.SoPara ?? [])
            .Select(Normalizar)
            .OfType<string>()
            .ToHashSet();

        // No start, uma vez, e em Warning: ambiente que cala mensagem tem que dizer isso na
        // primeira tela do log. Descobrir a restrição depois, caçando por que o e-mail não
        // chegou, é caro; descobrir lendo o start é de graça.
        if (Restringindo)
        {
            logger.LogWarning(
                "SAÍDA RESTRITA neste ambiente: e-mail, WhatsApp e push só saem pros {Quantos} destinos de Entrega:SoPara. " +
                "O resto é registrado no log e descartado.", _permitidos.Count);
        }
    }

    // Produção roda com a lista vazia, e aqui isso significa "porteiro sem trabalho": o
    // `PodeSair` devolve true sem olhar nada. É o que torna esta classe segura de existir no
    // caminho de todo e-mail do sistema.
    public bool Restringindo => _permitidos.Count > 0;

    public bool PodeSair(string? destino)
    {
        if (!Restringindo) return true;

        var normalizado = Normalizar(destino);

        // Destino que não dá pra normalizar (número torto, e-mail em branco) NÃO passa com o
        // ambiente restrito. A dúvida se resolve pro lado de não incomodar um desconhecido —
        // o oposto do padrão da lista vazia, e pelo mesmo motivo de sempre: lá o risco era
        // calar a produção, aqui é escrever pra quem não pediu nada.
        return normalizado != null && _permitidos.Contains(normalizado);
    }

    // Qualquer um dos dois serve, e é o que o PUSH precisa: a inscrição é de um APARELHO, e
    // aparelho não tem endereço que uma pessoa reconheça. Quem tem é o dono dele.
    public bool PodeSairParaAlgum(string? email, string? celular) =>
        !Restringindo || PodeSair(email) || PodeSair(celular);

    private static string? Normalizar(string? destino)
    {
        if (string.IsNullOrWhiteSpace(destino)) return null;

        var limpo = destino.Trim();

        if (limpo.Contains('@')) return limpo.ToLowerInvariant();

        // O MESMO normalizador que o canal usa pra falar com a Meta: "51 99999-8888",
        // "5551999998888" e "(51) 99999-8888" são a mesma pessoa, e a lista não pode depender
        // de alguém acertar a pontuação — quem a escreve está com pressa, no meio de um deploy.
        return EvolutionApiService.NumeroInternacional(limpo);
    }
}
