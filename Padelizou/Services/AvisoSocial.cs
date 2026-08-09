namespace Padelizou.Services;

// O TEXTO dos três avisos que o perfil gera: elogio, comentário e seguidor novo.
//
// Fica fora do controller pelo motivo de sempre — texto de aviso é regra, e regra escrita
// dentro da ação não tem como ser conferida por teste sem subir meio sistema junto.
//
// ⚠️ Os três são BILHETES SOCIAIS: bons de ver, mas ninguém precisa agir por causa deles e
// nenhum perde valor se for lido amanhã. Por isso saem em AlcanceDoAviso.AppSemEmail — nem
// WhatsApp (que é caro) nem e-mail (que é o que faz a pessoa marcar o remetente como lixo,
// e aí ela perde junto o aviso de que a chave saiu).
public record TextoDoAviso(string Titulo, string Corpo);

public static class AvisoSocial
{
    // Quanto do comentário cabe no aviso. O aviso não é o lugar de ler o texto inteiro — é o
    // lugar de saber que ele existe e de quem é. O resto está a um toque de distância.
    private const int TamanhoDoTrecho = 120;

    public static TextoDoAviso Elogio(string? deQuem, string? apelidoDeQuem, string tituloDoElogio) =>
        new("Você recebeu um elogio",
            $"{QuemFoi(deQuem, apelidoDeQuem)} te marcou como \"{tituloDoElogio}\".");

    public static TextoDoAviso Comentario(string? deQuem, string? apelidoDeQuem, string texto) =>
        new("Novo comentário no seu perfil",
            $"{QuemFoi(deQuem, apelidoDeQuem)}: \"{Trecho(texto)}\"");

    // O corpo diz o que MUDA pra pessoa, não só o fato: quem segue passa a receber aviso dos
    // torneios e dos jogos de mata-mata dela (ver TorneiosController e EncerramentoDaPartida).
    public static TextoDoAviso NovoSeguidor(string? deQuem, string? apelidoDeQuem) =>
        new("Você tem um novo seguidor",
            $"{QuemFoi(deQuem, apelidoDeQuem)} começou a te seguir — e vai receber aviso "
            + "quando você se inscrever num torneio ou jogar um mata-mata.");

    // Aviso sem nome não serve pra nada ("alguém te elogiou" só gera a pergunta "quem?"), mas
    // o banco tem pré-cadastro sem nome preenchido — daí o "Alguém" como último recurso.
    private static string QuemFoi(string? nome, string? apelido)
    {
        var comApelido = NomeBonito.ComApelido(nome, apelido);
        return comApelido.Length > 0 ? comApelido : "Alguém";
    }

    // Corta em espaço, não no meio da palavra, e só corta se sobrar bastante coisa — cortar
    // "Jogou muito!" em "Jogou mui…" seria pior que mostrar a frase inteira.
    private static string Trecho(string? texto)
    {
        var limpo = (texto ?? "").Trim();
        if (limpo.Length <= TamanhoDoTrecho) return limpo;

        var pedaco = limpo[..TamanhoDoTrecho];
        var ultimoEspaco = pedaco.LastIndexOf(' ');
        if (ultimoEspaco > TamanhoDoTrecho / 2) pedaco = pedaco[..ultimoEspaco];

        return pedaco.TrimEnd() + "…";
    }
}
