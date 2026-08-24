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

    // CASAL: a mista sem nível. O organizador abre uma categoria só, pra casais de verdade
    // jogarem juntos, e o que decide quem entra não é força — é a vida. Não existe "Casal B"
    // porque não existe casal de 5ª: os dois jogam o que jogam.
    //
    // Pro Padelímetro ela é irmã da mista, e pelo mesmo motivo: a dupla tem um de cada, então
    // o resultado não diz em qual das duas réguas cada um corre. Os jogos contam e medem
    // padel como qualquer outro — o que ela não faz é definir escada nem trancar nível.
    public static bool EhCasal(string? nomeCategoria)
    {
        var n = nomeCategoria ?? "";
        return n.Contains("Casal", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Casais", StringComparison.OrdinalIgnoreCase);
    }

    // LENDAS: categoria de veterania, sem nível — quem entra é pela idade/tempo de padel, não
    // pela força. Uma só, sem separar por sexo nem graduar A/B/C (mesma decisão do Felipe pra
    // Casais). Fora da escada pelo mesmo motivo de Casal/Mista: o campeão dela não é "mais
    // forte" nem "mais fraco" que o de uma categoria numerada, é outro corte de gente.
    //
    // ⚠️ NÃO exige um homem e uma mulher — ao contrário de Casal/Mista, aqui não há par misto
    // nenhum: dois veteranos do mesmo sexo jogam juntos numa boa. Por isso `ForaDaEscada`
    // (que ela integra) não pode ser reaproveitado por `SexoDoJogador.ExigeUmDeCada` — essa
    // checagem lê `EhMista`/`EhCasal` direto, não por aqui.
    public static bool EhLendas(string? nomeCategoria) =>
        (nomeCategoria ?? "").Contains("Lendas", StringComparison.OrdinalIgnoreCase);

    // Categoria que não é degrau de escada nenhuma (mista, casal, lendas...). É por aqui que
    // passam as decisões de RÉGUA — faixa, seed e a inferência de escada — pra que uma
    // categoria nova do mesmo feitio entre num lugar só, em vez de em cinco `if`s espalhados.
    public static bool ForaDaEscada(string? nomeCategoria) =>
        EhMista(nomeCategoria) || EhCasal(nomeCategoria) || EhLendas(nomeCategoria);

    public static bool EhFeminina(string? nomeCategoria) =>
        (nomeCategoria ?? "").Contains("Fem", StringComparison.OrdinalIgnoreCase);

    // A faixa que o NOME da categoria descreve. Nulo pra mista (não tem faixa) e pra
    // nome fora da convenção.
    public static Faixa? DaCategoria(string? nomeCategoria)
    {
        var n = nomeCategoria ?? "";
        if (ForaDaEscada(n)) return null;

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
        // Casal e Lendas não têm letra pra graduar, então nascem no meio do caminho entre as
        // duas réguas — o mesmo lugar da mista sem letra.
        if (EhCasal(nomeCategoria) || EhLendas(nomeCategoria)) return EntradaDeMista("") ?? EntradaNeutra;
        if (EhMista(nomeCategoria)) return EntradaDeMista(nomeCategoria!) ?? EntradaNeutra;
        return DaCategoria(nomeCategoria)?.Entrada ?? EntradaNeutra;
    }

    // A folga da HISTERESE de descida (RANKING.md, "Subir e descer de faixa"): só se
    // volta pra faixa de baixo 50 pontos ABAIXO do piso. É a mesma linha que limita o
    // ajuste de campanha (CampanhaNoPadelimetro) — a campanha te leva ATÉ a porta, nunca
    // através dela — e é de onde a trava da fase 3 vai ler a mesma folga.
    public const int FolgaDeDescida = 50;

    // As duas portas de uma faixa: descer é chegar 50 abaixo do piso; subir é cruzar o
    // teto. Presas às pontas da régua pra faixa de borda não prometer porta que não existe.
    public static int LinhaDeDescida(Faixa faixa) =>
        Math.Max(faixa.Piso - FolgaDeDescida, Padelimetro.Minimo);

    public static int LinhaDeSubida(Faixa faixa) =>
        Math.Min(faixa.Teto + 1, Padelimetro.Maximo);

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
