namespace Padelizou.Services;

// A estrutura da categoria de TIMES: o organizador decide quantos times disputam, em
// quantos grupos e quantos classificam por grupo — e o sistema só aceita conta que fecha.
//
// A regra dura daqui é a do quadro: se classificam G×C times e isso não é potência de 2
// (2, 4, 8, 16), alguém que "se classificou" ficaria de fora do mata-mata na hora de montar
// a chave — uma promessa quebrada descoberta no pior momento, com o time na quadra. Melhor
// recusar a configuração na criação, quando ajustar é de graça.
//
// Dentro do grupo o time funciona igual dupla: todos contra todos, e a mesma Partida.
public static class CategoriaDeTimes
{
    public const int MinimoDeTimes = 2;
    public const int MaximoDeTimes = 32;          // 32 times já é evento de outro tamanho
    public const int MaximoClassificadosPorGrupo = 4;
    public const int MaiorQuadro = 16;            // Oitavas — o nome de fase mais fundo que existe

    // Null = configuração válida. Texto = o que explicar pro organizador.
    public static string? ProblemaNaConfiguracao(int? times, int? grupos, int? classificados)
    {
        if (times is null or < MinimoDeTimes)
            return $"Diga quantos times disputam — no mínimo {MinimoDeTimes}.";
        if (times > MaximoDeTimes)
            return $"O máximo é {MaximoDeTimes} times por categoria.";
        if (grupos is null or < 1)
            return "Diga em quantos grupos os times jogam.";
        if (times < grupos * 2)
            return $"Com {grupos} grupo(s) precisa de pelo menos {grupos * 2} times — grupo de 1 não tem jogo.";
        if (classificados is null or < 1 or > MaximoClassificadosPorGrupo)
            return $"Classificam de 1 a {MaximoClassificadosPorGrupo} times por grupo.";

        // O grupo mais magro precisa ter gente suficiente pra classificar o combinado.
        int menorGrupo = times.Value / grupos.Value;
        if (classificados > menorGrupo)
            return $"Vai ter grupo com só {menorGrupo} times — não dá pra classificar {classificados} dele.";

        int vagas = grupos.Value * classificados.Value;
        if (!EhPotenciaDe2(vagas) || vagas > MaiorQuadro)
            return $"A conta precisa fechar um quadro de mata-mata (2, 4, 8 ou 16 vagas): " +
                   $"{grupos} grupo(s) × {classificados} classificado(s) = {vagas} não fecha. " +
                   "Ajuste os grupos ou os classificados.";

        return null;
    }

    // A mesma régua, na hora do SORTEIO, com os times que existem de verdade — o organizador
    // pode ter prometido 8 e cadastrado 6, e aí a conta que valia pode ter deixado de valer.
    public static string? ProblemaNoSorteio(int timesCadastrados, int? grupos, int? classificados)
        => ProblemaNaConfiguracao(timesCadastrados, grupos, classificados) is { } problema
            ? $"Categoria de times: {problema} Há {timesCadastrados} time(s) cadastrado(s)."
            : null;

    // Tamanho de cada grupo, do primeiro ao último: o resto da divisão engorda os primeiros.
    // Ex.: 10 times em 3 grupos → 4, 3, 3.
    public static int[] TamanhosDosGrupos(int times, int grupos)
    {
        int baseCada = times / grupos, resto = times % grupos;
        return Enumerable.Range(0, grupos).Select(i => baseCada + (i < resto ? 1 : 0)).ToArray();
    }

    // Reparte a lista (já embaralhada por quem chamou — time não tem ranking de pontos)
    // nos grupos, na ordem dos tamanhos.
    public static List<List<T>> Distribuir<T>(IReadOnlyList<T> embaralhados, int grupos)
    {
        var tamanhos = TamanhosDosGrupos(embaralhados.Count, grupos);
        var resultado = new List<List<T>>(grupos);
        int cursor = 0;
        foreach (var tamanho in tamanhos)
        {
            resultado.Add(embaralhados.Skip(cursor).Take(tamanho).ToList());
            cursor += tamanho;
        }
        return resultado;
    }

    // Previsão pra grade: todos contra todos dentro de cada grupo.
    public static int JogosDeGrupo(int times, int grupos)
        => TamanhosDosGrupos(times, grupos).Sum(k => k * (k - 1) / 2);

    // Um quadro de N tem N-1 jogos: cada jogo elimina um, sobra o campeão.
    public static int JogosDeMataMata(int grupos, int classificados)
        => ChaveamentoMataMata.MaiorPotenciaDe2Ate(grupos * classificados) - 1;

    private static bool EhPotenciaDe2(int n) => n > 0 && (n & (n - 1)) == 0;
}
