using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.ViewModels;

namespace Padelizou.Services;

public class PalpiteService : IPalpiteService
{
    private readonly DbPadelContext _context;

    public PalpiteService(DbPadelContext context)
    {
        _context = context;
    }

    public async Task<Dictionary<int, PalpiteResumoVM>> ObterResumosAsync(IEnumerable<int> partidaIds, int? jogadorId)
    {
        var ids = partidaIds.ToList();
        if (ids.Count == 0) return new Dictionary<int, PalpiteResumoVM>();

        var partidas = await _context.Partidas
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Dupla1Id, p.Dupla2Id, p.TorneioId, p.Fase })
            .ToListAsync();

        var votos = await _context.PalpitesPartida
            .Where(v => ids.Contains(v.PartidaId))
            .Select(v => new
            {
                v.PartidaId, v.JogadorId, v.DuplaEscolhidaId,
                v.GamesDupla1, v.GamesDupla2, v.SetsDupla1, v.SetsDupla2,
            })
            .ToListAsync();

        // Os torneios de uma vez só. É quase sempre UM (a tela mostra os jogos de um torneio),
        // e a alternativa — perguntar o formato jogo a jogo — seria uma consulta por linha da
        // lista, que é como uma tela de 40 jogos vira 40 idas ao banco.
        var torneios = await TorneiosDasPartidasAsync(partidas.Select(p => p.TorneioId));

        var resultado = new Dictionary<int, PalpiteResumoVM>();
        foreach (var p in partidas)
        {
            var votosDaPartida = votos.Where(v => v.PartidaId == p.Id).ToList();
            var meuVoto = jogadorId.HasValue
                ? votosDaPartida.FirstOrDefault(v => v.JogadorId == jogadorId.Value)
                : null;

            var meuPlacar = meuVoto == null
                ? new PlacarPalpitado(null, null, EmSets: false)
                : PlacaresPossiveis.Lido(meuVoto.GamesDupla1, meuVoto.GamesDupla2,
                                         meuVoto.SetsDupla1, meuVoto.SetsDupla2);

            var formato = FormatoDaPartida.De(
                p.TorneioId is int t ? torneios.GetValueOrDefault(t) : null, p.Fase);

            resultado[p.Id] = new PalpiteResumoVM
            {
                PartidaId = p.Id,
                VotosDupla1 = votosDaPartida.Count(v => v.DuplaEscolhidaId == p.Dupla1Id),
                VotosDupla2 = votosDaPartida.Count(v => v.DuplaEscolhidaId == p.Dupla2Id),
                MeuVotoDuplaId = meuVoto?.DuplaEscolhidaId,
                MeuPlacarLado1 = meuPlacar.Lado1,
                MeuPlacarLado2 = meuPlacar.Lado2,
                PlacarEmSets = PlacaresPossiveis.EmSets(formato),
            };
        }
        return resultado;
    }

    public async Task<PalpiteResumoVM> RegistrarVotoAsync(int partidaId, int jogadorId, int duplaEscolhidaId,
        int? placarLado1 = null, int? placarLado2 = null)
    {
        var partida = await _context.Partidas.FindAsync(partidaId);
        if (partida == null) throw new InvalidOperationException("Partida não encontrada.");
        if (partida.Status != "Agendada") throw new InvalidOperationException("Esta partida já começou — não é mais possível palpitar.");
        if (duplaEscolhidaId != partida.Dupla1Id && duplaEscolhidaId != partida.Dupla2Id)
            throw new InvalidOperationException("Dupla inválida para esta partida.");

        // ⚠️ TODA a validação do placar acontece AQUI, e não na tela. A tela oferece fichas com
        // os placares possíveis; quem RECUSA é o servidor — um POST montado à mão não passa por
        // view nenhuma, e é ele que gravaria o "6 x 9" que nenhum jogo termina.
        var formato = FormatoDaPartida.De(await TorneioDaPartidaAsync(partida), partida.Fase);
        var placar = ValidarPlacar(formato, partida, duplaEscolhidaId, placarLado1, placarLado2);

        var voto = await _context.PalpitesPartida
            .FirstOrDefaultAsync(v => v.PartidaId == partidaId && v.JogadorId == jogadorId);

        if (voto == null)
        {
            voto = new PalpitePartida { PartidaId = partidaId, JogadorId = jogadorId, DuplaEscolhidaId = duplaEscolhidaId };
            _context.PalpitesPartida.Add(voto);
        }
        else
        {
            voto.DuplaEscolhidaId = duplaEscolhidaId;
            voto.DataHora = DateTime.Now;
        }

        // ⚠️ As quatro colunas são reescritas SEMPRE, inclusive pra nulo. Trocar de opinião sem
        // dizer o placar tem que APAGAR o placar anterior: ele apontava a outra dupla, e uma
        // linha com "vence a Dupla 1" e "4 x 6" gravados juntos é uma contradição que o ranking
        // leria como palpite de placar — e contaria contra a própria pessoa.
        voto.GamesDupla1 = placar.EmSets ? null : placar.Lado1;
        voto.GamesDupla2 = placar.EmSets ? null : placar.Lado2;
        voto.SetsDupla1 = placar.EmSets ? placar.Lado1 : null;
        voto.SetsDupla2 = placar.EmSets ? placar.Lado2 : null;

        await _context.SaveChangesAsync();

        var resumos = await ObterResumosAsync(new[] { partidaId }, jogadorId);
        return resumos[partidaId];
    }

    // O placar palpitado, conferido contra o formato do jogo e contra o próprio voto. Devolve
    // "não palpitou placar" quando não veio nada — que é o caminho de sempre.
    private static PlacarPalpitado ValidarPlacar(FormatoDaPartida.Formato formato, Partida partida,
        int duplaEscolhidaId, int? lado1, int? lado2)
    {
        var vazio = new PlacarPalpitado(null, null, PlacaresPossiveis.EmSets(formato));

        if (lado1 == null && lado2 == null) return vazio;

        // ⚠️ Meio placar é recusa, não "deixa pra lá": completar o lado que faltou com zero
        // inventaria um palpite que a pessoa não deu, e ignorar em silêncio faria a tela
        // dizer "palpite registrado" pra um placar que não foi gravado.
        if (lado1 == null || lado2 == null)
            throw new InvalidOperationException("Palpite de placar incompleto — diga os dois lados.");

        if (!PlacaresPossiveis.Aceita(formato, lado1.Value, lado2.Value))
            throw new InvalidOperationException(
                $"{lado1} x {lado2} não é um placar que fecha este jogo.");

        // O placar e o voto têm que dizer a MESMA coisa. Palpitar "a Dupla 1 vence" e "4 x 6"
        // ao mesmo tempo são duas respostas contraditórias, e escolher uma delas pela pessoa
        // seria o sistema decidindo qual das duas ela quis dizer.
        int? ladoDoPlacar = PlacaresPossiveis.LadoVencedor(lado1.Value, lado2.Value);
        int duplaDoPlacar = ladoDoPlacar == 1 ? partida.Dupla1Id : partida.Dupla2Id;

        if (duplaDoPlacar != duplaEscolhidaId)
            throw new InvalidOperationException("O placar aponta a outra dupla — escolha o placar de quem você acha que vence.");

        return vazio with { Lado1 = lado1, Lado2 = lado2 };
    }

    private async Task<Torneio?> TorneioDaPartidaAsync(Partida partida) =>
        partida.TorneioId is int id ? await _context.Torneios.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id) : null;

    private async Task<Dictionary<int, Torneio>> TorneiosDasPartidasAsync(IEnumerable<int?> torneioIds)
    {
        var ids = torneioIds.Where(t => t != null).Select(t => t!.Value).Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, Torneio>();

        return await _context.Torneios
            .AsNoTracking()
            .Where(t => ids.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id);
    }

    public async Task<VotantesPartidaVM> ObterVotantesAsync(int partidaId)
    {
        var partida = await _context.Partidas.FindAsync(partidaId);
        if (partida == null) throw new InvalidOperationException("Partida não encontrada.");

        var votos = await _context.PalpitesPartida
            .Include(v => v.Jogador)
            .Where(v => v.PartidaId == partidaId)
            .ToListAsync();

        return new VotantesPartidaVM
        {
            VotantesDupla1 = votos.Where(v => v.DuplaEscolhidaId == partida.Dupla1Id)
                .Select(v => new VotanteVM { Nome = v.Jogador.Nome, FotoPerfil = v.Jogador.FotoPerfil }).ToList(),
            VotantesDupla2 = votos.Where(v => v.DuplaEscolhidaId == partida.Dupla2Id)
                .Select(v => new VotanteVM { Nome = v.Jogador.Nome, FotoPerfil = v.Jogador.FotoPerfil }).ToList()
        };
    }
}
