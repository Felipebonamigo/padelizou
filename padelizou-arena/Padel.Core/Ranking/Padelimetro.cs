namespace Padel.Core.Ranking;

/// <summary>
/// A matemática do Padelímetro — o nível de 0 a 1000 do Padelizou — portada pro jogo, pura.
/// O online ranqueado (CRONOGRAMA.md, M2: "pode reusar a régua do Padelímetro") usa a MESMA
/// régua que o site usa com jogadores reais: a mesma partida tem que dar o mesmo número aqui e
/// lá, senão o "ranking cruzado" vira duas réguas com o mesmo nome.
/// </summary>
/// <remarks>
/// Fonte da verdade: <c>RANKING.md</c> na raiz do repositório, seção "Trilha A — o Padelímetro".
/// Código de origem: <c>Padelizou/Services/Padelimetro.cs</c> (esta classe é o porte dela, método
/// por método). Mudou a regra lá? Muda aqui também — e o teste que prende o número junto.
/// <para>
/// É Elo clássico rodando DIRETO na escala visível (sem número interno escondido): expectativa
/// com divisor 400, faixas de 100 de largura. <c>double</c> como no Padelizou, e não o
/// <c>float</c> da simulação (D3): aqui não há passo de física, há um número que tem que bater
/// com o do site até o arredondamento.
/// </para>
/// </remarks>
public static class Padelimetro
{
    // RANKING.md "Trilha A": "Um número de 0 a 1000 por jogador". Padelimetro.Minimo/Maximo.
    public const int Minimo = 0;
    public const int Maximo = 1000;

    // RANKING.md "Onde o número nasce (seed)" + FaixasDePadelimetro.EntradaNeutra: quem estreia
    // sem categoria que o descreva nasce no meio da régua. No jogo ninguém se inscreve numa
    // categoria, então é a entrada padrão (ver NivelNoRanking.Estreante).
    public const int EntradaNeutra = 500;

    // RANKING.md "O que move o número": "K = 40 nos primeiros 10 jogos ('em calibração'),
    // K = 20 depois". Padelimetro.JogosDeCalibracao / KCalibrando / KEstavel. Alto no começo
    // porque o seed é um chute e precisa se corrigir rápido.
    public const int JogosDeCalibracao = 10;
    public const int KCalibrando = 40;
    public const int KEstavel = 20;

    // Padelimetro.K — o K de quem já tem esses jogos contados (o K de ANTES do jogo).
    public static int K(int jogosJaContados) =>
        jogosJaContados < JogosDeCalibracao ? KCalibrando : KEstavel;

    // Padelimetro.EmCalibracao — o "selo cinza" do RANKING.md.
    public static bool EmCalibracao(int jogosJaContados) => jogosJaContados < JogosDeCalibracao;

    // RANKING.md "O que move o número": "Nível da dupla = média dos dois". Padelimetro.NivelDaDupla.
    // O forte carrega o fraco na quadra e na conta.
    public static double NivelDaDupla(int jogador1, int jogador2) => (jogador1 + jogador2) / 2.0;

    // RANKING.md "Trilha A": divisor 400 — "100 pontos de diferença ≈ 64%; 200 ≈ 76%".
    // Padelimetro.Expectativa: a chance esperada de vitória da dupla A contra a dupla B.
    public static double Expectativa(double nivelA, double nivelB) =>
        1.0 / (1.0 + Math.Pow(10.0, (nivelB - nivelA) / 400.0));

    // RANKING.md "O que move o número": "1 + 0,1 × min(|diferença de games|, 6) — 6x0 vale 1,6×,
    // 7x6 vale 1,1×". Padelimetro.FatorDeGames. O teto em 6 é o que impede um placar inflado de
    // explodir a conta.
    public static double FatorDeGames(int gamesA, int gamesB) =>
        1.0 + 0.1 * Math.Min(Math.Abs(gamesA - gamesB), 6);

    // O maior fator possível — é o que o abandono cobra (Ranqueamento, "abandono").
    public static double FatorMaximo => FatorDeGames(6, 0);

    // DECISÃO DO JOGO (o site lê um set só — Partida.GamesDupla1/2): o fator de uma partida de N
    // sets é o de UM set com a margem MÉDIA do vencedor por set, (games do vencedor − games do
    // perdedor) ÷ sets, presa entre 0 e 6. Com set único e o vencedor na frente é exatamente o
    // FatorDeGames (a conta do site). Por que não a soma crua com |diferença|: em melhor de 3 quem
    // vence pode ter MENOS games (7-6 0-6 7-6 = 14x18), e o módulo lia a vitória mais apertada que
    // existe como passeio (1,4); e dois 6-3 somavam 12x6, o fator de um 6x0. Margem negativa vira 0
    // (fator 1,0): vencer com menos games é o "no detalhe" máximo — nunca passeio.
    public static double FatorDaPartida(int gamesDoVencedor, int gamesDoPerdedor, int sets)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sets, 1);
        return 1.0 + 0.1 * Math.Clamp((gamesDoVencedor - gamesDoPerdedor) / (double)sets, 0.0, 6.0);
    }

    // RANKING.md "O que move o número": "K_dele × fator_de_games × (resultado − expectativa),
    // arredondada; 1 pra quem venceu, 0 pra quem perdeu — cada um usa o próprio K".
    // Padelimetro.Variacao, com o MESMO arredondamento (AwayFromZero): é o que deixa vencedor e
    // perdedor de K igual com deltas simétricos (+15/−15), e o que faz o número bater com o site.
    public static int Variacao(int kDoJogador, double fatorDeGames, bool venceu, double expectativaDoTime)
    {
        double resultado = venceu ? 1.0 : 0.0;
        return (int)Math.Round(kDoJogador * fatorDeGames * (resultado - expectativaDoTime),
            MidpointRounding.AwayFromZero);
    }

    // Padelimetro.Acomodar: a régua tem pontas.
    public static int Acomodar(int nivel) => Math.Clamp(nivel, Minimo, Maximo);
}
