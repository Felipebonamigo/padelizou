namespace Padelizou.Services;

// As fases que AINDA VÃO ACONTECER, com horário e com quem pode jogá-las.
//
// O motor do mata-mata cria cada rodada só quando a anterior fecha (Dupla1Id/Dupla2Id são
// obrigatórios — não existe partida "a definir" no banco). O efeito na tela era que a
// Semifinal e a Final simplesmente NÃO EXISTIAM pra quem olhava: o jogador via a primeira
// rodada e mais nada, sem saber a que horas voltar nem contra quem pode jogar.
//
// O horário não é chute: sai da duração de partida configurada no torneio e das regras de
// grade que já valem pro resto (Services/GradeDeJogos). Atrasar na prática é normal e não
// muda o slot — decisão do Felipe em 05/08/2026.
//
// A projeção usa a MESMA regra do avanço de verdade (AvancoDaChave.QuemAvancaAsync +
// ChaveamentoMataMata.ParearVencedores): vencedores na ordem de Id, byes no fim, e o
// primeiro cruza com o último. Uma conta paralela diria um cruzamento que o sorteio não faz.
public static class ProximasFasesDaChave
{
    // O que a projeção precisa saber de uma partida já existente.
    public record PartidaDaChave(int Id, string Fase, string Dupla1, string Dupla2, DateTime? Horario);

    // Um lado do confronto que ainda não tem dono, descrito por quem pode ocupá-lo.
    public record Lado(IReadOnlyList<string> Candidatos)
    {
        public bool JaDefinido => Candidatos.Count == 1;

        public string Rotulo => Candidatos.Count switch
        {
            0 => "A definir",
            // Passou direto (bye): não é candidato, é a dupla mesmo.
            1 => Candidatos[0],
            // A rodada seguinte é EXATA: só dois podem chegar ali, e são estes dois.
            2 => $"Vencedor de {Candidatos[0]} × {Candidatos[1]}",
            _ => $"Um destes {Candidatos.Count}: {string.Join(", ", Candidatos)}",
        };
    }

    public record JogoQueVem(string Fase, DateTime? Horario, Lado Lado1, Lado Lado2)
    {
        // Quantas duplas ainda disputam essa vaga — a tela usa pra decidir se cabe na linha.
        public int TotalDeCandidatos => Lado1.Candidatos.Count + Lado2.Candidatos.Count;
    }

    // `byes` são as duplas que pularam a primeira rodada: elas entram na conta do pareamento
    // DEPOIS dos vencedores, igual ao avanço de verdade. Sem elas, o quadro projetado teria
    // menos gente que o real e os cruzamentos sairiam todos errados.
    public static List<JogoQueVem> Montar(
        IReadOnlyList<PartidaDaChave> partidasDeMataMata,
        IReadOnlyList<string> byes,
        int duracaoMinutos,
        int quadras,
        TimeSpan ultimoInicioDoDia,
        TimeSpan aberturaDiasSeguintes)
    {
        var vazio = new List<JogoQueVem>();
        if (partidasDeMataMata.Count == 0) return vazio;

        // A fase mais adiantada que JÁ tem jogo é de onde a projeção parte.
        var faseAtual = FaseMaisAdiantada(partidasDeMataMata);
        if (faseAtual == null) return vazio;

        var daFase = partidasDeMataMata
            .Where(p => p.Fase == faseAtual)
            .OrderBy(p => p.Id)          // a MESMA ordem do avanço de verdade
            .ToList();

        // Cada jogo da fase atual entrega um vencedor; cada bye entrega a própria dupla.
        var lados = daFase
            .Select(p => new Lado(new[] { p.Dupla1, p.Dupla2 }))
            .Concat(byes.Select(b => new Lado(new[] { b })))
            .ToList();

        var horario = UltimoHorario(partidasDeMataMata);
        var jogos = new List<JogoQueVem>();
        var fase = faseAtual;

        // Trava de segurança: uma chave real nunca passa de meia dúzia de rodadas, e um dado
        // torto não pode virar laço infinito numa página que o jogador abre.
        for (int rodada = 0;
             ChaveamentoMataMata.ProximaFase(fase) != null && lados.Count >= 2 && rodada < 10;
             rodada++)
        {
            // O NOME sai de quantos ainda estão vivos, não de encadear ProximaFase — é o que
            // os dois robôs de verdade fazem (NomeFase(avancam.Count) em TorneiosController.
            // Chaves e PartidasController). Numa chave com BYE os dois divergem: 2 jogos de
            // primeira rodada + 2 duplas descansadas são 4 duplas, ou seja SEMIFINAL, mas
            // encadear diria "Oitavas de Final" — e a tela anunciaria uma fase que o torneio
            // nunca vai criar.
            fase = ChaveamentoMataMata.NomeFase(lados.Count);

            var confrontos = Parear(lados);
            int noSlot = 0;

            foreach (var (lado1, lado2) in confrontos)
            {
                // Quadra livre acabou: o próximo jogo só começa na leva seguinte.
                if (noSlot % Math.Max(1, quadras) == 0 && horario != null)
                {
                    horario = GradeDeJogos.DepoisDe(
                        horario.Value, ultimoInicioDoDia, aberturaDiasSeguintes, duracaoMinutos);
                }
                noSlot++;

                jogos.Add(new JogoQueVem(fase, horario, lado1, lado2));
            }

            // Quem vence aquele confronto é UMA das duplas dos dois lados: é isso que faz a
            // fase seguinte ser "um destes N" em vez de um nome só.
            lados = confrontos
                .Select(c => new Lado(c.Lado1.Candidatos.Concat(c.Lado2.Candidatos).ToList()))
                .ToList();
        }

        return jogos;
    }

    // Primeiro cruza com último — a regra de ChaveamentoMataMata.ParearVencedores, aplicada
    // aqui sobre LADOS em vez de ids, porque aqui ainda não há vencedor nenhum.
    private static List<(Lado Lado1, Lado Lado2)> Parear(IReadOnlyList<Lado> lados)
    {
        var pares = new List<(Lado, Lado)>(lados.Count / 2);
        for (int i = 0; i < lados.Count / 2; i++)
            pares.Add((lados[i], lados[lados.Count - 1 - i]));
        return pares;
    }

    private static string? FaseMaisAdiantada(IReadOnlyList<PartidaDaChave> partidas)
    {
        // "Mais adiantada" pela ordem do próprio motor, não por nome nem por Id: seguir a
        // corrente de ProximaFase a partir de cada fase presente diz qual é a última.
        var fases = partidas.Select(p => p.Fase).Distinct().ToHashSet();

        return fases.FirstOrDefault(f =>
        {
            var proxima = ChaveamentoMataMata.ProximaFase(f);
            return proxima == null || !fases.Contains(proxima);
        });
    }

    private static DateTime? UltimoHorario(IReadOnlyList<PartidaDaChave> partidas) =>
        partidas.Where(p => p.Horario != null).Select(p => p.Horario!.Value)
            .DefaultIfEmpty()
            .Max() is { } m && m != default ? m : null;
}
