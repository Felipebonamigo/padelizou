namespace Padelizou.Services;

// Os três formatos de torneio, num lugar só.
//
// As comparações viviam soltas — a palavra "Americano" digitada à mão em uma dúzia de arquivos.
// Enquanto isso decidia só qual tela abrir, uma letra errada dava um bug visível. Agora o
// formato decide RANKING (o Americano tem o dele, separado do oficial), e errar a comparação
// passaria a ser ponto entrando na conta errada — em silêncio, que é como o ranking estraga.
public static class FormatoDoTorneio
{
    // Duplas fixas, categorias, grupos e mata-mata. O torneio "oficial".
    public const string Padrao = "Padrao";

    // Inscrição individual, o parceiro troca a cada rodada.
    public const string Americano = "Americano";

    // Dupla fixa, todos contra todos, vence quem somar mais games.
    public const string AmericanoDeDuplas = "AmericanoDuplas";

    // A FAMÍLIA do Americano. Os dois são rodízio — todo mundo joga contra todo mundo, sem
    // chave e sem eliminação —, e é isso que os separa do Oficial: não há "chegar na final",
    // então não há fase alcançada pra virar ponto.
    public static bool EhAmericano(string? formato) =>
        formato is Americano or AmericanoDeDuplas;

    public static bool Existe(string? formato) =>
        formato is Padrao or Americano or AmericanoDeDuplas;
}
