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

    // Fase vazia = não dá pra projetar (grupo de menos).
    public static (string Fase, List<ConfrontoProjetado> Confrontos) Montar(
        IReadOnlyList<string> grupos, int classificadosPorGrupo = 2)
    {
        if (grupos.Count == 0) return ("", new List<ConfrontoProjetado>());

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

        var (fase, confrontos) = ChaveamentoMataMata.MontarPrimeiraFase(classificados, passam);

        return (fase, confrontos
            .Select(c => new ConfrontoProjetado(vagas[c.Dupla1Id], vagas[c.Dupla2Id]))
            .ToList());
    }

    // ---- O caminho inteiro, da primeira fase à final ----

    public record JogoProjetado(int Numero, string Lado1, string Lado2);
    public record RodadaProjetada(string Fase, List<JogoProjetado> Jogos);

    // A primeira rodada sai por colocação ("1º do Grupo A x 2º do Grupo D"); da segunda em
    // diante, por procedência ("Vencedor do jogo 1 x Vencedor do jogo 4"). O jogador quer
    // ver o CAMINHO — quem encontra na semi se passar, e de que lado da chave está.
    //
    // O encadeamento usa o mesmo ParearVencedores do robô, e os jogos são numerados na
    // ordem em que nascem: é essa ordem que o robô lê (OrderBy Id) pra montar a fase
    // seguinte, então o mapa aqui e a chave de verdade contam a mesma história.
    public static List<RodadaProjetada> MontarCompleta(
        IReadOnlyList<string> grupos, int classificadosPorGrupo = 2)
    {
        var (fase, primeiraRodada) = Montar(grupos, classificadosPorGrupo);
        if (primeiraRodada.Count == 0) return new List<RodadaProjetada>();

        var rodadas = new List<RodadaProjetada>();
        int proximoNumero = 1;

        var numeros = new List<int>();
        var jogos = new List<JogoProjetado>();
        foreach (var confronto in primeiraRodada)
        {
            int numero = proximoNumero++;
            numeros.Add(numero);
            jogos.Add(new JogoProjetado(numero, confronto.Lado1.Rotulo, confronto.Lado2.Rotulo));
        }
        rodadas.Add(new RodadaProjetada(fase, jogos));

        // Cada rodada entrega tantos vencedores quantos jogos teve; a próxima é o nome do
        // quadro desse tanto de gente (4 vencedores = Semifinal, 2 = Final).
        while (numeros.Count > 1)
        {
            var proximos = new List<int>();
            var jogosDaRodada = new List<JogoProjetado>();

            foreach (var par in ChaveamentoMataMata.ParearVencedores(numeros))
            {
                int numero = proximoNumero++;
                proximos.Add(numero);
                jogosDaRodada.Add(new JogoProjetado(
                    numero,
                    $"Vencedor do jogo {par.Dupla1Id}",
                    $"Vencedor do jogo {par.Dupla2Id}"));
            }

            rodadas.Add(new RodadaProjetada(ChaveamentoMataMata.NomeFase(numeros.Count), jogosDaRodada));
            numeros = proximos;
        }

        return rodadas;
    }
}
