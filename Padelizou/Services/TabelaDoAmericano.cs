using Padelizou.Models;

namespace Padelizou.Services;

// A tabela do Americano: no fim, vence quem somou mais GAMES — não quem ganhou mais jogos.
// O parceiro muda a cada rodada, então a conta é por jogador, e cada um leva os games que a
// SUA dupla fez na partida.
public static class TabelaDoAmericano
{
    // `Desempate` conta COMO a posição foi decidida quando houve empate em games — a tela
    // precisa explicar isso, senão duas pessoas com o mesmo número aparecem uma acima da
    // outra sem motivo visível, e quem ficou embaixo acha que o sistema errou.
    // Null = não houve empate em games nesta posição.
    public record Linha(Jogador Jogador, int TotalGames, int Jogos, bool Empatado, string? Desempate = null);

    // Os dois critérios que separam quem empatou em games, na ordem em que são aplicados.
    public const string PorConfrontoDireto = "ConfrontoDireto";
    public const string PorSorteio = "Sorteio";

    // As partidas que DECIDEM o torneio: o grupo final quando ele existe, senão a fase de
    // grupos. Somar o torneio inteiro juntaria gente de grupos diferentes, que nunca se
    // enfrentou — e a liderança sairia de uma soma que não aconteceu.
    //
    // Mora aqui, e não no controller, porque agora tem DOIS leitores: a tela do desempate e o
    // Ranking Americano. Era a cópia certa de fazer antes de existir a segunda.
    public static List<Partida> QueDecidem(IEnumerable<Partida> partidasDoAmericano)
    {
        var todas = partidasDoAmericano.ToList();
        bool temGrupoFinal = todas.Any(p => FaseDoAmericano.EhDoGrupoFinal(p.Fase));

        return todas
            .Where(p => temGrupoFinal
                ? FaseDoAmericano.EhDoGrupoFinal(p.Fase)
                : FaseDoAmericano.EhDaFaseDeGrupos(p.Fase))
            .ToList();
    }

    public static List<Linha> Montar(IEnumerable<Partida> partidasFinalizadas)
    {
        // Materializado uma vez: o confronto direto varre as partidas de novo, e uma consulta
        // do EF percorrida duas vezes bate no banco duas vezes.
        var jogos = partidasFinalizadas.ToList();

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

        foreach (var p in jogos)
        {
            Somar(p.Dupla1?.Jogador1, p.GamesDupla1 ?? 0);
            Somar(p.Dupla1?.Jogador2, p.GamesDupla1 ?? 0);
            Somar(p.Dupla2?.Jogador1, p.GamesDupla2 ?? 0);
            Somar(p.Dupla2?.Jogador2, p.GamesDupla2 ?? 0);
        }

        // ⚠️ A ordem tem que ser TOTAL. Só `OrderByDescending(Games)` deixa quem empata na
        // ordem em que o dicionário foi preenchido — que é a ordem em que as partidas
        // voltaram da consulta, e cada chamador monta a consulta do seu jeito. A mesma tabela
        // respondia colocações diferentes conforme quem perguntasse.
        //
        // QUEM EMPATA EM GAMES é separado por CONFRONTO DIRETO e, se ainda empatar, por
        // sorteio (Felipe, 08/08/2026). Antes o critério era o Id do jogador: estável, mas
        // não esportivo — na prática passava quem tinha se inscrito primeiro, e isso decidia
        // quem ia pro grupo final.
        var lista = soma.Values.ToList();
        var desempatePor = new Dictionary<int, string?>();

        var ordenada = new List<(Jogador Jogador, int Games, int Jogos)>();
        foreach (var empatados in lista.GroupBy(v => v.Games).OrderByDescending(g => g.Key))
        {
            var doGrupo = empatados.ToList();
            if (doGrupo.Count == 1)
            {
                ordenada.Add(doGrupo[0]);
                continue;
            }

            var ids = doGrupo.Select(v => v.Jogador.Id).ToHashSet();
            var confronto = ConfrontoDireto(jogos, ids);

            // Semente do sorteio: os próprios empatados. Assim dois jogadores que empatam em
            // torneios diferentes não saem sempre na mesma ordem (o que faria o "sorteio"
            // favorecer a mesma pessoa pra sempre), e dentro do MESMO torneio o resultado não
            // muda entre dois carregamentos da tela.
            long semente = ids.Aggregate(0L, (acc, id) => acc + id);

            var emOrdem = doGrupo
                .OrderByDescending(v => confronto.GetValueOrDefault(v.Jogador.Id))
                .ThenBy(v => ChaveDoSorteio(v.Jogador.Id, semente))
                .ToList();

            // O rótulo é por PESSOA: num empate de três, o confronto direto pode separar duas
            // e deixar as outras no sorteio. Dizer "sorteio" pra todas mentiria pra quem foi
            // separada na quadra.
            foreach (var v in emOrdem)
            {
                int meu = confronto.GetValueOrDefault(v.Jogador.Id);
                bool alguemMaisTemOMesmo = emOrdem.Count(o => confronto.GetValueOrDefault(o.Jogador.Id) == meu) > 1;
                desempatePor[v.Jogador.Id] = alguemMaisTemOMesmo ? PorSorteio : PorConfrontoDireto;
            }

            ordenada.AddRange(emOrdem);
        }

        // Quem está empatado na LIDERANÇA fica marcado: é dali que sai a decisão de jogar
        // uma final de desempate.
        //
        // ⚠️ Continua sendo empate em GAMES, e NÃO "sobrou empatado depois do confronto
        // direto": o título é decidido numa partida de desempate (regra do Felipe), e deixar
        // o confronto direto coroar alguém trocaria essa regra por baixo, calado.
        int melhor = ordenada.Count > 0 ? ordenada[0].Games : 0;
        int quantosNoTopo = ordenada.Count(v => v.Games == melhor);

        return ordenada
            .Select(v => new Linha(
                v.Jogador, v.Games, v.Jogos,
                quantosNoTopo > 1 && v.Games == melhor,
                desempatePor.GetValueOrDefault(v.Jogador.Id)))
            .ToList();
    }

    // Games que cada empatado fez JOGANDO CONTRA os outros empatados. É o "confronto direto"
    // possível num Americano: o parceiro muda a cada rodada, então não existe "o jogo entre os
    // dois" — existe o que cada um fez quando estavam em lados opostos da quadra.
    //
    // Com três ou mais empatados vira uma mini-tabela entre eles, que é como o confronto
    // direto se resolve em qualquer esporte de grupo.
    private static Dictionary<int, int> ConfrontoDireto(List<Partida> partidas, HashSet<int> empatados)
    {
        var pontos = new Dictionary<int, int>();

        void Creditar(Jogador? jogador, int games)
        {
            if (jogador == null || !empatados.Contains(jogador.Id)) return;
            pontos[jogador.Id] = pontos.GetValueOrDefault(jogador.Id) + games;
        }

        foreach (var p in partidas)
        {
            var lado1 = new[] { p.Dupla1?.Jogador1, p.Dupla1?.Jogador2 };
            var lado2 = new[] { p.Dupla2?.Jogador1, p.Dupla2?.Jogador2 };

            bool adversarioEmpatadoDoLado2 = lado2.Any(j => j != null && empatados.Contains(j.Id));
            bool adversarioEmpatadoDoLado1 = lado1.Any(j => j != null && empatados.Contains(j.Id));

            // Só conta quando o time DE FRENTE tem um dos empatados: games feitos contra
            // terceiros já estão no total geral e não separam ninguém.
            if (adversarioEmpatadoDoLado2)
                foreach (var j in lado1) Creditar(j, p.GamesDupla1 ?? 0);

            if (adversarioEmpatadoDoLado1)
                foreach (var j in lado2) Creditar(j, p.GamesDupla2 ?? 0);
        }

        return pontos;
    }

    // O "sorteio": arbitrário, mas DETERMINÍSTICO. Um Random de verdade faria a mesma tabela
    // mostrar colocações diferentes a cada F5 — e o robô que monta o grupo final enxergaria
    // uma classificação, a tela outra. Mistura simples (xorshift) do Id com a semente.
    private static long ChaveDoSorteio(int jogadorId, long semente)
    {
        unchecked
        {
            long x = jogadorId * 2654435761L + semente * 40503L + 1;
            x ^= x << 13;
            x ^= x >> 7;
            x ^= x << 17;
            return x;
        }
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
