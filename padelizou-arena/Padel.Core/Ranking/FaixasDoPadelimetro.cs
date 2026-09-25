namespace Padel.Core.Ranking;

/// <summary>Uma faixa da régua: o rótulo que a tela mostra ("4ª"), onde ela começa e termina, e
/// onde nasce o número de quem estreia por ela.</summary>
public sealed record FaixaDoPadelimetro(string Rotulo, int Piso, int Teto, int Entrada);

/// <summary>
/// As faixas do Padelímetro e a histerese do rótulo. Fonte: <c>RANKING.md</c>, seções "Faixas e
/// valores de entrada", "Subir e descer de faixa (histerese)" e "O RÓTULO da tela não é a trava
/// (14/09/2026)"; código de origem <c>Padelizou/Services/FaixasDePadelimetro.cs</c>.
/// </summary>
/// <remarks>
/// O que ficou de fora do porte, e por quê (cada um volta quando o jogo tiver o que o justifica):
/// <list type="bullet">
/// <item>A escada FEMININA e as entradas de Mista/Casal/Lendas: o ranqueado não separa ninguém
/// por categoria — é uma régua e uma escada só (a masculina, que é a que o Padelizou usa quando
/// não há categoria: <c>DoNivel(nivel, feminina: false)</c>).</item>
/// <item>A soma da dupla (<c>SomaDaDupla</c>) e a regra do bicampeão: são TRAVA de inscrição em
/// torneio, e o ranqueado não tem inscrição.</item>
/// <item>A campanha (<c>CampanhaNoPadelimetro</c>: +10 campeão, −5/−10 chave): mora no fechamento
/// da final de uma categoria, e o ranqueado não tem categoria nem mata-mata. Entra junto com os
/// torneios online com chave (pós-1.0, CRONOGRAMA.md).</item>
/// </list>
/// </remarks>
public static class FaixasDoPadelimetro
{
    // RANKING.md "Faixas e valores de entrada", tabela masculina; FaixasDePadelimetro.Masculinas.
    // Do topo pra base — DoNivel depende dessa ordem.
    public static IReadOnlyList<FaixaDoPadelimetro> Escada { get; } =
    [
        new("Open", 850, Padelimetro.Maximo, 900),
        new("2ª", 750, 849, 800),
        new("3ª", 650, 749, 700),
        new("4ª", 550, 649, 600),
        new("5ª", 450, 549, 500),
        new("6ª", 350, 449, 400),
        new("7ª", Padelimetro.Minimo, 349, 300),
    ];

    // RANKING.md "O RÓTULO da tela não é a trava": "passada a calibração, o número manda, com
    // folga de 50 PROS DOIS LADOS: sobe com teto + 50, desce com piso − 50".
    // FaixasDePadelimetro.FolgaDoRotulo.
    public const int FolgaDoRotulo = 50;

    // RANKING.md "Subir e descer de faixa (histerese)": descer exige "pelo menos 10 jogos desde
    // que subiu". ⚠️ O Padelizou NÃO liga esta condição no rótulo — lá ela "exige guardar QUANDO
    // cada um subiu, e isso é histórico que o Jogador não tem" (mesma seção do RANKING.md). O jogo
    // nasce sem esse passado, então guarda o contador (NivelNoRanking.JogosNoRotulo) e liga a
    // regra inteira. DECISÃO DO JOGO: a contagem é desde a última TROCA de rótulo, e não só desde
    // a última subida — descer duas faixas seguidas perdendo de propósito também espera 10 jogos
    // entre uma e outra (o mesmo "sem perder de propósito pra descer" do RANKING.md).
    public const int JogosPraDescer = 10;

    // FaixasDePadelimetro.DoNivel: em que faixa CRUA da régua um nível cai. Não é o rótulo — o
    // rótulo tem histerese (RotuloDepoisDoJogo).
    public static FaixaDoPadelimetro DoNivel(int nivel)
    {
        foreach (var faixa in Escada)
            if (nivel >= faixa.Piso) return faixa;
        return Escada[^1];
    }

    /// <summary>
    /// O rótulo depois de um jogo que contou, já com o número e as contagens de DEPOIS dele.
    /// </summary>
    /// <remarks>
    /// Porte de <c>FaixasDePadelimetro.FaixaExibida</c> com uma diferença de âncora: lá a âncora é
    /// a CATEGORIA que a pessoa joga (fixa); no jogo não há categoria, então a âncora é o próprio
    /// rótulo atual — que é o que torna isto histerese de verdade (o rótulo só se move quando o
    /// número sai da folga dele). As comparações são as mesmas de lá: sobe com número &gt; teto + 50,
    /// desce com número &lt; piso − 50.
    /// </remarks>
    public static (FaixaDoPadelimetro Rotulo, int JogosNoRotulo) RotuloDepoisDoJogo(
        FaixaDoPadelimetro rotulo, int jogosNoRotulo, int nivel, int jogos)
    {
        // RANKING.md "O RÓTULO da tela": "Em calibração (menos de 10 jogos), o rótulo é a faixa
        // da CATEGORIA QUE A PESSOA JOGA. O número corre por baixo normalmente; só o rótulo
        // espera." No jogo, a faixa de onde o número nasceu (NivelNoRanking.Estreante).
        if (Padelimetro.EmCalibracao(jogos)) return (rotulo, jogosNoRotulo);

        // Subir não espera (RANKING.md "Subir é imediato") — só precisa passar da folga.
        if (nivel > rotulo.Teto + FolgaDoRotulo) return (DoNivel(nivel), 0);

        // Descer precisa passar da folga E ter jogado 10 jogos com este rótulo.
        if (nivel < rotulo.Piso - FolgaDoRotulo && jogosNoRotulo >= JogosPraDescer)
            return (DoNivel(nivel), 0);

        return (rotulo, jogosNoRotulo);
    }
}
