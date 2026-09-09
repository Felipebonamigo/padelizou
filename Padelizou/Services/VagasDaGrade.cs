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

    // ATÉ ONDE A GRADE PRECISA IR pra que as restrições de horário deixem algo de pé.
    //
    // 🕳️ MEDIDO EM 09/09/2026, auditando o torneio do Er: com 63 duplas, 33 delas com
    // impedimento e **4 quadras**, dois jogos da mesma dupla caíam no MESMO horário, dentro da
    // janela que ela tinha pago pra evitar. Com 2 quadras, zero furos — ou seja, MAIS capacidade
    // dava PIOR resultado, o contrário do que a intuição diz.
    //
    // A razão: a grade oferece `jogos + margem` vagas, e cada rodada rende uma vaga por quadra.
    // Com 4 quadras isso vira METADE das rodadas — e o impedimento bloqueia DIAS INTEIROS.
    // Sobram menos horários distintos pra dupla escapar da janela dela, o orçamento acaba, e o
    // último recurso do `Encaixar` entra: primeiro cede o impedimento, depois cede a regra de
    // não repetir gente. Foi exatamente o que apareceu.
    //
    // ⚠️ É O MESMO DEFEITO QUE A CONCENTRAÇÃO TEVE (08/09) e o mesmo conserto: a grade precisa
    // ALCANÇAR o fim da janela mais tardia, senão a restrição não tem pra onde empurrar o jogo.
    // Aqui ele vale pros três mapas de janela — impedimento, concentração e noite de sábado —
    // porque a pergunta é a mesma pros três.
    public static DateTime? AlcanceNecessario(
        params IReadOnlyDictionary<int, (DateTime Inicio, DateTime Fim)[]>?[] mapas)
    {
        DateTime? maisTarde = null;

        foreach (var mapa in mapas)
        {
            if (mapa == null) continue;

            foreach (var janelas in mapa.Values)
                foreach (var janela in janelas)
                    if (maisTarde == null || janela.Fim > maisTarde) maisTarde = janela.Fim;
        }

        return maisTarde;
    }

    // A mais tardia entre dois alcances. Nulo é "não pede nada", então ele nunca vence.
    public static DateTime? MaisTarde(DateTime? um, DateTime? outro) =>
        um == null ? outro : outro == null ? um : (um > outro ? um : outro);

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

            // ⚠️ NÃO PARA NO LIMITE — SEGUE UMA MARGEM ALÉM DELE, e isso foi medido (09/09/2026,
            // auditando o Er com 4 quadras). Parando no primeiro horário que ALCANÇA o fim da
            // janela, o outro lado dela ganha uma rodada só: com 4 quadras isso são 4 vagas pra
            // todas as duplas que a janela empurrou pra lá, e o último recurso do encaixe entra
            // de novo. O furo caiu de 2 pra 1 e não zerou. Alcançar não é o mesmo que caber.
            int cabem = 0;
            int depoisDoLimite = 0;
            foreach (var h in GradeDeJogos.Horarios(inicio, torneio.HoraFimDoDia,
                         torneio.QuantidadeQuadras, Duracao(torneio), teto,
                         aberturaDiasSeguintes: torneio.HoraInicioDiasSeguintes))
            {
                cabem++;

                if (h >= limite && ++depoisDoLimite > margem) break;
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
