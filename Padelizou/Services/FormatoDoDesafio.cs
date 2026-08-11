namespace Padelizou.Services;

// Como o desafio vai ser jogado — combinado na PROPOSTA, não na hora de lançar o placar.
//
// Fica gravado no desafio (`Desafio.Formato`) porque é parte do que a outra dupla aceitou:
// "topo jogar sábado às 10h" e "topo jogar três sets sábado às 10h" são convites diferentes.
// Descobrir o formato só depois do jogo é a receita da discussão que vira placar contestado.
//
// ⚠️ Nada aqui conversa com FormatoDaPartida. Aquele traduz FASE DE TORNEIO → sets/games, a
// partir do que o organizador configurou; aqui não há organizador nem fase. Somar as duas
// coisas faria o desafio herdar regra de torneio que ninguém combinou.
public static class FormatoDoDesafio
{
    public const string UmSet = "1 set";
    public const string MelhorDeTres = "Melhor de 3 sets";
    public const string GamesCombinados = "Games combinados";

    public static readonly string[] Todos = { UmSet, MelhorDeTres, GamesCombinados };

    // Formulário antigo em cache, link velho ou dedo no teclado caem no padrão em vez de
    // gravar um formato que a tela do placar depois não sabe desenhar.
    public static string Valido(string? escolhido) =>
        Todos.Contains(escolhido) ? escolhido! : UmSet;

    // Só o melhor de 3 pergunta sets — nos outros dois o placar é games, e um campo de sets
    // ali só serviria pra alguém preencher errado.
    public static bool PedeSets(string? formato) => Valido(formato) == MelhorDeTres;

    // O que a tela do placar explica embaixo dos campos.
    public static string ComoLancar(string? formato) => Valido(formato) switch
    {
        MelhorDeTres => "Marque quantos sets cada dupla fez e o total de games.",
        GamesCombinados => "Marque quantos games cada dupla fez.",
        _ => "Marque quantos games cada dupla fez no set."
    };
}
