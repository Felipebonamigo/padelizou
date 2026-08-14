namespace Padelizou.ViewModels;

// O que o parcial Shared/_FotoDoJogador precisa pra desenhar uma foto com moldura.
//
// Existia (13/08/2026) o <img> da foto copiado em 27 lugares, cada um com o próprio fallback
// pro avatar padrão — e nenhum deles saberia da moldura. Este VM + o parcial são o lugar
// único; a moldura foi a desculpa, a unificação é o ganho.
// `Tamanho` zero = quem dimensiona é uma CLASSE CSS (caso do chip do torneio, que encolhe no
// modo compacto) — nesse caso o parcial NÃO emite o --foto inline, porque estilo inline
// venceria a classe e o chip compacto ficaria grande.
public record FotoDoJogadorVM(string? FotoUrl, string? Moldura, int Tamanho, string Alt = "", string Classe = "")
{
    public static FotoDoJogadorVM De(Padelizou.Models.Jogador? jogador, int tamanho, string classe = "") =>
        new(jogador?.FotoPerfil, jogador?.MolduraEscolhida, tamanho, jogador?.ComoChamar ?? "", classe);
}
