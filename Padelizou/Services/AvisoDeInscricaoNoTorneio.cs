using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// O texto do apito, puro, longe do banco e da rede — é texto que precisa estar certo, e texto
// certo se testa. Mesmo padrão do TextoDoTeste.
public static class TextoDoApito
{
    public const string Titulo = "Apitouuuu! 📣";

    // "Dupla Ana e Bia inscrita no torneio Copa de Verão. Na categoria Open Feminino."
    // "Carlos inscrito no torneio Copa de Verão. Na categoria Open Masculino."
    //
    // ⚠️ Uma linha só, sem quebra: a caixa de entrada guarda o corpo como texto e a
    // notificação do celular corta o que passa de duas linhas. O "Na categoria" que o Felipe
    // pediu vira a segunda frase em vez de segunda linha — some em menos telas.
    public static string Corpo(IReadOnlyList<string> nomes, string torneio, string categoria)
    {
        var quem = nomes.Count switch
        {
            0 => "Alguém",
            1 => nomes[0],
            _ => string.Join(" e ", nomes),
        };

        // Dupla é o caso de DOIS nomes. Inscrição sem parceiro entra como um nome só e é
        // tratada como individual de propósito: dizer "dupla" pra quem ainda está sozinho
        // seria descrever errado o que apareceu na chave.
        var frase = nomes.Count >= 2
            ? $"Dupla {quem} inscrita no torneio {torneio}."
            : $"{quem} inscrito no torneio {torneio}.";

        return string.IsNullOrWhiteSpace(categoria)
            ? frase
            : $"{frase} Na categoria {categoria}.";
    }
}

// Avisa quem SEGUE o torneio que entrou gente nova — e, desde 20/08/2026, é também quem
// FAZ a inscrição seguir o torneio sozinha (ver SeguirAsync). Existe como serviço, e não
// como método de controller, por um motivo específico deste projeto: a inscrição tem TRÊS
// portas — dupla (DuplasController), individual/americano (TorneiosController.Inscricoes)
// e a do pagamento obrigatório, onde a inscrição nasce no webhook
// (PagamentoInscricaoService.EfetivarTorneioAsync) — e regra de torneio duplicada é a
// causa histórica dos defeitos graves daqui. O gancho de seguidores de PESSOA já vive
// espalhado nas cópias; este nasce com uma implementação só.
public class AvisoDeInscricaoNoTorneio
{
    private readonly DbPadelContext _context;
    private readonly IPushNotificationService _push;

    public AvisoDeInscricaoNoTorneio(DbPadelContext context, IPushNotificationService push)
    {
        _context = context;
        _push = push;
    }

    // Inscreveu, seguiu (Felipe, 20/08/2026): quem entra na chave é exatamente quem quer
    // saber quem mais entra — e quase ninguém achava o botão. Seguir deixou de ser convite
    // e virou consequência da inscrição; o botão na tela do torneio continua existindo,
    // agora como porta de SAÍDA (e de volta) pra quem não quiser os avisos.
    //
    // Idempotente de propósito: a mesma pessoa se inscreve em duas categorias do mesmo
    // torneio, e a segunda inscrição não pode nem duplicar a linha nem estourar na chave
    // composta (TorneioId + JogadorId). Efeito colateral assumido: quem deixou de seguir e
    // se inscreve DE NOVO volta a seguir — inscrição nova é interesse novo, e a porta de
    // saída continua a um clique.
    //
    // Lista de espera TAMBÉM segue, ao contrário do apito: o apito anuncia vaga ocupada, e
    // seguir é assinatura — quem espera vaga é quem mais quer os avisos quando ela abrir.
    public async Task SeguirAsync(int torneioId, IEnumerable<int> jogadorIds)
    {
        var ids = jogadorIds.Distinct().ToList();

        var jaSeguem = await _context.SeguidoresTorneio
            .Where(s => s.TorneioId == torneioId && ids.Contains(s.JogadorId))
            .Select(s => s.JogadorId)
            .ToListAsync();

        var novos = ids.Except(jaSeguem).ToList();
        if (novos.Count == 0) return;

        foreach (var jogadorId in novos)
            _context.SeguidoresTorneio.Add(new SeguidorTorneio { TorneioId = torneioId, JogadorId = jogadorId });

        await _context.SaveChangesAsync();
    }

    // `recemInscritos` são os ids de quem ACABOU de entrar. Eles saem da lista de destinatários
    // mesmo que sigam o torneio: a pessoa não precisa de notificação pra saber que ela mesma
    // se inscreveu, e receber isso é o tipo de aviso que ensina a ignorar o canal.
    public async Task NotificarAsync(int torneioId, string categoria,
        IReadOnlyList<string> nomesInscritos, IEnumerable<int> recemInscritos, string? url)
    {
        var torneio = await _context.Torneios
            .Where(t => t.Id == torneioId)
            .Select(t => new { t.Nome })
            .FirstOrDefaultAsync();

        if (torneio == null) return;

        var excluir = recemInscritos.ToHashSet();

        var seguidores = await _context.SeguidoresTorneio
            .Where(s => s.TorneioId == torneioId && !excluir.Contains(s.JogadorId))
            .Select(s => s.JogadorId)
            .ToListAsync();

        if (seguidores.Count == 0) return;

        var corpo = TextoDoApito.Corpo(nomesInscritos, torneio.Nome, categoria);

        // ⚠️ SEM E-MAIL, e a decisão é do mesmo tipo que tirou o resultado de partida do
        // e-mail em 09/08: isto é RAJADA (um torneio de 46 duplas dispara 46 avisos por
        // seguidor) e não pede ação nenhuma de quem recebe. E-mail assim é o que faz a pessoa
        // marcar o remetente como lixo — e aí ela perde junto o aviso de que a chave saiu.
        foreach (var jogadorId in seguidores)
            await _push.EnviarParaJogadorAsync(jogadorId, TextoDoApito.Titulo, corpo, url,
                AlcanceDoAviso.AppSemEmail);
    }
}
