using Padelizou.Models;

namespace Padelizou.Services;

// AS VAGAS QUE A GRADE TEM PRA OFERECER — um lugar só, porque eram três e discordavam.
//
// ⚠️ O QUE MOTIVOU ISTO (21/08/2026): montar a lista de horários que vai pro
// `GradeDeJogos.Encaixar` é uma receita de quatro passos (quantos slots pedir, descontar as
// vagas que já têm dono, cortar no tamanho certo, e com que duração) e ela estava copiada em
// TRÊS lugares — TorneiosController.Chaves, TorneiosController.Americano e
// Services/RoboDoChaveamento. Os três faziam contas DIFERENTES:
//
//   • Chaves pedia `jogos + margem + intocados` e descontava os intocados;
//   • RoboDoChaveamento pedia `jogos + margem + jaMarcados` e descontava os jaMarcados;
//   • Americano pedia `jogos + margem` e NÃO descontava nada.
//
// Enquanto cada um agendava um pedaço isolado do torneio, a diferença não aparecia. Ela vira
// quadra vazia (ou quadra dobrada) na hora em que dois pedaços passam a dividir a mesma grade —
// que é exatamente pra onde o torneio em mais de um clube caminha.
//
// A regra de ouro do organizador, dita por ele: NENHUMA QUADRA FICA SEM JOGO ATÉ O FIM DO
// TORNEIO. Por isso a receita pede COM SOBRA e corta depois — pedir justo faria a primeira
// vaga descontada virar um buraco no fim da grade.
public static class VagasDaGrade
{
    // A duração de uma partida, do jeito que a grade conta.
    //
    // ⚠️ 0 VIRA 50 — mesma normalização de `GradeDeJogos.Horarios` e de `Encaixar`. Torneio
    // com o tempo zerado existe (o campo aceita), e sem esta conversão a grade nasceria com
    // vagas em cima umas das outras.
    public static int Duracao(Torneio torneio) =>
        torneio.TempoPrevistoPartidaMinutos > 0 ? torneio.TempoPrevistoPartidaMinutos : 50;

    /// <summary>
    /// Os horários livres pra encaixar <paramref name="quantosJogos"/> a partir de
    /// <paramref name="inicio"/>, já descontando as vagas que os jogos de
    /// <paramref name="jaMarcados"/> ocupam.
    /// </summary>
    public static List<DateTime> Montar(Torneio torneio, DateTime inicio, int quantosJogos,
        IEnumerable<Partida>? jaMarcados = null)
    {
        var ocupadas = (jaMarcados ?? Enumerable.Empty<Partida>())
            .Where(p => p.HorarioPrevisto != null)
            .Select(p => p.HorarioPrevisto!.Value)
            .ToList();

        // Folga: sem vaga sobrando, o encaixe não teria como deixar uma quadra vazia pra
        // evitar chamar a mesma pessoa duas vezes seguidas. Ver GradeDeJogos.MargemDeHorarios.
        int margem = GradeDeJogos.MargemDeHorarios(torneio.QuantidadeQuadras);

        var horarios = GradeDeJogos.Horarios(
            inicio,
            torneio.HoraFimDoDia,
            torneio.QuantidadeQuadras,
            Duracao(torneio),
            // Pede a mais justamente porque parte vai ser descontada logo abaixo.
            quantosJogos + margem + ocupadas.Count,
            aberturaDiasSeguintes: torneio.HoraInicioDiasSeguintes);

        // Vaga que já tem dono sai da lista: num recálculo no meio do torneio, os jogos que já
        // rolaram e os que estão em quadra continuam ocupando as quadras deles. Sem este
        // desconto a grade ofereceria cinco quadras num horário em que três já estão jogando.
        //
        // ⚠️ ESTE DESCONTO É POR INSTANTE EXATO, e não substitui a checagem de sobreposição que
        // o `Encaixar` faz: com a grade partindo de um minuto quebrado (o "Refazer grade" das
        // 20h13), nada aqui bate com os jogos das 20h00 e o desconto não remove nada. Quem
        // segura o conflito nessa hora é o `Encaixar`. Aqui é o cinto; lá é o suspensório.
        return GradeDeJogos.Descontando(horarios, ocupadas)
            .Take(quantosJogos + margem)
            .ToList();
    }
}
