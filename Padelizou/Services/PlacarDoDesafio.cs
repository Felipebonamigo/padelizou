using Padelizou.Models;

namespace Padelizou.Services;

// O placar do desafio virado texto — o mesmo em toda tela e em todo aviso.
//
// Existe porque o placar aparece em cinco lugares (cartão do mural, meus desafios, histórico,
// aviso de confirmação e aviso de contestação) e cada um formatando do seu jeito é como o
// mesmo jogo passa a ser lido de dois jeitos diferentes na mesma conversa.
public static class PlacarDoDesafio
{
    // "6 x 4" no set único; "2 x 1 (13 x 11)" quando há sets, porque só o número de sets
    // esconde se o jogo foi passeio ou guerra.
    public static string Texto(Desafio desafio)
    {
        var games = $"{desafio.GamesDesafiante ?? 0} x {desafio.GamesDesafiado ?? 0}";

        if (desafio.SetsDesafiante == null && desafio.SetsDesafiado == null) return games;

        return $"{desafio.SetsDesafiante ?? 0} x {desafio.SetsDesafiado ?? 0} ({games})";
    }

    // O placar do ponto de vista de quem está olhando: quem abre "meus desafios" quer ler o
    // próprio número primeiro, e inverter na view seria a segunda cópia da regra.
    public static string TextoParaOLado(Desafio desafio, bool souDesafiante)
    {
        if (souDesafiante) return Texto(desafio);

        var games = $"{desafio.GamesDesafiado ?? 0} x {desafio.GamesDesafiante ?? 0}";

        if (desafio.SetsDesafiante == null && desafio.SetsDesafiado == null) return games;

        return $"{desafio.SetsDesafiado ?? 0} x {desafio.SetsDesafiante ?? 0} ({games})";
    }
}
