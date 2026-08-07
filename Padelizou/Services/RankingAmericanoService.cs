using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.ViewModels;

namespace Padelizou.Services;

// O Ranking Americano — a Trilha C do RANKING.md.
//
// Separado do oficial de propósito: rodízio com parceiro trocando a cada rodada não é a mesma
// coisa que torneio de chave, e somar os dois estragaria os dois.
//
// ⚠️ Só entra Americano que (1) o organizador CONTRATOU, (2) foi PAGO, (3) fechou com o piso
// de 8 pessoas e (4) terminou. As quatro condições moram aqui, num lugar só — espalhadas pela
// consulta e pela tela, uma delas ficaria de fora um dia e o ranking passaria a contar o que
// não devia, calado.
public interface IRankingAmericanoService
{
    Task<List<RankingAmericanoLinhaVM>> ListarAsync(HashSet<int>? jogadoresFiltro = null);
}

public class RankingAmericanoService : IRankingAmericanoService
{
    private readonly DbPadelContext _context;

    public RankingAmericanoService(DbPadelContext context) => _context = context;

    public async Task<List<RankingAmericanoLinhaVM>> ListarAsync(HashSet<int>? jogadoresFiltro = null)
    {
        // Terminado, contratado e pago. `Status == "Finalizado"` e não "tem partida acabada":
        // a colocação de um Americano só existe quando o último jogo saiu — antes disso a
        // liderança ainda muda, e ponto que aparece e some é pior que ponto que demora.
        var torneios = await _context.Torneios
            .Where(t => t.PontuaNoRankingAmericano
                     && t.RankingAmericanoPagoEm != null
                     && t.Status == "Finalizado")
            .Select(t => new { t.Id, t.Nome, t.DataInicio })
            .ToListAsync();

        if (torneios.Count == 0) return new List<RankingAmericanoLinhaVM>();

        var ids = torneios.Select(t => t.Id).ToList();

        // Quantas pessoas cada categoria teve. Lista de espera fica de fora: quem esperou não
        // jogou, e o preço também não a cobra.
        var inscritosPorCategoria = await _context.InscricoesAmericanas
            .Where(i => ids.Contains(i.Categoria.TorneioId) && !i.EmListaDeEspera)
            .GroupBy(i => i.CategoriaId)
            .Select(g => new { CategoriaId = g.Key, Quantos = g.Count() })
            .ToListAsync();

        var quantosNaCategoria = inscritosPorCategoria.ToDictionary(x => x.CategoriaId, x => x.Quantos);

        var partidas = await _context.Partidas
            .Include(p => p.Dupla1).ThenInclude(d => d!.Jogador1)
            .Include(p => p.Dupla1).ThenInclude(d => d!.Jogador2)
            .Include(p => p.Dupla2).ThenInclude(d => d!.Jogador1)
            .Include(p => p.Dupla2).ThenInclude(d => d!.Jogador2)
            // `TorneioId` é anulável na Partida (herança de quando o torneio não era
            // obrigatório), então o filtro precisa peneirar o nulo antes de comparar.
            .Where(p => p.TorneioId != null && ids.Contains(p.TorneioId.Value)
                     && p.Fase != null && p.Fase.StartsWith("Americano"))
            .ToListAsync();

        var acumulado = new Dictionary<int, RankingAmericanoLinhaVM>();

        foreach (var porCategoria in partidas.GroupBy(p => p.CategoriaId))
        {
            int pessoas = quantosNaCategoria.TryGetValue(porCategoria.Key, out var q) ? q : 0;

            // O piso vive em PontosDoAmericano — repetir o número 8 aqui seria a segunda cópia
            // da mesma regra, e uma delas mudaria sozinha um dia.
            if (!PontosDoAmericano.PontuaNesteTamanho(pessoas)) continue;

            var finalizadas = TabelaDoAmericano.QueDecidem(porCategoria)
                .Where(p => p.Status == "Finalizada");

            var classificacao = TabelaDoAmericano.Montar(finalizadas);
            if (classificacao.Count == 0) continue;

            // Quem NÃO está na tabela que decide (não passou da fase de grupos) leva a
            // participação. Do 5º pra trás o ponto é chato, então não é preciso inventar uma
            // ordem entre grupos que nunca se enfrentaram — o que o sistema não sabe, ele não
            // finge saber.
            var jogaramTudo = porCategoria
                .SelectMany(p => new[] { p.Dupla1?.Jogador1, p.Dupla1?.Jogador2, p.Dupla2?.Jogador1, p.Dupla2?.Jogador2 })
                .Where(j => j != null)
                .Select(j => j!)
                .DistinctBy(j => j.Id)
                .ToList();

            var colocacaoDe = new Dictionary<int, int>();
            for (int i = 0; i < classificacao.Count; i++)
            {
                colocacaoDe[classificacao[i].Jogador.Id] = i + 1;
            }

            var doTorneio = torneios.First(t => t.Id == porCategoria.First().TorneioId);

            foreach (var jogador in jogaramTudo)
            {
                if (jogadoresFiltro != null && !jogadoresFiltro.Contains(jogador.Id)) continue;

                // Fora da tabela decisiva = participou. `int.MaxValue` cairia no mesmo lugar,
                // mas um número que se lê ("participou") é o que a tela vai mostrar depois.
                int colocacao = colocacaoDe.TryGetValue(jogador.Id, out var c)
                    ? c
                    : classificacao.Count + 1;

                int pontos = PontosDoAmericano.Pontos(colocacao, pessoas);
                if (pontos == 0) continue;

                if (!acumulado.TryGetValue(jogador.Id, out var linha))
                {
                    linha = new RankingAmericanoLinhaVM { Jogador = jogador };
                    acumulado[jogador.Id] = linha;
                }

                linha.Pontos += pontos;
                linha.Americanos += 1;
                if (colocacao == 1) linha.Vitorias += 1;
                if (colocacao <= 3) linha.Podios += 1;

                if (doTorneio.DataInicio is DateTime quando &&
                    (linha.UltimoEm == null || quando > linha.UltimoEm))
                {
                    linha.UltimoEm = quando;
                    linha.UltimoNome = doTorneio.Nome;
                }
            }
        }

        // Ordem TOTAL, pelo mesmo motivo da tabela do Americano: sem o desempate final por Id,
        // dois jogadores com a mesma pontuação trocariam de lugar entre duas visitas à página.
        return acumulado.Values
            .OrderByDescending(l => l.Pontos)
            .ThenByDescending(l => l.Vitorias)
            .ThenByDescending(l => l.Podios)
            .ThenBy(l => l.Jogador.Id)
            .ToList();
    }
}
