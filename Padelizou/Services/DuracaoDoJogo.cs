using Padelizou.Models;

namespace Padelizou.Services;

// QUANDO O JOGO ACONTECEU E QUANTO ELE DUROU — a régua única das duas perguntas.
//
// 🗣️ Felipe, 11/09/2026, com as Finalizadas do 2ª Etapa ER PADEL TOUR na tela: *"as
// finalizadas tem q ficar em ordem da mais recente finalizada para mais tempo atras"* e
// *"coloque também o tempo de duração da partida, em baixo do horario previsto, coloque que
// horario começou e que horario terminou mas nao pode ocupar muito a tela"*.
//
// ⚠️ OS DOIS PEDIDOS SÃO O MESMO DEFEITO. A lista já ordenava pelo fim real desde 08/08/2026
// — mas o card mostra o HORÁRIO PREVISTO, e só ele. Quem varre a tela lê "21:20 · 20:30 ·
// 22:05" e conclui que a ordem quebrou, porque a grandeza que ordena não está escrita em
// lugar nenhum. Mostrar começo e fim é o que torna a ordem legível; por isso a ordem e o
// rótulo moram no MESMO arquivo — são a mesma verdade, uma ordenando e a outra aparecendo.
public static class DuracaoDoJogo
{
    // ⚠️ TRÊS PASSOS, e o do meio é o que faltava na lista de Finalizadas (ela parava em
    // `HorarioFimReal ?? HorarioPrevisto`). O jogo que entrou em quadra às 21:30 e teve o
    // placar lançado sem carimbo de fim voltava pro horário do SORTEIO — num torneio por
    // ordem de liberação, uma hora que nunca existiu — e afundava no meio da lista, abaixo
    // de jogos que acabaram antes de ele começar.
    //
    // É a mesma cadeia que EstatisticasService, MvpDoTorneio e EnqueteDoTorneio já usavam;
    // aqui ela vira um lugar só, pra tela do dia do torneio e a aba Jogos não divergirem.
    public static DateTime? Quando(Partida jogo) =>
        jogo.HorarioFimReal ?? jogo.HorarioInicioReal ?? jogo.HorarioPrevisto;

    // A linha compacta debaixo do horário previsto, ou NULO quando não há o que dizer.
    //
    // ⚠️ Nulo é o caso das AGENDADAS, e o mesmo card as desenha: jogo que não entrou em quadra
    // não ganha linha nenhuma. Era o pedido — "nao podemos poluir muito".
    //
    // A duração sai de `Partida.MinutosDecorridos`, a MESMA propriedade do cronômetro do card
    // AO VIVO: duas contas de "quanto durou" é como a tela do fim discorda da tela do meio.
    public static string? Rotulo(Partida jogo)
    {
        var inicio = jogo.HorarioInicioReal;
        var fim = jogo.HorarioFimReal;

        if (inicio is DateTime largada && fim is DateTime encerramento)
        {
            var faixa = $"{largada:HH:mm}–{encerramento:HH:mm}";

            // Carimbos trocados (correção na mão) não viram "-12 min" na tela: os dois
            // horários continuam sendo verdade, a subtração deles não.
            return encerramento < largada ? faixa : $"{faixa} · {jogo.MinutosDecorridos} min";
        }

        if (inicio is DateTime so_largada) return $"começou {so_largada:HH:mm}";
        if (fim is DateTime so_fim) return $"terminou {so_fim:HH:mm}";
        return null;
    }
}
