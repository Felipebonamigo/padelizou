using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.ViewModels;

namespace Padelizou.Services;

// A tela do ranking de desafios, montada num lugar só.
//
// Nasceu quando a MESMA tabela passou a aparecer em dois lugares: a tela cheia
// (/Desafios/Ranking, com os filtros de categoria e clube) e a aba Desafios do hub de ranking
// (/Jogadores/Ranking). Enquanto a montagem morava dentro do DesafiosController, levá-la pro hub
// significava copiá-la — e a cópia é como uma das duas telas segue contando desafio que a outra
// já parou de contar, calada.
//
// ⚠️ Aqui não se decide QUEM PODE VER: isso é PortaDosDesafios, e cada tela pergunta a ela antes
// de chamar esta montagem. `emConstrucao` só chega junto porque é a faixa amarela que a tela
// desenha — não é permissão.
public class TelaDoRankingDeDesafios
{
    private readonly DbPadelContext _context;

    public TelaDoRankingDeDesafios(DbPadelContext context) => _context = context;

    public async Task<RankingDeDesafiosVM> MontarAsync(int meuId, bool emConstrucao, DateTime agora,
        int? categoria = null, int? clube = null)
    {
        // ⚠️ O "o que conta" (confirmado + últimos 12 meses) sai de RankingDeDesafios.QueContam,
        // o mesmo que o retrospecto do mural e a linha do perfil usam. Repetir a cláusula aqui
        // seria a terceira cópia — e a que ficaria pra trás no dia em que nascer um estado novo.
        var query = RankingDeDesafios.QueContam(_context.Desafios.AsNoTracking(), agora);

        if (categoria is > 0) query = query.Where(d => d.CategoriaPadraoId == categoria);
        if (clube is > 0) query = query.Where(d => d.ClubeId == clube);

        var confirmados = await query.ToListAsync();
        var pessoas = await PessoasDosDesafiosAsync(confirmados);

        return new RankingDeDesafiosVM(
            RankingDeDesafios.PorDupla(confirmados, pessoas),
            RankingDeDesafios.PorJogador(confirmados, pessoas),
            await CinturoesNoArAsync(agora, categoria),
            await _context.CategoriasPadrao.Ativas().OrderBy(c => c.Id).ToListAsync(),
            await ClubesComDesafioAsync(agora),
            categoria,
            clube,
            meuId,
            emConstrucao);
    }

    // Os cinturões de pé agora, com o quanto cada dono está perto de perder por não defender.
    //
    // ⚠️ "Quantas faltam" é lido do RELÓGIO aqui, e não de uma coluna: o vigia que executa a
    // troca roda de 6 em 6 horas, e a tela não pode esperar por ele pra contar a verdade. Vigia
    // parado atrasa a troca; nunca esconde o estado.
    private async Task<List<CinturaoNaTela>> CinturoesNoArAsync(DateTime agora, int? categoria)
    {
        var donos = await _context.ReinadosNoCinturao
            .AsNoTracking()
            .Include(r => r.CategoriaPadrao)
            .Include(r => r.Jogador1)
            .Include(r => r.Jogador2)
            .Where(r => r.TerminouEm == null)
            .Where(r => categoria == null || r.CategoriaPadraoId == categoria)
            .ToListAsync();

        if (donos.Count == 0) return new();

        // A janela da regra mais uma folga: um desafio proposto pouco antes dela ainda pode
        // VENCER dentro dela (a proposta morre 48h depois de nascer).
        var desdeQuando = agora - Cinturao.JanelaDaDefesa - EstadoDoDesafio.PrazoParaResponder;
        var categorias = donos.Select(d => d.CategoriaPadraoId).ToList();

        var recentes = await _context.Desafios
            .AsNoTracking()
            .Where(d => categorias.Contains(d.CategoriaPadraoId) && d.PropostoEm >= desdeQuando)
            .ToListAsync();

        return donos
            .Select(d => new CinturaoNaTela(
                d.CategoriaPadrao.Nome,
                DuplaNaTela.Nome(d.Jogador1, d.Jogador2),
                d.Donos.ToList(),
                d.ComecouEm,
                d.Defesas,
                Cinturao.QuantasFaltamParaPerder(d, recentes, agora)))
            .OrderByDescending(c => c.Defesas)
            .ThenBy(c => c.Categoria, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // Os quatro jogadores de cada desafio, pro ranking saber escrever os nomes.
    private async Task<Dictionary<int, Jogador>> PessoasDosDesafiosAsync(List<Desafio> desafios)
    {
        var ids = desafios.SelectMany(d => d.Envolvidos).Distinct().ToList();
        if (ids.Count == 0) return new();

        return await _context.Jogadores
            .AsNoTracking()
            .Where(j => ids.Contains(j.Id))
            .ToDictionaryAsync(j => j.Id);
    }

    // Só os clubes que JÁ receberam desafio contado. Oferecer o catálogo inteiro encheria o
    // filtro de clubes que devolvem tabela vazia — e um filtro que só sabe esvaziar a tela
    // ensina a pessoa a não usar filtro.
    private async Task<List<Clube>> ClubesComDesafioAsync(DateTime agora)
    {
        var ids = await RankingDeDesafios
            .QueContam(_context.Desafios.AsNoTracking(), agora)
            .Select(d => d.ClubeId)
            .Distinct()
            .ToListAsync();

        return await _context.Clubes
            .Where(c => ids.Contains(c.Id))
            .OrderBy(c => c.Nome)
            .ToListAsync();
    }
}
