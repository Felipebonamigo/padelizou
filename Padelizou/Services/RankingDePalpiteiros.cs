using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// QUEM MAIS ACERTA NO PALPITRÔMETRO — no torneio, no hub do Ranking e no perfil.
//
// O palpitrômetro guardava o palpite de todo mundo desde sempre e nunca dizia quem ACERTOU: a
// tela mostrava a barra antes do jogo, um "galera acertou/errou" depois, e o palpite de cada
// pessoa morria ali. Este ranking é derivado do que JÁ estava gravado — por isso ele nasce com
// o histórico inteiro, sem migração de dados e sem começar zerado.
//
// 💾 ZERO ponto gravado: a conta é feita na hora, comparando `PalpitePartida` com o placar da
// `Partida`. É isso que faz o ranking se CORRIGIR SOZINHO quando o organizador conserta um
// placar depois — ponto gravado ficaria congelado no placar errado, e ninguém descobriria.
//
// 🚫 QUEM JOGA A PARTIDA NÃO ENTRA NA CONTA DELA. Os quatro em quadra são os únicos que podem
// MUDAR o resultado do próprio palpite. Continuam podendo votar (e o voto conta na barra do
// palpitrômetro, que é opinião pública); só não conta no ranking — nem no acerto, nem no
// total. ⚠️ **Em categoria de TIMES não exclui ninguém**: ali o `Jogador1` da linha é o
// organizador que cadastrou o time, não quem entra em quadra, e excluir por ali tiraria o
// acerto de quem nem jogou.
//
// 🧭 SÃO TRÊS PERGUNTAS E UM NÚCLEO SÓ (`Apurar`). As três carregam dados diferentes — o
// torneio parte das partidas dele, o hub e o perfil partem dos PALPITES — mas a apuração é a
// mesma função. Três somatórios diferentes acabariam discordando: o perfil dizendo 12 pontos e
// a aba do hub dizendo 11, sem nada na tela pra explicar a diferença.
public static class RankingDePalpiteiros
{
    public const string PartidaFinalizada = "Finalizada";

    // ── 1. O RANKING DE UM TORNEIO ────────────────────────────────────────────────────────
    //
    // Null = torneio não existe. Lista vazia é resposta legítima e frequente: torneio sem jogo
    // terminado, ou com jogos terminados e nenhum palpite. Quem decide o que fazer com o vazio
    // é quem chama — a página devolve 404, como a do MVP.
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

        var partidas = await ConsultaDePartidas(contexto, p => p.TorneioId == torneioId).ToListAsync();

        var apuracao = await ApurarAsync(contexto, partidas,
            await PalpitesDasPartidasAsync(contexto, partidas.Select(p => p.Id).ToList()));

        return new PalpiteirosDoTorneio
        {
            TorneioId = torneio.Id,
            Torneio = torneio.Nome,
            JogosApurados = apuracao.JogosApurados,
            PalpitesComPlacar = apuracao.PalpitesComPlacar,
            EuId = olhandoId,
            Linhas = apuracao.Linhas,
        };
    }

    // ── 2. O RANKING GERAL (a aba do hub) ─────────────────────────────────────────────────
    //
    // `doLocal` é o MESMO filtro regional das outras abas do hub (nulo = país inteiro).
    //
    // ⚠️ Parte dos PALPITES, não das partidas, e isso é o que a mantém barata: partida
    // finalizada o sistema tem aos milhares, e a esmagadora maioria nunca teve palpite nenhum.
    // Varrer todas pra depois descobrir que 95% não interessam seria pagar o preço da tabela
    // grande pra usar a pequena.
    public static async Task<List<PalpiteiroNoRanking>> GeralAsync(
        DbPadelContext contexto, HashSet<int>? doLocal)
    {
        var palpites = await PalpitesAsync(contexto, doLocal);
        if (palpites.Count == 0) return new List<PalpiteiroNoRanking>();

        var partidaIds = palpites.Select(v => v.PartidaId).Distinct().ToList();

        var partidas = await ConsultaDePartidas(contexto, p => partidaIds.Contains(p.Id)).ToListAsync();

        // ⚠️ TORNEIO OCULTO NÃO SOMA. O ranking do TORNEIO é protegido pela porta dele (oculto
        // responde 404); esta lista é pública e não tem porta nenhuma — sem o filtro, ela
        // somaria o torneio que o organizador ainda não divulgou.
        var contam = await TorneiosQueContamAsync(contexto, partidas.Select(p => p.TorneioId));
        partidas = partidas.Where(p => p.TorneioId is int t && contam.Contains(t)).ToList();

        var apuracao = await ApurarAsync(contexto, partidas, palpites);
        return apuracao.Linhas;
    }

    // ── 3. O RESUMO DE UMA PESSOA (o selo do perfil) ──────────────────────────────────────
    //
    // Null = esta pessoa não tem palpite que conte — e aí o perfil não desenha selo nenhum.
    //
    // ⚠️ Mesmo universo do hub (nada de oculto nem cancelado), de propósito: o selo do perfil
    // e a linha da aba precisam dizer o MESMO número. Duas contagens diferentes pro mesmo nome
    // é o tipo de divergência que ninguém reporta como bug — só desconfia das duas.
    public static async Task<PalpiteiroNoRanking?> DoJogadorAsync(DbPadelContext contexto, int jogadorId)
    {
        var meus = await PalpitesAsync(contexto, apenasEstesJogadores: new HashSet<int> { jogadorId });
        if (meus.Count == 0) return null;

        var partidaIds = meus.Select(v => v.PartidaId).Distinct().ToList();
        var partidas = await ConsultaDePartidas(contexto, p => partidaIds.Contains(p.Id)).ToListAsync();

        var contam = await TorneiosQueContamAsync(contexto, partidas.Select(p => p.TorneioId));
        partidas = partidas.Where(p => p.TorneioId is int t && contam.Contains(t)).ToList();

        var apuracao = await ApurarAsync(contexto, partidas, meus);
        return apuracao.Linhas.FirstOrDefault();
    }

    // ── O NÚCLEO ──────────────────────────────────────────────────────────────────────────

    public sealed record Apuracao(List<PalpiteiroNoRanking> Linhas, int JogosApurados, int PalpitesComPlacar);

    private static async Task<Apuracao> ApurarAsync(
        DbPadelContext contexto, List<PartidaApurada> partidas, List<PalpiteApurado> palpites)
    {
        if (partidas.Count == 0 || palpites.Count == 0)
            return new Apuracao(new List<PalpiteiroNoRanking>(), 0, 0);

        var duplaIds = partidas.SelectMany(p => new[] { p.Dupla1Id, p.Dupla2Id }).Distinct().ToList();
        return Apurar(partidas, palpites, await EmQuadraAsync(contexto, duplaIds));
    }

    // A apuração PURA — sem banco, e é onde a régua toda acontece.
    public static Apuracao Apurar(
        IEnumerable<PartidaApurada> partidas,
        IEnumerable<PalpiteApurado> palpites,
        Dictionary<int, HashSet<int>> emQuadra)
    {
        var porPartida = palpites.GroupBy(v => v.PartidaId).ToDictionary(g => g.Key, g => g.ToList());
        var porJogador = new Dictionary<int, PalpiteiroNoRanking>();
        var partidasQueContaram = new HashSet<int>();
        int palpitesComPlacar = 0;

        foreach (var partida in partidas)
        {
            if (!porPartida.TryGetValue(partida.Id, out var doJogo)) continue;

            var jogadoresDaPartida = new HashSet<int>(emQuadra.GetValueOrDefault(partida.Dupla1Id, new HashSet<int>()));
            jogadoresDaPartida.UnionWith(emQuadra.GetValueOrDefault(partida.Dupla2Id, new HashSet<int>()));

            foreach (var palpite in doJogo)
            {
                if (jogadoresDaPartida.Contains(palpite.JogadorId)) continue;

                // ⚠️ A MOEDA É DITADA PELO PALPITE, não pelo formato de hoje. Quem palpitou em
                // games é conferido contra os games; quem palpitou em sets, contra os sets.
                // Parece o mesmo, mas não é: o organizador pode editar o formato do torneio
                // DEPOIS de o palpite estar gravado, e aí perguntar ao formato compararia o que
                // a pessoa disse com um placar que ela não tinha como estar respondendo.
                var palpitado = PlacaresPossiveis.Lido(
                    palpite.GamesDupla1, palpite.GamesDupla2, palpite.SetsDupla1, palpite.SetsDupla2);

                int pontos = PontosDoPalpite.De(new PalpiteConferido
                {
                    DuplaEscolhidaId = palpite.DuplaEscolhidaId,
                    VencedorId = partida.VencedorId,
                    PalpitouLado1 = palpitado.Lado1,
                    PalpitouLado2 = palpitado.Lado2,
                    PlacarLado1 = palpitado.EmSets ? partida.SetsDupla1 : partida.GamesDupla1,
                    PlacarLado2 = palpitado.EmSets ? partida.SetsDupla2 : partida.GamesDupla2,
                    EmSets = palpitado.EmSets,
                    PorWo = partida.MotivoDoEncerramento == EncerramentoPorWo.Motivo,
                });

                if (!porJogador.TryGetValue(palpite.JogadorId, out var linha))
                {
                    linha = new PalpiteiroNoRanking
                    {
                        JogadorId = palpite.JogadorId,
                        Nome = palpite.Nome,
                        Foto = palpite.Foto,
                    };
                    porJogador[palpite.JogadorId] = linha;
                }

                linha.Palpites++;
                linha.Pontos += pontos;
                if (pontos > 0) linha.Acertos++;
                if (pontos == PontosDoPalpite.Cravou) linha.Cravadas++;
                if (palpitado.Existe) palpitesComPlacar++;

                partidasQueContaram.Add(partida.Id);
            }
        }

        return new Apuracao(Classificar(porJogador.Values), partidasQueContaram.Count, palpitesComPlacar);
    }

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

    // ── O QUE VEM DO BANCO ────────────────────────────────────────────────────────────────

    // As partidas que podem ser apuradas, já filtradas.
    //
    // ⚠️ `VencedorId != null` E `Status == "Finalizada"`, e não um só dos dois: jogo em
    // andamento já tem placar parcial (a Mesa grava game a game) e apurar por ele diria quem
    // está ganhando, não quem ganhou.
    //
    // ⚠️ O FILTRO DE QUEM CHAMA ENTRA AQUI DENTRO, ANTES da projeção, e isso não é estilo — é a
    // diferença entre a página abrir e dar 500. Pendurar `.Where(p => p.TorneioId == id)` DEPOIS
    // do `Select` obriga o EF a achar a coluna a partir do record projetado, e ele não sabe
    // fazer isso: a consulta inteira para de traduzir. ⚠️ E o banco InMemory da suíte NÃO pega
    // (lá o Where roda sobre objetos), o que fez isto passar por 4 mil testes verdes e só
    // aparecer na primeira visita à página no Postgres. Ver
    // TraducaoDasConsultasDePalpiteTests, que agora compila cada consulta uma a uma.
    public static IQueryable<PartidaApurada> ConsultaDePartidas(
        DbPadelContext contexto, System.Linq.Expressions.Expression<Func<Partida, bool>> filtro) =>
        contexto.Partidas
            .AsNoTracking()
            .Where(p => p.Status == PartidaFinalizada && p.VencedorId != null)
            .Where(filtro)
            .Select(p => new PartidaApurada(p.Id, p.TorneioId, p.VencedorId, p.Dupla1Id, p.Dupla2Id,
                p.GamesDupla1, p.GamesDupla2, p.SetsDupla1, p.SetsDupla2, p.MotivoDoEncerramento));

    private static async Task<List<PalpiteApurado>> PalpitesDasPartidasAsync(
        DbPadelContext contexto, List<int> partidaIds) =>
        partidaIds.Count == 0
            ? new List<PalpiteApurado>()
            : await MaterializarAsync(contexto, v => partidaIds.Contains(v.PartidaId));

    private static async Task<List<PalpiteApurado>> PalpitesAsync(
        DbPadelContext contexto, HashSet<int>? apenasEstesJogadores)
    {
        if (apenasEstesJogadores == null) return await MaterializarAsync(contexto, v => true);

        var ids = apenasEstesJogadores.ToList();
        return await MaterializarAsync(contexto, v => ids.Contains(v.JogadorId));
    }

    // A consulta dos palpites — SÓ COLUNAS, nada de método nosso dentro dela.
    //
    // ⚠️ O nome bonito (`NomeBonito.ComApelido`) é montado depois, na materialização. O EF até
    // aceitaria o método na projeção FINAL (ele avalia no cliente), mas essa licença vale só
    // enquanto o `Select` for o último passo: basta alguém compor um `Where` depois pra virar
    // erro de tradução em produção — o mesmo tipo de erro que o `Where` sobre record projetado
    // causou aqui. Consulta que só carrega coluna não tem como quebrar assim.
    public static IQueryable<PalpiteCru> ConsultaDePalpites(
        DbPadelContext contexto, System.Linq.Expressions.Expression<Func<PalpitePartida, bool>> filtro) =>
        contexto.PalpitesPartida
            .AsNoTracking()
            .Where(filtro)
            .Select(v => new PalpiteCru(
                v.PartidaId, v.JogadorId, v.DuplaEscolhidaId,
                v.Jogador.Nome, v.Jogador.Apelido, v.Jogador.FotoPerfil,
                v.GamesDupla1, v.GamesDupla2, v.SetsDupla1, v.SetsDupla2));

    private static async Task<List<PalpiteApurado>> MaterializarAsync(
        DbPadelContext contexto, System.Linq.Expressions.Expression<Func<PalpitePartida, bool>> filtro) =>
        (await ConsultaDePalpites(contexto, filtro).ToListAsync())
            .Select(v => new PalpiteApurado(
                v.PartidaId, v.JogadorId, NomeBonito.ComApelido(v.Nome, v.Apelido), v.FotoPerfil,
                v.DuplaEscolhidaId, v.GamesDupla1, v.GamesDupla2, v.SetsDupla1, v.SetsDupla2))
            .ToList();

    // Quem estava EM QUADRA em cada dupla. Consultado à parte de propósito: pendurar as duas
    // duplas na projeção da partida traria quatro navegações obrigatórias num JOIN só, e é a
    // linha inteira que some quando uma delas falta.
    private static async Task<Dictionary<int, HashSet<int>>> EmQuadraAsync(
        DbPadelContext contexto, List<int> duplaIds)
    {
        var duplas = await contexto.Duplas
            .AsNoTracking()
            .Where(d => duplaIds.Contains(d.Id))
            .Select(d => new { d.Id, d.NomeTime, d.Jogador1Id, d.Jogador2Id })
            .ToListAsync();

        return duplas.ToDictionary(
            d => d.Id,
            d => d.NomeTime != null
                // Linha de TIME: o Jogador1 é quem cadastrou, não quem joga. Ninguém a excluir.
                ? new HashSet<int>()
                : d.Jogador2Id is int parceiro
                    ? new HashSet<int> { d.Jogador1Id, parceiro }
                    : new HashSet<int> { d.Jogador1Id });
    }

    // Quais destes torneios podem somar no ranking geral: os que NÃO estão ocultos e NÃO foram
    // cancelados.
    //
    // ⚠️ De propósito NÃO é a régua da VITRINE (`ApareceParaOPublico`), que exige também a
    // aprovação do admin. São perguntas diferentes: a vitrine decide o que é LISTADO e
    // anunciado, e torneio esperando aprovação fica de fora dela por isso — mas a página dele é
    // pública o tempo todo (quem recusa acesso é `VisibilidadeDoTorneio`, e ela só olha o
    // `Oculto`). Este ranking não lista torneio nenhum: ele soma pontos de gente. Exigir
    // aprovação aqui apagaria da tabela torneios de verdade, já jogados, sem que nada na tela
    // dissesse por quê — e a aba nasceria quase vazia.
    //
    // ⚠️ Cancelado fica de fora pela régua de sempre: o evento não aconteceu. É a mesma que
    // tira o torneio cancelado do MVP, do card de campeão e do ponto de ranking.
    private static async Task<HashSet<int>> TorneiosQueContamAsync(
        DbPadelContext contexto, IEnumerable<int?> torneioIds)
    {
        var ids = torneioIds.Where(t => t != null).Select(t => t!.Value).Distinct().ToList();
        if (ids.Count == 0) return new HashSet<int>();

        var torneios = await contexto.Torneios
            .AsNoTracking()
            .Where(t => ids.Contains(t.Id))
            .Select(t => new { t.Id, t.Oculto, t.Status })
            .ToListAsync();

        return torneios
            .Where(t => !t.Oculto && !CancelamentoDoTorneio.EstaCancelado(t.Status))
            .Select(t => t.Id)
            .ToHashSet();
    }
}

// Uma partida já pronta pra apuração.
public sealed record PartidaApurada(
    int Id, int? TorneioId, int? VencedorId, int Dupla1Id, int Dupla2Id,
    int? GamesDupla1, int? GamesDupla2, int? SetsDupla1, int? SetsDupla2, string? MotivoDoEncerramento);

// O palpite como ele sai do BANCO: só coluna, sem nada calculado. Existe pra a consulta não
// ter método nosso dentro dela — ver ConsultaDePalpites.
public sealed record PalpiteCru(
    int PartidaId, int JogadorId, int DuplaEscolhidaId, string Nome, string? Apelido, string? FotoPerfil,
    int? GamesDupla1, int? GamesDupla2, int? SetsDupla1, int? SetsDupla2);

// Um palpite já pronto pra apuração, com o nome de quem palpitou.
public sealed record PalpiteApurado(
    int PartidaId, int JogadorId, string Nome, string? Foto, int DuplaEscolhidaId,
    int? GamesDupla1, int? GamesDupla2, int? SetsDupla1, int? SetsDupla2);

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

    // Quantas vezes cravou o placar exato.
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
