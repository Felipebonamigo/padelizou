namespace Padelizou.Services;

// A identidade de uma dupla de desafio, independente de quem publicou o anúncio.
//
// O Desafio guarda quatro `JogadorId` soltos (ver o comentário do modelo: reusar a entidade
// `Dupla`, que é casada com Torneio e mata-mata, criaria a segunda cópia de meia dúzia de
// regras de torneio). Só que o ranking precisa saber que "Felipe + Lucas" e "Lucas + Felipe"
// são A MESMA dupla — senão o par que desafiou numa semana e foi desafiado na outra apareceria
// duas vezes na tabela, com metade dos jogos cada.
//
// A chave é o par ORDENADO. Ordenar é o que torna a identidade estável: qualquer outra coisa
// (ordem de digitação, quem criou o anúncio, quem lançou o placar) muda de jogo pra jogo.
public static class ChaveDaDupla
{
    public static string De(int jogadorA, int jogadorB)
    {
        var menor = Math.Min(jogadorA, jogadorB);
        var maior = Math.Max(jogadorA, jogadorB);
        return $"{menor}-{maior}";
    }

    // As duas duplas são a mesma? É o que o anti-farm pergunta: "quantas vezes essas duas já
    // se enfrentaram neste mês?" (ver PontosDoDesafio).
    public static bool Mesma(int a1, int a2, int b1, int b2) => De(a1, a2) == De(b1, b2);
}
