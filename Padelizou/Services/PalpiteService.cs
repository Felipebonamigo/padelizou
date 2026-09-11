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

    // Quantos palpites no MESMO placar fazem uma leitura da galera. Ver o comentário no cálculo.
    public const int MinimoParaOMaisPalpitado = 2;

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
            //
            // ⚠️ E SÓ EXISTE A PARTIR DE DOIS PALPITES NO MESMO PLACAR (11/09/2026). 🗣️ Felipe,
            // apontando a frase num jogo com 3 de 8: *"aqui por que tem isso? nao sei se faz
            // muito sentido"*. Sem limiar a tela anunciava como leitura da galera o palpite de
            // UMA pessoa — e, com todos os placares diferentes, o "mais palpitado" era o
            // desempate interno decidindo por sorteio qual 1 a 1 a 1 ganhava a frase.
            //
            // ⚠️ O limiar mora AQUI, e não na view: as duas telas e o JS que repinta depois do
            // voto leem o mesmo resumo. Escrito na view seria a terceira cópia da régua, e a
            // linha voltaria a aparecer sozinha no primeiro `atualizarPalpitrometro`.
            //
            // ⚠️ É DOIS, e não uma proporção: exigir maioria esconderia a leitura num jogo com
            // 20 palpites espalhados, que é justamente onde saber o mais votado interessa. A
            // contagem ao lado ("2 de 5") é o que deixa quem lê julgar o peso.
            var maisPalpitado = comPlacar
                .GroupBy(placar => (placar.Lado1, placar.Lado2))
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => g.Key.Lado1)
                .ThenByDescending(g => g.Key.Lado2)
                .FirstOrDefault(g => g.Count() >= MinimoParaOMaisPalpitado);

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

        bool ehLinhaNova = voto == null;
        if (voto == null)
        {
            voto = new PalpitePartida { PartidaId = partidaId, JogadorId = jogadorId };
            _context.PalpitesPartida.Add(voto);
        }

        Escrever(voto, duplaEscolhidaId, placar);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException) when (ehLinhaNova)
        {
            // ⚠️ CLIQUE DUPLO: os dois POSTs leram "esse jogador ainda não votou" e os dois
            // tentaram INSERIR — o palpitrômetro não tem trava de clique, e cada requisição tem o
            // próprio DbContext. Quem segura é o índice único (PartidaId, JogadorId), e está certo
            // que seja ele (mesma decisão do chamado do mural em DuplasController). O que faltava
            // era o serviço saber PERDER a corrida: quem chega depois grava por cima, porque o
            // palpite dele é o último que a pessoa deu. Sem isto virava 500 — o controller só
            // trata InvalidOperationException — e push de erro em produção por um toque duplo.
            _context.Entry(voto).State = EntityState.Detached;

            var doGemeo = await _context.PalpitesPartida
                .FirstOrDefaultAsync(v => v.PartidaId == partidaId && v.JogadorId == jogadorId);

            // ⚠️ Sem linha do outro lado, a gravação falhou por OUTRO motivo — e aí o erro TEM
            // que subir. Engolir tudo que é DbUpdateException trocaria um 500 que avisa por um
            // palpite que some caladinho.
            if (doGemeo == null) throw;

            Escrever(doGemeo, duplaEscolhidaId, placar);
            await _context.SaveChangesAsync();
        }

        var resumos = await ObterResumosAsync(new[] { partidaId }, jogadorId);
        return resumos[partidaId];
    }

    // RETIRAR O PALPITE (10/09/2026). 🗣️ Felipe: *"tambem permita retirar o palpite colocado"*.
    //
    // 🕳️ Dava pra TROCAR de dupla e trocar a ficha do placar, mas não pra sair: uma vez tocado o
    // nome, aquele palpite ficava na barra e no ranking pra sempre. Quem tocou sem querer — e o
    // alvo é um nome no meio de uma lista de 97 jogos — não tinha caminho de volta.
    //
    // ⚠️ A JANELA É A MESMA DO PALPITAR, e não uma régua nova: 🗣️ *"todo jogo pode ser palpitado
    // até começar"*. Começou, a aposta está valendo — sair ali seria desistir vendo o primeiro
    // game. Uma segunda régua aqui também deixaria a tela poder retirar o que não pode trocar.
    //
    // ⚠️ IDEMPOTENTE: o toque duplo manda dois POSTs, e o segundo chega com a linha já apagada.
    // Estourar ali daria alerta vermelho a quem conseguiu exatamente o que queria — mesma lição
    // da corrida do clique duplo no votar.
    public async Task<PalpiteResumoVM> RetirarPalpiteAsync(int partidaId, int jogadorId)
    {
        var partida = await _context.Partidas.FindAsync(partidaId);
        if (partida == null) throw new InvalidOperationException("Partida não encontrada.");
        if (partida.Status != "Agendada")
            throw new InvalidOperationException("Esta partida já começou — não é mais possível mudar o palpite.");

        // ⚠️ A CHECAGEM DE DONO É ESTRUTURAL (Regra 0): a linha é procurada por (partida,
        // jogador), e o jogador vem da claim de quem está logado. Não existe caminho por onde
        // pedir a retirada do palpite de outra pessoa — nem com um POST montado à mão.
        var meu = await _context.PalpitesPartida
            .FirstOrDefaultAsync(v => v.PartidaId == partidaId && v.JogadorId == jogadorId);

        if (meu != null)
        {
            // O placar palpitado mora na MESMA linha do voto e sai junto: deixá-lo pra trás
            // faria a frase "a galera crava 6x4" continuar contando um palpite que não existe.
            _context.PalpitesPartida.Remove(meu);
            await _context.SaveChangesAsync();
        }

        var resumos = await ObterResumosAsync(new[] { partidaId }, jogadorId);
        return resumos[partidaId];
    }

    // O palpite inteiro numa passada só — e é de propósito que ele seja UM lugar: a segunda
    // tentativa (a que perdeu a corrida) escreve na linha do gêmeo, e duas listas de colunas
    // acabariam divergindo justo no caminho que quase nunca roda.
    //
    // ⚠️ As quatro colunas do placar são reescritas SEMPRE, inclusive pra nulo. Trocar de
    // opinião sem dizer o placar tem que APAGAR o placar anterior: ele apontava a outra dupla, e
    // uma linha com "vence a Dupla 1" e "4 x 6" gravados juntos é uma contradição que o ranking
    // leria como palpite de placar — e contaria contra a própria pessoa.
    private static void Escrever(PalpitePartida voto, int duplaEscolhidaId, PlacarPalpitado placar)
    {
        voto.DuplaEscolhidaId = duplaEscolhidaId;
        voto.DataHora = DateTime.Now;

        voto.GamesDupla1 = placar.EmSets ? null : placar.Lado1;
        voto.GamesDupla2 = placar.EmSets ? null : placar.Lado2;
        voto.SetsDupla1 = placar.EmSets ? placar.Lado1 : null;
        voto.SetsDupla2 = placar.EmSets ? placar.Lado2 : null;
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

    // Uma linha do modal "quem votou em quem", com o placar que a pessoa palpitou.
    //
    // ⚠️ MAIOR × MENOR é o que orienta o placar pelo VOTO: a validação do RegistrarVoto já
    // garante que o placar aponta a dupla escolhida, então o maior dos dois lados é sempre o
    // dela. Comparar lado a lado precisaria saber de que lado a pessoa está — e é exatamente a
    // conta que a tela já faz pra marcar a ficha escolhida.
    private static VotanteVM Montar(PalpitePartida v)
    {
        var palpitado = PlacaresPossiveis.Lido(v.GamesDupla1, v.GamesDupla2, v.SetsDupla1, v.SetsDupla2);

        return new VotanteVM
        {
            // ⚠️ `Curto`, e não `Jogador.Nome`: "JOAO EGIDIO FERREIRA DA ROCHA" ocupava três
            // linhas do modal no celular, e o mesmo torneio escrevia o nome de dois jeitos —
            // o "Cravaram o placar" logo acima já passa pelo NomeBonito desde sempre.
            Nome = NomeBonito.Curto(v.Jogador.Nome),
            FotoPerfil = v.Jogador.FotoPerfil,
            PlacarVencedor = palpitado.Existe ? Math.Max(palpitado.Lado1!.Value, palpitado.Lado2!.Value) : null,
            PlacarPerdedor = palpitado.Existe ? Math.Min(palpitado.Lado1!.Value, palpitado.Lado2!.Value) : null,
            PlacarEmSets = palpitado.EmSets,
        };
    }

    // A lista de um lado do modal, EM ORDEM DE PLACAR (11/09/2026). 🗣️ Felipe: *"coloque em ordem
    // de placar, por exemplo, se colocaram o placar igual, deixe próximo"*. Com 14 nomes numa
    // coluna, achar quem apostou o mesmo que você era ler a lista inteira.
    //
    // ⚠️ A ORDEM É A DAS FICHAS DA TELA (ver PlacaresPossiveis.Do): do mais folgado ao mais
    // apertado — 6x0, 6x1, …, 6x5, 7x5. Inventar outra aqui faria a mesma lista de placares
    // aparecer em duas ordens diferentes na mesma página.
    //
    // ⚠️ Quem NÃO palpitou placar vai pro FIM: sem placar não há lugar na escala, e intercalar
    // essa gente quebraria justamente os grupos que a ordem acaba de juntar.
    //
    // ⚠️ A ordenação é TOTAL (vai até o nome) pela razão de sempre: ordenação parcial faz a lista
    // trocar de ordem entre duas aberturas do MESMO modal, e ninguém reporta isso como defeito —
    // só desconfia da tela.
    private static List<VotanteVM> EmOrdemDePlacar(IEnumerable<PalpitePartida> votos) =>
        votos
            .Select(Montar)
            .OrderBy(v => v.PlacarVencedor == null)
            .ThenBy(v => v.PlacarPerdedor)
            .ThenBy(v => v.PlacarVencedor)
            .ThenBy(v => v.Nome, StringComparer.OrdinalIgnoreCase)
            .ToList();

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
            VotantesDupla1 = EmOrdemDePlacar(votos.Where(v => v.DuplaEscolhidaId == partida.Dupla1Id)),
            VotantesDupla2 = EmOrdemDePlacar(votos.Where(v => v.DuplaEscolhidaId == partida.Dupla2Id))
        };
    }
}
