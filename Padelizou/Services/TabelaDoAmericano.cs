using Padelizou.Models;

namespace Padelizou.Services;

// A tabela do Americano: no fim, vence quem somou mais GAMES — não quem ganhou mais jogos.
// O parceiro muda a cada rodada, então a conta é por jogador, e cada um leva os games que a
// SUA dupla fez na partida.
public static class TabelaDoAmericano
{
    public record Linha(Jogador Jogador, int TotalGames, int Jogos, bool Empatado);

    public static List<Linha> Montar(IEnumerable<Partida> partidasFinalizadas)
    {
        var soma = new Dictionary<int, (Jogador Jogador, int Games, int Jogos)>();

        void Somar(Jogador? jogador, int games)
        {
            // Dupla sem parceiro não deveria existir no Americano, mas um dado torto não
            // pode derrubar a tabela inteira no meio do torneio.
            if (jogador == null) return;

            var atual = soma.TryGetValue(jogador.Id, out var registrado)
                ? registrado
                : (Jogador: jogador, Games: 0, Jogos: 0);

            soma[jogador.Id] = (atual.Jogador, atual.Games + games, atual.Jogos + 1);
        }

        foreach (var p in partidasFinalizadas)
        {
            Somar(p.Dupla1?.Jogador1, p.GamesDupla1 ?? 0);
            Somar(p.Dupla1?.Jogador2, p.GamesDupla1 ?? 0);
            Somar(p.Dupla2?.Jogador1, p.GamesDupla2 ?? 0);
            Somar(p.Dupla2?.Jogador2, p.GamesDupla2 ?? 0);
        }

        var ordenada = soma.Values.OrderByDescending(v => v.Games).ToList();

        // Quem está empatado na LIDERANÇA fica marcado: é dali que sai a decisão de jogar
        // uma final de desempate.
        int melhor = ordenada.Count > 0 ? ordenada[0].Games : 0;
        int quantosNoTopo = ordenada.Count(v => v.Games == melhor);

        return ordenada
            .Select(v => new Linha(v.Jogador, v.Games, v.Jogos, quantosNoTopo > 1 && v.Games == melhor))
            .ToList();
    }

    // Quem está empatado na primeira colocação. Vazio quando há um líder isolado.
    public static List<Jogador> EmpatadosNaLideranca(IEnumerable<Linha> classificacao)
    {
        var lista = classificacao.ToList();
        if (lista.Count < 2) return new List<Jogador>();

        var empatados = lista.Where(l => l.TotalGames == lista[0].TotalGames).ToList();
        return empatados.Count > 1 ? empatados.Select(l => l.Jogador).ToList() : new List<Jogador>();
    }

    public const string FaseDesempate = "Desempate";

    // Por que o desempate ainda não pode ser criado — null quando pode.
    //
    // Só nasce com o torneio inteiro jogado: criar antes congelaria uma liderança que ainda
    // vai mudar. E só resolve empate de DOIS: com três ou mais, uma partida só não coroa
    // ninguém, e inventar um chaveiro aqui seria decidir por regra que o organizador não deu.
    public static string? ProblemaParaDesempatar(
        bool torneioPermite, int rodadasPendentes, int quantosEmpatados)
    {
        if (!torneioPermite)
            return "Este torneio não previu partida de desempate. O critério é do organizador.";

        if (rodadasPendentes > 0)
            return $"Ainda faltam {rodadasPendentes} jogo(s) pra terminar. A liderança pode mudar.";

        if (quantosEmpatados < 2)
            return "Não há empate na liderança.";

        if (quantosEmpatados > 2)
            return $"São {quantosEmpatados} empatados. Uma partida só não decide entre mais de dois — " +
                   "o critério fica com o organizador.";

        return null;
    }
}
