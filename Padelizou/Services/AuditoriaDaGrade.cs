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
        foreach (var mesmoHorario in jogos.Where(j => j.HorarioPrevisto != null)
                                          .GroupBy(j => j.HorarioPrevisto!.Value))
        {
            var pessoas = mesmoHorario
                .SelectMany(j => new[] { j.Dupla1Id, j.Dupla2Id })
                .Where(porId.ContainsKey)
                .SelectMany(id => new[] { porId[id].Jogador1Id, porId[id].Jogador2Id })
                .Where(i => i != null)
                .GroupBy(i => i!.Value)
                .Where(g => g.Count() > 1);

            foreach (var pessoa in pessoas)
            {
                achados.Add(new Achado(PessoaEmDoisJogos,
                    $"Alguém está escalado em {pessoa.Count()} jogos {mesmoHorario.Key:dd/MM 'às' HH:mm} "
                    + "— ninguém joga em duas quadras ao mesmo tempo.", mesmoHorario.Key));
            }
        }

        return achados.OrderBy(a => a.Quando ?? DateTime.MaxValue).ThenBy(a => a.Regra).ToList();
    }
}
