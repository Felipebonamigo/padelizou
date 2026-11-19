namespace Padelizou.Services;

// O FILTRO DO "CONFERIR A GRADE" (19/11/2026). 🗣️ Felipe, com 21 pontos na tela da Wallau:
// *"Crie um filtro aqui para exibir ou ocultar os tipos de pontos — 0 jogos de descanso, 1 jogo de
// descanso, jogo retardatário, e os demais que podem ter"*.
//
// O TIPO é a regra do achado, com "Jogos seguidos" partido pelo descanso que sobrou (a `Gravidade`
// do achado: 2 = emendou, 1 = descansou um) — são decisões diferentes pro organizador: o emendado
// quase sempre se mexe, o de um horário de folga quase sempre se aceita.
//
// ⚠️ NO ENDEREÇO (`?ocultar=`), e não só no navegador: recarregar a página ou mandar o link pro
// co-organizador mostra o mesmo recorte. Os achados continuam sendo calculados inteiros — o
// filtro só decide o que a tela desenha.
public static class FiltroDaConferencia
{
    public static string Tipo(AuditoriaDaGrade.Achado a) =>
        a.Regra == AuditoriaDaGrade.JogosSeguidos
            ? $"{AuditoriaDaGrade.JogosSeguidos} · {Math.Max(0, GradeDeJogos.HorariosDeDescanso - a.Gravidade)} de descanso"
            : a.Regra;

    public static List<AuditoriaDaGrade.Achado> Visiveis(IEnumerable<AuditoriaDaGrade.Achado> achados, IEnumerable<string>? ocultos)
    {
        var fora = (ocultos ?? Array.Empty<string>()).ToHashSet(StringComparer.Ordinal);
        return achados.Where(a => !fora.Contains(Tipo(a))).ToList();
    }

    // Os tipos que existem NESTA grade, com quantos casos — os mais numerosos primeiro, como as
    // etiquetas já eram ordenadas.
    public static List<(string Tipo, int Quantos)> Tipos(IEnumerable<AuditoriaDaGrade.Achado> achados) =>
        achados.GroupBy(Tipo)
            .Select(g => (Tipo: g.Key, Quantos: g.Count()))
            .OrderByDescending(t => t.Quantos)
            .ThenBy(t => t.Tipo, StringComparer.Ordinal)
            .ToList();

    // A lista de ocultos depois de clicar no tipo: some se estava, entra se não estava.
    public static string[] Alternar(IEnumerable<string>? ocultos, string tipo)
    {
        var lista = (ocultos ?? Array.Empty<string>()).Distinct().ToList();
        if (!lista.Remove(tipo)) lista.Add(tipo);
        return lista.ToArray();
    }
}
