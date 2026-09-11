using Padelizou.Models;

namespace Padelizou.Services;

// Quem classificou em cada grupo, em que posição e com que campanha — a régua única.
//
// Esta conta existia COPIADA em três lugares (o robô da Mesa, o robô do Controle de Placar
// e, agora, a detecção de bye do avanço de fase). Três cópias de um ranking é um convite a
// três campeões diferentes: bastaria um desempate divergir. A regra: vitórias, depois saldo
// de games, dentro de cada grupo; classificam os N primeiros.
public static class ClassificacaoDeGrupos
{
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
    public static List<Linha> Ordenar(IEnumerable<Dupla> duplasDoGrupo, IReadOnlyList<Partida> partidasDeGrupo) =>
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
            // Confronto direto NÃO entra: naquele grupo ele é circular (Target.it ganhou
            // da Valandro, a Valandro da Argentus e a Argentus da Target.it), então além
            // de mais caro ele não resolveria justamente o empate que apareceu.
            //
            // O Id no fim não é critério esportivo — é a garantia de que a régua sempre
            // responde a mesma coisa. Empate que sobrevive a games pró é sorteio de
            // qualquer jeito; melhor um sorteio ESTÁVEL do que um que muda de ideia entre
            // duas telas.
            .OrderByDescending(x => x.Vitorias)
            .ThenByDescending(x => x.Saldo)
            .ThenByDescending(x => x.GamesPro)
            .ThenBy(x => x.Dupla.Id)
            .ToList();

    // ── O CORTE FOI ESPORTIVO, OU FOI SORTEIO? ───────────────────────────────────────────
    //
    // O `Ordenar` acima sempre responde a MESMA coisa — é o que o `ThenBy(Id)` no fim garante.
    // Mas quando o empate sobrevive aos três critérios esportivos (vitórias, saldo, games a
    // favor), quem passa sai da ORDEM DE CADASTRO, e isso não é mérito de quadra.
    //
    // 🗣️ Felipe achou o caso olhando o painel no ar (11/09/2026): *"e esse caso aqui se for os
    // 3 jogos 9x4 e houver empate?"*. Num grupo de 3 com os três jogos 9x4, as três terminam
    // com 1 vitória, saldo 0 e 13 games a favor — empate perfeito.
    //
    // ⚠️ ISTO NÃO MUDA A RÉGUA, e é de propósito: o `ThenBy(Id)` fica, porque melhor um sorteio
    // ESTÁVEL do que um que muda de ideia entre duas telas (a razão está escrita acima). O que
    // esta função existe pra permitir é o painel DIZER que foi sorteio, em vez de apresentar
    // cara-ou-coroa como eliminação esportiva.
    //
    // Devolve vazio quando o corte foi decidido na quadra.
    public static List<Linha> EmpateNoCorte(IReadOnlyList<Linha> ranking, int passam)
    {
        var vazio = new List<Linha>();
        if (passam <= 0 || passam >= ranking.Count) return vazio;

        var ultimaQuePassa = ranking[passam - 1];
        bool EmpataComElá(Linha l) =>
            l.Vitorias == ultimaQuePassa.Vitorias
            && l.Saldo == ultimaQuePassa.Saldo
            && l.GamesPro == ultimaQuePassa.GamesPro;

        // O corte só é sorteio se quem ficou de FORA empata com quem passou em tudo.
        return EmpataComElá(ranking[passam]) ? ranking.Where(EmpataComElá).ToList() : vazio;
    }

    public static List<ChaveamentoMataMata.Classificado> Calcular(
        IEnumerable<Dupla> duplasComGrupo,
        IReadOnlyList<Partida> partidasDeGrupo,
        int classificamPorGrupo = VagasPadrao)
    {
        var classificados = new List<ChaveamentoMataMata.Classificado>();
        int passam = Math.Max(1, classificamPorGrupo);

        foreach (var grupo in duplasComGrupo
                     .Where(d => d.Grupo != null)
                     .GroupBy(d => d.Grupo!)
                     .OrderBy(g => g.Key))
        {
            var ranking = Ordenar(grupo, partidasDeGrupo);

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
