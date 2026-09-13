namespace Padelizou.Services;

// O resumo de uma escolha e o texto da frase.
//  - `Exceto` verdadeiro: as pílulas são o que a pessoa NÃO aceita, e a frase começa com
//    "Todos os dias, exceto". Quem exibir precisa deixar isso visível — pílula de exceção
//    com a mesma cara de pílula de preferência diz exatamente o contrário do que é.
//  - Lista vazia: nada escolhido, nada a mostrar.
public sealed record ResumoDeHorarios(bool Exceto, IReadOnlyList<string> Pilulas);

// Dias e períodos preferidos do jogador, escritos de forma curta.
//
// 🗣️ Felipe, 13/09/2026, com o print do perfil e catorze pílulas empilhadas: *"tem que fazer
// uma recurso 'Todos os dias, exceto...' e melhor isso"*.
//
// 🔑 O CONJUNTO TEM TAMANHO FIXO — 7 dias × 3 períodos = 21 —, e é isso que faz o truque
// funcionar: o COMPLEMENTO de uma escolha é tão exato quanto ela. Quem aceita vinte das vinte
// e uma combinações está dizendo uma frase de quatro palavras, e a tela mostrava vinte pílulas.
// Por isso "todos, exceto" não precisa de coluna nova nem de migration: é a mesma informação,
// lida do outro lado.
//
// 🔎 E o que encurta a forma DIRETA não é agrupar por dia (o print daria sete pílulas), é
// agrupar os dias que compartilham o MESMO conjunto de períodos — as catorze do print viram
// "Fim de semana · Tarde e Noite" e "Segunda a Sexta · Manhã e Noite".
public static class ResumoDeDiasEHorarios
{
    // 0 = Domingo ... 6 = Sábado, o mesmo mapeamento de `DayOfWeek` que `JogadorDiaHorario` usa.
    public static readonly string[] Dias = { "Domingo", "Segunda", "Terça", "Quarta", "Quinta", "Sexta", "Sábado" };

    // Em ordem cronológica, e não alfabética: é a ordem em que a tabela do editor mostra as
    // colunas, e a ordem em que "Manhã e Noite" precisa sair escrito.
    public static readonly string[] Periodos = { "Manhã", "Tarde", "Noite" };

    public static int TotalDeCombinacoes => Dias.Length * Periodos.Length;

    // Peneira do formulário: "3|Noite" vira (3, "Noite"), e o resto é descartado.
    //
    // ⚠️ Descartar não é capricho. A consulta do aviso compara `d.Periodo == periodo` com os
    // três textos daqui, então "1|Madrugada" nunca casaria com nada — mas CONTARIA: basta uma
    // linha morta pro jogador deixar de ser "sem restrição" e parar de receber convite.
    public static IReadOnlyList<(int Dia, string Periodo)> Normalizar(IEnumerable<string>? valores)
    {
        if (valores == null) return Array.Empty<(int, string)>();

        var normalizadas = new List<(int Dia, string Periodo)>();

        foreach (var valor in valores)
        {
            var partes = (valor ?? "").Split('|');
            if (partes.Length != 2) continue;
            if (!int.TryParse(partes[0], out var dia)) continue;
            if (dia < 0 || dia >= Dias.Length) continue;
            if (!Periodos.Contains(partes[1])) continue;

            var combinacao = (dia, partes[1]);
            if (!normalizadas.Contains(combinacao)) normalizadas.Add(combinacao);
        }

        return normalizadas;
    }

    public static ResumoDeHorarios Resumir(IEnumerable<(int Dia, string Periodo)> escolhas)
    {
        var escolhidas = escolhas
            .Where(e => e.Dia >= 0 && e.Dia < Dias.Length && Periodos.Contains(e.Periodo))
            .ToHashSet();

        if (escolhidas.Count == 0) return new ResumoDeHorarios(false, Array.Empty<string>());
        if (escolhidas.Count == TotalDeCombinacoes) return new ResumoDeHorarios(false, new[] { "Todos os dias e horários" });

        var diretas = Agrupar(escolhidas);
        var faltando = Todas().Where(c => !escolhidas.Contains(c)).ToHashSet();
        var complementares = Agrupar(faltando);

        // Empate fica com a forma direta: ela diz o que a pessoa marcou, sem pedir que quem lê
        // inverta a frase na cabeça. "Exceto" só vale a pena quando encurta de verdade.
        return complementares.Count < diretas.Count
            ? new ResumoDeHorarios(true, complementares)
            : new ResumoDeHorarios(false, diretas);
    }

    private static IEnumerable<(int Dia, string Periodo)> Todas() =>
        Enumerable.Range(0, Dias.Length).SelectMany(dia => Periodos.Select(periodo => (dia, periodo)));

    private static List<string> Agrupar(HashSet<(int Dia, string Periodo)> combinacoes) =>
        Enumerable.Range(0, Dias.Length)
            .Select(dia => (Dia: dia, Periodos: Periodos.Where(p => combinacoes.Contains((dia, p))).ToArray()))
            .Where(linha => linha.Periodos.Length > 0)
            // A chave é o conjunto de períodos do dia: os dias que têm a mesma jornada viram
            // uma pílula só.
            .GroupBy(linha => string.Join("|", linha.Periodos))
            .Select(grupo => (Dias: grupo.Select(l => l.Dia).ToArray(), Periodos: grupo.First().Periodos))
            .OrderBy(grupo => grupo.Dias[0])
            .Select(grupo => $"{TextoDosDias(grupo.Dias)} · {TextoDosPeriodos(grupo.Periodos)}")
            .ToList();

    private static string TextoDosDias(int[] dias)
    {
        if (dias.Length == Dias.Length) return "Todos os dias";
        if (dias.Length == 2 && dias[0] == 0 && dias[1] == 6) return "Fim de semana";

        // "Quarta a Sexta" só a partir de três: com dois, "Quarta e Quinta" é mais curto e não
        // deixa dúvida sobre o meio. Sem volta pela semana de propósito — sexta/sábado/domingo
        // sai listado, porque "Sexta a Domingo" numa lista ordenada por domingo confunde mais
        // do que economiza.
        if (dias.Length >= 3 && dias[^1] - dias[0] == dias.Length - 1)
        {
            return $"{Dias[dias[0]]} a {Dias[dias[^1]]}";
        }

        return Juntar(dias.Select(d => Dias[d]).ToArray());
    }

    private static string TextoDosPeriodos(string[] periodos) =>
        periodos.Length == Periodos.Length ? "Dia inteiro" : Juntar(periodos);

    private static string Juntar(IReadOnlyList<string> partes) =>
        partes.Count == 1
            ? partes[0]
            : string.Join(", ", partes.Take(partes.Count - 1)) + " e " + partes[^1];
}
