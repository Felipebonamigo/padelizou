using Padelizou.Models;

namespace Padelizou.Services;

// O motor do Padelímetro — o nível de habilidade do jogador, de 0 a 1000.
//
// É Elo clássico rodando DIRETO na escala visível (sem número interno escondido):
// expectativa com divisor 400, faixa de categoria com 100 de largura. Regras completas
// em RANKING.md, na raiz do repo — mudou a regra, muda LÁ primeiro.
//
// Esta classe é pura de propósito: recebe números, devolve números, não conhece banco.
// Quem decide QUAIS partidas contam (restrito fora, time fora, W.O. fora) é o
// PadelimetroService — aqui só mora a matemática, pra os testes cobrirem cada conta.
public static class Padelimetro
{
    public const int Minimo = 0;
    public const int Maximo = 1000;

    // K = quanto um jogo pode mexer no número. Alto no começo porque o seed é um chute
    // (a categoria da primeira inscrição) e precisa se corrigir em 1–2 torneios.
    public const int JogosDeCalibracao = 10;
    public const int KCalibrando = 40;
    public const int KEstavel = 20;

    public static int K(int jogosJaContados) =>
        jogosJaContados < JogosDeCalibracao ? KCalibrando : KEstavel;

    public static bool EmCalibracao(int jogosJaContados) => jogosJaContados < JogosDeCalibracao;

    // Nível da dupla = média simples. O forte carrega o fraco na quadra e na conta.
    public static double NivelDaDupla(int jogador1, int jogador2) => (jogador1 + jogador2) / 2.0;

    // Chance esperada de vitória da dupla A contra a dupla B (Elo, divisor 400):
    // 100 pontos de diferença ≈ 64%, 200 ≈ 76%.
    public static double Expectativa(double nivelA, double nivelB) =>
        1.0 / (1.0 + Math.Pow(10.0, (nivelB - nivelA) / 400.0));

    // Ganhar passeando move mais que ganhar no detalhe: 6x0 vale 1,6×, 7x6 vale 1,1×.
    // O teto em 6 de diferença evita que um 9x0 de W.O. disfarçado exploda a conta.
    public static double FatorDeGames(int gamesA, int gamesB) =>
        1.0 + 0.1 * Math.Min(Math.Abs(gamesA - gamesB), 6);

    // A variação de UM jogador numa partida. Cada jogador usa o próprio K — parceiro em
    // calibração anda mais rápido que o veterano do lado, de propósito.
    // venceu: o TIME deste jogador venceu. expectativaDoTime: a chance que o time dele tinha.
    public static int Variacao(int kDoJogador, double fatorDeGames, bool venceu, double expectativaDoTime)
    {
        double resultado = venceu ? 1.0 : 0.0;
        return (int)Math.Round(kDoJogador * fatorDeGames * (resultado - expectativaDoTime),
            MidpointRounding.AwayFromZero);
    }

    public static int Acomodar(int nivel) => Math.Clamp(nivel, Minimo, Maximo);
}
