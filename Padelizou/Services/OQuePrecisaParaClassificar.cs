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

    public record Quadro(
        Partida JogoQueFalta,
        Dupla Lado1,
        Dupla Lado2,
        IReadOnlyList<Cenario> Cenarios,
        IReadOnlyList<Dupla> JaClassificados,
        IReadOnlyList<Dupla> SemChance);

    // Devolve null quando não é hora de mostrar: 0 jogos restantes (grupo acabou), 2 ou mais
    // (ainda não dá pra falar em "o que precisa"), ou o jogo que falta sem as duas duplas.
    public static Quadro? Montar(
        IReadOnlyList<Dupla> duplasDoGrupo,
        IReadOnlyList<Partida> partidasDoGrupo,
        int classificamPorGrupo,
        FormatoDaPartida.Formato formato)
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

        // Para cada placar possível, quem a RÉGUA OFICIAL classifica.
        var simulados = possiveis
            .Select(r => (r.g1, r.g2, Classificados: ClassificadosCom(duplasDoGrupo, jogados, jogo, r.g1, r.g2, classificamPorGrupo)))
            .ToList();

        var todos = duplasDoGrupo.ToList();
        var jaClassificados = todos.Where(d => simulados.All(s => s.Classificados.Contains(d.Id))).ToList();
        var semChance = todos.Where(d => simulados.All(s => !s.Classificados.Contains(d.Id))).ToList();

        return new Quadro(jogo, lado1, lado2,
            Blocos(simulados, lado1, lado2), jaClassificados, semChance);
    }

    private static HashSet<int> ClassificadosCom(
        IReadOnlyList<Dupla> duplas, List<Partida> jogados, Partida jogo, int g1, int g2, int passam)
    {
        // ⚠️ CÓPIA da partida, nunca a instância que veio do banco: mexer nela deixaria o
        // objeto rastreado pelo EF com um placar inventado, e bastaria um SaveChanges de
        // qualquer outro ponto da requisição pra gravar um resultado que ninguém jogou.
        var simulada = new Partida
        {
            Id = jogo.Id,
            Dupla1Id = jogo.Dupla1Id,
            Dupla2Id = jogo.Dupla2Id,
            GamesDupla1 = g1,
            GamesDupla2 = g2,
            Fase = jogo.Fase,
        };

        var comEsteResultado = new List<Partida>(jogados) { simulada };

        return ClassificacaoDeGrupos.Calcular(duplas, comEsteResultado, passam)
            .Select(c => c.DuplaId)
            .ToHashSet();
    }

    // Junta placares vizinhos que dão no mesmo conjunto de classificados, pra tela não virar
    // uma lista de 20 linhas dizendo a mesma coisa.
    private static List<Cenario> Blocos(
        List<(int g1, int g2, HashSet<int> Classificados)> simulados, Dupla lado1, Dupla lado2)
    {
        // O caso mais importante de todos, e o mais fácil de perder no meio de uma tabela:
        // o jogo não muda nada. Quem lê isso guarda a raquete sem ansiedade.
        var primeiro = simulados[0].Classificados;
        if (simulados.All(s => s.Classificados.SetEquals(primeiro)))
        {
            return new List<Cenario> { new("Qualquer resultado", primeiro.ToList()) };
        }

        var cenarios = new List<Cenario>();

        // Os blocos são formados DENTRO de cada vencedor. Um bloco que atravessasse o 0 daria
        // uma frase impossível de ler ("da vitória da B por 2 até a da A por 3").
        foreach (var vencedor in new[] { 2, 1 })
        {
            var doLado = simulados
                .Where(s => (vencedor == 1 && s.g1 > s.g2) || (vencedor == 2 && s.g2 > s.g1))
                .Select(s => (Margem: Math.Abs(s.g1 - s.g2), s.Classificados))
                .OrderBy(s => s.Margem)
                .ToList();
            if (doLado.Count == 0) continue;

            int menorPossivel = doLado.First().Margem;
            int maiorPossivel = doLado.Last().Margem;
            var nome = NomeCurto(vencedor == 1 ? lado1 : lado2);

            int i = 0;
            while (i < doLado.Count)
            {
                int inicio = i;
                var conjunto = doLado[i].Classificados;
                while (i < doLado.Count && doLado[i].Classificados.SetEquals(conjunto)) i++;

                cenarios.Add(new Cenario(
                    Frase(nome, doLado[inicio].Margem, doLado[i - 1].Margem, menorPossivel, maiorPossivel),
                    conjunto.ToList()));
            }
        }

        return cenarios;
    }

    private static string Frase(string nome, int de, int ate, int menorPossivel, int maiorPossivel)
    {
        if (de == menorPossivel && ate == maiorPossivel) return $"{nome} vencer";
        if (de == ate) return $"{nome} vencer por {de} game{(de == 1 ? "" : "s")}";
        if (ate == maiorPossivel) return $"{nome} vencer por {de} game{(de == 1 ? "" : "s")} ou mais";
        if (de == menorPossivel) return $"{nome} vencer por até {ate} games";
        return $"{nome} vencer por {de} a {ate} games";
    }

    private static string NomeCurto(Dupla dupla) =>
        dupla.EhTime && !string.IsNullOrWhiteSpace(dupla.NomeTime)
            ? dupla.NomeTime!
            : dupla.Jogador1?.ComoChamar ?? $"Dupla {dupla.Id}";

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
