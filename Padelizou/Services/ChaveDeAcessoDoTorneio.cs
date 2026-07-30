namespace Padelizou.Services;

// A chave que o jogador digita pra se inscrever num torneio restrito.
//
// Nasceu sorteada (6 caracteres tipo K7P2QM) e continua assim quando o organizador não
// escolhe nada — chave sorteada é a mais segura. Mas quem organiza costuma querer uma que
// se diga por telefone e se lembre no grupo do WhatsApp ("virgili10"), e uma chave que a
// pessoa não consegue repetir vira ligação pro organizador.
public static class ChaveDeAcessoDoTorneio
{
    public const int TamanhoMinimo = 4;
    public const int TamanhoMaximo = 20;

    // Sorteada: sem I, O, 0 e 1 — a diferença entre eles não existe quando alguém lê a chave
    // em voz alta ou copia de um print.
    private const string CaracteresSorteio = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Sortear()
    {
        var buffer = new char[6];
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = CaracteresSorteio[Random.Shared.Next(CaracteresSorteio.Length)];
        }
        return new string(buffer);
    }

    // O que o organizador digitou, pronto pra guardar. Espaço nas pontas sai (ele vem colado
    // ao copiar); a caixa é PRESERVADA porque é assim que ele vai mostrar a chave pros
    // jogadores — a conferência na inscrição já ignora maiúsculas dos dois lados.
    public static string? Normalizar(string? escolhida) =>
        string.IsNullOrWhiteSpace(escolhida) ? null : escolhida.Trim();

    // Devolve o motivo da recusa, ou null quando serve. Espaço NO MEIO é recusado de
    // propósito: "copa dos amigos" não sobrevive a ser ditada por telefone nem colada de
    // um print, e a inscrição só faz Trim das pontas.
    public static string? ProblemaCom(string? escolhida)
    {
        var chave = Normalizar(escolhida);
        if (chave == null) return null;   // vazio é válido: cai no sorteio

        if (chave.Length < TamanhoMinimo)
            return $"A chave de acesso precisa de pelo menos {TamanhoMinimo} caracteres.";

        if (chave.Length > TamanhoMaximo)
            return $"A chave de acesso pode ter no máximo {TamanhoMaximo} caracteres.";

        if (chave.Any(char.IsWhiteSpace))
            return "A chave de acesso não pode ter espaços — use uma palavra só.";

        return null;
    }

    // A chave final do torneio: a escolhida, se veio boa; senão, uma sorteada.
    public static string Definir(string? escolhida) =>
        ProblemaCom(escolhida) == null && Normalizar(escolhida) is { } boa ? boa : Sortear();
}
