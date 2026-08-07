namespace Padelizou.Services;

// Quanto vale um Americano no ranking DELE — a Trilha C do RANKING.md.
//
// COLOCAÇÃO COM PESO PELO TAMANHO (decisão do Felipe, 07/08/2026). A alternativa era somar
// games, e foi recusada de propósito: um Americano de 20 rodadas renderia mais que dois de 7,
// e aí o ranking passaria a medir quem tem mais tempo livre em vez de quem joga melhor.
//
// A tabela tem a MESMA FORMA da oficial (100/60/40/25/10) porque a pessoa lê os dois no mesmo
// perfil — duas escalas diferentes fariam ela comparar números que não se comparam. O que muda
// é de onde o número vem: no oficial é a fase alcançada, aqui é a colocação final.
public static class PontosDoAmericano
{
    // O Americano de 8 pessoas é a referência: peso 1. É o tamanho mais comum do rodízio de
    // sábado (2 quadras, 7 rodadas) e o número que fecha "cada um com cada um" sem sobra.
    public const int TamanhoDeReferencia = 8;

    // Só as quatro primeiras colocações valem diferente. Da 5ª pra trás é "jogou" — no
    // Americano todo mundo joga contra todo mundo, então terminar em 9º de 16 não é uma
    // conquista menor que terminar em 9º de 12; é a mesma coisa.
    public static int PontosPorColocacao(int colocacao) => colocacao switch
    {
        <= 0 => 0,
        1 => 100,
        2 => 60,
        3 => 40,
        4 => 25,
        _ => 10,
    };

    // Proporcional ao número de gente em quadra: 4 pessoas valem metade, 16 valem o dobro.
    //
    // Linear, e não uma curva: o teto de 16 pessoas (acima disso o Americano passa pelo Felipe)
    // já limita o peso em 2×, então não há a distorção que uma tabela sem teto teria. E peso
    // que se explica numa frase é peso que o organizador confere sozinho.
    public static decimal Peso(int pessoas) =>
        pessoas <= 0 ? 0m : (decimal)pessoas / TamanhoDeReferencia;

    // ⚠️ Arredondamento AwayFromZero, não o padrão do .NET (ToEven): com ToEven, 2,5 vira 2 e
    // 3,5 vira 4 — dois jogadores com a mesma conta receberiam pontos diferentes conforme a
    // paridade, e ninguém consegue explicar isso pra quem perguntar.
    public static int Pontos(int colocacao, int pessoas) =>
        pessoas <= 0 || colocacao <= 0
            ? 0
            : (int)Math.Round(PontosPorColocacao(colocacao) * Peso(pessoas), MidpointRounding.AwayFromZero);

    // O que o organizador paga pra este Americano valer ponto: R$ 5 por pessoa INSCRITA.
    //
    // Fica em constante e não em configuração porque é preço anunciado na tela de criação —
    // mudar sem deploy faria alguém publicar um Americano prometendo um valor e receber outro.
    public const decimal PrecoPorPessoa = 5m;

    public static decimal CustoParaPontuar(int pessoas) =>
        pessoas <= 0 ? 0m : PrecoPorPessoa * pessoas;
}
