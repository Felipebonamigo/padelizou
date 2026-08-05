namespace Padelizou.Services;

// As fases que AINDA VÃO ACONTECER, com hora, QUADRA e com quem pode jogá-las.
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
// O pareamento usa a MESMA regra do avanço de verdade (ChaveamentoMataMata.ParearVencedores):
// primeiro cruza com último. Uma conta paralela diria um cruzamento que o sorteio não faz.
//
// ---- Por que ESTRUTURA e GRADE são duas coisas separadas aqui ----
//
// Cada categoria sabe sozinha QUEM joga contra quem (a cadeia de fases). Mas ONDE e QUANDO
// é uma pergunta do TORNEIO INTEIRO: as quadras são as mesmas pra todo mundo. Enquanto cada
// categoria calculava o próprio horário por conta própria, a tela do Interno mostrou OITO
// jogos às 22:23 num torneio de CINCO quadras — e nenhum deles com quadra, porque não havia
// como dizer qual. Por isso a cadeia sai sem hora (`Montar`, `MontarDosGrupos`) e uma
// segunda passada (`Agendar`) põe todas as cadeias na mesma grade, junto com o que já está
// marcado de verdade.
public static class ProximasFasesDaChave
{
    // O que a projeção precisa saber de uma partida já existente.
    public record PartidaDaChave(int Id, string Fase, string Dupla1, string Dupla2, DateTime? Horario);

    // Um lado do confronto que ainda não tem dono: ou a dupla que passou direto (bye, e aí
    // tem nome), ou a procedência ("Vencedor Quartas de Final 1", "1º do Grupo A").
    public record Lado(string Rotulo);

    // A categoria vem junto porque a lista de jogos mistura todas: sem ela, duas semifinais
    // de categorias diferentes viram duas linhas idênticas.
    public record JogoQueVem(string Categoria, string Fase, int Numero, DateTime? Horario,
                             Lado Lado1, Lado Lado2, string? Quadra = null)
    {
        // "Quartas de Final 2" — o rótulo que a tela mostra e que os lados citam.
        // A FINAL não numera: é um jogo só, e "Final 1" faria pensar que existe uma Final 2.
        public string FaseNumerada => Fase == "Final" ? Fase : $"{Fase} {Numero}";
    }

    // ================= ESTRUTURA: quem joga com quem, ainda sem hora =================

    public record RodadaQueVem(string Fase, IReadOnlyList<(Lado Lado1, Lado Lado2)> Confrontos);

    // O caminho de UMA categoria até a final. `DepoisDe` é o último jogo que já tem hora e
    // que alimenta a primeira rodada projetada — dele sai a folga de abertura.
    public record CadeiaDeFases(string Categoria, DateTime? DepoisDe, IReadOnlyList<RodadaQueVem> Rodadas)
    {
        public static readonly CadeiaDeFases Vazia = new("", null, Array.Empty<RodadaQueVem>());
    }

    // ---- Entrada 1: a chave JÁ COMEÇOU (existe pelo menos uma fase de mata-mata) ----
    //
    // `byes` são as duplas que pularam a primeira rodada: elas entram na conta do pareamento
    // DEPOIS dos vencedores, igual ao avanço de verdade. Sem elas, o quadro projetado teria
    // menos gente que o real e os cruzamentos sairiam todos errados.
    public static CadeiaDeFases Montar(
        IReadOnlyList<PartidaDaChave> partidasDeMataMata,
        IReadOnlyList<string> byes,
        string categoria = "")
    {
        if (partidasDeMataMata.Count == 0) return CadeiaDeFases.Vazia;

        var faseAtual = FaseMaisAdiantada(partidasDeMataMata);
        if (faseAtual == null) return CadeiaDeFases.Vazia;

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

        return new CadeiaDeFases(categoria, UltimoHorario(partidasDeMataMata), Encadear(lados));
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
    public static CadeiaDeFases MontarDosGrupos(
        IReadOnlyList<string> grupos,
        int classificadosPorGrupo,
        DateTime? fimDosGrupos,
        string categoria = "")
    {
        var (fase, confrontos, byes) = ChaveProjetada.Montar(grupos, classificadosPorGrupo);
        if (confrontos.Count == 0) return CadeiaDeFases.Vazia;

        var primeira = new RodadaQueVem(fase, confrontos
            .Select(c => (new Lado(c.Lado1.Rotulo), new Lado(c.Lado2.Rotulo)))
            .ToList());

        var proximos = confrontos
            .Select((_, i) => new Lado($"Vencedor {fase} {i + 1}"))
            // Quem folga a primeira rodada entra DEPOIS dos vencedores, na mesma ordem do
            // avanço de verdade — é o que faz cada vencedor cruzar com uma vaga que passou
            // direto. Aqui o bye ainda não tem nome: é a colocação ("2º do Grupo C").
            .Concat(byes.Select(b => new Lado(b.Rotulo)))
            .ToList();

        var rodadas = new List<RodadaQueVem> { primeira };
        rodadas.AddRange(Encadear(proximos));

        return new CadeiaDeFases(categoria, fimDosGrupos, rodadas);
    }

    // O encadeamento, rodada a rodada, até a final.
    private static List<RodadaQueVem> Encadear(List<Lado> lados)
    {
        var rodadas = new List<RodadaQueVem>();

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

            rodadas.Add(new RodadaQueVem(fase, confrontos));
            lados = confrontos.Select((_, i) => new Lado($"Vencedor {fase} {i + 1}")).ToList();

            if (fase == "Final") break;
        }

        return rodadas;
    }

    // ================= GRADE: quando e em que quadra =================

    // Uma vaga que já tem dono — os jogos REAIS que estão marcados. A projeção precisa deles
    // pra não prometer uma quadra que já está ocupada. `Quadra` nula é o torneio que não
    // cadastrou quadra: ela ocupa a vaga sem tomar um nome.
    public record VagaOcupada(DateTime Horario, string? Quadra);

    // `Quadras` são os nomes NA ORDEM; `Capacidade` é quantos jogos rodam ao mesmo tempo.
    // Os dois existem porque podem discordar: um torneio de 5 quadras pode ter cadastrado só
    // 3 nomes, e aí duas vagas de cada horário ficam sem nome — o que é a verdade, e melhor
    // que inventar "Quadra 4".
    public record ConfiguracaoDaGrade(
        int DuracaoMinutos, int Capacidade, IReadOnlyList<string> Quadras,
        TimeSpan UltimoInicioDoDia, TimeSpan AberturaDiasSeguintes);

    // Põe TODAS as cadeias na mesma grade, disputando as mesmas quadras.
    //
    // A regra que manda é a folga entre fases (GradeDeJogos.AberturaDaProximaFase): uma
    // rodada abre uma rodada inteira depois do fim da que a alimenta. Dentro disso, os jogos
    // ocupam as quadras livres do horário; quando o horário lota, o resto cai no seguinte.
    //
    // ⚠️ Nada aqui sabe QUEM vai jogar (os lados são "Vencedor Quartas de Final 1"), então a
    // grade não tem como evitar que a mesma pessoa apareça em duas categorias no mesmo
    // horário. Quem garante isso é o encaixe de verdade (GradeDeJogos.Encaixar), na hora em
    // que a rodada nasce com nome e sobrenome.
    public static List<JogoQueVem> Agendar(
        IReadOnlyList<CadeiaDeFases> cadeias,
        ConfiguracaoDaGrade grade,
        IReadOnlyList<VagaOcupada>? jaMarcados = null)
    {
        var vivas = cadeias.Where(c => c.Rodadas.Count > 0).ToList();
        if (vivas.Count == 0) return new List<JogoQueVem>();

        int capacidade = Math.Max(1, grade.Capacidade);
        var ocupadas = new Dictionary<DateTime, HashSet<string>>();
        var lotacao = new Dictionary<DateTime, int>();

        foreach (var vaga in jaMarcados ?? Array.Empty<VagaOcupada>())
        {
            lotacao[vaga.Horario] = lotacao.GetValueOrDefault(vaga.Horario) + 1;

            if (!string.IsNullOrEmpty(vaga.Quadra))
            {
                if (!ocupadas.TryGetValue(vaga.Horario, out var nomes))
                    ocupadas[vaga.Horario] = nomes = new HashSet<string>();
                nomes.Add(vaga.Quadra!);
            }
        }

        // Estado de cada cadeia: qual a próxima rodada e a partir de quando ela pode abrir.
        var proxima = new int[vivas.Count];
        var abertura = vivas.Select(c => AbrirRodada(c.DepoisDe, grade)).ToArray();

        var jogos = new List<JogoQueVem>();

        while (true)
        {
            // A cadeia que abre mais cedo entra primeiro — é o que faz as categorias
            // dividirem as quadras do mesmo horário em vez de uma esperar a outra acabar.
            int escolha = -1;
            for (int i = 0; i < vivas.Count; i++)
            {
                if (proxima[i] >= vivas[i].Rodadas.Count) continue;
                if (escolha < 0) { escolha = i; continue; }

                // Sem horário (torneio por ordem de liberação) todas empatam: vale a ordem
                // de entrada, e a lista sai agrupada por categoria.
                if ((abertura[i] ?? DateTime.MaxValue) < (abertura[escolha] ?? DateTime.MaxValue))
                    escolha = i;
            }

            if (escolha < 0) break;

            var cadeia = vivas[escolha];
            var rodada = cadeia.Rodadas[proxima[escolha]];
            var horario = abertura[escolha];

            for (int i = 0; i < rodada.Confrontos.Count; i++)
            {
                string? quadra = null;

                if (horario is DateTime h)
                {
                    // Horário lotado: o jogo escorrega pro seguinte. Sem esta parada, a
                    // projeção anunciava 8 jogos no mesmo minuto num torneio de 5 quadras.
                    while (lotacao.GetValueOrDefault(h) >= capacidade)
                        h = GradeDeJogos.DepoisDe(h, grade.UltimoInicioDoDia,
                                                  grade.AberturaDiasSeguintes, grade.DuracaoMinutos);

                    var nomes = ocupadas.TryGetValue(h, out var usadas) ? usadas : null;
                    quadra = grade.Quadras.FirstOrDefault(q => nomes == null || !nomes.Contains(q));

                    lotacao[h] = lotacao.GetValueOrDefault(h) + 1;
                    if (quadra != null)
                    {
                        if (nomes == null) ocupadas[h] = nomes = new HashSet<string>();
                        nomes.Add(quadra);
                    }

                    horario = h;
                }

                jogos.Add(new JogoQueVem(cadeia.Categoria, rodada.Fase, i + 1, horario,
                    rodada.Confrontos[i].Lado1, rodada.Confrontos[i].Lado2, quadra));
            }

            abertura[escolha] = AbrirRodada(horario, grade);
            proxima[escolha]++;
        }

        return jogos;
    }

    // Quando a rodada abre: uma rodada de folga depois do fim da anterior. A regra mora em
    // GradeDeJogos.AberturaDaProximaFase, que é a mesma usada na grade de verdade — projeção
    // e grade dizendo horas diferentes seria pior que não projetar.
    private static DateTime? AbrirRodada(DateTime? ultimoDaFaseAnterior, ConfiguracaoDaGrade grade) =>
        ultimoDaFaseAnterior is DateTime ultimo
            ? GradeDeJogos.AberturaDaProximaFase(ultimo, grade.UltimoInicioDoDia,
                                                 grade.AberturaDiasSeguintes, grade.DuracaoMinutos)
            : null;

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
