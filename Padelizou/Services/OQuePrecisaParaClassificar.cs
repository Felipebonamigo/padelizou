using Padelizou.Models;

namespace Padelizou.Services;

// "O que cada um precisa pra classificar" — quando falta UM jogo no grupo (pedido do Felipe,
// 13/08/2026: "quando já aconteceu 2 dos 3 jogos").
//
// ⚠️ ESTA CONTA NÃO PODE TER RÉGUA PRÓPRIA. É a armadilha inteira desta feature: escrever aqui
// "quem tem mais vitória passa" cria a segunda resposta pra pergunta que o
// `ClassificacaoDeGrupos` já responde — e no dia em que as duas divergirem, o painel vai
// prometer uma vaga que o chaveamento não vai dar. Então a simulação não julga nada: ela
// PREENCHE o placar que falta e pergunta à régua oficial quem classificou.
//
// O jeito de garantir isso é a assinatura: o serviço recebe as partidas e devolve conjuntos de
// ids vindos de `ClassificacaoDeGrupos.Calcular`. Não há aqui nenhum `>` comparando vitórias.
public static class OQuePrecisaParaClassificar
{
    // Um bloco de resultados que dá no MESMO conjunto de classificados.
    public record Cenario(string Resultado, IReadOnlyList<int> ClassificadosIds);

    // Em que pé cada dupla está a UM jogo do fim.
    public enum Estado { JaClassificado, SemChance, Depende }

    // A resposta direta da pergunta do Felipe (11/09/2026): *"fica a duvida de quantos games
    // precisa fazer para passar de fase"*. A tabela de cenários responde isso de lado — a
    // pessoa procura o próprio nome nas linhas e deduz; esta frase responde de frente.
    //
    // ⚠️ Ela NÃO é uma segunda conta: sai dos mesmos blocos de cenário que a régua oficial já
    // decidiu. Escrever aqui um "se tem mais vitória então passa" seria exatamente a régua
    // paralela que este arquivo inteiro existe pra impedir.
    //
    // ⚠️ `NomeCurto` é o `Dupla.NomeCurto` — "Marcelo / Enio", o MESMO da lista de jogos do
    // grupo. Havia três formas do mesmo nome na mesma tela (nome completo dos dois na lista,
    // `Jogador1.ComoChamar` nas frases, nome curto na tabela), e foi metade da queixa do Felipe:
    // *"ta meio confuso aqui, nao ficou claro para mim"*.
    public record Situacao(Dupla Dupla, Estado Estado, string Frase, string NomeCurto);

    public record Quadro(
        Partida JogoQueFalta,
        Dupla Lado1,
        Dupla Lado2,
        IReadOnlyList<Cenario> Cenarios,
        IReadOnlyList<Dupla> JaClassificados,
        IReadOnlyList<Dupla> SemChance,
        IReadOnlyList<Situacao> Situacoes,
        IReadOnlyList<EmpateNoCorte> EmpatesNoCorte);

    // O corte do grupo terminou empatado em TUDO o que é esportivo — vitórias, saldo e games a
    // favor. Quem passa então sai do `ThenBy(Id)` do ClassificacaoDeGrupos: ordem de cadastro.
    //
    // ⚠️ O painel PRECISA dizer isso. 🗣️ Felipe achou o caso olhando a tela no ar: *"e esse caso
    // aqui se for os 3 jogos 9x4 e houver empate?"* — e ele está certo: num grupo de 3 com os
    // três jogos 9x4, as três ficam com 1 vitória, saldo 0 e 13 games a favor. Apresentar isso
    // como "passa vencendo por 6 ou mais" faz cara-ou-coroa passar por eliminação esportiva.
    public record EmpateNoCorte(
        string Placar, IReadOnlyList<Dupla> Empatadas, int Vitorias, int Saldo, int GamesPro);

    // Um trecho contíguo de placares que dá no mesmo conjunto de classificados, com a frase
    // pronta em três vozes: a da tabela, em MARGEM ("Paulo vencer por até 4 games"), e as duas
    // em PLACAR, que é o que a linha de cada dupla mostra.
    //
    // ⚠️ A MARGEM SÓ SOBREVIVE NO `Cenarios`, que nenhuma tela desenha mais. 🗣️ Felipe, vendo
    // "por 5 games ou mais" no ar: *"seria 4 e 5 games de diferença? nao sei, ficou confuso,
    // talvez se colocar o placar fica mais facil"*. Quem lê não deve ter que converter margem
    // em placar de cabeça na beira da quadra.
    private record Bloco(
        int Vencedor,
        string Frase,
        string ComPlacar,
        string EmPrimeiraPessoa,
        HashSet<int> Classificados);

    // Devolve null quando não é hora de mostrar: 0 jogos restantes (grupo acabou), 2 ou mais
    // (ainda não dá pra falar em "o que precisa"), ou o jogo que falta sem as duas duplas.
    public static Quadro? Montar(
        IReadOnlyList<Dupla> duplasDoGrupo,
        IReadOnlyList<Partida> partidasDoGrupo,
        int classificamPorGrupo,
        FormatoDaPartida.Formato formato,
        IReadOnlyDictionary<int, int> pontosPorJogador)
    {
        // "Jogado" é ter vencedor pela régua única — não é o Status. Um jogo marcado como
        // finalizado 0x0 não decidiu nada, e contá-lo como jogado faria o painel simular o
        // grupo errado.
        var jogados = partidasDoGrupo.Where(p => QuemVenceu.Da(p) != null).ToList();
        var faltando = partidasDoGrupo.Where(p => QuemVenceu.Da(p) == null).ToList();

        if (faltando.Count != 1) return null;

        var jogo = faltando[0];
        var lado1 = duplasDoGrupo.FirstOrDefault(d => d.Id == jogo.Dupla1Id);
        var lado2 = duplasDoGrupo.FirstOrDefault(d => d.Id == jogo.Dupla2Id);
        if (lado1 == null || lado2 == null) return null;

        var possiveis = ResultadosPossiveis(formato)
            .OrderBy(r => r.g1 - r.g2)
            .ToList();
        if (possiveis.Count == 0) return null;

        // Para cada placar possível, quem a RÉGUA OFICIAL classifica — e se o corte foi
        // esportivo ou caiu no desempate por ordem de cadastro.
        int passam = Math.Max(1, classificamPorGrupo);
        var simulados = new List<(int g1, int g2, HashSet<int> Classificados)>();
        var empates = new List<EmpateNoCorte>();

        foreach (var r in possiveis)
        {
            var ranking = RankingCom(duplasDoGrupo, jogados, jogo, r.g1, r.g2, pontosPorJogador);
            simulados.Add((r.g1, r.g2, ranking.Take(passam).Select(l => l.Dupla.Id).ToHashSet()));

            if (ClassificacaoDeGrupos.EmpateNoCorte(ranking, passam,
                    ComOJogoSimulado(jogados, jogo, r.g1, r.g2), pontosPorJogador)
                is { Count: > 0 } empatadas)
                empates.Add(new EmpateNoCorte(
                    PlacarDoJogo(lado1, lado2, r.g1, r.g2),
                    empatadas.Select(l => l.Dupla).ToList(),
                    empatadas[0].Vitorias, empatadas[0].Saldo, empatadas[0].GamesPro));
        }

        // NA ORDEM DA TABELA, e pela régua oficial: o pop-up abre em cima do card do grupo, e
        // duas ordens pra mesma lista fariam a pessoa procurar o próprio nome duas vezes.
        var todos = ClassificacaoDeGrupos.Ordenar(duplasDoGrupo, jogados, pontosPorJogador)
            .Select(l => l.Dupla).ToList();
        var jaClassificados = todos.Where(d => simulados.All(s => s.Classificados.Contains(d.Id))).ToList();
        var semChance = todos.Where(d => simulados.All(s => !s.Classificados.Contains(d.Id))).ToList();

        var blocos = Blocos(simulados, lado1, lado2);

        return new Quadro(jogo, lado1, lado2,
            Cenarios(blocos, simulados), jaClassificados, semChance,
            Situacoes(todos, jaClassificados, semChance, blocos, lado1, lado2, jogo, simulados),
            empates);
    }

    // A linha de cada dupla. Os três estados são excludentes por construção: quem classifica em
    // TODOS os cenários já está dentro, quem não classifica em NENHUM está fora, e só o resto
    // tem placar a fazer.
    private static List<Situacao> Situacoes(
        List<Dupla> todos, List<Dupla> jaClassificados, List<Dupla> semChance,
        List<Bloco> blocos, Dupla lado1, Dupla lado2, Partida jogo,
        List<(int g1, int g2, HashSet<int> Classificados)> simulados)
    {
        var situacoes = new List<Situacao>();

        foreach (var dupla in todos)
        {
            if (jaClassificados.Any(d => d.Id == dupla.Id))
            {
                situacoes.Add(new Situacao(dupla, Estado.JaClassificado,
                    "Já classificado. Não depende deste jogo.", dupla.NomeCurto));
                continue;
            }

            if (semChance.Any(d => d.Id == dupla.Id))
            {
                situacoes.Add(new Situacao(dupla, Estado.SemChance,
                    "Não passa — nenhum resultado deste jogo muda isso.", dupla.NomeCurto));
                continue;
            }

            // QUEM VAI JOGAR ganha a frase curta, com o placar de corte e o placar que já não
            // serve. É a resposta que o Felipe pediu, e ela só existe pra quem está em quadra.
            if (FraseDeQuemJoga(dupla.Id, jogo, simulados) is { } curta)
            {
                situacoes.Add(new Situacao(dupla, Estado.Depende, curta, dupla.NomeCurto));
                continue;
            }

            // O resto (quem NÃO joga este jogo, e o caso raro em que classificar não melhora
            // com o placar) cai na descrição por blocos: "Passa se Fulano vencer por 9x4 ou
            // mais folgado". O próprio jogo primeiro — quem lê quer saber o que ELA pode fazer
            // antes de saber o que precisa torcer.
            var partes = blocos
                .Where(b => b.Classificados.Contains(dupla.Id))
                .OrderBy(b => LadoDa(b.Vencedor, lado1, lado2) == dupla.Id ? 0 : 1)
                .Select(b => LadoDa(b.Vencedor, lado1, lado2) == dupla.Id
                    ? b.EmPrimeiraPessoa
                    : $"se {b.ComPlacar}")
                .ToList();

            situacoes.Add(new Situacao(dupla, Estado.Depende,
                $"Passa {string.Join(" ou ", partes)}.", dupla.NomeCurto));
        }

        return situacoes;
    }

    // A frase de quem VAI JOGAR, na voz dela e em PLACAR: o pior resultado que ainda serve, e o
    // primeiro que já não serve. O contra-exemplo é metade da frase — é ele que mata a dúvida de
    // UM game (🗣️ *"seria 4 e 5 games de diferença?"*).
    //
    // Devolve null quando a frase curta não pode ser dita: a dupla não joga este jogo, ou o
    // conjunto que classifica NÃO é um prefixo do melhor pro pior resultado dela.
    //
    // ⚠️ A TRAVA DO PREFIXO NÃO É ZELO EXCESSIVO. "9x4 ou mais folgado" afirma que TODO placar
    // melhor que 9x4 também classifica. Em toda mesa real isso vale (saldo cresce com a margem),
    // mas o desempate por games a favor pode furar — e aí a frase prometeria uma vaga que a
    // régua não dá, que é o defeito exato que este arquivo existe pra impedir. Furou, cai na
    // descrição por blocos, que é mais longa e sempre verdadeira.
    private static string? FraseDeQuemJoga(
        int duplaId, Partida jogo, List<(int g1, int g2, HashSet<int> Classificados)> simulados)
    {
        bool souLado1 = jogo.Dupla1Id == duplaId;
        if (!souLado1 && jogo.Dupla2Id != duplaId) return null;

        // Do MEU melhor resultado pro pior.
        var meus = simulados
            .Select(s => (Meus: souLado1 ? s.g1 : s.g2,
                          Dele: souLado1 ? s.g2 : s.g1,
                          Passo: s.Classificados.Contains(duplaId)))
            .OrderByDescending(x => x.Meus - x.Dele)
            .ToList();

        int quantos = meus.TakeWhile(x => x.Passo).Count();
        if (quantos == 0 || quantos == meus.Count) return null;       // sem chance / já dentro
        if (meus.Skip(quantos).Any(x => x.Passo)) return null;        // não é prefixo

        var limite = meus[quantos - 1];
        var fora = meus[quantos];
        string P((int Meus, int Dele, bool Passo) x) => $"{x.Meus}x{x.Dele}";

        // Ainda serve perdendo: toda vitória passa, e a derrota tem um teto.
        if (limite.Meus < limite.Dele)
            return "Vencendo, passa com qualquer placar. "
                 + $"Perdendo, o pior placar que ainda serve é {P(limite)} — {P(fora)} já elimina.";

        // O limite é a vitória mais apertada que existe: vencer basta, perder não.
        if (fora.Meus < fora.Dele)
            return "Passa vencendo, com qualquer placar. Qualquer derrota elimina.";

        return $"Só passa vencendo por {P(limite)} ou mais folgado — {P(fora)} não basta.";
    }

    private static int LadoDa(int vencedor, Dupla lado1, Dupla lado2) =>
        vencedor == 1 ? lado1.Id : lado2.Id;

    private static List<Cenario> Cenarios(
        List<Bloco> blocos, List<(int g1, int g2, HashSet<int> Classificados)> simulados)
    {
        // O caso mais importante de todos, e o mais fácil de perder no meio de uma tabela: o
        // jogo não muda nada. Quem lê isso guarda a raquete sem ansiedade.
        var primeiro = simulados[0].Classificados;
        if (simulados.All(s => s.Classificados.SetEquals(primeiro)))
            return new List<Cenario> { new("Qualquer resultado", primeiro.ToList()) };

        return blocos.Select(b => new Cenario(b.Frase, b.Classificados.ToList())).ToList();
    }

    // ⚠️ `Ordenar`, e não `Calcular`: é a MESMA régua (o `Calcular` é este ranking cortado nos N
    // primeiros), e é o ranking inteiro que permite perguntar se o corte foi esportivo. Continua
    // valendo a regra do arquivo: aqui não há nenhum `>` comparando vitórias.
    // As partidas do grupo COM o placar simulado — o `EmpateNoCorte` precisa delas pra saber
    // se o confronto direto resolve, e o jogo que falta é justamente um dos confrontos.
    private static List<Partida> ComOJogoSimulado(List<Partida> jogados, Partida jogo, int g1, int g2) =>
        // ⚠️ CÓPIA da partida, nunca a instância que veio do banco: mexer nela deixaria o
        // objeto rastreado pelo EF com um placar inventado, e bastaria um SaveChanges de
        // qualquer outro ponto da requisição pra gravar um resultado que ninguém jogou.
        new List<Partida>(jogados)
        {
            new Partida
            {
                Id = jogo.Id,
                Dupla1Id = jogo.Dupla1Id,
                Dupla2Id = jogo.Dupla2Id,
                GamesDupla1 = g1,
                GamesDupla2 = g2,
                Fase = jogo.Fase,
            }
        };

    private static List<ClassificacaoDeGrupos.Linha> RankingCom(
        IReadOnlyList<Dupla> duplas, List<Partida> jogados, Partida jogo, int g1, int g2,
        IReadOnlyDictionary<int, int> pontosPorJogador)
    {
        // ⚠️ CÓPIA da partida, nunca a instância que veio do banco: mexer nela deixaria o
        // objeto rastreado pelo EF com um placar inventado, e bastaria um SaveChanges de
        // qualquer outro ponto da requisição pra gravar um resultado que ninguém jogou.
        return ClassificacaoDeGrupos.Ordenar(
            duplas, ComOJogoSimulado(jogados, jogo, g1, g2), pontosPorJogador);
    }

    // "Cadu / Dedé vence por 9x4" — o placar sempre na orientação do VENCEDOR.
    private static string PlacarDoJogo(Dupla lado1, Dupla lado2, int g1, int g2) =>
        g1 > g2
            ? $"{lado1.NomeCurto} vence por {g1}x{g2}"
            : $"{lado2.NomeCurto} vence por {g2}x{g1}";

    // Junta placares vizinhos que dão no mesmo conjunto de classificados, pra tela não virar
    // uma lista de 20 linhas dizendo a mesma coisa.
    private static List<Bloco> Blocos(
        List<(int g1, int g2, HashSet<int> Classificados)> simulados, Dupla lado1, Dupla lado2)
    {
        var blocos = new List<Bloco>();

        // Os blocos são formados DENTRO de cada vencedor. Um bloco que atravessasse o 0 daria
        // uma frase impossível de ler ("da vitória da B por 2 até a da A por 3").
        foreach (var vencedor in new[] { 2, 1 })
        {
            var doLado = simulados
                .Where(s => (vencedor == 1 && s.g1 > s.g2) || (vencedor == 2 && s.g2 > s.g1))
                .Select(s => (Margem: Math.Abs(s.g1 - s.g2),
                              Placar: $"{Math.Max(s.g1, s.g2)}x{Math.Min(s.g1, s.g2)}",
                              s.Classificados))
                .OrderBy(s => s.Margem)
                .ToList();
            if (doLado.Count == 0) continue;

            int menorPossivel = doLado.First().Margem;
            int maiorPossivel = doLado.Last().Margem;
            var nome = (vencedor == 1 ? lado1 : lado2).NomeCurto;

            int i = 0;
            while (i < doLado.Count)
            {
                int inicio = i;
                var conjunto = doLado[i].Classificados;
                while (i < doLado.Count && doLado[i].Classificados.SetEquals(conjunto)) i++;

                int de = doLado[inicio].Margem, ate = doLado[i - 1].Margem;
                // O APERTADO é o placar da menor margem do bloco; o FOLGADO, o da maior.
                string apertado = doLado[inicio].Placar, folgado = doLado[i - 1].Placar;
                blocos.Add(new Bloco(vencedor,
                    Frase(nome, de, ate, menorPossivel, maiorPossivel),
                    $"{nome} vencer{EmPlacar(de, ate, menorPossivel, maiorPossivel, apertado, folgado)}",
                    $"vencendo{EmPlacar(de, ate, menorPossivel, maiorPossivel, apertado, folgado)}",
                    conjunto));
            }
        }

        return blocos;
    }

    private static string Frase(string nome, int de, int ate, int menorPossivel, int maiorPossivel) =>
        $"{nome} vencer{Margem(de, ate, menorPossivel, maiorPossivel)}";

    // O mesmo trecho em PLACAR, que é o que a linha de cada dupla diz. "por 9x4 ou mais
    // folgado" responde direto; "por 5 games ou mais" manda a pessoa converter de cabeça.
    private static string EmPlacar(
        int de, int ate, int menorPossivel, int maiorPossivel, string apertado, string folgado)
    {
        if (de == menorPossivel && ate == maiorPossivel) return "";
        if (de == ate) return $" por {apertado}";
        if (ate == maiorPossivel) return $" por {apertado} ou mais folgado";
        if (de == menorPossivel) return $" no máximo por {folgado}";
        return $" de {folgado} a {apertado}";
    }

    private static string Margem(int de, int ate, int menorPossivel, int maiorPossivel)
    {
        if (de == menorPossivel && ate == maiorPossivel) return "";
        if (de == ate) return $" por {de} game{(de == 1 ? "" : "s")}";
        if (ate == maiorPossivel) return $" por {de} game{(de == 1 ? "" : "s")} ou mais";
        if (de == menorPossivel) return $" por até {ate} games";
        return $" por {de} a {ate} games";
    }

    // Todos os placares com que este jogo PODE terminar, no formato do torneio.
    //
    // ⚠️ Empate não entra: `QuemVenceu` devolve null e o sistema recusa finalizar — simular um
    // resultado que não pode ser gravado encheria o painel de linha impossível.
    private static IEnumerable<(int g1, int g2)> ResultadosPossiveis(FormatoDaPartida.Formato formato)
    {
        if (formato.EhSoma)
        {
            // Na soma o total é fixo: 5x2 e 4x3 fecham a mesma soma de 7.
            for (int g1 = 0; g1 <= formato.Games; g1++)
            {
                int g2 = formato.Games - g1;
                if (g1 != g2) yield return (g1, g2);
            }
            yield break;
        }

        // O +1 é o desempate do limite par (jogo até 4 que empata em 3x3 vai até 5) — a mesma
        // conta do TetoDeGames, perguntada a ele em vez de repetida aqui.
        int teto = formato.Games + 1;
        for (int g1 = 0; g1 <= teto; g1++)
        {
            for (int g2 = 0; g2 <= teto; g2++)
            {
                if (g1 == g2) continue;
                if (FormatoDaPartida.PlacarValido(formato, g1, g2) != (g1, g2)) continue;
                if (!FormatoDaPartida.PodeEncerrar(formato, g1, g2)) continue;

                // Placar FINAL, não "um que já dava pra encerrar antes": tirando o último game
                // do vencedor, o jogo ainda não podia ter acabado. Sem isto, um jogo até 9
                // listaria 10x3, 11x3… — placares que a quadra nunca produz.
                int antes1 = g1 > g2 ? g1 - 1 : g1;
                int antes2 = g2 > g1 ? g2 - 1 : g2;
                if (FormatoDaPartida.PodeEncerrar(formato, antes1, antes2)) continue;

                yield return (g1, g2);
            }
        }
    }
}
