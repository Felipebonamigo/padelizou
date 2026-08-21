using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.ViewModels;

namespace Padelizou.Services;

public interface IPadelimetroService
{
    // Gancho do FinalizarPartida: aplica UMA partida recém-finalizada ao Padelímetro
    // dos quatro jogadores. Silencioso quando a partida não conta.
    Task AplicarAsync(int partidaId);

    // Gancho do CoroarCampeao: a final da categoria fechou, aplica o AJUSTE DE CAMPANHA
    // dos que jogaram nela (bônus de campeão, pena de quem ficou na chave — RANKING.md,
    // "A campanha também move o número"). Silencioso quando a campanha não conta ou já
    // foi aplicada.
    Task AplicarCampanhaAsync(int categoriaId);

    // Replay: zera tudo e reconstrói do zero a partir das partidas finalizadas, em
    // ordem cronológica. Devolve quantas partidas contaram.
    Task<int> RecalcularTudoAsync();

    // A aba Padelímetro do ranking: todo mundo com nível, do maior pro menor.
    // filtroJogadores nulo = país todo (mesmo contrato do ObterJogadoresDoLocalAsync).
    Task<List<PadelimetroLinhaVM>> ListarRankingAsync(HashSet<int>? filtroJogadores);

    // Quantas posições cada um ganhou/perdeu desde `corte` — o instante antes do último
    // torneio. Preenche PadelimetroLinhaVM.Movimento. Ver Services/MovimentoNoRanking.
    Task AplicarMovimentoAsync(List<PadelimetroLinhaVM> linhas, DateTime corte);
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
    // - sem placar ou sem os 4 jogadores, não há o que medir;
    // - W.O. fora: o placar existe, mas ninguém entrou em quadra. Esta linha é nova de
    //   18/08/2026 e fechou um buraco que o comentário do EncerramentoDaPartida já dizia
    //   estar fechado — antes não havia como distinguir um W.O. de um 6x0 jogado, e o nível
    //   dos quatro se mexia por um jogo que não aconteceu (ver Services/EncerramentoPorWo).
    public static bool Conta(Partida partida, Categoria categoria, Dupla dupla1, Dupla dupla2) =>
        partida.Status == "Finalizada"
        && partida.GamesDupla1 != null && partida.GamesDupla2 != null
        && !EncerramentoPorWo.Foi(partida)
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

    public async Task AplicarCampanhaAsync(int categoriaId)
    {
        // Uma vez por categoria: correção de placar re-dispara o robô da final e a fila
        // offline da Mesa reentrega — o extrato é a memória do que já foi aplicado.
        if (await _context.HistoricosDePadelimetro.AnyAsync(h => h.CategoriaId == categoriaId)) return;

        var categoria = await _context.Categorias
            .Include(c => c.Torneio)
            .FirstOrDefaultAsync(c => c.Id == categoriaId);
        if (categoria == null) return;

        var partidas = await _context.Partidas
            .Include(p => p.Dupla1)
            .Include(p => p.Dupla2)
            .Where(p => p.CategoriaId == categoriaId)
            .ToListAsync();

        var ids = partidas
            .SelectMany(p => new[] { p.Dupla1, p.Dupla2 })
            .Where(d => d != null)
            .SelectMany(d => new[] { d.Jogador1Id, d.Jogador2Id ?? -1 })
            .Where(id => id > 0)
            .ToHashSet();
        var jogadores = await _context.Jogadores
            .Where(j => ids.Contains(j.Id))
            .ToDictionaryAsync(j => j.Id);

        if (AplicarCampanha(categoria, partidas, jogadores, DateTime.Now) > 0)
            await _context.SaveChangesAsync();
    }

    // DESFAZ o ajuste de campanha de uma categoria: subtrai o delta de cada linha e apaga
    // as linhas — o extrato limpo deixa o gancho da final REAPLICAR com os carimbos novos.
    // Subtração de delta, e não NivelAntes, pra não apagar o que o jogador ganhou DEPOIS
    // da campanha (a mista da noite move o mesmo número). Dois chamadores, uma regra:
    // ReabrirPartida (a final deixou de valer) e a correção de placar que troca o
    // vencedor da final (ControlePlacar). NÃO salva — quem chama decide quando, e a
    // reaplicação depende do RemoveRange estar SALVO antes do guard de idempotência.
    public static async Task DesfazerCampanhaAsync(DbPadelContext context, int categoriaId)
    {
        var campanha = await context.HistoricosDePadelimetro
            .Where(h => h.CategoriaId == categoriaId).ToListAsync();
        if (campanha.Count == 0) return;

        var jogadores = await context.Jogadores
            .Where(j => campanha.Select(h => h.JogadorId).Contains(j.Id))
            .ToDictionaryAsync(j => j.Id);

        foreach (var linha in campanha)
            if (jogadores.TryGetValue(linha.JogadorId, out var jogador) && jogador.Padelimetro != null)
                jogador.Padelimetro = Padelimetro.Acomodar(jogador.Padelimetro.Value - linha.Delta);

        context.HistoricosDePadelimetro.RemoveRange(campanha);
    }

    // DESFAZ o Padelímetro de UMA partida: subtrai o delta de cada linha do extrato,
    // devolve o jogo pra contagem (JogosDePadelimetro) e apaga as linhas — o extrato limpo
    // deixa o guard de idempotência de AplicarAsync (linha 66 acima) reaplicar do zero.
    //
    // Extraído de ReabrirPartida em 21/08/2026, achado da análise de sistema: corrigir o
    // placar de um jogo FINALIZADO sem passar por "Reabrir" primeiro nunca chamava isto —
    // AplicarAsync via a linha do extrato já existente (a do placar VELHO) e não fazia
    // nada, então o nível dos 4 jogadores continuava calculado pelo placar errado até
    // alguém rodar o replay manual (RecalcularTudoAsync). O delta depende da MARGEM de
    // games (Padelimetro.FatorDeGames), então mesmo uma correção que não troca o
    // vencedor — só aperta ou alarga o placar — vale um novo cálculo.
    //
    // NÃO salva — mesma convenção de DesfazerCampanhaAsync: quem chama decide quando, e a
    // reaplicação depende do RemoveRange estar SALVO antes do guard de idempotência rodar.
    public static async Task DesfazerPartidaAsync(DbPadelContext context, int partidaId)
    {
        var extrato = await context.HistoricosDePadelimetro
            .Where(h => h.PartidaId == partidaId).ToListAsync();
        if (extrato.Count == 0) return;

        var jogadores = await context.Jogadores
            .Where(j => extrato.Select(h => h.JogadorId).Contains(j.Id))
            .ToDictionaryAsync(j => j.Id);

        foreach (var linha in extrato)
        {
            if (!jogadores.TryGetValue(linha.JogadorId, out var jogador)) continue;

            if (jogador.Padelimetro != null)
                jogador.Padelimetro = Padelimetro.Acomodar(jogador.Padelimetro.Value - linha.Delta);
            jogador.JogosDePadelimetro = Math.Max(0, jogador.JogosDePadelimetro - 1);
        }

        context.HistoricosDePadelimetro.RemoveRange(extrato);
    }

    // O núcleo do ajuste de campanha, compartilhado entre o gancho ao vivo e o replay.
    // Mexe nas entidades rastreadas e devolve quantas linhas de extrato criou — quem
    // chama decide quando salvar. A matemática e as portas da faixa moram em
    // CampanhaNoPadelimetro; aqui moram os porteiros de QUEM leva o ajuste.
    private int AplicarCampanha(Categoria categoria, List<Partida> partidasDaCategoria,
        IReadOnlyDictionary<int, Jogador> jogadores, DateTime quando)
    {
        // Os mesmos porteiros das partidas (Conta), mais o cancelado: torneio que não mede
        // padel contra o mundo não tem campanha, e torneio cancelado não valeu — a mesma
        // régua do CampeoesDoTorneio.PodeAnunciar.
        var torneio = categoria.Torneio;
        if (!EstatisticasService.ContaNoRanking(torneio)) return 0;
        if (categoria.DeTimes) return 0;
        if (!CampeoesDoTorneio.PodeAnunciar(torneio?.Status)) return 0;

        // A campanha só fecha com a FINAL fechada — ao vivo é o gancho do CoroarCampeao;
        // no replay é o que reencontra este ponto na linha do tempo.
        if (!partidasDaCategoria.Any(p => p.Fase == "Final" && p.Status == "Finalizada")) return 0;

        string? primeiraFase = CampanhaNoPadelimetro.PrimeiraFaseDoMataMata(
            partidasDaCategoria.Select(p => p.Fase));

        // Só quem ENTROU EM QUADRA em jogo que conta leva ajuste: campanha inteira de
        // W.O. não mediu padel nenhum — a mesma razão do W.O. não mover o número.
        var entrouEmQuadra = new HashSet<int>();
        foreach (var p in partidasDaCategoria)
        {
            if (!Conta(p, categoria, p.Dupla1, p.Dupla2)) continue;
            entrouEmQuadra.Add(p.Dupla1.Jogador1Id);
            entrouEmQuadra.Add(p.Dupla1.Jogador2Id!.Value);
            entrouEmQuadra.Add(p.Dupla2.Jogador1Id);
            entrouEmQuadra.Add(p.Dupla2.Jogador2Id!.Value);
        }

        // As duplas saem das PARTIDAS, não de uma consulta própria: quem não aparece em
        // jogo nenhum não pode ter entrado em quadra, então não faz falta. A ordem é da
        // MELHOR campanha pra pior (campeão, depois a fase mais funda — OrdemDaFase põe
        // "Grupos" em -1, atrás de todo mata-mata) com Id no desempate: se um dado torto
        // puser a mesma pessoa em duas duplas, vale a melhor campanha, sempre na mesma
        // ordem.
        var duplas = partidasDaCategoria
            .SelectMany(p => new[] { p.Dupla1, p.Dupla2 })
            .Where(d => d != null && !d.EhTime && d.Jogador2Id != null)
            .DistinctBy(d => d.Id)
            .OrderByDescending(d => d.UltimaFase == CampeoesDoTorneio.FaseDeCampeao)
            .ThenByDescending(d => DesfazerDoJogo.OrdemDaFase(d.UltimaFase))
            .ThenBy(d => d.Id)
            .ToList();

        int linhas = 0;
        var ajustados = new HashSet<int>();
        foreach (var dupla in duplas)
        {
            foreach (var jogadorId in new[] { dupla.Jogador1Id, dupla.Jogador2Id!.Value })
            {
                if (!entrouEmQuadra.Contains(jogadorId)) continue;
                if (!ajustados.Add(jogadorId)) continue;
                if (!jogadores.TryGetValue(jogadorId, out var jogador) || jogador.Padelimetro == null) continue;

                var ajuste = CampanhaNoPadelimetro.Ajuste(jogador.Padelimetro.Value,
                    dupla.UltimaFase, primeiraFase, categoria.Nome);
                if (ajuste == null) continue;

                int antes = jogador.Padelimetro.Value;
                jogador.Padelimetro = Padelimetro.Acomodar(antes + ajuste.Value.Delta);
                // JogosDePadelimetro fica parado: campanha não é jogo e não conta pro K.

                _context.HistoricosDePadelimetro.Add(new HistoricoDePadelimetro
                {
                    JogadorId = jogadorId,
                    PartidaId = null,
                    CategoriaId = categoria.Id,
                    NivelAntes = antes,
                    Delta = jogador.Padelimetro.Value - antes,
                    Motivo = ajuste.Value.Motivo,
                    CriadoEm = quando,
                });
                linhas++;
            }
        }
        return linhas;
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

        // A linha do tempo INTEIRA, com W.O. e tudo: jogo de W.O. não move nível, mas uma
        // final de W.O. fecha a campanha da categoria — e o ajuste de campanha precisa
        // cair no mesmo ponto da história em que caiu ao vivo.
        var ordenadas = partidas
            .OrderBy(DataDaPartida)
            .ThenBy(p => p.Id) // desempate estável: mesma entrada, mesmo resultado, sempre
            .ToList();

        var idsEnvolvidos = ordenadas
            .Where(p => Conta(p, p.Categoria, p.Dupla1, p.Dupla2))
            .SelectMany(p => IdsDosJogadores(p.Dupla1, p.Dupla2) ?? Array.Empty<int>())
            .Distinct()
            .ToList();
        var jogadores = await _context.Jogadores
            .Where(j => idsEnvolvidos.Contains(j.Id))
            .ToDictionaryAsync(j => j.Id);

        var partidasPorCategoria = partidas
            .GroupBy(p => p.CategoriaId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // A campanha fecha na ÚLTIMA partida da categoria NA LINHA DO TEMPO, e não na
        // posição da final. Quase sempre é a mesma coisa — mas o PlacarMarcadoEm vem do
        // relógio do APARELHO da Mesa (design do offline), e um aparelho adiantado pode
        // datar jogo de grupo DEPOIS da final: fechar a campanha na final pularia, em
        // silêncio, jogador que ainda nem foi semeado naquele ponto da história. Na
        // última partida da categoria, todo mundo que jogou já passou pelo motor.
        var ultimaDaCategoria = new Dictionary<int, int>();
        foreach (var p in ordenadas) ultimaDaCategoria[p.CategoriaId] = p.Id;

        int aplicadas = 0;
        var categoriasComCampanha = new HashSet<int>();
        foreach (var partida in ordenadas)
        {
            if (Conta(partida, partida.Categoria, partida.Dupla1, partida.Dupla2)
                && IdsDosJogadores(partida.Dupla1, partida.Dupla2) is { } ids
                && ids.All(jogadores.ContainsKey))
            {
                // No replay o extrato é datado pela PARTIDA, senão o gráfico inteiro
                // nasceria empilhado no dia do recálculo.
                Aplicar(partida, jogadores, DataDaPartida(partida));
                aplicadas++;
            }

            // O gancho do CoroarCampeao ao vivo, reencontrado na linha do tempo (ver o
            // comentário do ultimaDaCategoria). O HashSet é cinto de segurança pra não
            // aplicar duas vezes; o núcleo confere se a categoria tem final fechada.
            if (ultimaDaCategoria[partida.CategoriaId] == partida.Id
                && categoriasComCampanha.Add(partida.CategoriaId))
            {
                AplicarCampanha(partida.Categoria, partidasPorCategoria[partida.CategoriaId],
                    jogadores, DataDaPartida(partida));
            }
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
            if (FaixasDePadelimetro.ForaDaEscada(i.Categoria)) continue;
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

    // QUANTAS POSIÇÕES O ÚLTIMO TORNEIO MOVEU CADA UM na aba Padelímetro.
    //
    // ⚠️ Aqui não dá pra "recalcular o ranking com data de corte" como nas outras abas: o nível
    // é um número GUARDADO no jogador, não uma soma que se refaz. Mas o extrato existe justo
    // pra isso — `HistoricoDePadelimetro` tem o delta de cada partida com a data. Então o nível
    // de antes é o de hoje MENOS tudo que entrou depois do corte, o que é exato e custa uma
    // consulta. Refazer o Elo do zero até a data daria o mesmo número por um caminho caro e
    // com uma segunda implementação da mesma matemática pra divergir.
    public async Task AplicarMovimentoAsync(List<PadelimetroLinhaVM> linhas, DateTime corte)
    {
        if (linhas.Count == 0) return;

        var ids = linhas.Select(l => l.Jogador.Id).ToHashSet();

        var depois = await _context.HistoricosDePadelimetro
            .Where(h => ids.Contains(h.JogadorId) && h.CriadoEm > corte)
            .GroupBy(h => h.JogadorId)
            // O delta soma TODA linha (campanha inclusive); a contagem de JOGOS só as de
            // partida — linha de campanha e de nascimento não incrementam
            // JogosDePadelimetro, então subtraí-las inventaria jogos no "antes".
            .Select(g => new
            {
                JogadorId = g.Key,
                Delta = g.Sum(h => h.Delta),
                Quantos = g.Count(h => h.PartidaId != null),
            })
            .ToListAsync();
        var deltaDepois = depois.ToDictionary(d => d.JogadorId, d => d.Delta);
        var quantosDepois = depois.ToDictionary(d => d.JogadorId, d => d.Quantos);

        // ⚠️ Quem SÓ tem extrato depois do corte não tinha nível antes: ele fica fora da lista
        // "antes" e aparece como NOVO. Sem esta consulta ele entraria com o nível de estreia
        // (500) numa posição do meio da tabela, e o selo diria que ele "desceu 3" num ranking
        // em que ele acabou de entrar.
        var tinhaAntes = (await _context.HistoricosDePadelimetro
            .Where(h => ids.Contains(h.JogadorId) && h.CriadoEm <= corte)
            .Select(h => h.JogadorId)
            .Distinct()
            .ToListAsync()).ToHashSet();

        // A MESMA ordenação de `ListarRankingAsync` — nível, depois jogos, depois nome. Ordenar
        // diferente aqui inventaria movimento em quem empatou e não saiu do lugar.
        var ordemAntes = linhas
            .Where(l => tinhaAntes.Contains(l.Jogador.Id))
            .Select(l => new
            {
                l.Jogador.Id,
                l.Jogador.Nome,
                Pdz = l.Pdz - deltaDepois.GetValueOrDefault(l.Jogador.Id),
                Jogos = l.Jogos - quantosDepois.GetValueOrDefault(l.Jogador.Id),
            })
            .OrderByDescending(x => x.Pdz)
            .ThenByDescending(x => x.Jogos)
            .ThenBy(x => x.Nome)
            .Select(x => x.Id)
            .ToList();

        MovimentoNoRanking.Aplicar(linhas, ordemAntes, l => l.Jogador.Id, (l, mov) => l.Movimento = mov);
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
