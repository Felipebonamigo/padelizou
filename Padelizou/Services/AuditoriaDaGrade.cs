using Padelizou.Models;

namespace Padelizou.Services;

// CONFERIR A GRADE: as restrições de horário foram respeitadas?
//
// 🗣️ Felipe, 09/09/2026: "faz esse botão e sobe". Nasceu de um beco — ele pediu duas vezes que
// eu conferisse a grade do torneio dele em `dev`, e a sessão não alcança o `dev`. A saída não
// é pedir print: é virar a auditoria em TELA, pra ele apertar e ver, em qualquer torneio.
//
// ⚠️ ESTE SERVIÇO É A ÚNICA CÓPIA DA AUDITORIA, e é de propósito: a tela e o teste de
// regressão chamam ele. Uma auditoria escrita duas vezes é uma que um dia diz "tudo certo"
// sobre uma regra que mudou do outro lado — e ela seria acreditada, porque é justamente ela
// que existe pra ser acreditada.
//
// ⚠️ ACHADO NÃO É NECESSARIAMENTE BUG. O `GradeDeJogos.Encaixar` CEDE de propósito quando as
// vagas acabam ("um jogo sem horário nenhum é pior"), e nessa hora ele fura o impedimento antes
// de deixar jogo sem hora. A tela mostra o que cedeu pra o organizador decidir — mudar a grade,
// falar com a dupla, ou aceitar. Por isso o texto é descritivo e não acusatório.
public static class AuditoriaDaGrade
{
    public const string Impedimento = "Impedimento";
    public const string PessoaEmDoisJogos = "Mesma pessoa em dois jogos";
    public const string Concentracao = "Concentração";
    public const string NoiteDeSabado = "Sábado à noite";
    public const string Retardatario = "Jogo retardatário";
    public const string QuadraFechada = "Quadra fora do horário";
    public const string SemHorario = "Jogo sem horário";

    // 🗣️ Felipe, 09/09/2026: *"e ali esta marcando dia 15, como assim? tem q rever isso, torneio
    // termina no domingo dia 13"*. `Torneio.DataFim` existia e o motor nunca a leu — era um aviso
    // na previsão e mais nada. Aqui ela vira achado: o organizador aperta "Conferir grade" e vê
    // exatamente quais jogos passaram do dia em que ele devolve a quadra.
    public const string DepoisDoFim = "Depois do fim do torneio";

    // 🗣️ Felipe, 09/09/2026, depois de DUAS correções da ordem: *"como que ele nao ta respeitando
    // a ordem que eu tinha solicitado de nao jogar chaves no final?"*. Enquanto a conferência não
    // sabia olhar isso, a única forma de responder era eu ler o print dele — e print mostra um
    // pedaço da lista. Agora é uma pergunta que ele faz sozinho, em qualquer torneio.
    public const string FaseForaDeOrdem = "Fase fora de ordem";

    // 🗣️ Felipe, 10/09/2026: *"varios jogos seguidos, temos q evitar isso [...] é bom ter um botão
    // para 'ver jogos seguidos da mesma pessoa'"*. O botão é esta tela; a régua é a folga que a
    // grade promete (GradeDeJogos.HorariosDeDescanso), aplicada por PESSOA como a de "dois jogos
    // ao mesmo tempo".
    public const string JogosSeguidos = "Jogos seguidos";

    // `Quando` fica separado do texto pra tela poder ordenar por ele — o organizador lê a grade
    // no relógio, não em ordem alfabética de regra.
    public record Achado(string Regra, string Descricao, DateTime? Quando);

    public static List<Achado> Conferir(Torneio torneio, IReadOnlyCollection<Partida> jogos,
        IReadOnlyCollection<Dupla> duplas, SedesDoTorneio sedes)
    {
        var achados = new List<Achado>();
        var porId = duplas.ToDictionary(d => d.Id);

        string Nome(int duplaId) =>
            porId.TryGetValue(duplaId, out var d) ? d.NomeDeExibicao : $"dupla {duplaId}";

        // ── 1. Jogo dentro da janela que a dupla pagou pra evitar ────────────────────────
        var impedimentos = JanelasDeImpedimento.PorDupla(torneio, duplas);
        // ── 3. Concentração: vale SÓ na fase de grupos (decisão do Felipe) ───────────────
        var concentracoes = ConcentracaoDeJogos.PorDupla(torneio, duplas);
        // ── 4. Eliminatória no sábado à noite, por categoria ─────────────────────────────
        var noiteDeSabado = EliminatoriaNoSabado.PorCategoria(torneio);

        static bool Dentro(IReadOnlyDictionary<int, (DateTime Inicio, DateTime Fim)[]> mapa,
            int chave, DateTime quando) =>
            mapa.TryGetValue(chave, out var janelas)
            && janelas.Any(j => quando >= j.Inicio && quando < j.Fim);

        foreach (var jogo in jogos)
        {
            if (jogo.HorarioPrevisto is not DateTime quando)
            {
                achados.Add(new Achado(SemHorario,
                    $"{Nome(jogo.Dupla1Id)} × {Nome(jogo.Dupla2Id)} ({jogo.Fase}) está sem horário.", null));
                continue;
            }

            bool ehGrupo = FasesTorneio.EhFaseDeGrupos(jogo.Fase);

            foreach (var duplaId in new[] { jogo.Dupla1Id, jogo.Dupla2Id })
            {
                if (Dentro(impedimentos, duplaId, quando) && porId.TryGetValue(duplaId, out var dupla))
                {
                    achados.Add(new Achado(Impedimento,
                        $"{Nome(duplaId)} joga {quando:dd/MM 'às' HH:mm}, dentro do impedimento "
                        + $"\"{AlteracaoDeImpedimento.Rotulo(AlteracaoDeImpedimento.TurnoAtual(dupla))}\".",
                        quando));
                }

                if (ehGrupo && Dentro(concentracoes, duplaId, quando)
                    && porId.TryGetValue(duplaId, out var concentrada)
                    && concentrada.ConcentrarJogosEm is TurnoDeConcentracao turno)
                {
                    achados.Add(new Achado(Concentracao,
                        $"{Nome(duplaId)} joga {quando:dd/MM 'às' HH:mm}, fora de "
                        + $"\"{ConcentracaoDeJogos.Rotulo(turno)}\".", quando));
                }
            }

            if (!ehGrupo && Dentro(noiteDeSabado, jogo.CategoriaId, quando))
            {
                achados.Add(new Achado(NoiteDeSabado,
                    $"{jogo.Fase} de {Nome(jogo.Dupla1Id)} × {Nome(jogo.Dupla2Id)} está "
                    + $"{quando:dd/MM 'às' HH:mm} — essa categoria pediu pra não ter eliminatória "
                    + "no sábado à noite.", quando));
            }

            // Comparação por DIA, e não por hora: o limite é "até domingo", não "até domingo às
            // 00h" — a mesma leitura de PrevisaoGradeVM.EstouraOPrazo. Um jogo que COMEÇA 23h50 do
            // domingo e varre a madrugada é o normal do torneio, não um estouro.
            if (torneio.DataFim is DateTime prazo && quando.Date > prazo.Date)
            {
                achados.Add(new Achado(DepoisDoFim,
                    $"{jogo.Fase} de {Nome(jogo.Dupla1Id)} × {Nome(jogo.Dupla2Id)} está marcado "
                    + $"{quando:dd/MM 'às' HH:mm}, depois de {prazo:dd/MM} — o dia que você marcou "
                    + "como limite do torneio.", quando));
            }

            if (!string.IsNullOrEmpty(jogo.NomeQuadra) && !sedes.QuadraAberta(jogo.NomeQuadra, quando))
            {
                achados.Add(new Achado(QuadraFechada,
                    $"{jogo.NomeQuadra} está marcada {quando:dd/MM 'às' HH:mm}, fora do horário "
                    + "em que esse local está disponível.", quando));
            }
        }

        // ── 5. A ORDEM DAS FASES DO TORNEIO ──────────────────────────────────────────────
        //
        // A régua é o POSTO (Services/OrdemDasFases): todos os grupos, depois todas as primeiras
        // eliminatórias, e as finais por último — do TORNEIO inteiro, não de cada categoria.
        //
        // ⚠️ `>=` E NÃO `>` NO LIMITE, e isso é o pedido e não uma folga minha: *"a menos que fique
        // horario vazio, mas a ordem é colocar todos jogos de chave antes"*. O primeiro jogo de um
        // posto PODE dividir o horário com o último do posto anterior — é o que ocupa a quadra que
        // sobraria vazia naquele minuto. O que não pode é vir ANTES.
        //
        // ⚠️ E O FIM DE UM POSTO É O FIM DO BLOCO DELE, NÃO O `Max` (OrdemDasFases.FimDoBloco,
        // 09/09/2026): um jogo de grupo retardatário no domingo de manhã não faz a eliminatória
        // de sábado à noite ser "fora de ordem". O retardatário em si é apontado logo abaixo.
        DateTime Seguinte(DateTime h) =>
            GradeDeJogos.DepoisDe(h, torneio.HoraFimDoDia, torneio.HoraInicioDiasSeguintes, VagasDaGrade.Duracao(torneio));

        var porPosto = jogos
            .Where(j => j.HorarioPrevisto != null)
            .GroupBy(j => OrdemDasFases.Posto(j.Fase))
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                Posto = g.Key,
                Jogos = g.ToList(),
                Acaba = OrdemDasFases.FimDoBloco(g.Select(j => j.HorarioPrevisto!.Value), Seguinte,
                                                 torneio.QuantidadeQuadras)!.Value,
            })
            .ToList();

        // ── 5b. O JOGO DE GRUPO RETARDATÁRIO ─────────────────────────────────────────────
        // Só na fase de grupos: ela é UMA leva, e um jogo dela depois de um horário inteiro vazio
        // é o que o organizador vai querer mexer na mão (impedimento, concentração — o Conferir
        // grade acusa o motivo nas outras regras). Nas eliminatórias os buracos são naturais:
        // cada categoria entra quando fecha a fase anterior DELA.
        foreach (var grupos in porPosto.Where(p => p.Posto == OrdemDasFases.PostoDaFaseDeGrupos))
        {
            foreach (var jogo in grupos.Jogos.Where(j => j.HorarioPrevisto > grupos.Acaba))
            {
                achados.Add(new Achado(Retardatario,
                    $"{jogo.Fase} de {Nome(jogo.Dupla1Id)} × {Nome(jogo.Dupla2Id)} está "
                    + $"{jogo.HorarioPrevisto:dd/MM 'às' HH:mm}, depois de a fase de grupos fechar "
                    + $"({grupos.Acaba:dd/MM 'às' HH:mm}) — as eliminatórias não esperam por ele.",
                    jogo.HorarioPrevisto));
            }
        }

        for (int i = 1; i < porPosto.Count; i++)
        {
            var anterior = porPosto[i - 1];

            foreach (var jogo in porPosto[i].Jogos.Where(j => j.HorarioPrevisto < anterior.Acaba))
            {
                achados.Add(new Achado(FaseForaDeOrdem,
                    $"{jogo.Fase} de {Nome(jogo.Dupla1Id)} × {Nome(jogo.Dupla2Id)} está "
                    + $"{jogo.HorarioPrevisto:dd/MM 'às' HH:mm}, antes de a fase anterior do torneio "
                    + $"terminar ({anterior.Acaba:dd/MM 'às' HH:mm}).",
                    jogo.HorarioPrevisto));
            }
        }

        // ── 2. A mesma PESSOA em dois jogos ao mesmo tempo ───────────────────────────────
        //
        // ⚠️ POR PESSOA, NÃO POR DUPLA, e é a mesma razão do `Encaixar`: com a chave direta (ou
        // duas categorias) o mesmo jogador está em duplas de Ids diferentes. Comparando dupla,
        // o caso que mais dói passa batido.
        //
        // ⚠️ AS DUAS RÉGUAS SÃO EMPRESTADAS DO MOTOR, NÃO REESCRITAS AQUI (09/09/2026) — é o que
        // o cabeçalho deste arquivo exige, e a primeira versão desrespeitava nos dois sentidos:
        //
        //   • QUEM OCUPA A QUADRA sai de `RoboDoChaveamento.OcupantesPorDupla`, que deixa TIME de
        //     fora de propósito. Todo time é uma Dupla com o ORGANIZADOR no `Jogador1Id` (coluna
        //     NOT NULL), então ler `Jogador1Id` na mão via a mesma pessoa em todos os times e
        //     acusava a grade inteira — um achado por horário, num torneio sem defeito nenhum.
        //
        //   • O CHOQUE é por INTERVALO, como em `GradeDeJogos.CruzaComAPessoa`, e não por instante
        //     exato. `GroupBy(HorarioPrevisto)` só cruzava jogos no MESMO minuto — e a grade
        //     desalinhada não é hipótese: `AberturaDoRecalculo` parte de `DateTime.Now` quando há
        //     jogo em quadra, então o "Refazer grade" das 20h13 põe jogos novos em 20:13 ao lado
        //     dos antigos em 20:00. A tela dizia "Nada fora do lugar" exatamente ali, que é onde
        //     o organizador aperta o botão.
        var ocupantes = RoboDoChaveamento.OcupantesPorDupla(duplas);
        var duracao = TimeSpan.FromMinutes(VagasDaGrade.Duracao(torneio));

        var agenda = new Dictionary<int, List<DateTime>>();
        foreach (var jogo in jogos)
        {
            if (jogo.HorarioPrevisto is not DateTime quando) continue;

            foreach (var duplaId in new[] { jogo.Dupla1Id, jogo.Dupla2Id })
            {
                if (!ocupantes.TryGetValue(duplaId, out var pessoas)) continue;

                foreach (var pessoa in pessoas)
                {
                    if (!agenda.TryGetValue(pessoa, out var quandos))
                        agenda[pessoa] = quandos = new List<DateTime>();
                    quandos.Add(quando);
                }
            }
        }

        // Dois jogos da MESMA dupla se cruzando dariam um achado por jogador, com texto idêntico
        // (o texto diz "alguém", não o nome) — e dois avisos iguais lado a lado fazem a tela
        // parecer quebrada. Deduplicado pelo par de horários, que é o que o organizador lê.
        var jaAvisado = new HashSet<(DateTime, DateTime)>();

        foreach (var quandos in agenda.Values)
        {
            var ordenados = quandos.OrderBy(q => q).ToList();

            for (int i = 1; i < ordenados.Count; i++)
            {
                var (antes, depois) = (ordenados[i - 1], ordenados[i]);
                if (!jaAvisado.Add((antes, depois))) continue;

                // Seguidos, sem a folga da régua: não se sobrepõem (isso é o achado abaixo), mas
                // entre um e outro passam menos horários do que a grade promete.
                var folga = duracao * GradeDeJogos.HorariosDeDescanso;
                if (depois - antes >= duracao && depois - antes < duracao + folga)
                {
                    achados.Add(new Achado(JogosSeguidos,
                        $"Alguém joga {antes:dd/MM 'às' HH:mm} e de novo {depois:HH:mm} — "
                        + $"{(depois - antes - duracao).TotalMinutes / duracao.TotalMinutes:0} horário(s) de "
                        + $"descanso, e a grade promete {GradeDeJogos.HorariosDeDescanso}.", antes));
                    continue;
                }

                if (depois - antes >= duracao) continue;

                achados.Add(new Achado(PessoaEmDoisJogos,
                    $"Alguém está escalado {antes:dd/MM 'às' HH:mm} e de novo {depois:HH:mm} — "
                    + $"os dois jogos se sobrepõem ({duracao.TotalMinutes:0} min de partida) e "
                    + "ninguém joga em duas quadras ao mesmo tempo.", antes));
            }
        }

        return achados.OrderBy(a => a.Quando ?? DateTime.MaxValue).ThenBy(a => a.Regra).ToList();
    }
}
