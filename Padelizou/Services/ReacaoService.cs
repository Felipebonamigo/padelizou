using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.ViewModels;

namespace Padelizou.Services;

// AS REAÇÕES COM EMOJI DE CADA JOGO — 12/09/2026.
//
// 🗣️ Felipe: *"aqui, a cada jogo, permita a pessoa 'reagir' tipo o que tem aqui no discord, com
// emojis"* e *"e ao clicar no emoji, veja quem colocou o que, igual no whats app"*.
//
// A forma é a do PalpiteService, de propósito — mesma tela, mesmos 97 jogos, mesma pergunta
// "quem colocou o quê". O que muda é que aqui a pessoa pode ter VÁRIAS reações no mesmo jogo
// (o emoji entra na chave), e a peneira do que é emoji mora no EmojiDeReacao.
public interface IReacaoService
{
    Task<Dictionary<int, ReacoesDaPartidaVM>> ObterResumosAsync(IEnumerable<int> partidaIds, int? jogadorId);
    Task<ReacoesDaPartidaVM> ReagirAsync(int partidaId, int jogadorId, string? emoji);
    Task<ReacoesDaPartidaVM> TirarReacaoAsync(int partidaId, int jogadorId, string? emoji);
    Task<QuemReagiuVM?> ObterQuemReagiuAsync(int partidaId, int? jogadorId);
}

public class ReacaoService : IReacaoService
{
    private readonly DbPadelContext _context;

    public ReacaoService(DbPadelContext context) => _context = context;

    // ⚠️ EM LOTE, UMA CONSULTA PRA LISTA INTEIRA. A lista de jogos do torneio tem 97 cartões, e
    // perguntar as reações jogo a jogo é como uma tela vira 97 idas ao banco — o mesmo desenho
    // (e o mesmo motivo) do `PalpiteService.ObterResumosAsync`.
    //
    // ⚠️ TODO id pedido volta no dicionário, inclusive o jogo sem reação nenhuma: a tela faz
    // `GetValueOrDefault`, e um id faltando ali e um jogo vazio são coisas diferentes.
    public async Task<Dictionary<int, ReacoesDaPartidaVM>> ObterResumosAsync(IEnumerable<int> partidaIds, int? jogadorId)
    {
        var ids = partidaIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, ReacoesDaPartidaVM>();

        var reacoes = await _context.ReacoesDaPartida
            .Where(r => ids.Contains(r.PartidaId))
            .Select(r => new { r.PartidaId, r.JogadorId, r.Emoji, r.CriadoEm })
            .ToListAsync();

        return ids.ToDictionary(id => id, id => new ReacoesDaPartidaVM
        {
            PartidaId = id,
            Reacoes = reacoes
                .Where(r => r.PartidaId == id)
                .GroupBy(r => r.Emoji)
                .Select(g => new ReacaoContadaVM
                {
                    Emoji = g.Key,
                    Total = g.Count(),
                    // ⚠️ `jogadorId.HasValue &&` na frente: sem ele, quem não está logado
                    // compararia JogadorId com nulo e a pílula nasceria marcada por acidente.
                    EuReagi = jogadorId.HasValue && g.Any(r => r.JogadorId == jogadorId.Value),
                })
                // A mais usada primeiro; empate desfeito por quem chegou antes, pra fileira não
                // trocar de ordem a cada tique da atualização automática.
                .OrderByDescending(p => p.Total)
                .ThenBy(p => reacoes.Where(r => r.PartidaId == id && r.Emoji == p.Emoji).Min(r => r.CriadoEm))
                .ToList(),
        });
    }

    public async Task<ReacoesDaPartidaVM> ReagirAsync(int partidaId, int jogadorId, string? emoji)
    {
        // A PENEIRA ANTES DE TUDO. A paleta é livre (escolha do Felipe), então este é o único
        // ponto entre o POST e o que o torneio inteiro lê no card do jogo.
        var normalizado = EmojiDeReacao.Normalizar(emoji)
            ?? throw new InvalidOperationException("Isso não é um emoji.");

        // ⚠️ O `partidaId` vem congelado no HTML e o jogo não: regerar a chave, regerar o
        // americano e mudar um resultado do mata-mata APAGAM partidas. Sem esta checagem, quem
        // está com a lista velha aberta recebe a violação de chave estrangeira crua — erro de
        // sistema no lugar de "este jogo saiu da lista" (foram três 500 no vigia em 11/09 pela
        // mesma porta, no botão de ver quem votou).
        if (!await _context.Partidas.AnyAsync(p => p.Id == partidaId))
            throw new InvalidOperationException("Este jogo saiu da lista — atualize a página.");

        // ⚠️ IDEMPOTENTE: o toque duplo manda dois POSTs. A trava DE VERDADE é a chave composta
        // (PartidaId, JogadorId, Emoji) — este `if` é o que evita o erro na cara de quem
        // conseguiu exatamente o que queria, e não o que garante uma linha só.
        bool jaReagi = await _context.ReacoesDaPartida
            .AnyAsync(r => r.PartidaId == partidaId && r.JogadorId == jogadorId && r.Emoji == normalizado);

        if (!jaReagi)
        {
            _context.ReacoesDaPartida.Add(new ReacaoDaPartida
            {
                PartidaId = partidaId,
                JogadorId = jogadorId,
                Emoji = normalizado,
            });
            await _context.SaveChangesAsync();
        }

        return await ResumoDeUmAsync(partidaId, jogadorId);
    }

    // ⚠️ A CHECAGEM DE DONO É ESTRUTURAL: a linha é achada por (partida, jogador, emoji), e o
    // jogador vem da claim no controller. Não existe parâmetro por onde pedir a reação de outra
    // pessoa — é a mesma forma do `RetirarPalpiteAsync`.
    //
    // ⚠️ E É IDEMPOTENTE pelo mesmo motivo do irmão: o segundo POST do toque duplo chega com a
    // linha já apagada, e estourar ali viraria alerta vermelho pra quem já conseguiu o que quis.
    public async Task<ReacoesDaPartidaVM> TirarReacaoAsync(int partidaId, int jogadorId, string? emoji)
    {
        var normalizado = EmojiDeReacao.Normalizar(emoji);

        if (normalizado != null)
        {
            var minha = await _context.ReacoesDaPartida
                .FirstOrDefaultAsync(r => r.PartidaId == partidaId
                    && r.JogadorId == jogadorId && r.Emoji == normalizado);

            if (minha != null)
            {
                _context.ReacoesDaPartida.Remove(minha);
                await _context.SaveChangesAsync();
            }
        }

        return await ResumoDeUmAsync(partidaId, jogadorId);
    }

    // QUEM COLOCOU O QUÊ (🗣️ *"igual no whats app"*). Uma linha por REAÇÃO: quem pôs 😂 e 🔥
    // aparece nas duas, que é o que o painel do WhatsApp mostra.
    //
    // ⚠️ Devolve NULO quando o jogo não existe mais, e NÃO estoura como as duas que gravam —
    // é a mesma divisão do PalpiteService: o controller vira isso em 404, e o JS já sabe dizer
    // "este jogo saiu da lista". 404 é resposta; 500 é defeito.
    public async Task<QuemReagiuVM?> ObterQuemReagiuAsync(int partidaId, int? jogadorId)
    {
        if (!await _context.Partidas.AnyAsync(p => p.Id == partidaId)) return null;

        var reacoes = await _context.ReacoesDaPartida
            .Where(r => r.PartidaId == partidaId)
            .Select(r => new { r.Emoji, r.CriadoEm, r.Jogador.Nome, r.Jogador.FotoPerfil })
            .ToListAsync();

        return new QuemReagiuVM
        {
            // As MESMAS pílulas do cartão, pro painel abrir já com onde tocar.
            Reacoes = (await ObterResumosAsync(new[] { partidaId }, jogadorId))[partidaId].Reacoes,

            // A MESMA ORDEM DAS PÍLULAS do card — quem clicou numa pílula procura aquele emoji
            // na lista, e duas ordens diferentes pras mesmas reações fariam ele procurar duas
            // vezes. Dentro de cada emoji, quem reagiu primeiro vem primeiro.
            Linhas = reacoes
                .GroupBy(r => r.Emoji)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Min(r => r.CriadoEm))
                .SelectMany(g => g
                    .OrderBy(r => r.CriadoEm)
                    .Select(r => new QuemReagiuLinhaVM
                    {
                        Emoji = g.Key,
                        Nome = r.Nome,
                        FotoPerfil = r.FotoPerfil,
                    }))
                .ToList(),
        };
    }

    private async Task<ReacoesDaPartidaVM> ResumoDeUmAsync(int partidaId, int? jogadorId) =>
        (await ObterResumosAsync(new[] { partidaId }, jogadorId))[partidaId];
}
