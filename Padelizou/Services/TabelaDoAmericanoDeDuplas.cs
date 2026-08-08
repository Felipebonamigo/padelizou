using Padelizou.Models;

namespace Padelizou.Services;

// A tabela do AMERICANO DE DUPLAS: vence a DUPLA que somou mais games. A dupla aqui é fixa
// (a mesma da inscrição), então a conta é por dupla — diferente da TabelaDoAmericano, onde o
// parceiro muda a cada rodada e cada pessoa leva os games da sua dupla de ocasião.
//
// São duas tabelas de propósito, não uma régua duplicada: as somas são de UNIDADES diferentes
// (pessoa × dupla). O que é compartilhado de verdade — quando um empate PODE virar partida —
// continua morando num lugar só: TabelaDoAmericano.ProblemaParaDesempatar.
public static class TabelaDoAmericanoDeDuplas
{
    public record Linha(Dupla Dupla, int TotalGames, int Jogos, bool Empatada);

    // `participantes`: as duplas que TÊM QUE APARECER mesmo sem ter jogado — a mesma regra da
    // TabelaDoAmericano individual (Felipe, 08/08/2026). Quem foi sorteado já está no torneio;
    // esperar o primeiro placar pra existir na tabela deixava a aba Classificação vazia
    // justamente no começo. Quem ainda não jogou fica no fim, zerada e nunca marcada como
    // empatada: não há empate a desfazer entre quem não entrou em quadra.
    //
    // Quem chama sem `participantes` (o robô que fecha o torneio, o Ranking) continua vendo só
    // quem pontuou — lá a pergunta é "quem venceu", e uma dupla zerada não disputa nada.
    public static List<Linha> Montar(IEnumerable<Partida> partidasFinalizadas,
        IEnumerable<Dupla>? participantes = null)
    {
        var soma = new Dictionary<int, (Dupla Dupla, int Games, int Jogos)>();

        void Somar(Dupla? dupla, int games)
        {
            // Partida sem a navegação carregada não pode derrubar a tabela no meio do torneio.
            if (dupla == null) return;

            var atual = soma.TryGetValue(dupla.Id, out var registrada)
                ? registrada
                : (Dupla: dupla, Games: 0, Jogos: 0);

            soma[dupla.Id] = (atual.Dupla, atual.Games + games, atual.Jogos + 1);
        }

        foreach (var p in partidasFinalizadas)
        {
            Somar(p.Dupla1, p.GamesDupla1 ?? 0);
            Somar(p.Dupla2, p.GamesDupla2 ?? 0);
        }

        // ⚠️ A ordem tem que ser TOTAL — a mesma lição da TabelaDoAmericano: só ordenar por
        // games deixa quem empata na ordem em que as partidas voltaram da consulta, e cada
        // chamador monta a consulta do seu jeito. O Id é sorteio, mas é um sorteio ESTÁVEL.
        var ordenada = soma.Values
            .OrderByDescending(v => v.Games)
            .ThenBy(v => v.Dupla.Id)
            .ToList();

        int melhor = ordenada.Count > 0 ? ordenada[0].Games : 0;
        int quantasNoTopo = ordenada.Count(v => v.Games == melhor);

        var linhas = ordenada
            .Select(v => new Linha(v.Dupla, v.Games, v.Jogos, quantasNoTopo > 1 && v.Games == melhor))
            .ToList();

        // E quem ainda não jogou entra zerada, no fim. Ordem alfabética e, no empate de nome,
        // pelo Id: ordem TOTAL, senão a mesma tabela sai diferente a cada carregamento.
        if (participantes != null)
        {
            var jaEstao = linhas.Select(l => l.Dupla.Id).ToHashSet();

            linhas.AddRange(participantes
                .GroupBy(d => d.Id)
                .Select(g => g.First())
                .Where(d => !jaEstao.Contains(d.Id))
                .OrderBy(d => d.NomeDeExibicao, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(d => d.Id)
                .Select(d => new Linha(d, 0, 0, false)));
        }

        return linhas;
    }

    // As duplas empatadas na primeira colocação. Vazio quando há uma líder isolada.
    public static List<Dupla> EmpatadasNaLideranca(IEnumerable<Linha> classificacao)
    {
        var lista = classificacao.ToList();
        if (lista.Count < 2) return new List<Dupla>();

        var empatadas = lista.Where(l => l.TotalGames == lista[0].TotalGames).ToList();
        return empatadas.Count > 1 ? empatadas.Select(l => l.Dupla).ToList() : new List<Dupla>();
    }
}
