namespace Padelizou.Services;

// O NOME DA FASE COMO SE LÊ — "Semifinal 2", "Final".
//
// A regra vivia escrita dentro do `_JogoEmLinha.cshtml`, e ganhou um segundo leitor quando a
// arte do jogo pro story passou a precisar do mesmo rótulo (14/09/2026). Duas cópias de um
// rótulo divergem na primeira mudança — e aqui a divergência seria visível de fora: a etiqueta
// da lista dizendo "Semifinal 2" e a arte postada dizendo "Semifinal".
//
// ⚠️ A FINAL NUNCA NUMERA. "Final 1" anuncia que existe uma segunda; o resto do mata-mata
// numera porque é assim que os jogos seguintes citam este ("Vencedor Quartas de Final 2"), e
// sem o número a referência não teria onde ser encontrada. Jogo de grupo não recebe número
// nenhum de quem chama — o nome do grupo já identifica.
public static class FaseNaTela
{
    public static string Rotulo(string? fase, int? numero)
    {
        var nome = (fase ?? "").Trim();
        if (nome.Length == 0 || numero == null) return nome;

        return nome.Equals("Final", StringComparison.OrdinalIgnoreCase) ? nome : $"{nome} {numero}";
    }
}
