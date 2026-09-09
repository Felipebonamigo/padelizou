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
    public const string QuadraFechada = "Quadra fora do horário";
    public const string SemHorario = "Jogo sem horário";

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

            if (!string.IsNullOrEmpty(jogo.NomeQuadra) && !sedes.QuadraAberta(jogo.NomeQuadra, quando))
            {
                achados.Add(new Achado(QuadraFechada,
                    $"{jogo.NomeQuadra} está marcada {quando:dd/MM 'às' HH:mm}, fora do horário "
                    + "em que esse local está disponível.", quando));
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
                if (depois - antes >= duracao || !jaAvisado.Add((antes, depois))) continue;

                achados.Add(new Achado(PessoaEmDoisJogos,
                    $"Alguém está escalado {antes:dd/MM 'às' HH:mm} e de novo {depois:HH:mm} — "
                    + $"os dois jogos se sobrepõem ({duracao.TotalMinutes:0} min de partida) e "
                    + "ninguém joga em duas quadras ao mesmo tempo.", antes));
            }
        }

        return achados.OrderBy(a => a.Quando ?? DateTime.MaxValue).ThenBy(a => a.Regra).ToList();
    }
}
