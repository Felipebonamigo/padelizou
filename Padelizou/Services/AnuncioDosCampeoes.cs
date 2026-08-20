using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// O texto do título decidido, puro (mesmo padrão do TextoDoApito).
public static class TextoDosCampeoes
{
    public const string Titulo = "Título decidido! 🏆";

    // "Ana e Bia levaram a Open do Copa de Verão. Conta pra gente como foi: avalie o torneio."
    //
    // "Levou/levaram" de propósito: é a frase que funciona pra dupla, pro campeão solo do
    // Americano e pra qualquer gênero de categoria — sem o sistema ter que adivinhar entre
    // "campeões" e "campeãs" (a mesma prudência do CampeoesDoTorneio.Rotulo, sem precisar
    // dele num push).
    public static string Corpo(IReadOnlyList<string> nomes, string categoria, string torneio)
    {
        var quem = nomes.Count switch
        {
            0 => "O título",
            1 => nomes[0],
            _ => string.Join(" e ", nomes),
        };

        var verbo = nomes.Count == 1 ? "levou" : "levaram";
        var onde = string.IsNullOrWhiteSpace(categoria) ? torneio : $"{categoria} do {torneio}";

        return nomes.Count == 0
            ? $"A {onde} tem dono. Conta pra gente como foi: avalie o torneio."
            : $"{quem} {verbo} a {onde}. Conta pra gente como foi: avalie o torneio.";
    }
}

// "TEMOS CAMPEÕES": quando uma categoria coroa, quem SEGUE o torneio fica sabendo — e é
// convidado a avaliar. Agora que toda inscrição segue o torneio sozinha, este é o aviso que
// fecha o ciclo aberto pelo "Apitouuuu!" da inscrição.
//
// O gancho é a CORONAÇÃO, não a final: o Americano coroa sem partida de final (líder de
// games), o desempate coroa por outra fase, e o organizador coroa na mão no empate de 3+.
// Quem chama confere "não havia campeão antes" e chama depois dos robôs — aqui só se
// pergunta "há campeão agora?".
public class AnuncioDosCampeoes
{
    private readonly DbPadelContext _context;
    private readonly IPushNotificationService _push;

    public AnuncioDosCampeoes(DbPadelContext context, IPushNotificationService push)
    {
        _context = context;
        _push = push;
    }

    public async Task AnunciarSeCoroouAsync(int categoriaId, int torneioId, string? url)
    {
        // O mais recente (maior Id) ganha se houver dois carimbos — a mesma escolha do
        // CampeoesDoTorneio, pelo mesmo motivo: dado torto não pode virar dois anúncios.
        var campea = await _context.Duplas
            .Include(d => d.Jogador1).Include(d => d.Jogador2)
            .Where(d => d.CategoriaId == categoriaId && d.UltimaFase == CampeoesDoTorneio.FaseDeCampeao)
            .OrderByDescending(d => d.Id)
            .FirstOrDefaultAsync();

        if (campea == null) return;

        var torneio = await _context.Torneios
            .Where(t => t.Id == torneioId)
            .Select(t => new { t.Nome, t.Status })
            .FirstOrDefaultAsync();
        if (torneio == null) return;

        // A mesma régua da página do torneio: campeão de torneio cancelado não se anuncia.
        if (!CampeoesDoTorneio.PodeAnunciar(torneio.Status)) return;

        var categoria = await _context.Categorias
            .Where(c => c.Id == categoriaId)
            .Select(c => c.Nome)
            .FirstOrDefaultAsync() ?? "";

        // Time campeão: o nome do time é o nome que se anuncia (a linha não tem jogadores de
        // verdade — o Jogador1 é quem cadastrou).
        var nomes = campea.NomeTime != null
            ? new List<string> { campea.NomeTime }
            : new[] { campea.Jogador1?.ComoChamar, campea.Jogador2?.ComoChamar }
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n!)
                .ToList();

        // Os campeões ficam DE FORA da lista: eles acabaram de viver isso na quadra (e o
        // vencedor da final já recebeu o "🏆 Campeões!" pessoal do resultado).
        var campeoes = campea.NomeTime != null
            ? new HashSet<int>()
            : new[] { campea.Jogador1Id, campea.Jogador2Id }
                .Where(id => id != null).Select(id => id!.Value).ToHashSet();

        var seguidores = await _context.SeguidoresTorneio
            .Where(s => s.TorneioId == torneioId && !campeoes.Contains(s.JogadorId))
            .Select(s => s.JogadorId)
            .ToListAsync();

        var corpo = TextoDosCampeoes.Corpo(nomes, categoria, torneio.Nome);

        // Bilhete social em rajada (um por seguidor, uma vez por categoria): a régua do
        // AppSemEmail — bom de ver, ninguém age agora, e-mail pra isso é o que queima cota.
        foreach (var jogadorId in seguidores)
            await _push.EnviarParaJogadorAsync(jogadorId, TextoDosCampeoes.Titulo, corpo, url,
                AlcanceDoAviso.AppSemEmail);
    }
}
