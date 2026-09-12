using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// Quem passa pra próxima fase do mata-mata, respondido em UM lugar só.
//
// A pergunta parece boba ("os vencedores, ora") e tem três armadilhas:
//
//  1. **A fase fechou?** O robô antigo comparava os vencedores com uma CONSTANTE por nome de
//     fase (Oitavas = 8 jogos, Quartas = 4...). Isso vale só na chave cheia: a primeira
//     rodada de uma CHAVE DIRETA com bye tem menos jogos que o nome da fase promete — 24
//     duplas num quadro de 32 são 8 jogos, não 16 — e com a constante o robô esperaria pra
//     sempre por 8 vencedores que nunca viriam. Agora a conta é contra as partidas que
//     EXISTEM naquela fase.
//
//  2. **Quem pegou bye também avança.** Numa chave direta as duplas que sobraram do quadro
//     não jogam a primeira rodada. Elas não venceram nada, então não estão entre os
//     vencedores — e sem somá-las aqui os 8 vencedores fariam 4 jogos entre si e os 8 que
//     passaram direto sumiriam do torneio sem nunca ter perdido.
//
//  3. **A fase já avançou?** Uma fase completa continua completa pra sempre, então todo
//     finalizar posterior refaz a pergunta. Quem responde "já foi" é o robô, contando os
//     jogos que a fase seguinte JÁ TEM (ver RoboDoChaveamento.AvancarFaseAsync) — e não mais
//     a lista de vagas, que hoje é estável de propósito (ver o bloco sobre o bye abaixo).
//
// Os dois robôs (Mesa de Controle, em TorneiosController.Chaves, e Controle de Placar, em
// PartidasController) chamam esta função. Eram cópias um do outro, e é exatamente o tipo de
// regra que não pode divergir: cada cópia decidiria um campeão diferente.
public static class AvancoDaChave
{
    // AS VAGAS DA PRÓXIMA FASE, NA ORDEM DO QUADRO. `null` = vaga ainda sem dono.
    //
    // 🗣️ Felipe, 11/09/2026: *"terminou a primeira quarta de final, esse que já classificou, já
    // vai a dupla para a semi, mesmo que as outras quartas não tenham finalizado"*. Até aqui a
    // resposta era tudo-ou-nada: um jogo pendente na fase e ninguém avançava. Agora a lista sai
    // com buraco, e quem monta o jogo (RoboDoChaveamento) cria os confrontos cujas DUAS vagas
    // já têm dono.
    //
    // A ORDEM importa: os vencedores vêm primeiro, na ordem dos jogos, e os byes depois, porque
    // quem pareia (ChaveamentoMataMata.ParearVencedores) cruza o primeiro com o último. Assim
    // cada vencedor da primeira rodada encontra uma dupla que passou direto — que é o desenho
    // certo de uma chave de 24 em quadro de 32.
    //
    // ⚠️ E a SEMEADURA da primeira fase conta com exatamente esta ordem (vencedores por Id do
    // jogo, byes do melhor pro pior) pra saber em que metade da chave cada bye cai — é assim
    // que ela mantém os dois classificados de um grupo em lados opostos até a final
    // (ChaveamentoMataMata.Semear, ensaio do Er de 10/09/2026). Mudar a ordem aqui, ou a de
    // ByesDaCategoriaAsync, muda os lados lá — e a semifinal volta a juntar o mesmo grupo.
    //
    // Lista VAZIA = não há o que avançar: a fase não existe, ou o quadro já passou dela.
    //
    // `buscarPontos`: o ranking do desempate de grupo, consultado só se algum grupo empatar
    // até ele (ver ClassificacaoDeGrupos.PontosSePrecisarAsync). Chega até aqui porque o BYE
    // sai da classificação, e a conta precisa bater com a do chaveamento.
    public static async Task<List<int?>> VagasDaProximaFaseAsync(
        DbPadelContext context, int categoriaId, string faseConcluida,
        BuscarPontosDoRanking buscarPontos)
    {
        var daCategoria = await context.Partidas
            .Where(p => p.CategoriaId == categoriaId)
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, p.Fase, p.Status, p.VencedorId })
            .ToListAsync();

        var partidasDaFase = daCategoria.Where(p => p.Fase == faseConcluida).ToList();
        if (partidasDaFase.Count == 0) return new List<int?>();

        // ⚠️ COM A FASE DE GRUPOS ABERTA, A ABERTURA DO MATA-MATA AINDA ESTÁ CRESCENDO.
        //
        // Desde o avanço parcial dos grupos (11/09/2026, RoboDoChaveamento.
        // MontarAberturaDesenhadaAsync) os jogos da primeira rodada nascem grupo a grupo. Contar
        // vagas aí seria contar meia chave: com 1 dos 2 jogos criados e terminado, a lista teria
        // UMA vaga, `NomeFase` batizaria isso de "Final" e o torneio ganharia uma decisão com
        // metade da categoria ainda em quadra. Só a abertura sofre disso — daí a trava valer
        // exatamente nela.
        if (daCategoria.Any(p => FasesTorneio.EhFaseDeGrupos(p.Fase) && p.Status != "Finalizada")
            && faseConcluida == PrimeiraFaseDeMataMata(daCategoria.Select(p => p.Fase)))
            return new List<int?>();

        // ⚠️ O QUADRO JÁ PASSOU DAQUI. Reabrir e finalizar de novo um jogo de uma fase cuja
        // SEGUINTE já acabou não pode remontar a rodada que veio depois: ela pode estar em
        // quadra, ou já ter dado um campeão. A trava olha da fase depois da próxima pra frente
        // — a PRÓXIMA pode existir pela metade, que é justamente o que o avanço parcial faz.
        var fasesDaCategoria = daCategoria.Select(p => p.Fase).ToHashSet();
        var proxima = ChaveamentoMataMata.ProximaFase(faseConcluida);
        for (var seguinte = ChaveamentoMataMata.ProximaFase(proxima);
             seguinte != null;
             seguinte = ChaveamentoMataMata.ProximaFase(seguinte))
        {
            if (fasesDaCategoria.Contains(seguinte)) return new List<int?>();
        }

        var vagas = partidasDaFase
            .Select(p => p.Status == "Finalizada" ? p.VencedorId : null)
            .ToList();

        // O bye é coisa da PRIMEIRA rodada do mata-mata: quem folgou entra na SEGUNDA fase e
        // pronto. Somá-lo em qualquer outra ressuscitaria gente a cada rodada.
        if (faseConcluida == PrimeiraFaseDeMataMata(fasesDaCategoria))
            vagas.AddRange((await ByesDaCategoriaAsync(context, categoriaId, buscarPontos))
                .Select(id => (int?)id));

        return vagas;
    }

    // A fase inteira decidida, ou nada. É a pergunta de antes do avanço parcial, e continua
    // valendo pra quem precisa do quadro FECHADO — não do que já dá pra montar.
    public static async Task<List<int>> QuemAvancaAsync(
        DbPadelContext context, int categoriaId, string faseConcluida,
        BuscarPontosDoRanking buscarPontos)
    {
        var vagas = await VagasDaProximaFaseAsync(context, categoriaId, faseConcluida, buscarPontos);
        return vagas.Any(v => v == null)
            ? new List<int>()
            : vagas.Select(v => v!.Value).ToList();
    }

    // A primeira fase de mata-mata da categoria — a única que tem bye. Nula quando não há
    // mata-mata nenhum ainda.
    private static string? PrimeiraFaseDeMataMata(IEnumerable<string> fases) =>
        fases.Where(ChaveamentoMataMata.EhFaseDeMataMata)
            .OrderBy(OrdemDasFases.Posto)
            .FirstOrDefault();

    // Quem passou DIRETO pra fase seguinte sem jogar a primeira rodada do mata-mata.
    //
    // Bye é "não ter jogo NA PRIMEIRA RODADA": quem perdeu tem jogo, quem venceu tem jogo, quem
    // pulou a rodada não tem.
    //
    // ⚠️ A RESPOSTA É ESTÁVEL, e isso mudou em 11/09/2026. Até aqui ela se esgotava sozinha —
    // a consulta olhava TODAS as fases de mata-mata, então assim que a segunda nascia os byes
    // já tinham jogo e a lista vinha vazia. Isso funcionava enquanto a fase seguinte só nascia
    // INTEIRA; com o avanço parcial (a Semifinal 1 no ar e a Quartas 2 ainda em quadra) os
    // byes sumiriam da conta no meio do caminho, a lista de vagas cairia de 4 pra 2 e o robô
    // montaria uma Final por cima de uma semifinal pela metade. É o bug do Interno de
    // 05/08/2026 por outra porta. Ancorada na primeira rodada, a lista é a mesma do começo ao
    // fim do torneio — e quem não quer ver bye em fase adiantada filtra onde desenha
    // (QuadroDoMataMata e ProximasFasesDaChave só os somam na primeira fase).
    //
    // O que muda por tipo de categoria é QUEM É CANDIDATO a bye:
    //  • chave direta: toda dupla inscrita que entrou no sorteio;
    //  • pós-grupos: só quem CLASSIFICOU — a dupla eliminada no grupo também não tem jogo
    //    de mata-mata, e sem essa régua ela entraria nas oitavas de carona, ressuscitada.
    // Público porque o DESENHO da chave também precisa saber quem descansou: sem isso as
    // duplas de bye somem do quadro — jogam a fase seguinte e não aparecem em lugar nenhum.
    // `buscarPontos`: o ranking só é consultado se algum grupo empatar até ele
    // (ClassificacaoDeGrupos.PontosSePrecisarAsync). Aqui a conta PRECISA bater com a do
    // chaveamento — é este método que diz quem descansou, e divergir dele foi o defeito de
    // 05/08 descrito logo abaixo.
    public static async Task<List<int>> ByesDaCategoriaAsync(
        DbPadelContext context, int categoriaId, BuscarPontosDoRanking buscarPontos)
    {
        var categoria = await context.Categorias
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == categoriaId);
        if (categoria == null) return new List<int>();

        var duplas = await context.Duplas
            .Where(d => d.CategoriaId == categoriaId)
            .ToListAsync();

        var partidas = await context.Partidas
            .Where(p => p.CategoriaId == categoriaId)
            .ToListAsync();

        var partidasDeMataMata = partidas
            .Where(p => !FasesTorneio.EhFaseDeGrupos(p.Fase))
            .ToList();

        // Sem mata-mata nenhum ainda não existe bye — existe fase de grupos em andamento.
        if (partidasDeMataMata.Count == 0 && !categoria.ChaveDireta) return new List<int>();

        // ⚠️ SEM RODADA DE MATA-MATA, SEM BYE — e isto precisa estar escrito desde que a guarda
        // do "mais de uma fase" saiu daqui. `partidasDeMataMata` é tudo que não é grupo, o que
        // inclui as rodadas do Americano: sem esta linha, uma categoria de Americano (que não
        // tem quadro nenhum) devolveria a lista inteira de duplas como se todas tivessem
        // folgado. Nenhum chamador de hoje chega aqui com uma, e é justamente por isso que a
        // trava tem que ser explícita.
        var primeiraFase = PrimeiraFaseDeMataMata(partidasDeMataMata.Select(p => p.Fase));
        if (primeiraFase == null) return new List<int>();

        var primeiraRodada = partidasDeMataMata.Where(p => p.Fase == primeiraFase).ToList();

        var jogaramAPrimeiraRodada = primeiraRodada
            .SelectMany(p => new[] { p.Dupla1Id, p.Dupla2Id })
            .ToHashSet();

        // ⚠️ E SÓ EXISTE BYE SE SOBROU VAGA NO QUADRO. Esta é a trava que faltava, e é
        // aritmética, não palpite: a primeira rodada tem `jogos × 2` lugares. Se cabe todo
        // mundo, ninguém descansou — quem não está lá não classificou, ponto.
        //
        // Foi assim que a categoria de TIMES do Interno de 05/08/2026 terminou com a final
        // errada. Quatro times classificados, dois jogos de semifinal, quadro cheio: zero
        // byes. Mas a classificação foi recalculada aqui com um empate que ainda não tinha
        // desempate estável (ver ClassificacaoDeGrupos), devolveu um 2º colocado DIFERENTE do
        // que jogou a semifinal — e esse time, que "classificou e não jogou", entrou como bye.
        // Os que avançavam viraram três, o pareamento cruza primeiro com último, e a final
        // saiu entre o vencedor de uma semi e um time que não tinha vencido nada. O vencedor
        // da outra semifinal simplesmente sumiu do torneio.
        //
        // Com esta conta, mesmo que a classificação volte a divergir um dia, o estrago não
        // passa daqui: quadro cheio nunca inventa um participante a mais.
        // Quem estava no quadro, do melhor pro pior. A ORDEM importa no pareamento: o
        // ParearVencedores cruza primeiro com último, então o melhor bye pega o pior vencedor.
        List<int> noQuadro;
        if (categoria.ChaveDireta)
        {
            // O mesmo filtro do sorteio: dupla sem parceiro ou na lista de espera nunca
            // entrou na chave, e não pode entrar por esta porta.
            noQuadro = duplas
                .Where(d => !ForaDoSorteio.FicaDeFora(d))
                .OrderBy(d => d.Id)
                .Select(d => d.Id)
                .ToList();
        }
        else
        {
            // ⚠️ FASE DE GRUPOS ABERTA = SEM BYE. A classificação de um grupo em andamento é
            // provisória (ClassificacaoDeGrupos responde com o que tem), e a abertura da chave
            // ainda está nascendo grupo a grupo desde 11/09/2026 — a conta `noQuadro − jogos×2`
            // acusaria como "folgou" todo mundo cujo jogo ainda não foi criado.
            if (partidas.Any(p => FasesTorneio.EhFaseDeGrupos(p.Fase) && p.Status != "Finalizada"))
                return new List<int>();

            // Pós-grupos: recalcula a classificação com a MESMA régua da geração do mata-mata
            // (Services/ClassificacaoDeGrupos). Os jogos de grupo estão todos finalizados — o
            // mata-mata só nasce depois deles —, então a conta dá sempre o mesmo resultado.
            // Na ORDEM DOS BYES (ChaveamentoMataMata.OrdemDosByes — quem jogou menos, depois o
            // grupo de cima), a mesma com que a primeira fase escolheu quem descansa: é essa
            // ordem que a semeadura usa pra saber de que lado da chave cada bye cai.
            var partidasDeGrupo = partidas.Where(p => FasesTorneio.EhFaseDeGrupos(p.Fase)).ToList();
            var pontos = await ClassificacaoDeGrupos.PontosSePrecisarAsync(
                duplas, partidasDeGrupo, buscarPontos);
            noQuadro = ChaveamentoMataMata.OrdemDosByes(ClassificacaoDeGrupos.Calcular(
                    duplas, partidasDeGrupo, pontos, ClassificacaoDeGrupos.VagasPorGrupo(categoria)))
                .Select(c => c.DuplaId)
                .ToList();
        }

        int vagasNaPrimeiraRodada = primeiraRodada.Count * 2;
        int quantosDescansaram = noQuadro.Count - vagasNaPrimeiraRodada;
        if (quantosDescansaram <= 0) return new List<int>();

        // `Take` no fim é a mesma trava vista de outro ângulo: ainda que a lista dos que "não
        // jogaram" venha maior do que devia, saem daqui no máximo os que cabiam de folga.
        return noQuadro
            .Where(id => !jogaramAPrimeiraRodada.Contains(id))
            .Take(quantosDescansaram)
            .ToList();
    }
}
