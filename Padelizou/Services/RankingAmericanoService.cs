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
// ⚠️ São DOIS rankings, e não um com uma coluna a mais. No individual o parceiro troca a cada
// rodada, então o resultado é seu; no de duplas ele é da dupla fixa, e a metade do mérito é do
// parceiro que você escolheu. Misturar os dois numa lista só compara coisas que não se comparam
// — a mesma razão pela qual o Americano já não soma com o ranking oficial.
public record RankingAmericanoVM(
    List<RankingAmericanoLinhaVM> Individual,
    List<RankingAmericanoLinhaVM> Duplas);

public interface IRankingAmericanoService
{
    // `ate` = corte de data pro ranking "como estava antes": só entram torneios que começaram
    // até ali. É o que permite dizer quantas posições o último Americano moveu cada um
    // (ver Services/MovimentoNoRanking). Nulo = o ranking de hoje, com tudo.
    Task<RankingAmericanoVM> ListarAsync(HashSet<int>? jogadoresFiltro = null, DateTime? ate = null);
}

public class RankingAmericanoService : IRankingAmericanoService
{
    private readonly DbPadelContext _context;

    public RankingAmericanoService(DbPadelContext context) => _context = context;

    public async Task<RankingAmericanoVM> ListarAsync(HashSet<int>? jogadoresFiltro = null, DateTime? ate = null)
    {
        // Terminado, contratado e pago. `Status == "Finalizado"` e não "tem partida acabada":
        // a colocação de um Americano só existe quando o último jogo saiu — antes disso a
        // liderança ainda muda, e ponto que aparece e some é pior que ponto que demora.
        var torneios = (await _context.Torneios
                .Where(t => t.PontuaNoRankingAmericano
                         && t.RankingAmericanoPagoEm != null
                         && t.Status == "Finalizado"
                         // O corte do "antes": torneio sem data fica FORA quando se pede um
                         // recorte, porque não dá pra afirmar que ele já tinha acontecido.
                         && (ate == null || (t.DataInicio != null && t.DataInicio <= ate)))
                .Select(t => new { t.Id, t.Nome, t.DataInicio, t.Formato })
                .ToListAsync())
            // Cinto de segurança: a caixinha "pontua no Ranking Americano" só aparece nos
            // formatos de Americano, mas ela é uma coluna — um POST montado à mão a marcaria
            // num torneio de chave, e aí ele pontuaria nos DOIS rankings.
            .Where(t => FormatoDoTorneio.EhAmericano(t.Formato))
            .ToList();

        var vazio = new RankingAmericanoVM(new(), new());
        if (torneios.Count == 0) return vazio;

        var ids = torneios.Select(t => t.Id).ToList();

        // A contagem de pessoas mora em PessoasDoAmericano porque o acerto de R$ 5 do admin faz
        // a MESMA pergunta — e porque ela precisa saber que o formato de duplas guarda a
        // inscrição em outra tabela.
        var quantosNaCategoria = await PessoasDoAmericano.PorCategoriaAsync(_context, ids);

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

        // Um acumulado POR FORMATO: o mesmo jogador pode aparecer nos dois, com pontos
        // independentes, e é isso que a tela mostra em abas separadas.
        var acumuladoIndividual = new Dictionary<int, RankingAmericanoLinhaVM>();
        var acumuladoDuplas = new Dictionary<int, RankingAmericanoLinhaVM>();

        foreach (var porCategoria in partidas.GroupBy(p => p.CategoriaId))
        {
            int pessoas = quantosNaCategoria.TryGetValue(porCategoria.Key, out var q) ? q : 0;

            var doTorneioDaCategoria = torneios.First(t => t.Id == porCategoria.First().TorneioId);
            bool ehDeDuplas = FormatoDoTorneio.EhAmericanoDeDuplas(doTorneioDaCategoria.Formato);
            var acumulado = ehDeDuplas ? acumuladoDuplas : acumuladoIndividual;

            // O piso vive em PontosDoAmericano — repetir o número 8 aqui seria a segunda cópia
            // da mesma regra, e uma delas mudaria sozinha um dia.
            if (!PontosDoAmericano.PontuaNesteTamanho(pessoas)) continue;

            var finalizadas = TabelaDoAmericano.QueDecidem(porCategoria)
                .Where(p => p.Status == "Finalizada");

            // ⚠️ CADA FORMATO TEM A SUA TABELA, e usar a errada não dá erro — dá ponto errado,
            // calado. No Americano de Duplas os dois parceiros jogam exatamente as mesmas
            // partidas, então a tabela POR PESSOA lhes dá somas idênticas, empata os dois e
            // desempata pelo Id: a dupla campeã saía com um jogador em 1º (100 pontos) e o
            // outro em 2º (60), pelo MESMO resultado.
            var colocacaoDe = new Dictionary<int, int>();
            int quantosClassificados;

            if (ehDeDuplas)
            {
                var tabela = TabelaDoAmericanoDeDuplas.Montar(finalizadas);
                if (tabela.Count == 0) continue;

                quantosClassificados = tabela.Count;
                for (int i = 0; i < tabela.Count; i++)
                {
                    // A colocação é da DUPLA, e os dois a recebem inteira — como no mata-mata,
                    // onde o título é dos dois.
                    colocacaoDe[tabela[i].Dupla.Jogador1Id] = i + 1;
                    if (tabela[i].Dupla.Jogador2Id is int parceiro) colocacaoDe[parceiro] = i + 1;
                }
            }
            else
            {
                var tabela = TabelaDoAmericano.Montar(finalizadas);
                if (tabela.Count == 0) continue;

                quantosClassificados = tabela.Count;
                for (int i = 0; i < tabela.Count; i++)
                {
                    colocacaoDe[tabela[i].Jogador.Id] = i + 1;
                }
            }

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

            var doTorneio = doTorneioDaCategoria;

            foreach (var jogador in jogaramTudo)
            {
                if (jogadoresFiltro != null && !jogadoresFiltro.Contains(jogador.Id)) continue;

                // Fora da tabela decisiva = participou. `int.MaxValue` cairia no mesmo lugar,
                // mas um número que se lê ("participou") é o que a tela vai mostrar depois.
                int colocacao = colocacaoDe.TryGetValue(jogador.Id, out var c)
                    ? c
                    : quantosClassificados + 1;

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
        static List<RankingAmericanoLinhaVM> Ordenar(Dictionary<int, RankingAmericanoLinhaVM> acumulado) =>
            acumulado.Values
                .OrderByDescending(l => l.Pontos)
                .ThenByDescending(l => l.Vitorias)
                .ThenByDescending(l => l.Podios)
                .ThenBy(l => l.Jogador.Id)
                .ToList();

        return new RankingAmericanoVM(Ordenar(acumuladoIndividual), Ordenar(acumuladoDuplas));
    }
}
