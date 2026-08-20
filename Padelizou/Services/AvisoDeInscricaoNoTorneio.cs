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

// O texto das últimas vagas, puro. Nulo pra qualquer contagem que não seja um dos DOIS
// limiares — é o nulo que faz o aviso disparar só no cruzamento exato (3 e 1), e não a cada
// inscrição de uma categoria quase cheia.
public static class TextoDasVagas
{
    public static TextoDoAviso? Do(int restantes, string categoria, string torneio)
    {
        var onde = string.IsNullOrWhiteSpace(categoria)
            ? $"no torneio {torneio}"
            : $"na {categoria} do {torneio}";

        return restantes switch
        {
            1 => new("Última vaga! ⏳", $"Ficou só 1 vaga {onde}. Quem quer entrar, é agora."),
            3 => new("As vagas estão acabando", $"Restam 3 vagas {onde}."),
            _ => null,
        };
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
    //
    // `categoriaId` (e não só o nome) desde 20/08/2026: é dele que sai a conta das ÚLTIMAS
    // VAGAS — o aviso de urgência que aproveita a mesma passada da inscrição.
    public async Task NotificarAsync(int torneioId, int categoriaId,
        IReadOnlyList<string> nomesInscritos, IEnumerable<int> recemInscritos, string? url)
    {
        var torneio = await _context.Torneios
            .Where(t => t.Id == torneioId)
            .Select(t => new { t.Nome })
            .FirstOrDefaultAsync();

        if (torneio == null) return;

        var categoria = await _context.Categorias
            .Where(c => c.Id == categoriaId)
            .Select(c => new { c.Nome, c.LimiteDuplas })
            .FirstOrDefaultAsync();

        var excluir = recemInscritos.ToHashSet();

        var seguidores = await _context.SeguidoresTorneio
            .Where(s => s.TorneioId == torneioId && !excluir.Contains(s.JogadorId))
            .Select(s => s.JogadorId)
            .ToListAsync();

        if (seguidores.Count == 0) return;

        var corpo = TextoDoApito.Corpo(nomesInscritos, torneio.Nome, categoria?.Nome ?? "");

        // ⚠️ SEM E-MAIL, e a decisão é do mesmo tipo que tirou o resultado de partida do
        // e-mail em 09/08: isto é RAJADA (um torneio de 46 duplas dispara 46 avisos por
        // seguidor) e não pede ação nenhuma de quem recebe. E-mail assim é o que faz a pessoa
        // marcar o remetente como lixo — e aí ela perde junto o aviso de que a chave saiu.
        foreach (var jogadorId in seguidores)
            await _push.EnviarParaJogadorAsync(jogadorId, TextoDoApito.Titulo, corpo, url,
                AlcanceDoAviso.AppSemEmail);

        await AvisarUltimasVagasAsync(torneioId, categoriaId, categoria?.Nome ?? "",
            categoria?.LimiteDuplas, torneio.Nome, url);
    }

    // AS ÚLTIMAS VAGAS: quando a inscrição que acabou de entrar deixa a categoria com
    // exatamente 3 (ou 1) vagas, quem segue o torneio e AINDA NÃO ESTÁ nessa categoria ouve
    // a urgência. O disparo no cruzamento exato dispensa marcador de "já avisei": a contagem
    // só passa por cada limiar uma vez descendo — e se alguém desistir e a vaga reabrir,
    // avisar de novo no próximo cruzamento é notícia verdadeira, não repetição.
    private async Task AvisarUltimasVagasAsync(int torneioId, int categoriaId, string categoria,
        int? limiteDuplas, string torneio, string? url)
    {
        if (limiteDuplas == null) return;

        // As DUAS portas contam (dupla e americano) — em cada categoria só uma delas tem
        // linhas, mas perguntar só a uma é o defeito clássico deste projeto.
        int ocupadas = await _context.Duplas
                           .CountAsync(d => d.CategoriaId == categoriaId && !d.EmListaDeEspera)
                       + await _context.InscricoesAmericanas
                           .CountAsync(i => i.CategoriaId == categoriaId && !i.EmListaDeEspera);

        var texto = TextoDasVagas.Do(limiteDuplas.Value - ocupadas, categoria, torneio);
        if (texto == null) return;

        // Quem já está NESTA categoria não precisa de urgência — a vaga dele já era. Lista de
        // espera idem: ele já fez a parte dele, avisá-lo só esfregaria a fila.
        var jaNaCategoria = (await _context.Duplas
                .Where(d => d.CategoriaId == categoriaId)
                .Select(d => new { d.Jogador1Id, d.Jogador2Id })
                .ToListAsync())
            .SelectMany(d => new[] { (int?)d.Jogador1Id, d.Jogador2Id })
            .Where(id => id != null)
            .Select(id => id!.Value)
            .Concat(await _context.InscricoesAmericanas
                .Where(i => i.CategoriaId == categoriaId)
                .Select(i => i.JogadorId)
                .ToListAsync())
            .ToHashSet();

        var interessados = await _context.SeguidoresTorneio
            .Where(s => s.TorneioId == torneioId && !jaNaCategoria.Contains(s.JogadorId))
            .Select(s => s.JogadorId)
            .ToListAsync();

        foreach (var jogadorId in interessados)
            await _push.EnviarParaJogadorAsync(jogadorId, texto.Titulo, texto.Corpo, url,
                AlcanceDoAviso.AppSemEmail);
    }
}
