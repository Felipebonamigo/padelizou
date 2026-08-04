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
}
