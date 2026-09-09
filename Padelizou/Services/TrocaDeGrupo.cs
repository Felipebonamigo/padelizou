using Padelizou.Models;

namespace Padelizou.Services;

// Troca DUAS duplas de grupo entre si, decidida pelo organizador em cima do sorteio que acabou
// de sair. Pedido do Felipe (09/09/2026): "permita também, que o organizador, troque a dupla de
// lugar no grupo".
//
// ⚠️ É um SWAP, e não um "mover pro grupo X", de propósito: mover uma dupla só desbalancearia
// os grupos — a categoria de 16 duplas que fecha em 2,2,3,3,3,3 viraria 1,2,3,3,3,4 —, e esse
// desenho é o que o organizador escolheu na criação (QuantidadeGrupos/ClassificadosPorGrupo).
// Trocando duas, todo grupo termina com o mesmo tamanho com que começou.
//
// ⚠️ E O CONJUNTO DE CONFRONTOS TAMBÉM FICA DE PÉ, sem regerar partida nenhuma: dentro do
// grupo é todos-contra-todos, então "trocar de lugar" é literalmente trocar os Ids das duas nos
// jogos que já existem. Cada uma herda os adversários da outra, e os jogos guardam o horário e
// a quadra que já tinham — o que muda de dono é o slot, não o calendário.
//
// Irmão de TrocaDeHorario, e pela mesma razão de existir: o sorteio acerta a conta, mas quem
// organiza conhece a vida. A diferença é que lá se troca o SLOT entre dois jogos e aqui se
// troca a DUPLA entre dois grupos — por isso a grade precisa ser refeita depois (ver
// TorneiosController.Chaves.TrocarDuplasDeGrupo), o que TrocaDeHorario nunca precisa.
public static class TrocaDeGrupo
{
    // Null = pode trocar. Texto = o motivo, na língua de quem organiza.
    public static string? MotivoParaNaoTrocar(Dupla? a, Dupla? b)
    {
        if (a == null || b == null) return "Não encontrei uma das duplas.";
        if (a.Id == b.Id) return "Escolha duas duplas diferentes.";

        // Categoria é onde a dupla se inscreveu e é o que define o nível dela — trocar entre
        // categorias não é "mudar de grupo", é remontar a inscrição.
        if (a.CategoriaId != b.CategoriaId) return "As duas duplas precisam ser da mesma categoria.";

        if (a.Grupo == null || b.Grupo == null)
            return "Uma das duplas não está em nenhum grupo — só dá pra trocar depois do sorteio.";

        // Sem isto a troca refaria a grade INTEIRA do torneio (mexendo no horário de todo
        // mundo) pra não mudar confronto nenhum: dentro de um grupo de todos-contra-todos, as
        // duas já jogam contra exatamente as mesmas duplas.
        if (a.Grupo == b.Grupo) return "As duas já estão no mesmo grupo — não há o que trocar.";

        return null;
    }

    // A troca em si: a letra, a FK do grupo e o lugar das duas nos jogos que já existem.
    //
    // ⚠️ `Grupo` (a letra) e `GrupoTorneioId` (a FK) ANDAM JUNTOS. Metade das telas lê um,
    // metade lê o outro — deixá-los fora de sincronia é o que quebrou a Mesa de Controle em
    // 31/07, e aqui não daria erro nenhum: daria grupo com 4 duplas numa tela e 2 na outra.
    public static void Trocar(Dupla a, Dupla b, IEnumerable<Partida> jogos)
    {
        (a.Grupo, b.Grupo) = (b.Grupo, a.Grupo);
        (a.GrupoTorneioId, b.GrupoTorneioId) = (b.GrupoTorneioId, a.GrupoTorneioId);

        // A `Fase` do jogo ("Grupo A") NÃO muda: o jogo continua sendo daquele grupo, quem
        // mudou foi quem o disputa. As duas estão em grupos diferentes (garantido acima), então
        // nenhum jogo tem as duas ao mesmo tempo e o `else if` nunca perde uma troca.
        foreach (var jogo in jogos)
        {
            if (jogo.Dupla1Id == a.Id) jogo.Dupla1Id = b.Id;
            else if (jogo.Dupla1Id == b.Id) jogo.Dupla1Id = a.Id;

            if (jogo.Dupla2Id == a.Id) jogo.Dupla2Id = b.Id;
            else if (jogo.Dupla2Id == b.Id) jogo.Dupla2Id = a.Id;
        }
    }
}
