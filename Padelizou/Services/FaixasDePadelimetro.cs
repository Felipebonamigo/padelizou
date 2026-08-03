namespace Padelizou.Services;

// As faixas de categoria do Padelímetro — a régua única 0–1000 (RANKING.md).
//
// Escala ÚNICA para todo mundo: é o que deixa uma mulher jogar categoria masculina sem
// regra especial. As faixas femininas ficam mais abaixo na MESMA régua; o degrau é
// provisório (chute educado) e vai sendo calibrado pelos jogos de MISTA, que cruzam as
// duas populações — diferente do concorrente, que cravou o offset por decreto.
//
// O nome da categoria é texto livre do organizador, então o reconhecimento é por trecho,
// na MESMA convenção do TrofeuDeMaterial ("Open"/"1ª", "2ª".."7ª", "Iniciantes",
// "Mista"/"Misto") + "Fem" para decidir a régua. Duas convenções divergiriam no dia em
// que uma categoria nova entrasse.
public static class FaixasDePadelimetro
{
    // Piso/Teto delimitam a faixa na régua; Entrada é onde o número NASCE (seed) pra quem
    // estreia por essa categoria; SomaDaDupla é o segundo porteiro — nulo = sem teto de
    // soma (topo e base da escada não precisam dele).
    public sealed record Faixa(string Rotulo, string Escada, int Piso, int Teto, int Entrada, int? SomaDaDupla);

    // A soma segue o padrão 2×teto−50: dois jogadores no TOPO da mesma faixa não jogam
    // juntos nela — ou um acha parceiro mais leve, ou a dupla sobe de categoria.
    private static readonly Faixa[] Masculinas =
    {
        new("Open", "masculina", 850, Padelimetro.Maximo, 900, null),
        new("2ª", "masculina", 750, 849, 800, 1650),
        new("3ª", "masculina", 650, 749, 700, 1450),
        new("4ª", "masculina", 550, 649, 600, 1250),
        new("5ª", "masculina", 450, 549, 500, 1050),
        new("6ª", "masculina", 350, 449, 400, 850),
        new("7ª", "masculina", Padelimetro.Minimo, 349, 300, null),
    };

    private static readonly Faixa[] Femininas =
    {
        new("Open", "feminina", 550, Padelimetro.Maximo, 600, null),
        new("2ª", "feminina", 450, 549, 500, 1050),
        new("3ª", "feminina", 350, 449, 400, 850),
        new("4ª", "feminina", 250, 349, 300, 650),
        new("5ª", "feminina", 150, 249, 200, 450),
        new("6ª", "feminina", 60, 149, 120, null),
        new("7ª", "feminina", Padelimetro.Minimo, 59, 40, null),
    };

    // Mista não tem faixa de trava (mesma filosofia do troféu de vidro: misto é OUTRO
    // jogo, não um degrau da escada) — mas quem ESTREIA por uma mista precisa nascer em
    // algum lugar. Valores entre as réguas masculina e feminina, porque a dupla tem um
    // de cada.
    private static int? EntradaDeMista(string nome)
    {
        if (nome.Contains(" A")) return 550;
        if (nome.Contains(" B")) return 450;
        if (nome.Contains(" C")) return 350;
        if (nome.Contains(" D")) return 250;
        return 400; // mista sem letra: meio do caminho
    }

    // Categoria fora de qualquer convenção ("45+", "Sub-14", nome inventado): o jogo
    // conta e mede padel do mesmo jeito — só o seed fica neutro, no meio da régua.
    public const int EntradaNeutra = 500;

    public static bool EhMista(string? nomeCategoria)
    {
        var n = nomeCategoria ?? "";
        return n.Contains("Mista") || n.Contains("Misto");
    }

    public static bool EhFeminina(string? nomeCategoria) =>
        (nomeCategoria ?? "").Contains("Fem", StringComparison.OrdinalIgnoreCase);

    // A faixa que o NOME da categoria descreve. Nulo pra mista (não tem faixa) e pra
    // nome fora da convenção.
    public static Faixa? DaCategoria(string? nomeCategoria)
    {
        var n = nomeCategoria ?? "";
        if (EhMista(n)) return null;

        var escada = EhFeminina(n) ? Femininas : Masculinas;
        if (n.Contains("Open") || n.Contains("1ª")) return escada[0];
        if (n.Contains("2ª")) return escada[1];
        if (n.Contains("3ª")) return escada[2];
        if (n.Contains("4ª")) return escada[3];
        if (n.Contains("5ª")) return escada[4];
        if (n.Contains("6ª")) return escada[5];
        if (n.Contains("7ª") || n.Contains("Iniciante")) return escada[6];
        return null;
    }

    // Onde nasce o Padelímetro de quem estreia por esta categoria.
    public static int Entrada(string? nomeCategoria)
    {
        if (EhMista(nomeCategoria)) return EntradaDeMista(nomeCategoria!) ?? EntradaNeutra;
        return DaCategoria(nomeCategoria)?.Entrada ?? EntradaNeutra;
    }

    // Em que faixa da régua um nível cai (pra pílula "620 · faixa da 4ª").
    public static Faixa DoNivel(int nivel, bool feminina)
    {
        var escada = feminina ? Femininas : Masculinas;
        foreach (var faixa in escada)
            if (nivel >= faixa.Piso) return faixa;
        return escada[^1];
    }

    // Quanto falta pra cruzar o teto da faixa atual — o "faltam 40 pra 4ª" do perfil.
    // Nulo já no topo da escada.
    public static int? FaltaPraSubir(int nivel, bool feminina)
    {
        var faixa = DoNivel(nivel, feminina);
        return faixa.Teto >= Padelimetro.Maximo ? null : faixa.Teto + 1 - nivel;
    }
}
