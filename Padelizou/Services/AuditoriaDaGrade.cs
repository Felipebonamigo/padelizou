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

    // As restrições das DUAS duplas de um jogo não cabem juntas em horário nenhum do torneio:
    // uma só joga sexta à noite (concentração), a outra não joga sexta (impedimento). O motor
    // marca onde der — é o retardatário do domingo, o "impedimento" do Conferir grade — e sem
    // esta linha o organizador procura o defeito na grade quando ele está no CADASTRO.
    public const string RestricoesEmConflito = "Restrições em conflito";

    // `Quando` fica separado do texto pra tela poder ordenar por ele — o organizador lê a grade
    // no relógio, não em ordem alfabética de regra.
    //
    // `Gravidade` é quanto FALTA: 1 pra quase todo achado; no "Jogos seguidos", quantos horários de
    // descanso faltaram (1 = descansou um, 2 = emendou). É o que deixa o reparo (ReparoDaGrade)
    // preferir dois avisos de "1 de folga" a uma dupla emendada — a tela não a mostra.
    public record Achado(string Regra, string Descricao, DateTime? Quando, int Gravidade = 1);

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
        //   • E QUEM NÃO ESTÁ NO MAPA OCUPA A QUADRA COMO ELE MESMO (10/09/2026). O time fica de
        //     fora do mapa de propósito (acima), mas "fora do mapa" não é "não ocupa quadra":
        //     `GradeDeJogos.Encaixar` e `RoboDoChaveamento` caem na identidade da própria dupla
        //     (`-duplaId`) e é assim que a grade evita chamar o MESMO time pra duas quadras. Aqui
        //     a dupla fora do mapa era PULADA, e o buraco não era só de tela: o `ReparoDaGrade`
        //     pesa por esta régua, então o mesmo time em dois jogos ao mesmo tempo custava ZERO e
        //     o reparo trocava de graça um impedimento por um choque que ele não enxergava.
        //     Medido em ~1 de 60 sorteios do Interno, sempre criado pelo reparo
        //     (ChaveDiretaNoSorteioTests.Torneio_completo_com_categorias_times_e_chave_direta_na_
        //     mesma_grade). O `-duplaId` é negativo pra nunca colidir com um JogadorId de verdade.
        var ocupantes = RoboDoChaveamento.OcupantesPorDupla(duplas);
        int[] Ocupam(int duplaId) =>
            ocupantes.TryGetValue(duplaId, out var pessoas) && pessoas.Length > 0 ? pessoas : new[] { -duplaId };
        var duracao = TimeSpan.FromMinutes(VagasDaGrade.Duracao(torneio));

        // O NOME DA PESSOA (10/09/2026). 🗣️ Felipe, com 14 "Alguém joga…" na tela: *"os horarios eu
        // vou trocar na mão"* — e não dá pra trocar na mão sem saber quem é nem quais jogos.
        var nomeDaPessoa = new Dictionary<int, string>();
        foreach (var d in duplas)
        {
            if (d.Jogador1 != null) nomeDaPessoa[d.Jogador1Id] = d.Jogador1.NomeNaTela;
            if (d.Jogador2Id is int j2 && d.Jogador2 != null) nomeDaPessoa[j2] = d.Jogador2.NomeNaTela;
        }
        // Quem ocupa a quadra "como ele mesmo" (o time) entra pelo `-duplaId`, e aí o nome que o
        // organizador precisa ler é o do TIME — "jogador -12" não diz nada a ninguém.
        string Pessoa(int id) => id < 0 ? Nome(-id)
            : nomeDaPessoa.TryGetValue(id, out var nome) ? nome : $"jogador {id}";
        string Rotulo(Partida j) => $"{j.Fase} ({Nome(j.Dupla1Id)} × {Nome(j.Dupla2Id)})";

        var agenda = new Dictionary<int, List<(DateTime Quando, Partida Jogo)>>();
        foreach (var jogo in jogos)
        {
            if (jogo.HorarioPrevisto is not DateTime quando) continue;

            foreach (var duplaId in new[] { jogo.Dupla1Id, jogo.Dupla2Id })
            {
                foreach (var pessoa in Ocupam(duplaId))
                {
                    if (!agenda.TryGetValue(pessoa, out var quandos))
                        agenda[pessoa] = quandos = new List<(DateTime, Partida)>();
                    quandos.Add((quando, jogo));
                }
            }
        }

        // Os dois jogadores da MESMA dupla cruzam os MESMOS dois jogos: é UM achado, com os dois
        // nomes — dois avisos iguais lado a lado fazem a tela parecer quebrada. Agrupado pelo par
        // de jogos, que é o que o organizador vai mexer.
        var pares = new Dictionary<(Partida, Partida), List<int>>();
        foreach (var (pessoa, quandos) in agenda)
        {
            // ⚠️ O DESEMPATE POR `Codigo` É A CORREÇÃO, NÃO ESTILO (10/09/2026). `OrderBy` é
            // ESTÁVEL: dois jogos da mesma pessoa NO MESMO HORÁRIO — que existe e tem nome, é o
            // achado "Mesma pessoa em dois jogos" logo abaixo — ficavam na ordem em que vieram na
            // LISTA. A varredura de pares consecutivos montava então pares diferentes ((A,B) e
            // (B,C) numa ordem, (B,A) e (A,C) na outra), o dicionário agrupava por chaves
            // diferentes, e A MESMA GRADE rendia 3 achados numa ordem e 2 na outra.
            //
            // 🕳️ Isso vazava para três lugares de uma vez: o número que o organizador lê no
            // "Conferir grade", o `Custo` do reparo (que aceitava numa passagem a troca que
            // recusava na outra) e o `Doentes` (que começava por outro jogo). Era a causa do
            // "Refazer grade" não reproduzir a grade do sorteio em ~15% dos sorteios, sempre com
            // um PAR de jogos trocando entre si — o defeito que o PR #118 tentou fechar pela
            // ponta do reparo e que só fecha aqui, na régua.
            //
            // `Codigo` e não `Id`: no sorteio os jogos ainda não foram gravados e são todos zero.
            // É o mesmo desempate que `ReparoDaGrade.EmOrdemEstavel` já usa, pelo mesmo motivo.
            var ordenados = quandos
                .OrderBy(q => q.Quando)
                .ThenBy(q => q.Jogo.Codigo, StringComparer.Ordinal)
                .ToList();
            for (int i = 1; i < ordenados.Count; i++)
            {
                var chave = (ordenados[i - 1].Jogo, ordenados[i].Jogo);
                if (!pares.TryGetValue(chave, out var quem)) pares[chave] = quem = new List<int>();
                quem.Add(pessoa);
            }
        }

        foreach (var ((jogoAntes, jogoDepois), quem) in pares)
        {
            var antes = jogoAntes.HorarioPrevisto!.Value;
            var depois = jogoDepois.HorarioPrevisto!.Value;
            var nomes = string.Join(" e ", quem.Select(Pessoa));

            // Seguidos, sem a folga da régua: não se sobrepõem (isso é o achado abaixo), mas
            // entre um e outro passam menos horários do que a grade promete.
            var folga = duracao * GradeDeJogos.HorariosDeDescanso;
            if (depois - antes >= duracao && depois - antes < duracao + folga)
            {
                // Um número só pro texto e pra gravidade: a grade desalinhada (refeita às 20h13)
                // dá descanso fracionário, e dois arredondamentos diferentes fariam a tela dizer
                // "1" enquanto o reparo pesa "0".
                int descansou = (int)Math.Round((depois - antes - duracao).TotalMinutes / duracao.TotalMinutes,
                    MidpointRounding.AwayFromZero);
                achados.Add(new Achado(JogosSeguidos,
                    $"{nomes}: {Rotulo(jogoAntes)} {antes:dd/MM 'às' HH:mm} e de novo "
                    + $"{Rotulo(jogoDepois)} às {depois:HH:mm} — "
                    + $"{descansou} horário(s) de "
                    + $"descanso, e a grade promete {GradeDeJogos.HorariosDeDescanso}.", antes,
                    Gravidade: Math.Max(1, GradeDeJogos.HorariosDeDescanso - descansou)));
                continue;
            }

            if (depois - antes >= duracao) continue;

            achados.Add(new Achado(PessoaEmDoisJogos,
                $"{nomes}: escalado em {Rotulo(jogoAntes)} {antes:dd/MM 'às' HH:mm} e de novo em "
                + $"{Rotulo(jogoDepois)} às {depois:HH:mm} — "
                + $"os dois jogos se sobrepõem ({duracao.TotalMinutes:0} min de partida) e "
                + "ninguém joga em duas quadras ao mesmo tempo.", antes));
        }

        // ── 7. AS RESTRIÇÕES DAS DUAS DUPLAS NÃO CABEM JUNTAS ────────────────────────────
        // Medido em GradeDoErMedidaTests: a dupla concentrada na sexta contra a dupla que não
        // joga sexta vira o "retardatário" do domingo à noite e um "fora da concentração" — dois
        // achados que apontam pra grade quando o problema é o cadastro. Aqui a pergunta é feita
        // de frente: existe UM horário do torneio em que as duas possam jogar?
        {
            var passo = VagasDaGrade.Duracao(torneio);
            var ultimoDia = (torneio.DataFim ?? (torneio.DataInicio ?? DateTime.Today).AddDays(2)).Date;

            bool Proibido(int duplaId, DateTime quando, bool ehGrupo) =>
                Dentro(impedimentos, duplaId, quando) || (ehGrupo && Dentro(concentracoes, duplaId, quando));

            bool AlgumHorarioServe(Partida jogo, bool ehGrupo)
            {
                for (var h = torneio.AberturaDaGrade; h.Date <= ultimoDia;
                     h = GradeDeJogos.DepoisDe(h, torneio.HoraFimDoDia, torneio.HoraInicioDiasSeguintes, passo))
                {
                    if (!Proibido(jogo.Dupla1Id, h, ehGrupo) && !Proibido(jogo.Dupla2Id, h, ehGrupo)) return true;
                }
                return false;
            }

            string Restricao(int duplaId, bool ehGrupo)
            {
                if (!porId.TryGetValue(duplaId, out var d)) return "sem restrição";
                if (ehGrupo && d.ConcentrarJogosEm is TurnoDeConcentracao turno && turno != TurnoDeConcentracao.Nenhuma)
                    return $"\"{ConcentracaoDeJogos.Rotulo(turno)}\"";
                if (impedimentos.ContainsKey(duplaId))
                    return $"impedimento \"{AlteracaoDeImpedimento.Rotulo(AlteracaoDeImpedimento.TurnoAtual(d))}\"";
                return "sem restrição";
            }

            foreach (var jogo in jogos)
            {
                bool ehGrupo = FasesTorneio.EhFaseDeGrupos(jogo.Fase);
                bool temRestricao = impedimentos.ContainsKey(jogo.Dupla1Id) || impedimentos.ContainsKey(jogo.Dupla2Id)
                    || (ehGrupo && (concentracoes.ContainsKey(jogo.Dupla1Id) || concentracoes.ContainsKey(jogo.Dupla2Id)));
                if (!temRestricao || AlgumHorarioServe(jogo, ehGrupo)) continue;

                achados.Add(new Achado(RestricoesEmConflito,
                    $"{jogo.Fase} de {Nome(jogo.Dupla1Id)} × {Nome(jogo.Dupla2Id)} não tem horário possível: "
                    + $"{Nome(jogo.Dupla1Id)} tem {Restricao(jogo.Dupla1Id, ehGrupo)} e "
                    + $"{Nome(jogo.Dupla2Id)} tem {Restricao(jogo.Dupla2Id, ehGrupo)}. A grade marca onde der — "
                    + "isso se resolve no cadastro das duplas, não no horário.",
                    jogo.HorarioPrevisto));
            }
        }

        // ⚠️ ORDEM TOTAL. Com só (Quando, Regra) o desempate caía na ordem de emissão — que segue a
        // ordem da LISTA de jogos, e o `pares` acima é um dicionário, cuja enumeração segue a
        // inserção. Dois achados da mesma regra no mesmo minuto trocavam de lugar conforme o banco
        // devolvesse os jogos, e a lista do "Conferir grade" se reembaralhava sozinha entre dois
        // F5 — o organizador lendo a mesma grade duas vezes e achando que algo tinha mudado.
        return achados
            .OrderBy(a => a.Quando ?? DateTime.MaxValue)
            .ThenBy(a => a.Regra, StringComparer.Ordinal)
            .ThenByDescending(a => a.Gravidade)
            .ThenBy(a => a.Descricao, StringComparer.Ordinal)
            .ToList();
    }
}
