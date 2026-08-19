using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// QUEM MAIS ACERTOU NO PALPITRÔMETRO, torneio a torneio.
//
// O palpitrômetro guardava o palpite de todo mundo desde sempre e nunca dizia quem ACERTOU: a
// tela mostrava a barra antes do jogo, um "galera acertou/errou" depois, e o palpite de cada
// pessoa morria ali. Este ranking é derivado do que JÁ estava gravado — por isso ele nasce com
// o histórico inteiro, sem migração de dados e sem começar zerado.
//
// 💾 ZERO coluna nova, ZERO ponto gravado: a conta é feita na hora, comparando
// `PalpitePartida` com o placar da `Partida`. É isso que faz o ranking se CORRIGIR SOZINHO
// quando o organizador conserta um placar depois — ponto gravado ficaria congelado no placar
// errado, e ninguém descobriria.
//
// 🚫 QUEM JOGA A PARTIDA NÃO ENTRA NA CONTA DELA. Os quatro em quadra são os únicos que podem
// MUDAR o resultado do próprio palpite. Continuam podendo votar (e o voto conta na barra do
// palpitrômetro, que é opinião pública); só não conta no ranking — nem no acerto, nem no
// total. ⚠️ **Em categoria de TIMES não exclui ninguém**: ali o `Jogador1` da linha é o
// organizador que cadastrou o time, não quem entra em quadra, e excluir por ali tiraria o
// acerto de quem nem jogou.
public static class RankingDePalpiteiros
{
    // Monta o ranking de um torneio. Null = torneio não existe.
    //
    // Lista vazia é resposta legítima e frequente: torneio sem jogo terminado, ou com jogos
    // terminados e nenhum palpite. Quem decide o que fazer com o vazio é quem chama — a
    // página devolve 404, como a do MVP.
    public static async Task<PalpiteirosDoTorneio?> DoTorneioAsync(
        DbPadelContext contexto, int torneioId, int? olhandoId)
    {
        var torneio = await contexto.Torneios
            .AsNoTracking()
            .Where(t => t.Id == torneioId)
            // ⚠️ Só colunas do próprio torneio: navegação obrigatória dentro de projeção vira
            // INNER JOIN e some com a linha inteira quando o outro lado falta.
            .Select(t => new { t.Id, t.Nome })
            .FirstOrDefaultAsync();

        if (torneio == null) return null;

        // ⚠️ `VencedorId != null` E `Status == "Finalizada"`, e não um só dos dois: jogo em
        // andamento já tem placar parcial (a Mesa grava game a game) e apurar por ele diria
        // quem está ganhando, não quem ganhou.
        var partidas = await contexto.Partidas
            .AsNoTracking()
            .Where(p => p.TorneioId == torneioId
                     && p.Status == PartidaFinalizada
                     && p.VencedorId != null)
            .Select(p => new
            {
                p.Id,
                p.VencedorId,
                p.Dupla1Id,
                p.Dupla2Id,
                p.GamesDupla1,
                p.GamesDupla2,
                p.MotivoDoEncerramento,
            })
            .ToListAsync();

        if (partidas.Count == 0) return Vazio(torneio.Id, torneio.Nome, olhandoId);

        var partidaIds = partidas.Select(p => p.Id).ToList();

        var palpites = await contexto.PalpitesPartida
            .AsNoTracking()
            .Where(v => partidaIds.Contains(v.PartidaId))
            .Select(v => new
            {
                v.PartidaId,
                v.JogadorId,
                v.DuplaEscolhidaId,
                v.Jogador.Nome,
                v.Jogador.Apelido,
                v.Jogador.FotoPerfil,
            })
            .ToListAsync();

        if (palpites.Count == 0) return Vazio(torneio.Id, torneio.Nome, olhandoId);

        // Quem estava EM QUADRA em cada partida. Consultado à parte de propósito: pendurar as
        // duas duplas na projeção da partida traria quatro navegações obrigatórias num JOIN só,
        // e é a linha inteira que some quando uma delas falta.
        var duplaIds = partidas.SelectMany(p => new[] { p.Dupla1Id, p.Dupla2Id }).Distinct().ToList();
        var duplas = await contexto.Duplas
            .AsNoTracking()
            .Where(d => duplaIds.Contains(d.Id))
            .Select(d => new { d.Id, d.NomeTime, d.Jogador1Id, d.Jogador2Id })
            .ToListAsync();

        var emQuadra = duplas.ToDictionary(
            d => d.Id,
            d => d.NomeTime != null
                // Linha de TIME: o Jogador1 é quem cadastrou, não quem joga. Ninguém a excluir.
                ? new HashSet<int>()
                : d.Jogador2Id is int parceiro
                    ? new HashSet<int> { d.Jogador1Id, parceiro }
                    : new HashSet<int> { d.Jogador1Id });

        var porJogador = new Dictionary<int, PalpiteiroNoRanking>();
        var partidasComPalpiteValido = new HashSet<int>();
        int palpitesComPlacar = 0;

        foreach (var partida in partidas)
        {
            var jogadoresDaPartida = new HashSet<int>(emQuadra.GetValueOrDefault(partida.Dupla1Id, new HashSet<int>()));
            jogadoresDaPartida.UnionWith(emQuadra.GetValueOrDefault(partida.Dupla2Id, new HashSet<int>()));

            foreach (var palpite in palpites.Where(v => v.PartidaId == partida.Id))
            {
                if (jogadoresDaPartida.Contains(palpite.JogadorId)) continue;

                var conferido = new PalpiteConferido
                {
                    DuplaEscolhidaId = palpite.DuplaEscolhidaId,
                    VencedorId = partida.VencedorId,
                    // ⚠️ O placar PALPITADO ainda não existe no banco (a coluna entra na fase
                    // seguinte, junto da tela que deixa escolher o placar). Até lá todo palpite
                    // chega aqui sem placar e vale o ponto do vencedor — que é exatamente o que
                    // a régua faz com palpite antigo, pra sempre.
                    PalpitouLado1 = null,
                    PalpitouLado2 = null,
                    PlacarLado1 = partida.GamesDupla1,
                    PlacarLado2 = partida.GamesDupla2,
                    PorWo = partida.MotivoDoEncerramento == EncerramentoPorWo.Motivo,
                };

                int pontos = PontosDoPalpite.De(conferido);

                if (!porJogador.TryGetValue(palpite.JogadorId, out var linha))
                {
                    linha = new PalpiteiroNoRanking
                    {
                        JogadorId = palpite.JogadorId,
                        Nome = NomeBonito.ComApelido(palpite.Nome, palpite.Apelido),
                        Foto = palpite.FotoPerfil,
                    };
                    porJogador[palpite.JogadorId] = linha;
                }

                linha.Palpites++;
                linha.Pontos += pontos;
                if (conferido.PalpitouOPlacar) palpitesComPlacar++;
                if (pontos > 0) linha.Acertos++;
                if (pontos == PontosDoPalpite.Cravou) linha.Cravadas++;

                partidasComPalpiteValido.Add(partida.Id);
            }
        }

        return new PalpiteirosDoTorneio
        {
            TorneioId = torneio.Id,
            Torneio = torneio.Nome,
            JogosApurados = partidasComPalpiteValido.Count,
            PalpitesComPlacar = palpitesComPlacar,
            EuId = olhandoId,
            Linhas = Classificar(porJogador.Values),
        };
    }

    public const string PartidaFinalizada = "Finalizada";

    // A ordem da tabela: PONTOS, aproveitamento, nome, id.
    //
    // ⚠️ A ordenação é TOTAL (vai até o id) pela razão de sempre: ordenação parcial faz a lista
    // trocar de ordem entre dois carregamentos da MESMA página, e ninguém reporta isso como
    // defeito — só desconfia da tela.
    //
    // ⚠️ Empate divide a POSIÇÃO (1, 2, 2, 4). Numerar em sequência poria dois jogadores com o
    // mesmo desempenho em lugares diferentes por causa da ordem alfabética, e é a pergunta que
    // volta primeiro: "por que ele está na frente se empatamos?".
    public static List<PalpiteiroNoRanking> Classificar(IEnumerable<PalpiteiroNoRanking> palpiteiros)
    {
        var ordenados = palpiteiros
            .OrderByDescending(p => p.Pontos)
            .ThenByDescending(p => p.Aproveitamento)
            .ThenBy(p => p.Nome, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.JogadorId)
            .ToList();

        for (int i = 0; i < ordenados.Count; i++)
        {
            var anterior = i > 0 ? ordenados[i - 1] : null;

            ordenados[i].Posicao = anterior != null
                                   && anterior.Pontos == ordenados[i].Pontos
                                   && anterior.Aproveitamento == ordenados[i].Aproveitamento
                ? anterior.Posicao
                : i + 1;
        }

        return ordenados;
    }

    private static PalpiteirosDoTorneio Vazio(int torneioId, string nome, int? olhandoId) => new()
    {
        TorneioId = torneioId,
        Torneio = nome,
        EuId = olhandoId,
    };
}

// Uma linha da tabela.
public sealed class PalpiteiroNoRanking
{
    public int JogadorId { get; set; }
    public string Nome { get; set; } = "";
    public string? Foto { get; set; }

    public int Posicao { get; set; }

    public int Pontos { get; set; }

    // Palpites que ACERTARAM o vencedor — inclusive os que cravaram o placar.
    public int Acertos { get; set; }

    // Quantas vezes cravou o placar exato. Zero até o placar entrar na tela.
    public int Cravadas { get; set; }

    // Quantos palpites deste jogador entraram na conta (jogo terminado, e fora os do próprio
    // jogo dele).
    public int Palpites { get; set; }

    // ⚠️ SÓ DESEMPATA — não ordena. Ver PontosDoPalpite: 9 de 11 fica na frente de 8 de 8.
    public double Aproveitamento => Palpites == 0 ? 0 : Math.Round(Acertos * 100.0 / Palpites, 1);
}

public sealed class PalpiteirosDoTorneio
{
    public int TorneioId { get; set; }
    public string Torneio { get; set; } = "";

    public List<PalpiteiroNoRanking> Linhas { get; set; } = new();

    // Em quantos jogos deste torneio houve palpite que valeu ponto. É o que dá tamanho à
    // tabela na tela ("12 jogos apurados") — sem isso, 3 pontos não diz se é muito ou pouco.
    public int JogosApurados { get; set; }

    // Quantos palpites deste torneio vieram COM placar.
    //
    // ⚠️ É daqui que a tela decide se fala em cravar placar — e é uma pergunta feita AO DADO,
    // nunca a um interruptor. Torneio jogado antes de o placar existir no palpitrômetro tem
    // zero aqui pra sempre, e mostrar a ele uma régua de "cravou 3" seria explicar um jeito de
    // pontuar que ninguém daquele torneio teve como usar. Mesma lição do "a janela é lida do
    // relógio" do MVP: estado derivado do dado não fica errado quando o mundo muda.
    public int PalpitesComPlacar { get; set; }

    // Quem está olhando, pra a VIEW destacar a própria linha sem ler claim nenhuma.
    public int? EuId { get; set; }

    public bool TemRanking => Linhas.Count > 0;
}
