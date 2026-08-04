namespace Padelizou.Services;

// O mata-mata ANTES de existir: quem vai cruzar com quem, dito por COLOCAÇÃO em vez de por
// nome ("1º do Grupo A x 2º do Grupo C").
//
// Sem isto, a aba de chaves dizia só "o mata-mata ainda não começou — é gerado quando o
// último jogo da fase de grupos for finalizado". Verdade, e inútil: o time que está jogando
// a última rodada do grupo quer saber o que ganha se terminar em primeiro, e o organizador
// quer explicar o caminho antes de a bola rolar.
//
// A projeção passa pelo MESMO motor do sorteio de verdade (ChaveamentoMataMata.
// MontarPrimeiraFase), não por uma conta paralela — duas contas divergiriam no dia em que a
// regra mudasse, e a tela prometeria um cruzamento que o sorteio não faria.
//
// ⚠️ O que ela NÃO consegue prever: a ordem dos "melhores 2ºs" depende da campanha de cada
// dupla, que ainda não existe. Aqui todos entram com campanha zerada, então o desempate cai
// no nome do grupo. A tela precisa dizer que é prévia.
public static class ChaveProjetada
{
    // Uma vaga do quadro: "2º do Grupo B" antes de se saber quem é.
    public record Vaga(int Posicao, string Grupo)
    {
        public string Rotulo => $"{Posicao}º do {Grupo}";
    }

    public record ConfrontoProjetado(Vaga Lado1, Vaga Lado2);

    // Fase vazia = não dá pra projetar (grupo de menos). `Byes` são as vagas que pulam a
    // primeira rodada — os melhores, na mesma regra do sorteio de verdade.
    public static (string Fase, List<ConfrontoProjetado> Confrontos, List<Vaga> Byes) Montar(
        IReadOnlyList<string> grupos, int classificadosPorGrupo = 2)
    {
        if (grupos.Count == 0) return ("", new List<ConfrontoProjetado>(), new List<Vaga>());

        int passam = Math.Max(1, classificadosPorGrupo);

        // Id sintético que carrega posição e grupo, pra reconhecer a vaga na volta. O motor
        // só compara Ids por igualdade, então qualquer número único serve.
        var vagas = new Dictionary<int, Vaga>();
        var classificados = new List<ChaveamentoMataMata.Classificado>();

        for (int g = 0; g < grupos.Count; g++)
        {
            for (int posicao = 1; posicao <= passam; posicao++)
            {
                int id = posicao * 1000 + g;
                vagas[id] = new Vaga(posicao, grupos[g]);

                // Campanha zerada em todo mundo: sem jogo jogado não há o que comparar, e
                // inventar números faria a prévia parecer mais certa do que é.
                classificados.Add(new ChaveamentoMataMata.Classificado(
                    id, grupos[g], Vitorias: 0, Saldo: 0, Posicao: posicao));
            }
        }

        var (fase, confrontos, byes) = ChaveamentoMataMata.MontarPrimeiraFase(classificados, passam);

        return (fase,
            confrontos.Select(c => new ConfrontoProjetado(vagas[c.Dupla1Id], vagas[c.Dupla2Id])).ToList(),
            byes.Select(id => vagas[id]).ToList());
    }

    // ---- O caminho inteiro, da primeira fase à final ----

    public record JogoProjetado(int Numero, string Lado1, string Lado2);
    public record RodadaProjetada(string Fase, List<JogoProjetado> Jogos);

    // A primeira rodada sai por colocação ("1º do Grupo A x 2º do Grupo D"); da segunda em
    // diante, por procedência ("Vencedor do jogo 1 x Vencedor do jogo 4"). O jogador quer
    // ver o CAMINHO — quem encontra na semi se passar, e de que lado da chave está.
    //
    // O encadeamento repete o do robô de verdade: os que avançam são os VENCEDORES na ordem
    // dos jogos, seguidos dos BYES (os melhores, que pularam a rodada), pareados primeiro x
    // último — o mesmo AvancoDaChave + ParearVencedores. Duas contas divergiriam no dia em
    // que a regra mudasse, e o mapa prometeria um cruzamento que a chave não faria.
    public static List<RodadaProjetada> MontarCompleta(
        IReadOnlyList<string> grupos, int classificadosPorGrupo = 2)
    {
        var (fase, primeiraRodada, byes) = Montar(grupos, classificadosPorGrupo);
        if (primeiraRodada.Count == 0) return new List<RodadaProjetada>();

        var rodadas = new List<RodadaProjetada>();
        int proximoNumero = 1;

        var jogos = new List<JogoProjetado>();
        foreach (var confronto in primeiraRodada)
            jogos.Add(new JogoProjetado(proximoNumero++, confronto.Lado1.Rotulo, confronto.Lado2.Rotulo));
        rodadas.Add(new RodadaProjetada(fase, jogos));

        // Quem entra na próxima rodada: os vencedores e, uma única vez, os byes.
        var entrantes = jogos.Select(j => $"Vencedor do jogo {j.Numero}").ToList();
        entrantes.AddRange(byes.Select(b => $"{b.Rotulo} (passou direto)"));

        while (entrantes.Count > 1)
        {
            var jogosDaRodada = new List<JogoProjetado>();
            var proximos = new List<string>();

            // O pareamento do robô: primeiro x último da lista de quem avança.
            for (int i = 0; i < entrantes.Count / 2; i++)
            {
                var jogo = new JogoProjetado(proximoNumero++, entrantes[i], entrantes[entrantes.Count - 1 - i]);
                jogosDaRodada.Add(jogo);
                proximos.Add($"Vencedor do jogo {jogo.Numero}");
            }

            rodadas.Add(new RodadaProjetada(ChaveamentoMataMata.NomeFase(entrantes.Count), jogosDaRodada));
            entrantes = proximos;
        }

        return rodadas;
    }
}
