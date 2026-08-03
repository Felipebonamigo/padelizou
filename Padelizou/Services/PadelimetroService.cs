using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.ViewModels;

namespace Padelizou.Services;

public interface IPadelimetroService
{
    // Gancho do FinalizarPartida: aplica UMA partida recém-finalizada ao Padelímetro
    // dos quatro jogadores. Silencioso quando a partida não conta.
    Task AplicarAsync(int partidaId);

    // Replay: zera tudo e reconstrói do zero a partir das partidas finalizadas, em
    // ordem cronológica. Devolve quantas partidas contaram.
    Task<int> RecalcularTudoAsync();

    // A aba Padelímetro do ranking: todo mundo com nível, do maior pro menor.
    // filtroJogadores nulo = país todo (mesmo contrato do ObterJogadoresDoLocalAsync).
    Task<List<PadelimetroLinhaVM>> ListarRankingAsync(HashSet<int>? filtroJogadores);
}

// Quem decide QUAIS partidas movem o Padelímetro e escreve o resultado no banco.
// A matemática mora em Padelimetro (pura); as faixas em FaixasDePadelimetro; as
// regras em RANKING.md.
public class PadelimetroService : IPadelimetroService
{
    private readonly DbPadelContext _context;

    public PadelimetroService(DbPadelContext context)
    {
        _context = context;
    }

    // O filtro do que mede padel de verdade:
    // - restrito fora (mesma razão do EstatisticasService.ContaNoRanking);
    // - categoria de times e dupla-TIME fora (o Jogador1Id de um time aponta pro
    //   organizador que cadastrou, não pra quem jogou);
    // - sem placar ou sem os 4 jogadores, não há o que medir.
    public static bool Conta(Partida partida, Categoria categoria, Dupla dupla1, Dupla dupla2) =>
        partida.Status == "Finalizada"
        && partida.GamesDupla1 != null && partida.GamesDupla2 != null
        && EstatisticasService.ContaNoRanking(categoria.Torneio)
        && !categoria.DeTimes
        && !dupla1.EhTime && !dupla2.EhTime
        && dupla1.Jogador2Id != null && dupla2.Jogador2Id != null;

    public async Task AplicarAsync(int partidaId)
    {
        // A fila offline da Mesa pode reentregar um "finalizar"; o extrato é a memória
        // do que já foi aplicado — partida com linha no extrato não aplica de novo.
        if (await _context.HistoricosDePadelimetro.AnyAsync(h => h.PartidaId == partidaId)) return;

        var partida = await _context.Partidas
            .Include(p => p.Categoria).ThenInclude(c => c.Torneio)
            .Include(p => p.Dupla1)
            .Include(p => p.Dupla2)
            .FirstOrDefaultAsync(p => p.Id == partidaId);
        if (partida == null || !Conta(partida, partida.Categoria, partida.Dupla1, partida.Dupla2)) return;

        var ids = IdsDosJogadores(partida.Dupla1, partida.Dupla2);
        if (ids == null) return;

        var jogadores = await _context.Jogadores
            .Where(j => ids.Contains(j.Id))
            .ToDictionaryAsync(j => j.Id);
        if (jogadores.Count != 4) return;

        Aplicar(partida, jogadores, DateTime.Now);
        await _context.SaveChangesAsync();
    }

    public async Task<int> RecalcularTudoAsync()
    {
        // Zera o extrato e o estado — RemoveRange (e não ExecuteDelete) porque o replay
        // também roda nos testes, e o provedor em memória não fala ExecuteDelete.
        _context.HistoricosDePadelimetro.RemoveRange(_context.HistoricosDePadelimetro);
        foreach (var j in await _context.Jogadores
                     .Where(j => j.Padelimetro != null || j.JogosDePadelimetro > 0).ToListAsync())
        {
            j.Padelimetro = null;
            j.JogosDePadelimetro = 0;
        }

        var partidas = await _context.Partidas
            .Include(p => p.Categoria).ThenInclude(c => c.Torneio)
            .Include(p => p.Dupla1)
            .Include(p => p.Dupla2)
            .Where(p => p.Status == "Finalizada")
            .ToListAsync();

        var contaveis = partidas
            .Where(p => Conta(p, p.Categoria, p.Dupla1, p.Dupla2) && IdsDosJogadores(p.Dupla1, p.Dupla2) != null)
            .OrderBy(DataDaPartida)
            .ThenBy(p => p.Id) // desempate estável: mesma entrada, mesmo resultado, sempre
            .ToList();

        var idsEnvolvidos = contaveis
            .SelectMany(p => IdsDosJogadores(p.Dupla1, p.Dupla2)!)
            .Distinct()
            .ToList();
        var jogadores = await _context.Jogadores
            .Where(j => idsEnvolvidos.Contains(j.Id))
            .ToDictionaryAsync(j => j.Id);

        int aplicadas = 0;
        foreach (var partida in contaveis)
        {
            var ids = IdsDosJogadores(partida.Dupla1, partida.Dupla2)!;
            if (!ids.All(jogadores.ContainsKey)) continue;

            // No replay o extrato é datado pela PARTIDA, senão o gráfico inteiro
            // nasceria empilhado no dia do recálculo.
            Aplicar(partida, jogadores, DataDaPartida(partida));
            aplicadas++;
        }

        await _context.SaveChangesAsync();
        return aplicadas;
    }

    public async Task<List<PadelimetroLinhaVM>> ListarRankingAsync(HashSet<int>? filtroJogadores)
    {
        var q = _context.Jogadores.Where(j => j.Padelimetro != null);
        if (filtroJogadores != null) q = q.Where(j => filtroJogadores.Contains(j.Id));

        var jogadores = await q
            .OrderByDescending(j => j.Padelimetro)
            .ThenByDescending(j => j.JogosDePadelimetro) // no empate, quem jogou mais sustenta melhor o número
            .ThenBy(j => j.Nome)
            .ToListAsync();
        if (jogadores.Count == 0) return new();

        // A escada de cada um (masc/fem) decide só o RÓTULO da faixa, nunca o número —
        // e vem da inscrição não-mista mais recente, igual ao perfil.
        var ids = jogadores.Select(j => j.Id).ToHashSet();
        var inscricoes = await _context.Duplas
            .Where(d => d.NomeTime == null
                     && (ids.Contains(d.Jogador1Id) || (d.Jogador2Id != null && ids.Contains(d.Jogador2Id.Value))))
            .Select(d => new
            {
                d.Jogador1Id,
                d.Jogador2Id,
                Categoria = d.Categoria.Nome,
                Data = d.Categoria.Torneio.DataInicio,
            })
            .ToListAsync();

        var escadaFeminina = new Dictionary<int, bool>();
        foreach (var i in inscricoes.OrderByDescending(i => i.Data)) // mais recente primeiro; a primeira vista vence
        {
            if (FaixasDePadelimetro.EhMista(i.Categoria)) continue;
            bool feminina = FaixasDePadelimetro.EhFeminina(i.Categoria);
            foreach (var id in new[] { i.Jogador1Id, i.Jogador2Id ?? -1 })
                if (id > 0 && ids.Contains(id) && !escadaFeminina.ContainsKey(id))
                    escadaFeminina[id] = feminina;
        }

        return jogadores.Select(j =>
        {
            FaixasDePadelimetro.Faixa? faixa = escadaFeminina.TryGetValue(j.Id, out var feminina)
                ? FaixasDePadelimetro.DoNivel(j.Padelimetro!.Value, feminina)
                : null;
            return new PadelimetroLinhaVM
            {
                Jogador = j,
                Pdz = j.Padelimetro!.Value,
                Jogos = j.JogosDePadelimetro,
                EmCalibracao = Padelimetro.EmCalibracao(j.JogosDePadelimetro),
                FaixaRotulo = faixa?.Rotulo,
                FaixaEscada = faixa?.Escada,
            };
        }).ToList();
    }

    // Os 4 jogadores da partida — nulo quando não são 4 pessoas DISTINTAS (dado torto:
    // mesmo jogador dos dois lados não pode mover o próprio número duas vezes).
    private static int[]? IdsDosJogadores(Dupla d1, Dupla d2)
    {
        if (d1.Jogador2Id == null || d2.Jogador2Id == null) return null;
        var ids = new[] { d1.Jogador1Id, d1.Jogador2Id.Value, d2.Jogador1Id, d2.Jogador2Id.Value };
        return ids.Distinct().Count() == 4 ? ids : null;
    }

    // A melhor data que a partida tem, na ordem em que os campos costumam existir.
    // Partida antiga sem carimbo nenhum cai na data do torneio.
    private static DateTime DataDaPartida(Partida p) =>
        p.PlacarMarcadoEm
        ?? p.HorarioFimReal
        ?? p.HorarioInicioReal
        ?? p.HorarioPrevisto
        ?? p.Categoria?.Torneio?.DataInicio
        ?? DateTime.MinValue;

    // O coração: seeds de quem estreia + a variação dos 4. Mexe nas entidades já
    // rastreadas e adiciona as linhas do extrato — quem chama decide quando salvar.
    private void Aplicar(Partida partida, IReadOnlyDictionary<int, Jogador> jogadores, DateTime quando)
    {
        var d1 = partida.Dupla1;
        var d2 = partida.Dupla2;
        var time1 = new[] { jogadores[d1.Jogador1Id], jogadores[d1.Jogador2Id!.Value] };
        var time2 = new[] { jogadores[d2.Jogador1Id], jogadores[d2.Jogador2Id!.Value] };

        foreach (var j in time1.Concat(time2))
            SemearSePreciso(j, partida.Categoria, quando);

        double nivel1 = Padelimetro.NivelDaDupla(time1[0].Padelimetro!.Value, time1[1].Padelimetro!.Value);
        double nivel2 = Padelimetro.NivelDaDupla(time2[0].Padelimetro!.Value, time2[1].Padelimetro!.Value);
        double expectativa1 = Padelimetro.Expectativa(nivel1, nivel2);

        int games1 = partida.GamesDupla1!.Value;
        int games2 = partida.GamesDupla2!.Value;
        double fator = Padelimetro.FatorDeGames(games1, games2);

        // Partida velha finalizada sem VencedorId (seed antigo): decide igual ao
        // FinalizarPartida — sets primeiro, games no desempate.
        bool dupla1Venceu = partida.VencedorId != null
            ? partida.VencedorId == partida.Dupla1Id
            : (partida.SetsDupla1 ?? 0) > (partida.SetsDupla2 ?? 0)
              || ((partida.SetsDupla1 ?? 0) == (partida.SetsDupla2 ?? 0) && games1 > games2);

        string nomes1 = $"{time1[0].ComoChamar} & {time1[1].ComoChamar}";
        string nomes2 = $"{time2[0].ComoChamar} & {time2[1].ComoChamar}";

        foreach (var j in time1)
            Mover(j, partida, dupla1Venceu, expectativa1, fator,
                dupla1Venceu
                    ? $"Vitória {games1}x{games2} sobre {nomes2}"
                    : $"Derrota {games1}x{games2} para {nomes2}", quando);

        foreach (var j in time2)
            Mover(j, partida, !dupla1Venceu, 1 - expectativa1, fator,
                dupla1Venceu
                    ? $"Derrota {games2}x{games1} para {nomes1}"
                    : $"Vitória {games2}x{games1} sobre {nomes1}", quando);
    }

    private void SemearSePreciso(Jogador jogador, Categoria categoria, DateTime quando)
    {
        if (jogador.Padelimetro != null) return;

        int entrada = FaixasDePadelimetro.Entrada(categoria.Nome);
        jogador.Padelimetro = entrada;
        jogador.JogosDePadelimetro = 0;

        _context.HistoricosDePadelimetro.Add(new HistoricoDePadelimetro
        {
            JogadorId = jogador.Id,
            PartidaId = null,
            NivelAntes = entrada,
            Delta = 0,
            Motivo = $"Entrou na régua pela categoria {categoria.Nome}",
            CriadoEm = quando,
        });
    }

    private void Mover(Jogador jogador, Partida partida, bool venceu, double expectativaDoTime,
        double fator, string motivo, DateTime quando)
    {
        int k = Padelimetro.K(jogador.JogosDePadelimetro);
        int delta = Padelimetro.Variacao(k, fator, venceu, expectativaDoTime);
        int antes = jogador.Padelimetro!.Value;

        jogador.Padelimetro = Padelimetro.Acomodar(antes + delta);
        jogador.JogosDePadelimetro++;

        _context.HistoricosDePadelimetro.Add(new HistoricoDePadelimetro
        {
            JogadorId = jogador.Id,
            PartidaId = partida.Id,
            NivelAntes = antes,
            Delta = jogador.Padelimetro.Value - antes, // pós-clamp: o extrato nunca mente
            Motivo = motivo,
            CriadoEm = quando,
        });
    }
}
