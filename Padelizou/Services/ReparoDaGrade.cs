using Padelizou.Models;

namespace Padelizou.Services;

// A PASSADA DE REPARO — o que o encaixe guloso não consegue enxergar.
//
// 🗣️ Felipe, 10/09/2026: *"temos q pensar melhor esse botão q ele seja mais inteligente, por que
// hoje ele refaz tudo e as vezes deixa impedimentos ainda [...] tem algo que o sistema esta
// fazendo que esta deixando a desejar no sorteio"*.
//
// 🕳️ POR QUE O GULOSO DEIXA IMPEDIMENTO PASSAR, e por que isso não é um defeito dele.
// `GradeDeJogos.Encaixar` anda vaga por vaga e NUNCA VOLTA ATRÁS. Quando as vagas restantes ficam
// menos que os jogos da fila, ele cai no último recurso e marca o jogo onde der — inclusive dentro
// do impedimento da dupla, porque um jogo ruim é melhor que um jogo sem horário (e foi o Felipe
// quem decidiu isso). No instante da decisão ele está certo. O que ele não pode saber é que DEZ
// VAGAS ATRÁS havia um jogo sem restrição nenhuma que teria cabido ali.
//
// Este arquivo olha a grade INTEIRA depois de pronta e desfaz exatamente esse tipo de nó: troca
// dois jogos de slot (a mesma troca da tela, `TrocaDeHorario.Trocar`) sempre que a troca melhora o
// conjunto. É o oposto do "Refazer grade" — nada é jogado fora, o que já está bom não se mexe, e
// o ajuste que o organizador fez na mão sobrevive se ainda for o melhor lugar.
//
// ── A RÉGUA É A DA TELA ──────────────────────────────────────────────────────────────────────
// O custo sai de `AuditoriaDaGrade.Conferir`, o mesmo do "Conferir grade". Reescrever as regras
// aqui criaria duas verdades: o reparo diria "ficou ótimo" e a tela mostraria dez pontos.
//
// ── QUEM CEDE, E EM QUE ORDEM ────────────────────────────────────────────────────────────────
// 🗣️ *"a prioridade é impedimento por que é pago, aquele de 'os 2 jogos na sexta a noite' é só se
// der"*. Daí os dois níveis:
//
//   • DUROS — pessoa em dois jogos ao mesmo tempo, impedimento, quadra fechada, jogo fora das
//     datas, fase fora de ordem. O reparo NUNCA aceita uma troca que aumente o custo duro, nem
//     pra zerar dez jogos seguidos. É a promessa que foi feita (e paga) ao jogador.
//   • MOLES — concentração ("se der"), sábado à noite, jogos seguidos, retardatário. Cedem entre
//     si pelo peso.
//
// `RestricoesEmConflito` pesa ZERO de propósito: é o cadastro que não fecha (dupla que só joga
// sexta contra dupla que não joga sexta), e nenhuma troca de horário resolve isso. Pesar faria o
// reparo caçar um alvo que não existe.
public static class ReparoDaGrade
{
    // O que o botão conta pro organizador.
    public sealed record Resultado(int Trocas, int AchadosAntes, int AchadosDepois);

    // ⚠️ Os pesos DUROS são comparados como um bloco só (ver `Custo`), então a ordem entre eles
    // também vale: trocar um impedimento por uma fase fora de ordem baixa o duro e é aceito;
    // o contrário sobe e é recusado.
    public static int Peso(string regra) => regra switch
    {
        AuditoriaDaGrade.SemHorario => 100_000,
        AuditoriaDaGrade.PessoaEmDoisJogos => 50_000,
        AuditoriaDaGrade.Impedimento => 20_000,
        AuditoriaDaGrade.QuadraFechada => 10_000,
        AuditoriaDaGrade.DepoisDoFim => 8_000,
        AuditoriaDaGrade.FaseForaDeOrdem => 4_000,

        AuditoriaDaGrade.Concentracao => 100,
        AuditoriaDaGrade.NoiteDeSabado => 100,
        AuditoriaDaGrade.JogosSeguidos => 10,
        AuditoriaDaGrade.Retardatario => 5,

        // O cadastro que não fecha: nenhuma troca resolve, então não vira alvo.
        _ => 0,
    };

    // Duro = promessa feita (e paga) ao jogador, ou impossibilidade física. Nunca pode piorar —
    // nem no reparo, nem numa troca na mão (Services/ImpactoDaTroca avisa em vermelho).
    public static bool EhDuro(string regra) => Peso(regra) >= 1_000;

    // O peso de UM achado: o da regra vezes o QUADRADO da gravidade. 🕳️ Sem o quadrado, "0 de
    // folga" e "1 de folga" pesavam igual, e o reparo EMENDAVA os dois jogos de uma dupla pra
    // apagar dois avisos de "1 de folga" de duplas diferentes — 8 pontos viravam 6 e três pessoas
    // jogavam sem sair da quadra (10/09/2026, ReparoDaGradeTests). Faltar dois horários tem que
    // custar mais que o dobro de faltar um.
    private static int Peso(AuditoriaDaGrade.Achado achado) =>
        Peso(achado.Regra) * achado.Gravidade * achado.Gravidade;

    // Quantas rodadas de melhoria no máximo. Cada uma percorre os jogos doentes; na prática duas
    // ou três bastam, e o teto existe pra que um empate patológico não vire laço infinito.
    private const int MaximoDePassadas = 6;

    /// <summary>
    /// Troca jogos de slot enquanto isso melhorar a grade, sem refazer nada. Mexe apenas nos
    /// <see cref="Partida.HorarioPrevisto"/>/<see cref="Partida.NomeQuadra"/>/
    /// <see cref="Partida.ClubeId"/> dos jogos ainda agendados. Quem chama grava.
    /// </summary>
    public static Resultado Reparar(Torneio torneio, IList<Partida> jogos,
        IReadOnlyCollection<Dupla> duplas, SedesDoTorneio sedes)
    {
        var (duroInicial, moleInicial, achadosInicial) = Custo(torneio, jogos, duplas, sedes);
        int trocas = 0;

        var (duro, mole) = (duroInicial, moleInicial);

        for (int passada = 0; passada < MaximoDePassadas; passada++)
        {
            bool melhorou = false;

            // Só os jogos que APARECEM em algum achado são candidatos a sair do lugar: a grade
            // limpa não é mexida, e o organizador reconhece a grade dele depois do botão.
            foreach (var doente in Doentes(torneio, jogos, duplas, sedes))
            {
                foreach (var outro in EmOrdemEstavel(jogos))
                {
                    if (ReferenceEquals(doente, outro)) continue;
                    if (!MesmoPosto(doente, outro)) continue;
                    if (TrocaDeHorario.MotivoParaNaoTrocar(doente, outro, torneio.Id, sedes) != null) continue;

                    TrocaDeHorario.Trocar(doente, outro);
                    var (novoDuro, novoMole, _) = Custo(torneio, jogos, duplas, sedes);

                    // Duro nunca piora — nem em troca de dez confortos. Empatado o duro, vale o mole.
                    bool vale = novoDuro < duro || (novoDuro == duro && novoMole < mole);
                    if (!vale)
                    {
                        TrocaDeHorario.Trocar(doente, outro);       // desfaz
                        continue;
                    }

                    (duro, mole) = (novoDuro, novoMole);
                    trocas++;
                    melhorou = true;
                    break;                                          // este jogo já achou lugar
                }
            }

            if (!melhorou) break;
        }

        var (_, _, achadosFinal) = Custo(torneio, jogos, duplas, sedes);
        return new Resultado(trocas, achadosInicial, achadosFinal);
    }

    // O custo em dois blocos (duro, mole) e a contagem de achados que PESAM — o número que o
    // organizador vê cair. Os de peso zero (cadastro em conflito) ficam de fora da conta: dizer
    // "3 pontos resolvidos" quando um deles é insolúvel seria mentir pra ele.
    private static (int Duro, int Mole, int Achados) Custo(Torneio torneio, IEnumerable<Partida> jogos,
        IReadOnlyCollection<Dupla> duplas, SedesDoTorneio sedes)
    {
        int duro = 0, mole = 0, achados = 0;

        foreach (var achado in AuditoriaDaGrade.Conferir(torneio, jogos.ToList(), duplas, sedes))
        {
            int peso = Peso(achado);
            if (peso == 0) continue;

            achados++;
            if (EhDuro(achado.Regra)) duro += peso; else mole += peso;
        }

        return (duro, mole, achados);
    }

    // Os jogos citados por algum achado que pesa, do mais grave pro menos. O achado não guarda o
    // jogo — guarda o HORÁRIO —, e é por ele que se acha quem está no nó: todo jogo daquele
    // instante é candidato, porque a troca é entre slots.
    private static List<Partida> Doentes(Torneio torneio, IList<Partida> jogos,
        IReadOnlyCollection<Dupla> duplas, SedesDoTorneio sedes)
    {
        var pesoPorHorario = new Dictionary<DateTime, int>();

        foreach (var achado in AuditoriaDaGrade.Conferir(torneio, jogos.ToList(), duplas, sedes))
        {
            int peso = Peso(achado);
            if (peso == 0 || achado.Quando is not DateTime quando) continue;

            pesoPorHorario[quando] = pesoPorHorario.GetValueOrDefault(quando) + peso;
        }

        return jogos
            .Where(j => j.HorarioPrevisto is DateTime h && pesoPorHorario.ContainsKey(h))
            .OrderByDescending(j => pesoPorHorario[j.HorarioPrevisto!.Value])
            .ThenBy(j => j.HorarioPrevisto)
            .ThenBy(j => j.Codigo, StringComparer.Ordinal)
            .ToList();
    }

    // ⚠️ SÓ TROCA JOGO DO MESMO POSTO DE FASE — grupo com grupo, semifinal com semifinal
    // (10/09/2026). 🕳️ Sem isto o reparo trocou uma semifinal emendada com a final por um jogo de
    // grupo de duas horas antes: pra régua da tela o jogo de grupo que foi parar depois é só um
    // "retardatário" (5 pontos — o bloco dos grupos "fecha" sem ele, ver OrdemDasFases.FimDoBloco),
    // e a semifinal no horário dele não é "fase fora de ordem". 40 − 5, negócio fechado — e o
    // torneio jogando uma eliminatória antes de fechar as chaves, exatamente o que o Felipe mandou
    // nunca fazer (🗣️ *"a ordem é colocar todos jogos de chave antes"*). Trocando só dentro do
    // posto, o conjunto de horários de cada posto não muda, e a ordem das fases fica como o
    // encaixe deixou. (ReparoDaGradeTests.Nao_troca_uma_eliminatoria_com_um_jogo_de_grupo)
    private static bool MesmoPosto(Partida a, Partida b) =>
        OrdemDasFases.Posto(a.Fase) == OrdemDasFases.Posto(b.Fase);

    // ⚠️ ORDEM ESTÁVEL, E ISSO É CORREÇÃO, NÃO ESTILO (10/09/2026). O reparo é guloso: aceita a
    // PRIMEIRA troca que melhora, então a ordem em que os candidatos aparecem muda o resultado. O
    // sorteio entrega a lista na ordem da fila e o "Refazer grade" na ordem do Id — sem esta
    // ordenação as duas grades saem diferentes, que é o defeito consertado no PR #118
    // (GradeDoErMedidaTests.Refazer_grade_sem_nada_mudado_reproduz_a_grade_do_sorteio).
    //
    // Por HORÁRIO e depois por `Codigo`: o Id não serve — no sorteio os jogos ainda não foram
    // gravados e são todos zero. O `Codigo` já existe ali e é o mesmo nas duas passagens.
    private static IEnumerable<Partida> EmOrdemEstavel(IEnumerable<Partida> jogos) =>
        jogos.OrderBy(j => j.HorarioPrevisto ?? DateTime.MaxValue)
             .ThenBy(j => j.Codigo, StringComparer.Ordinal);
}
