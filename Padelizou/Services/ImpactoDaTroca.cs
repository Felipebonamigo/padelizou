using Padelizou.Models;

namespace Padelizou.Services;

// O QUE ESTA TROCA VAI FAZER COM O CONFERIR GRADE — respondido ANTES de apertar Trocar.
//
// 🗣️ Felipe, 10/09/2026, no modal de trocar horário do Er: *"veja para avisar se o jogo q eu trocar
// altera algo do 'conferir grade', por exemplo, se vai atrapalhar o impedimento, restrição ou jogos
// seguidos"*.
//
// A conta é simples e é a única honesta: auditar a grade, aplicar a troca EM MEMÓRIA, auditar de
// novo, desfazer. O que mudou entre as duas contas é o impacto.
//
// ⚠️ A RÉGUA É A MESMA DAS OUTRAS DUAS TELAS — `AuditoriaDaGrade` pra achar e `ReparoDaGrade.Peso`
// pra separar o que é promessa (duro) do que é conforto (mole). Três lugares dizendo coisas
// diferentes sobre a mesma troca seria pior que não dizer nada: o organizador confiaria no que
// aparecesse primeiro.
public static class ImpactoDaTroca
{
    public enum Nivel
    {
        Impossivel,   // a própria troca é recusada (categoria presa em casa, jogo já começado)
        Perigo,       // cria promessa quebrada: impedimento, gente em dois jogos, quadra fechada
        Atencao,      // cria desconforto: jogos seguidos, concentração, sábado à noite
        Igual,        // nada muda no Conferir grade
        Melhora,      // some algum ponto
    }

    public sealed record Resultado(Nivel Grau, string Texto);

    /// <summary>
    /// O que muda no Conferir grade se <paramref name="a"/> e <paramref name="b"/> trocarem de
    /// slot. Não altera a grade: a troca é aplicada e desfeita.
    /// </summary>
    public static Resultado Avaliar(Torneio torneio, IList<Partida> jogos,
        IReadOnlyCollection<Dupla> duplas, SedesDoTorneio sedes, Partida? a, Partida? b)
    {
        if (TrocaDeHorario.MotivoParaNaoTrocar(a, b, torneio.Id, sedes) is { } motivo)
            return new Resultado(Nivel.Impossivel, motivo);

        var antes = PorRegra(torneio, jogos, duplas, sedes);

        TrocaDeHorario.Trocar(a!, b!);
        var depois = PorRegra(torneio, jogos, duplas, sedes);
        TrocaDeHorario.Trocar(a!, b!);          // desfaz: prever não mexe na grade

        var piorou = new List<string>();
        var melhorou = new List<string>();
        bool duroPiorou = false;

        foreach (var regra in antes.Keys.Union(depois.Keys).OrderByDescending(ReparoDaGrade.Peso))
        {
            int delta = depois.GetValueOrDefault(regra) - antes.GetValueOrDefault(regra);
            if (delta == 0) continue;

            if (delta > 0)
            {
                piorou.Add($"{regra} (+{delta})");
                if (ReparoDaGrade.EhDuro(regra)) duroPiorou = true;
            }
            else
            {
                melhorou.Add($"{regra} ({delta})");
            }
        }

        if (piorou.Count == 0 && melhorou.Count == 0)
            return new Resultado(Nivel.Igual, "Nada muda no Conferir grade.");

        var partes = new List<string>();
        if (piorou.Count > 0) partes.Add("piora: " + string.Join(", ", piorou));
        if (melhorou.Count > 0) partes.Add("melhora: " + string.Join(", ", melhorou));
        var texto = string.Join(" · ", partes);

        if (duroPiorou)
            return new Resultado(Nivel.Perigo, texto);

        if (piorou.Count > 0)
            return new Resultado(Nivel.Atencao, texto);

        return new Resultado(Nivel.Melhora, texto);
    }

    // Quantos achados de cada regra, ignorando os que não pesam (o cadastro em conflito, que
    // nenhuma troca de horário resolve — ver ReparoDaGrade).
    private static Dictionary<string, int> PorRegra(Torneio torneio, IList<Partida> jogos,
        IReadOnlyCollection<Dupla> duplas, SedesDoTorneio sedes) =>
        AuditoriaDaGrade.Conferir(torneio, jogos.ToList(), duplas, sedes)
            .Where(a => ReparoDaGrade.Peso(a.Regra) > 0)
            .GroupBy(a => a.Regra)
            .ToDictionary(g => g.Key, g => g.Count());
}
