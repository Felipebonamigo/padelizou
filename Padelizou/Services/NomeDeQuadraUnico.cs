namespace Padelizou.Services;

// O NOME DA QUADRA É A IDENTIDADE DELA DENTRO DO TORNEIO (21/08/2026).
//
// ⚠️ ATÉ AQUI NÃO ERA, e ninguém tinha percebido: `Quadra.Nome` é texto livre, a criação e a
// edição gravam o que o organizador digitar (TorneiosController.Criacao) e não havia checagem
// de repetido em lugar nenhum — nem no C#, nem índice no banco. Dava pra ter duas "Quadra 1"
// no mesmo torneio.
//
// Isso importa porque o resto do sistema TRATA o nome como identidade: `Partida.NomeQuadra` é
// uma string, e é por ela que a grade sabe se a quadra está ocupada
// (Services/GradeDeJogos.Encaixar), que o link de transmissão acha a quadra
// (PartidasController.aplicarLinkNaQuadra) e que o organizador troca um jogo de lugar
// (Services/TrocaDeQuadra). Com nome repetido, todos esses passam a agir sobre a quadra errada
// — em silêncio, porque a string bate.
//
// A régua mora aqui, e não dentro do controller, porque são DUAS portas (criar e editar) e a
// segunda cópia é sempre a que fica pra trás.
public static class NomeDeQuadraUnico
{
    // O motivo pra não gravar, ou null quando a lista está boa.
    //
    // Compara sem diferenciar maiúscula/minúscula e ignorando espaço nas pontas: "Quadra 1" e
    // "quadra 1 " são a mesma quadra pra quem lê a tela, e deixar as duas passar entregaria o
    // mesmo problema com a aparência de estar tudo certo.
    //
    // `maisDeUmClube` muda só a EXPLICAÇÃO, nunca a resposta. Num torneio de duas sedes o
    // organizador cai aqui por um motivo diferente e bem mais natural — os dois clubes têm uma
    // "Quadra 1" —, e a saída dele também é outra: pôr o clube dentro do nome. Sem essa frase a
    // recusa soaria como capricho do sistema, e é o contrário: `Partida.NomeQuadra` é texto
    // solto, então duas "Quadra 1" seriam a MESMA quadra pra grade, que marcaria um jogo em
    // cada clube no mesmo horário achando que tinha dobrado a quadra.
    public static string? MotivoParaNaoSalvar(IEnumerable<string?> nomes, bool maisDeUmClube = false)
    {
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var nome in nomes)
        {
            var limpo = (nome ?? "").Trim();
            if (limpo.Length == 0) continue;   // vazio vira "Quadra A".. no chamador

            if (!vistos.Add(limpo))
            {
                return $"Duas quadras com o mesmo nome (\"{limpo}\"). "
                     + "O nome é como o sistema sabe qual quadra é qual — dê nomes diferentes."
                     + (maisDeUmClube
                         ? " Com mais de um clube, ponha o clube no nome: \"Quadra 1 Nata\" e \"Quadra 1 Batata\"."
                         : "");
            }
        }

        return null;
    }
}
