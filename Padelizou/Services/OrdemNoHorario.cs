using Padelizou.Models;

namespace Padelizou.Services;

// A ORDEM DAS LINHAS DA ABA JOGOS — e, dentro de um mesmo horário, QUAL VEM PRIMEIRO (10/09/2026).
//
// 🗣️ Felipe, arrumando o domingo do Er: *"quando eu altero um jogo, no mesmo horario, ele nao esta
// trocando a ordem na linha, tem q trocar tambem para q eu possa colocar a ordem que eu quiser"* —
// e, no mesmo fôlego: *"por padrão, se tem semifinal 1 e semifinal 2 no mesmo horario, siga a ordem
// automatica de a 1 vir antes da 2, mas permita q o usuario edite se quiser"*.
//
// 🕳️ A CAUSA ERA A FALTA DE DESEMPATE, NÃO A TROCA. A lista ordenava só por HorarioPrevisto, sobre
// uma lista que veio do Postgres SEM `ORDER BY`: no mesmo horário a ordem era a que o banco
// devolvesse — e ela muda sozinha depois de um UPDATE. E o ⇄ (Services/TrocaDeHorario) troca hora +
// quadra + clube: entre dois jogos do mesmo horário de um torneio "por ordem" (quadra nula, mesmo
// clube) isso é trocar três valores iguais. Não mudava nada porque não havia nada pra mudar.
//
// ⚠️ ESTA É A ÚNICA RÉGUA DE ORDEM DA ABA. A tela desenha por ela, o modal do ⇄ lista por ela e as
// setas ↑↓ acham o vizinho por ela. Duas contas de "quem vem antes" fariam a seta mover o jogo pra
// um lugar diferente do que a tela mostrava — é a mesma lição de ImpactoDaTroca (três lugares
// discordando sobre a mesma troca é pior que não dizer nada).
public static class OrdemNoHorario
{
    // Uma linha da lista de agendados: o jogo REAL ou a PRÉVIA (a eliminatória que ainda não
    // nasceu). As duas ocupam vaga na grade e as duas se movem — por isso a fila é uma só.
    public sealed record Linha(DateTime? Horario, Partida? Jogo, ProximasFasesDaChave.JogoQueVem? Previsto)
    {
        // A posição gravada pelo organizador. Nula = automático.
        public int? Ordem => Jogo != null ? Jogo.OrdemNoHorario : Previsto?.OrdemNoHorario;

        // Como o POST das setas e do ⇄ chama esta linha (Services/ReferenciaDoJogo). Nula só na
        // prévia de projeção antiga, que não sabe a categoria dela — essa não tem botão.
        public ReferenciaDoJogo? Referencia =>
            Jogo is Partida jogo ? ReferenciaDoJogo.Real(jogo.Id)
            : Previsto is { CategoriaId: int categoria } previa ? ReferenciaDoJogo.Prevista(categoria, previa.Fase, previa.Numero)
            : null;
    }

    // O desempate, em ordem de importância:
    //
    //   1. o HORÁRIO — quem separa dois jogos é a hora, antes de tudo;
    //   2. a ORDEM GRAVADA, e o automático (nulo) vai pro fim: a numeração é 1..k a partir do topo
    //      do horário, então "sem número" só pode significar "abaixo dos numerados". É o que faz
    //      um jogo recém-nascido cair no fim em vez de furar a fila que o organizador montou;
    //   3. jogo REAL antes de PRÉVIA — é o que a tela já fazia (a lista concatenava um depois do
    //      outro), e mudar isso sem pedido seria inventar regra;
    //   4. o Id do jogo real. É ele que numera a fase (ReservasDeHorario.NumeroNaFase), então
    //      ordenar por Id É "a Semifinal 1 antes da Semifinal 2", que é o padrão pedido;
    //   5. na prévia, categoria → fase → número, que é a mesma coisa do outro lado.
    private static (DateTime, int, int, int, int, int) Chave(Linha linha) => (
        linha.Horario ?? DateTime.MaxValue,
        linha.Ordem ?? int.MaxValue,
        linha.Jogo != null ? 0 : 1,
        linha.Jogo?.Id ?? linha.Previsto?.CategoriaId ?? int.MaxValue,
        linha.Previsto is { } dePrevia ? FiltroDeJogos.OrdemDaFase(dePrevia.Fase) : 0,
        linha.Previsto?.Numero ?? 0);

    /// <summary>A fila da aba Jogos: os agendados e as prévias, na ordem em que a tela os mostra.</summary>
    public static List<Linha> Ordenar(
        IEnumerable<Partida> agendados, IEnumerable<ProximasFasesDaChave.JogoQueVem> previstos) =>
        agendados.Select(p => new Linha(p.HorarioPrevisto, p, null))
            .Concat(previstos.Select(j => new Linha(j.Horario, null, j)))
            .OrderBy(Chave)
            .ToList();

    /// <summary>
    /// Os números que precisam ser GRAVADOS pra que trocar <paramref name="a"/> com
    /// <paramref name="b"/> dentro do mesmo horário signifique alguma coisa: sem número explícito,
    /// os dois valem "automático" e trocar um pelo outro devolve a mesma fila.
    /// </summary>
    /// <remarks>
    /// ⚠️ SÓ O PREFIXO, até o mais abaixo dos dois. Numerar o horário inteiro fixaria também as
    /// prévias que estão DEBAIXO e que ninguém tocou — e fixar prévia é criar uma reserva
    /// (Models/ReservaDeHorario), que prende o horário dela contra a grade. Quem fica embaixo
    /// segue no automático, e o automático já vem depois do manual (ver <see cref="Chave"/>).
    ///
    /// Vazio quando os dois estão em horários diferentes: ali quem separa é a própria hora, e o
    /// que a troca faz é trocar o slot inteiro.
    /// </remarks>
    public static IEnumerable<(Linha Linha, int Ordem)> Materializar(
        IReadOnlyList<Linha> fila, Linha a, Linha b)
    {
        if (a.Horario is not DateTime horario || b.Horario != horario) yield break;

        var doHorario = fila.Where(l => l.Horario == horario).ToList();
        int deA = doHorario.IndexOf(a);
        int deB = doHorario.IndexOf(b);
        if (deA < 0 || deB < 0) yield break;

        for (int i = 0; i <= Math.Max(deA, deB); i++)
            if (doHorario[i].Ordem != i + 1)
                yield return (doHorario[i], i + 1);
    }
}
