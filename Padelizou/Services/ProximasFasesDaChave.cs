namespace Padelizou.Services;

// As fases que AINDA VÃO ACONTECER, com horário e com quem pode jogá-las.
//
// O motor do mata-mata cria cada rodada só quando a anterior fecha (Dupla1Id/Dupla2Id são
// obrigatórios — não existe partida "a definir" no banco). O efeito na tela era que a
// Semifinal e a Final simplesmente NÃO EXISTIAM pra quem olhava: o jogador via a primeira
// rodada e mais nada, sem saber a que horas voltar nem contra quem pode jogar.
//
// ⚠️ Os jogos do mata-mata são NUMERADOS por fase ("Quartas de Final 1", "Quartas de Final
// 2"...) e um lado se descreve pelo jogo de onde vem: "Vencedor Quartas de Final 1". A
// primeira versão listava os candidatos por nome — "Um destes 4: Bernardo Mendonça &
// Alexandre Medina, Geison Moyses & …" — e a linha estourava a tela, sendo cortada
// justamente no fim, onde estavam os últimos nomes. O número diz a mesma coisa em três
// palavras e é rastreável: o jogo "Quartas de Final 1" está na mesma lista, logo acima.
//
// O horário não é chute: sai da duração de partida configurada no torneio e das regras de
// grade que já valem pro resto (Services/GradeDeJogos). Atrasar na prática é normal e não
// muda o slot — decisão do Felipe em 05/08/2026.
//
// ⚠️ Cada rodada abre uma rodada DEPOIS do fim da anterior (GradeDeJogos.AberturaDaProximaFase),
// nunca colada nela: os dois finalistas saem da semifinal, então a final tem que deixar folga.
//
// O pareamento usa a MESMA regra do avanço de verdade (ChaveamentoMataMata.ParearVencedores):
// primeiro cruza com último. Uma conta paralela diria um cruzamento que o sorteio não faz.
public static class ProximasFasesDaChave
{
    // O que a projeção precisa saber de uma partida já existente.
    public record PartidaDaChave(int Id, string Fase, string Dupla1, string Dupla2, DateTime? Horario);

    // Um lado do confronto que ainda não tem dono: ou a dupla que passou direto (bye, e aí
    // tem nome), ou a procedência ("Vencedor Quartas de Final 1", "1º do Grupo A").
    public record Lado(string Rotulo);

    // A categoria vem junto porque a lista de jogos mistura todas: sem ela, duas semifinais
    // de categorias diferentes viram duas linhas idênticas.
    public record JogoQueVem(string Categoria, string Fase, int Numero, DateTime? Horario, Lado Lado1, Lado Lado2)
    {
        // "Quartas de Final 2" — o rótulo que a tela mostra e que os lados citam.
        // A FINAL não numera: é um jogo só, e "Final 1" faria pensar que existe uma Final 2.
        public string FaseNumerada => Fase == "Final" ? Fase : $"{Fase} {Numero}";
    }

    // ---- Entrada 1: a chave JÁ COMEÇOU (existe pelo menos uma fase de mata-mata) ----
    //
    // `byes` são as duplas que pularam a primeira rodada: elas entram na conta do pareamento
    // DEPOIS dos vencedores, igual ao avanço de verdade. Sem elas, o quadro projetado teria
    // menos gente que o real e os cruzamentos sairiam todos errados.
    public static List<JogoQueVem> Montar(
        IReadOnlyList<PartidaDaChave> partidasDeMataMata,
        IReadOnlyList<string> byes,
        int duracaoMinutos,
        int quadras,
        TimeSpan ultimoInicioDoDia,
        TimeSpan aberturaDiasSeguintes,
        string categoria = "")
    {
        if (partidasDeMataMata.Count == 0) return new List<JogoQueVem>();

        var faseAtual = FaseMaisAdiantada(partidasDeMataMata);
        if (faseAtual == null) return new List<JogoQueVem>();

        var daFase = partidasDeMataMata
            .Where(p => p.Fase == faseAtual)
            .OrderBy(p => p.Id)          // a MESMA ordem do avanço de verdade
            .ToList();

        // Cada jogo da fase atual entrega um vencedor — citado pelo NÚMERO dele naquela
        // fase; cada bye entrega a própria dupla, que já tem nome.
        var lados = daFase
            .Select((_, i) => new Lado($"Vencedor {faseAtual} {i + 1}"))
            .Concat(byes.Select(b => new Lado(b)))
            .ToList();

        return Encadear(lados, categoria, UltimoHorario(partidasDeMataMata),
            duracaoMinutos, quadras, ultimoInicioDoDia, aberturaDiasSeguintes);
    }

    // ---- Entrada 2: a chave AINDA NEM COMEÇOU (categoria na fase de grupos) ----
    //
    // Aqui não existe partida de mata-mata nenhuma, então a projeção parte das COLOCAÇÕES:
    // "1º do Grupo A × 2º do Grupo C". Sem isto, a categoria que sai de grupos não mostrava
    // mata-mata nenhum na lista de jogos — só a chave direta aparecia, porque ela já nasce
    // com a primeira rodada criada e a projeção tinha de onde partir.
    //
    // Os confrontos da primeira rodada saem do mesmo motor da aba de chaves
    // (Services/ChaveProjetada), que por sua vez usa o do sorteio de verdade.
    public static List<JogoQueVem> MontarDosGrupos(
        IReadOnlyList<string> grupos,
        int classificadosPorGrupo,
        DateTime? horarioBase,
        int duracaoMinutos,
        int quadras,
        TimeSpan ultimoInicioDoDia,
        TimeSpan aberturaDiasSeguintes,
        string categoria = "")
    {
        var (fase, confrontos, byes) = ChaveProjetada.Montar(grupos, classificadosPorGrupo);
        if (confrontos.Count == 0) return new List<JogoQueVem>();

        var jogos = new List<JogoQueVem>();
        var proximos = new List<Lado>();

        // O mata-mata abre uma rodada DEPOIS do fim da fase de grupos, não colado nela: quem
        // joga o último jogo do grupo é candidato a classificar, e cairia direto na quadra.
        var horarios = HorariosDaRodada(confrontos.Count,
            AbrirRodada(horarioBase, duracaoMinutos, ultimoInicioDoDia, aberturaDiasSeguintes),
            quadras, duracaoMinutos, ultimoInicioDoDia, aberturaDiasSeguintes);

        for (int i = 0; i < confrontos.Count; i++)
        {
            int numero = i + 1;
            jogos.Add(new JogoQueVem(categoria, fase, numero, horarios[i],
                new Lado(confrontos[i].Lado1.Rotulo), new Lado(confrontos[i].Lado2.Rotulo)));
            proximos.Add(new Lado($"Vencedor {fase} {numero}"));
        }

        // Quem folga a primeira rodada entra DEPOIS dos vencedores, na mesma ordem do avanço
        // de verdade — é o que faz cada vencedor cruzar com uma vaga que passou direto. Aqui
        // o bye ainda não tem nome: é a colocação ("2º do Grupo C").
        proximos.AddRange(byes.Select(b => new Lado(b.Rotulo)));

        jogos.AddRange(Encadear(proximos, categoria, horarios[^1],
            duracaoMinutos, quadras, ultimoInicioDoDia, aberturaDiasSeguintes));

        return jogos;
    }

    // ---- O encadeamento, rodada a rodada, até a final ----
    //
    // `ultimoDaFaseAnterior` é o horário do ÚLTIMO jogo da fase que alimenta esta — cada
    // rodada se afasta dele por conta própria (ver AbrirRodada). A conta corrida de slots que
    // existia aqui atravessava a fronteira das rodadas, e quando a rodada anterior não enchia
    // as quadras a seguinte começava no MESMO horário dela: a Final saía junto com a Semifinal
    // que a decide.
    private static List<JogoQueVem> Encadear(
        List<Lado> lados, string categoria, DateTime? ultimoDaFaseAnterior,
        int duracaoMinutos, int quadras, TimeSpan ultimoInicioDoDia, TimeSpan aberturaDiasSeguintes)
    {
        var jogos = new List<JogoQueVem>();

        // Trava de segurança: uma chave real nunca passa de meia dúzia de rodadas, e um dado
        // torto não pode virar laço infinito numa página que o jogador abre.
        for (int rodada = 0; lados.Count >= 2 && rodada < 10; rodada++)
        {
            // O NOME sai de quantos ainda estão vivos, não de encadear ProximaFase — é o que
            // os dois robôs de verdade fazem (NomeFase(avancam.Count)). Numa chave com BYE os
            // dois divergem: 2 jogos de primeira rodada + 2 duplas descansadas são 4 duplas,
            // ou seja SEMIFINAL, mas encadear diria "Oitavas de Final" — e a tela anunciaria
            // uma fase que o torneio nunca vai criar.
            var fase = ChaveamentoMataMata.NomeFase(lados.Count);
            var confrontos = Parear(lados);
            var proximos = new List<Lado>(confrontos.Count);

            var horarios = HorariosDaRodada(confrontos.Count,
                AbrirRodada(ultimoDaFaseAnterior, duracaoMinutos, ultimoInicioDoDia, aberturaDiasSeguintes),
                quadras, duracaoMinutos, ultimoInicioDoDia, aberturaDiasSeguintes);

            for (int i = 0; i < confrontos.Count; i++)
            {
                int numero = i + 1;
                jogos.Add(new JogoQueVem(categoria, fase, numero, horarios[i],
                    confrontos[i].Lado1, confrontos[i].Lado2));
                proximos.Add(new Lado($"Vencedor {fase} {numero}"));
            }

            lados = proximos;
            ultimoDaFaseAnterior = horarios[^1];
            if (fase == "Final") break;
        }

        return jogos;
    }

    // Quando a rodada abre: uma rodada de folga depois do fim da anterior. A regra mora em
    // GradeDeJogos.AberturaDaProximaFase, que é a mesma usada na grade de verdade — projeção
    // e grade dizendo horas diferentes seria pior que não projetar.
    private static DateTime? AbrirRodada(DateTime? ultimoDaFaseAnterior, int duracaoMinutos,
        TimeSpan ultimoInicioDoDia, TimeSpan aberturaDiasSeguintes) =>
        ultimoDaFaseAnterior is DateTime ultimo
            ? GradeDeJogos.AberturaDaProximaFase(ultimo, ultimoInicioDoDia, aberturaDiasSeguintes, duracaoMinutos)
            : null;

    // Os jogos de UMA rodada ao longo do relógio: as quadras rodam em paralelo, então a hora
    // só anda quando a leva enche. Torneio por ordem de liberação não tem hora — e aí a lista
    // é de nulos, que é a resposta honesta.
    private static List<DateTime?> HorariosDaRodada(int quantidade, DateTime? abertura, int quadras,
        int duracaoMinutos, TimeSpan ultimoInicioDoDia, TimeSpan aberturaDiasSeguintes)
    {
        var horarios = new List<DateTime?>(quantidade);
        var horario = abertura;

        for (int i = 0; i < quantidade; i++)
        {
            if (i > 0 && i % Math.Max(1, quadras) == 0 && horario != null)
            {
                horario = GradeDeJogos.DepoisDe(
                    horario.Value, ultimoInicioDoDia, aberturaDiasSeguintes, duracaoMinutos);
            }

            horarios.Add(horario);
        }

        return horarios;
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
