using Padelizou.Models;

namespace Padelizou.Services;

// Quem classificou em cada grupo, em que posição e com que campanha — a régua única.
//
// Esta conta existia COPIADA em três lugares (o robô da Mesa, o robô do Controle de Placar
// e, agora, a detecção de bye do avanço de fase). Três cópias de um ranking é um convite a
// três campeões diferentes: bastaria um desempate divergir. A regra: vitórias, depois saldo
// de games, dentro de cada grupo; classificam os N primeiros.
// Quem busca os pontos do ranking quando o empate chega até eles. É a assinatura do
// `IEstatisticasService.ObterPontosPorJogadorAsync`, de propósito: quem chama passa o método
// dele direto, e os serviços ESTÁTICOS (AvancoDaChave, ClassificacaoParaCard) alcançam o
// ranking sem precisar de DI nem de uma segunda cópia daquela consulta — que alimenta a busca
// de jogadores e o sorteio de chaves, e tem armadilha documentada.
//
// ⚠️ É chamado SÓ quando `ClassificacaoDeGrupos.PrecisaDePontos` diz que sim. São duas
// consultas ao banco, e a página do torneio é a mais visitada do site.
public delegate Task<Dictionary<int, int>> BuscarPontosDoRanking(IEnumerable<int> jogadorIds);

public static class ClassificacaoDeGrupos
{
    // Nenhum ponto em mãos: o empate que chegar ao ranking cai direto no sorteio estável.
    public static readonly IReadOnlyDictionary<int, int> SemPontos = new Dictionary<int, int>();

    // Os pontos de um grupo, buscados só se o empate for até eles. É o atalho que faz o
    // "ranking ao vivo" caber na página do torneio.
    public static async Task<IReadOnlyDictionary<int, int>> PontosSePrecisarAsync(
        IReadOnlyList<Dupla> duplasDoGrupo, IReadOnlyList<Partida> partidasDeGrupo,
        BuscarPontosDoRanking buscar)
    {
        if (!PrecisaDePontos(duplasDoGrupo, partidasDeGrupo)) return SemPontos;

        var jogadores = duplasDoGrupo
            .SelectMany(d => new[] { (int?)d.Jogador1Id, d.Jogador2Id })
            .Where(id => id != null)
            .Select(id => id!.Value);

        return await buscar(jogadores);
    }

    // A mesma coisa pra uma tela que desenha VÁRIOS grupos (a classificação, o card): uma
    // checagem e UMA consulta só, em vez de uma por grupo.
    public static async Task<IReadOnlyDictionary<int, int>> PontosSePrecisarAsync(
        IReadOnlyList<IReadOnlyList<Dupla>> grupos, IReadOnlyList<Partida> partidasDeGrupo,
        BuscarPontosDoRanking buscar)
    {
        var quePrecisam = grupos.Where(g => PrecisaDePontos(g, partidasDeGrupo)).ToList();
        if (quePrecisam.Count == 0) return SemPontos;

        var jogadores = quePrecisam
            .SelectMany(g => g)
            .SelectMany(d => new[] { (int?)d.Jogador1Id, d.Jogador2Id })
            .Where(id => id != null)
            .Select(id => id!.Value);

        return await buscar(jogadores);
    }
    // ── QUANTAS VAGAS CADA GRUPO DÁ — a régua única do NÚMERO ────────────────────────────
    //
    // O `Ordenar` logo abaixo responde QUEM vem primeiro; isto responde QUANTOS entram. São as
    // duas metades da mesma pergunta, e por isso moram no mesmo arquivo.
    //
    // ⚠️ ESTE NÚMERO ESTAVA ESCRITO À MÃO EM DEZ LUGARES (`categoria.ClassificadosPorGrupo ?? 2`,
    // com `Math.Max(1, …)` nos quatro serviços e SEM ele nas quatro views), e foi exatamente
    // assim que a tela de Classificação passou a discordar do chaveamento: ela lia o campo do
    // TORNEIO (`Torneio.ClassificadosPorGrupo`, que nasce 2 e nenhuma tela edita) enquanto o
    // `AvancoDaChave` lia o da CATEGORIA. Numa categoria de TIMES, onde o organizador escolhe de
    // 1 a 4 por grupo, a tela pintava 2 linhas de verde e a chave levava 4 — o time em 3º lia
    // que estava fora de uma vaga que ia receber. `VagasPorGrupoSaoUmaReguaSoTests` tem o gate
    // que quebra se o `?? 2` for reescrito fora daqui.
    //
    // ⚠️ `Torneio.ClassificadosPorGrupo` NÃO entra como padrão, e isso é escolha: hoje quem
    // manda no mata-mata é o campo da categoria, e pôr o do torneio de fallback mudaria a CHAVE
    // de todo torneio cuja coluna tenha valor diferente de 2 (a `DuplicacaoDeTorneio` copia
    // aquele campo, então não é impossível existir um). Alinhar a tela ao chaveamento é o que
    // foi pedido — o contrário seria alinhar o chaveamento à tela.
    public const int VagasPadrao = 2;

    // `categoria` nula vale a regra de sempre: as views chamam isto com navegação que pode não
    // ter vindo do Include, e estourar ali derrubaria a página por causa de um número de tela.
    public static int VagasPorGrupo(Categoria? categoria) =>
        Math.Max(1, categoria?.ClassificadosPorGrupo ?? VagasPadrao);

    // Uma linha da tabela do grupo, já com a campanha somada. A TELA de classificação também
    // come daqui desde 13/08/2026: ela montava a própria ordenação (vitórias + saldo, sem
    // terceiro critério e com a vitória contada por `meusGames > gamesAdversario` em vez do
    // QuemVenceu) — era a quarta cópia desta regra, viva justamente onde o jogador olha.
    public record Linha(Dupla Dupla, int Jogos, int Vitorias, int Derrotas, int Saldo, int GamesPro, int GamesContra);

    // A tabela COMPLETA de um grupo, na ordem oficial. `Calcular` corta os N primeiros disto.
    //
    // `pontosPorJogador`: os pontos do ranking anual, por Id de JOGADOR — só são consultados
    // quando o empate chega até eles. Passe vazio quando `PrecisaDePontos` disse que não
    // precisa; passar vazio num grupo que precisa não dá erro, manda o desempate pro sorteio.
    public static List<Linha> Ordenar(
        IEnumerable<Dupla> duplasDoGrupo,
        IReadOnlyList<Partida> partidasDeGrupo,
        IReadOnlyDictionary<int, int> pontosPorJogador) =>
        Desempatar(PorCampanha(duplasDoGrupo, partidasDeGrupo), partidasDeGrupo, pontosPorJogador);

    // Vitórias → saldo → games a favor. É a parte da régua que se decide NA QUADRA, e ela não
    // mudou: nenhum grupo que se resolve aqui muda de resultado por causa dos critérios novos.
    private static List<Linha> PorCampanha(
        IEnumerable<Dupla> duplasDoGrupo, IReadOnlyList<Partida> partidasDeGrupo) =>
        duplasDoGrupo
            .Select(dupla =>
            {
                var jogos = partidasDeGrupo
                    .Where(p => p.Dupla1Id == dupla.Id || p.Dupla2Id == dupla.Id)
                    .ToList();

                int vitorias = 0, derrotas = 0, gamesPro = 0, gamesContra = 0;
                foreach (var jogo in jogos)
                {
                    bool ehDupla1 = jogo.Dupla1Id == dupla.Id;
                    gamesPro += ehDupla1 ? (jogo.GamesDupla1 ?? 0) : (jogo.GamesDupla2 ?? 0);
                    gamesContra += ehDupla1 ? (jogo.GamesDupla2 ?? 0) : (jogo.GamesDupla1 ?? 0);

                    // ⚠️ A vitória sai de Services/QuemVenceu, a MESMA função que grava o
                    // VencedorId ao finalizar. Aqui existia a conta em separado (`pro >
                    // contra`), e ela divergia da outra quando um lado estava sem placar:
                    // a tabela dizia que a dupla A venceu e o registro do jogo dizia que
                    // foi a B. No Interno de 05/08 isso classificou a dupla errada.
                    var venceu = QuemVenceu.Da(jogo);
                    if (venceu == dupla.Id) vitorias++;
                    else if (venceu != null) derrotas++;
                }

                return new Linha(dupla, jogos.Count, vitorias, derrotas, gamesPro - gamesContra, gamesPro, gamesContra);
            })
            // ⚠️ A ORDEM PRECISA SER TOTAL — não pode sobrar empate nenhum no fim.
            //
            // Vitórias e saldo não bastam, e não é caso raro: no Interno de 05/08/2026 o
            // grupo A dos TIMES terminou com Target.it, Valandro e Argentus empatados em
            // 1 vitória e −2 de saldo. Sem terceiro critério, quem fica em 2º sai da ORDEM
            // EM QUE AS DUPLAS CHEGARAM na consulta — e cada chamador monta essa consulta
            // do seu jeito. A mesma tabela então respondia coisas diferentes conforme quem
            // perguntasse, e a chave saiu com um time que não tinha vencido a semifinal.
            //
            // O que vem DEPOIS dos games a favor mora no `Desempatar` — confronto direto,
            // ranking e sorteio estável.
            .OrderByDescending(x => x.Vitorias)
            .ThenByDescending(x => x.Saldo)
            .ThenByDescending(x => x.GamesPro)
            .ToList();

    // ── OS DESEMPATES DEPOIS DA QUADRA ───────────────────────────────────────────────────
    //
    // 🗣️ Felipe, 11/09/2026, depois de o painel revelar que um empate total caía na ordem de
    // cadastro: *"e se empatar entre apenas 2 duplas, passa quem venceu o confronto direto"* e
    // *"e empate entre os 3, passa quem esta na frente no ranking, se não tiver ninguem com
    // pontuação ainda, faça sorteio"*.
    //
    // ⚠️ A REGRA DELE RESOLVE A OBJEÇÃO DE 05/08/2026, que está escrita mais acima: confronto
    // direto tinha sido recusado porque no empate de TRÊS ele é circular (A ganhou de B, B de C
    // e C de A). Separar por TAMANHO do empate — direto pra 2, ranking pra 3+ — é o que o faz
    // funcionar, porque entre DUAS duplas nunca há circularidade.
    //
    // ⚠️ ESCOLHA DO FELIPE, COM O CUSTO NA MESA: o ranking é lido AO VIVO, então uma dupla pode
    // passar à frente da outra semana que vem por ter jogado outro torneio — a mesma tabela,
    // com os mesmos jogos, muda de 2º colocado depois de a chave estar montada. A alternativa
    // era congelar o número no sorteio (migration), e ele preferiu entregar sem. O teto: o que
    // vale é a chave já gerada; a tabela é a que pode escorregar.
    private static List<Linha> Desempatar(
        List<Linha> porCampanha, IReadOnlyList<Partida> partidas,
        IReadOnlyDictionary<int, int> pontosPorJogador)
    {
        var saida = new List<Linha>(porCampanha.Count);

        foreach (var bloco in BlocosEmpatados(porCampanha))
        {
            if (bloco.Count == 1)
            {
                saida.AddRange(bloco);
                continue;
            }

            // DUAS: quem venceu o jogo entre elas. Jogo sem vencedor (0x0, ou ainda por jogar)
            // cai no degrau seguinte em vez de inventar um ganhador.
            if (bloco.Count == 2 && ConfrontoDireto(bloco[0].Dupla.Id, bloco[1].Dupla.Id, partidas) is { } venceu)
            {
                saida.AddRange(bloco.OrderByDescending(l => l.Dupla.Id == venceu));
                continue;
            }

            // TRÊS OU MAIS (e o empate de duas sem confronto decidido): o ranking anual, e o
            // sorteio quando ele também não separa — inclusive quando ninguém tem pontuação,
            // que é o caso que o Felipe nomeou.
            saida.AddRange(bloco
                .OrderByDescending(l => PontosDaDupla(l.Dupla, pontosPorJogador))
                .ThenBy(l => Sorteio(l.Dupla)));
        }

        return saida;
    }

    // Buscar pontos custa DUAS consultas ao banco, e a página do torneio é a mais visitada do
    // site (24 grupos no 2ª Etapa ER). Esta pergunta é o que faz "ranking ao vivo" caber lá: só
    // paga quem tem empate que chega até o ranking.
    //
    // ⚠️ Responder `false` num grupo que precisava NÃO quebra a régua — manda o desempate pro
    // sorteio. Mas faria a tela discordar de quem consultou os pontos, que é o defeito de
    // 05/08. Por isso a conta é a MESMA do `Desempatar`, e não uma heurística à parte.
    public static bool PrecisaDePontos(
        IEnumerable<Dupla> duplasDoGrupo, IReadOnlyList<Partida> partidasDeGrupo) =>
        BlocosEmpatados(PorCampanha(duplasDoGrupo, partidasDeGrupo)).Any(bloco =>
            bloco.Count > 2
            || (bloco.Count == 2
                && ConfrontoDireto(bloco[0].Dupla.Id, bloco[1].Dupla.Id, partidasDeGrupo) == null));

    // Os trechos vizinhos com a campanha IDÊNTICA — é dentro deles, e só dentro deles, que os
    // critérios de fora da quadra entram.
    private static List<List<Linha>> BlocosEmpatados(List<Linha> porCampanha)
    {
        var blocos = new List<List<Linha>>();
        int i = 0;
        while (i < porCampanha.Count)
        {
            int ini = i;
            while (i < porCampanha.Count
                   && porCampanha[i].Vitorias == porCampanha[ini].Vitorias
                   && porCampanha[i].Saldo == porCampanha[ini].Saldo
                   && porCampanha[i].GamesPro == porCampanha[ini].GamesPro) i++;
            blocos.Add(porCampanha.GetRange(ini, i - ini));
        }
        return blocos;
    }

    // Quem venceu o jogo entre estas duas — null quando não houve jogo decidido entre elas.
    // A vitória sai do `QuemVenceu`, a MESMA função que grava o VencedorId ao finalizar.
    private static int? ConfrontoDireto(int duplaA, int duplaB, IReadOnlyList<Partida> partidas)
    {
        foreach (var jogo in partidas)
        {
            bool entreElas = (jogo.Dupla1Id == duplaA && jogo.Dupla2Id == duplaB)
                          || (jogo.Dupla1Id == duplaB && jogo.Dupla2Id == duplaA);
            if (entreElas && QuemVenceu.Da(jogo) is { } venceu) return venceu;
        }
        return null;
    }

    // O ranking da DUPLA é a soma dos dois jogadores. Jogador sem pontuação vale 0, que é o
    // que faz "ninguém pontuado" cair sozinho no sorteio, sem caso especial.
    private static int PontosDaDupla(Dupla dupla, IReadOnlyDictionary<int, int> pontos)
    {
        int Do(int? jogadorId) =>
            jogadorId is int id && pontos.TryGetValue(id, out int p) ? p : 0;

        return Do(dupla.Jogador1Id) + Do(dupla.Jogador2Id);
    }

    // O SORTEIO, e ele precisa de duas coisas ao mesmo tempo: não ser a ordem de inscrição (que
    // favorece quem se inscreveu primeiro — a queixa do Felipe) e responder SEMPRE a mesma
    // coisa (a invariante desta régua; sorteio que muda de ideia entre duas telas foi o defeito
    // de 05/08).
    //
    // ⚠️ FNV-1a À MÃO, e não `string.GetHashCode()`: o hash de string do .NET é ALEATORIZADO
    // POR PROCESSO (proteção contra ataque de colisão). Usá-lo aqui faria a régua responder uma
    // coisa antes do deploy e outra depois, com os mesmos jogos — e a chave montada ontem
    // deixaria de casar com a tabela de hoje. `DesempateDoGrupoTests` crava a ordem de saída
    // justamente pra travar isso.
    private static uint Sorteio(Dupla dupla)
    {
        uint hash = 2166136261;
        foreach (char c in dupla.Id.ToString(System.Globalization.CultureInfo.InvariantCulture))
            hash = (hash ^ c) * 16777619;
        return hash;
    }

    // ── O CORTE FOI DECIDIDO, OU FOI SORTEIO? ────────────────────────────────────────────
    //
    // 🗣️ Felipe achou o caso olhando o painel no ar (11/09/2026): *"e esse caso aqui se for os
    // 3 jogos 9x4 e houver empate?"*. Num grupo de 3 com os três jogos 9x4, as três terminam
    // com 1 vitória, saldo 0 e 13 games a favor — e, se ninguém tiver pontuação, a vaga sai do
    // sorteio. O painel PRECISA dizer isso, em vez de apresentar cara-ou-coroa como eliminação
    // esportiva.
    //
    // ⚠️ SÓ O SORTEIO CONTA COMO "SEM CRITÉRIO". Confronto direto e ranking são decisões
    // legítimas — avisar sobre elas seria assustar por nada. Por isso esta função recebe as
    // partidas e os pontos: ela pergunta se os degraus DEPOIS da quadra também deixaram de
    // separar, exatamente como o `Desempatar` faz.
    //
    // Devolve vazio quando o corte teve critério.
    public static List<Linha> EmpateNoCorte(
        IReadOnlyList<Linha> ranking, int passam,
        IReadOnlyList<Partida> partidas, IReadOnlyDictionary<int, int> pontosPorJogador)
    {
        var vazio = new List<Linha>();
        if (passam <= 0 || passam >= ranking.Count) return vazio;

        var ultimaQuePassa = ranking[passam - 1];
        bool EmpataComEla(Linha l) =>
            l.Vitorias == ultimaQuePassa.Vitorias
            && l.Saldo == ultimaQuePassa.Saldo
            && l.GamesPro == ultimaQuePassa.GamesPro;

        if (!EmpataComEla(ranking[passam])) return vazio;

        var empatadas = ranking.Where(EmpataComEla).ToList();

        // Duas duplas com confronto direto decidido: a quadra resolveu.
        if (empatadas.Count == 2
            && ConfrontoDireto(empatadas[0].Dupla.Id, empatadas[1].Dupla.Id, partidas) != null)
            return vazio;

        // O ranking separa alguma delas? Basta UMA estar na frente pra o corte ter critério.
        var porPontos = empatadas.Select(l => PontosDaDupla(l.Dupla, pontosPorJogador)).ToList();
        if (porPontos.Distinct().Count() > 1) return vazio;

        return empatadas;
    }

    public static List<ChaveamentoMataMata.Classificado> Calcular(
        IEnumerable<Dupla> duplasComGrupo,
        IReadOnlyList<Partida> partidasDeGrupo,
        IReadOnlyDictionary<int, int> pontosPorJogador,
        int classificamPorGrupo = VagasPadrao)
    {
        var classificados = new List<ChaveamentoMataMata.Classificado>();
        int passam = Math.Max(1, classificamPorGrupo);

        foreach (var grupo in duplasComGrupo
                     .Where(d => d.Grupo != null)
                     .GroupBy(d => d.Grupo!)
                     .OrderBy(g => g.Key))
        {
            var ranking = Ordenar(grupo, partidasDeGrupo, pontosPorJogador);

            for (int pos = 0; pos < ranking.Count && pos < passam; pos++)
            {
                classificados.Add(new ChaveamentoMataMata.Classificado(
                    ranking[pos].Dupla.Id, grupo.Key, ranking[pos].Vitorias, ranking[pos].Saldo, pos + 1,
                    Jogos: ranking[pos].Jogos));
            }
        }

        return classificados;
    }
}
