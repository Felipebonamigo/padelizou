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
            .Select(p => new
            {
                p.Id, p.Dupla1Id, p.Dupla2Id, p.TorneioId, p.Fase,
                // O placar de verdade entra pra tela poder dizer QUEM CRAVOU depois do jogo.
                p.VencedorId, p.GamesDupla1, p.GamesDupla2, p.SetsDupla1, p.SetsDupla2,
                p.MotivoDoEncerramento,
            })
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
        var cravaram = new Dictionary<int, List<int>>();   // partida → jogadores que cravaram

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

            var comPlacar = votosDaPartida
                .Select(v => PlacaresPossiveis.Lido(v.GamesDupla1, v.GamesDupla2, v.SetsDupla1, v.SetsDupla2))
                .Where(placar => placar.Existe)
                .ToList();

            // O placar mais palpitado. ⚠️ A ordenação é TOTAL (votos, depois os dois lados):
            // com dois placares empatados, uma ordenação parcial faria a frase da tela trocar
            // sozinha entre dois carregamentos da mesma página.
            var maisPalpitado = comPlacar
                .GroupBy(placar => (placar.Lado1, placar.Lado2))
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => g.Key.Lado1)
                .ThenByDescending(g => g.Key.Lado2)
                .FirstOrDefault();

            resultado[p.Id] = new PalpiteResumoVM
            {
                PartidaId = p.Id,
                VotosDupla1 = votosDaPartida.Count(v => v.DuplaEscolhidaId == p.Dupla1Id),
                VotosDupla2 = votosDaPartida.Count(v => v.DuplaEscolhidaId == p.Dupla2Id),
                MeuVotoDuplaId = meuVoto?.DuplaEscolhidaId,
                MeuPlacarLado1 = meuPlacar.Lado1,
                MeuPlacarLado2 = meuPlacar.Lado2,
                PlacarEmSets = PlacaresPossiveis.EmSets(formato),
                PlacaresDoFormato = PlacaresPossiveis.Do(formato),
                PalpitesComPlacar = comPlacar.Count,
                PlacarMaisPalpitadoLado1 = maisPalpitado?.Key.Lado1,
                PlacarMaisPalpitadoLado2 = maisPalpitado?.Key.Lado2,
                PlacarMaisPalpitadoVotos = maisPalpitado?.Count() ?? 0,
            };

            if (p.VencedorId == null) continue;

            // Quem CRAVOU. A régua é a mesma do ranking — o `PontosDoPalpite` decide, aqui só
            // se pergunta. Uma segunda definição de "cravou" na tela produziria o pior dos
            // mundos: o cartão dizendo que alguém cravou e o ranking não pagando os 3 pontos.
            var quemCravou = votosDaPartida.Where(v => PontosDoPalpite.CravouOPlacar(new PalpiteConferido
            {
                DuplaEscolhidaId = v.DuplaEscolhidaId,
                VencedorId = p.VencedorId,
                PalpitouLado1 = PlacaresPossiveis.Lido(v.GamesDupla1, v.GamesDupla2, v.SetsDupla1, v.SetsDupla2).Lado1,
                PalpitouLado2 = PlacaresPossiveis.Lido(v.GamesDupla1, v.GamesDupla2, v.SetsDupla1, v.SetsDupla2).Lado2,
                PlacarLado1 = v.SetsDupla1 != null ? p.SetsDupla1 : p.GamesDupla1,
                PlacarLado2 = v.SetsDupla1 != null ? p.SetsDupla2 : p.GamesDupla2,
                EmSets = v.SetsDupla1 != null && v.SetsDupla2 != null,
                PorWo = p.MotivoDoEncerramento == EncerramentoPorWo.Motivo,
            })).Select(v => v.JogadorId).ToList();

            if (quemCravou.Count > 0) cravaram[p.Id] = quemCravou;
        }

        await PreencherQuemCravouAsync(resultado, cravaram);

        return resultado;
    }

    // Os NOMES de quem cravou, numa consulta só — e só quando alguém cravou.
    //
    // ⚠️ Separado de propósito: trazer o nome de todo mundo que palpitou junto com os votos
    // faria a lista de jogos (que chega a 40 partidas numa tela) carregar centenas de nomes
    // pra usar nenhum. Quem crava é sempre pouca gente, e jogo nenhum finalizado é zero
    // consulta.
    private async Task PreencherQuemCravouAsync(
        Dictionary<int, PalpiteResumoVM> resumos, Dictionary<int, List<int>> cravaram)
    {
        if (cravaram.Count == 0) return;

        var jogadorIds = cravaram.Values.SelectMany(v => v).Distinct().ToList();

        var nomes = await _context.Jogadores
            .AsNoTracking()
            .Where(j => jogadorIds.Contains(j.Id))
            .Select(j => new { j.Id, j.Nome, j.Apelido })
            .ToDictionaryAsync(j => j.Id, j => NomeBonito.ComApelido(j.Nome, j.Apelido));

        foreach (var (partidaId, ids) in cravaram)
            resumos[partidaId].CravaramOPlacar = ids
                .Select(id => nomes.GetValueOrDefault(id))
                .Where(nome => nome != null)
                .Select(nome => nome!)
                .OrderBy(nome => nome, StringComparer.OrdinalIgnoreCase)
                .ToList();
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
