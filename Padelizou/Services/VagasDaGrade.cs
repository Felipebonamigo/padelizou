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
        IEnumerable<Partida>? jaMarcados = null, DateTime? peloMenosAte = null,
        SedesDoTorneio? sedes = null)
    {
        var ocupadas = (jaMarcados ?? Enumerable.Empty<Partida>())
            .Where(p => p.HorarioPrevisto != null)
            .Select(p => p.HorarioPrevisto!.Value)
            .ToList();

        // Folga: sem vaga sobrando, o encaixe não teria como deixar uma quadra vazia pra
        // evitar chamar a mesma pessoa duas vezes seguidas. Ver GradeDeJogos.MargemDeHorarios.
        int margem = GradeDeJogos.MargemDeHorarios(torneio.QuantidadeQuadras);
        int quantas = quantosJogos + margem;

        // ⚠️ `peloMenosAte` É PRA CONCENTRAÇÃO ("os 2 jogos no sábado à tarde", 08/09/2026), e
        // sem ele o favor sai calado. Toda outra restrição de horário tira UMA janela de muitas
        // e sempre sobra grade adiante; a concentração tira TODAS menos uma. Quando a lista
        // acaba antes do turno escolhido, o `Encaixar` cai no último recurso e marca a dupla
        // onde der — o organizador prometeu sexta e a pessoa joga no sábado.
        //
        // E a lista acaba mesmo: `jogos + margem` num fim de semana mal passa da manhã de
        // sábado, porque a sexta abre às 18h e come as primeiras rodadas.
        //
        // Omitido — o caso de todo torneio sem ninguém concentrado — a conta é EXATAMENTE a de
        // sempre, e nenhuma grade existente muda de tamanho.
        if (peloMenosAte is DateTime limite && limite > inicio)
        {
            // Teto de 14 dias de grade cheia: existe só pra que um limite absurdo não vire uma
            // lista gigante. Na prática o laço para no primeiro horário que alcança o limite —
            // e o limite é sempre o fim de um turno do próprio torneio.
            int teto = Math.Max(torneio.QuantidadeQuadras, 1) * (24 * 60 / Duracao(torneio)) * 14;

            int cabem = 0;
            foreach (var h in GradeDeJogos.Horarios(inicio, torneio.HoraFimDoDia,
                         torneio.QuantidadeQuadras, Duracao(torneio), teto,
                         aberturaDiasSeguintes: torneio.HoraInicioDiasSeguintes))
            {
                cabem++;
                if (h >= limite) break;
            }

            // NUNCA ENCOLHE: quem já pedia mais vagas que o alcance continua com as que pedia.
            quantas = Math.Max(quantas, cabem);
        }

        var horarios = GradeDeJogos.Horarios(
            inicio,
            torneio.HoraFimDoDia,
            torneio.QuantidadeQuadras,
            Duracao(torneio),
            // Pede a mais justamente porque parte vai ser descontada logo abaixo — e, com
            // quadra de horário limitado, também porque parte das vagas nasce morta e o laço
            // logo abaixo precisa ter o que percorrer até juntar `quantas` vagas ÚTEIS.
            quantas + ocupadas.Count + (sedes != null ? quantas : 0),
            aberturaDiasSeguintes: torneio.HoraInicioDiasSeguintes);

        // Vaga que já tem dono sai da lista: num recálculo no meio do torneio, os jogos que já
        // rolaram e os que estão em quadra continuam ocupando as quadras deles. Sem este
        // desconto a grade ofereceria cinco quadras num horário em que três já estão jogando.
        //
        // ⚠️ ESTE DESCONTO É POR INSTANTE EXATO, e não substitui a checagem de sobreposição que
        // o `Encaixar` faz: com a grade partindo de um minuto quebrado (o "Refazer grade" das
        // 20h13), nada aqui bate com os jogos das 20h00 e o desconto não remove nada. Quem
        // segura o conflito nessa hora é o `Encaixar`. Aqui é o cinto; lá é o suspensório.
        var livres = GradeDeJogos.Descontando(horarios, ocupadas);

        // ⚠️ QUADRA FECHADA CONTINUA OCUPANDO LUGAR NA LISTA (08/09/2026, com o local externo
        // alugado por hora). Cada rodada rende uma vaga POR QUADRA CADASTRADA, inclusive pelas
        // que estão fechadas naquele horário — e `Take(quantas)` conta as mortas junto. Com
        // metade das quadras alugadas só pra sábado de manhã, metade das vagas da sexta é
        // morta, o orçamento acaba antes dos jogos, e o `Encaixar` entra com o jogo numa vaga
        // sem quadra aberta: ele nasce COM HORA E SEM QUADRA (o incidente do Interno de
        // 05/08/2026, por outra porta). Medido em SedeExtraNoSorteioTests.
        //
        // Aqui as mortas são CONTADAS À PARTE: elas continuam na lista (o `Encaixar` sabe pular
        // vaga que não serve), mas não consomem o orçamento. Sem quadra nenhuma com janela —
        // todo torneio até esta data — `QuadrasAbertasEm` responde null e isto é um `Take`.
        if (sedes == null) return livres.Take(quantas).ToList();

        var vagas = new List<DateTime>();
        int uteis = 0;
        DateTime? instante = null;
        int abertasNoInstante = 0;
        int naVez = 0;

        foreach (var horario in livres)
        {
            if (horario != instante)
            {
                instante = horario;
                naVez = 0;
                // Limitado à quantidade de quadras do torneio: é ela que manda em quantas vagas
                // cada rodada tem (GradeDeJogos.Horarios). Uma quadra escrita direto num jogo,
                // sem cadastro, não pode inventar vaga que a grade nunca ofereceu.
                abertasNoInstante = Math.Min(
                    sedes.QuadrasAbertasEm(horario) ?? int.MaxValue,
                    Math.Max(torneio.QuantidadeQuadras, 1));
            }

            vagas.Add(horario);
            if (naVez < abertasNoInstante) uteis++;
            naVez++;

            if (uteis >= quantas) break;
        }

        return vagas;
    }
}
