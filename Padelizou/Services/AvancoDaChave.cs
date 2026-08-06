using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// Quem passa pra próxima fase do mata-mata, respondido em UM lugar só.
//
// A pergunta parece boba ("os vencedores, ora") e tem duas armadilhas:
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
// Os dois robôs (Mesa de Controle, em TorneiosController.Chaves, e Controle de Placar, em
// PartidasController) chamam esta função. Eram cópias um do outro, e é exatamente o tipo de
// regra que não pode divergir: cada cópia decidiria um campeão diferente.
public static class AvancoDaChave
{
    // Lista, na ordem do chaveamento, quem disputa a próxima fase.
    // Vazia = a fase ainda não terminou (ou não havia jogo nenhum nela).
    //
    // A ORDEM importa: os vencedores vêm primeiro e os byes depois, porque quem pareia
    // (ChaveamentoMataMata.ParearVencedores) cruza o primeiro com o último. Assim cada
    // vencedor da primeira rodada encontra uma dupla que passou direto — que é o desenho
    // certo de uma chave de 24 em quadro de 32.
    public static async Task<List<int>> QuemAvancaAsync(
        DbPadelContext context, int categoriaId, string faseConcluida)
    {
        var partidasDaFase = await context.Partidas
            .Where(p => p.CategoriaId == categoriaId && p.Fase == faseConcluida)
            .OrderBy(p => p.Id)
            .Select(p => new { p.Status, p.VencedorId })
            .ToListAsync();

        if (partidasDaFase.Count == 0) return new List<int>();
        if (partidasDaFase.Any(p => p.Status != "Finalizada" || p.VencedorId == null))
            return new List<int>();

        // 3. **Esta fase JÁ AVANÇOU?** Uma fase completa continua completa pra sempre, então
        //    todo finalizar posterior a pergunta de novo — e a resposta muda. Os byes só
        //    entram na conta enquanto o mata-mata tem UMA fase (ver ByesDaCategoriaAsync);
        //    depois que a fase seguinte nasce eles somem, e os mesmos 16 viram 8.
        //    `NomeFase(8)` é "Quartas de Final", que ainda não existe — e o robô monta uma
        //    quartas com os vencedores da primeira rodada cruzados entre si, por cima das
        //    oitavas que ainda estão em quadra.
        //
        //    Aconteceu no Interno de 05/08/2026: quartas de final montadas 1×8, 2×7, 3×6,
        //    4×5 sobre os 8 vencedores da primeira rodada, com as oitavas ainda rolando.
        //
        //    Bastam dois jogos da mesma fase finalizados quase juntos: o primeiro cria a
        //    fase seguinte, e o segundo faz esta conta já sem os byes. Reabrir e finalizar
        //    de novo um jogo de uma fase já avançada cai no mesmo lugar.
        //
        //    A guarda de duplicidade dos chamadores não pega: ela pergunta se a fase que
        //    ELA vai criar já existe, e "Quartas de Final" de fato não existia.
        var fasesDaCategoria = await context.Partidas
            .Where(p => p.CategoriaId == categoriaId)
            .Select(p => p.Fase)
            .Distinct()
            .ToListAsync();

        for (var seguinte = ChaveamentoMataMata.ProximaFase(faseConcluida);
             seguinte != null;
             seguinte = ChaveamentoMataMata.ProximaFase(seguinte))
        {
            if (fasesDaCategoria.Contains(seguinte)) return new List<int>();
        }

        var avancam = partidasDaFase.Select(p => p.VencedorId!.Value).ToList();
        avancam.AddRange(await ByesDaCategoriaAsync(context, categoriaId));
        return avancam;
    }

    // Quem passou DIRETO pra fase seguinte sem jogar a primeira rodada do mata-mata.
    //
    // Bye é "não ter jogo de mata-mata": quem perdeu tem jogo, quem venceu tem jogo, quem
    // pulou a rodada não tem. Por isso a consulta se esgota sozinha — depois da primeira
    // rodada todo sobrevivente já jogou, e daí em diante ela volta vazia sem precisar saber
    // em que fase estamos.
    //
    // O que muda por tipo de categoria é QUEM É CANDIDATO a bye:
    //  • chave direta: toda dupla inscrita que entrou no sorteio;
    //  • pós-grupos: só quem CLASSIFICOU — a dupla eliminada no grupo também não tem jogo
    //    de mata-mata, e sem essa régua ela entraria nas oitavas de carona, ressuscitada.
    // Público porque o DESENHO da chave também precisa saber quem descansou: sem isso as
    // duplas de bye somem do quadro — jogam a fase seguinte e não aparecem em lugar nenhum.
    public static async Task<List<int>> ByesDaCategoriaAsync(DbPadelContext context, int categoriaId)
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

        var jaJogaramMataMata = partidasDeMataMata
            .SelectMany(p => new[] { p.Dupla1Id, p.Dupla2Id })
            .ToHashSet();

        // Sem mata-mata nenhum ainda não existe bye — existe fase de grupos em andamento.
        if (partidasDeMataMata.Count == 0 && !categoria.ChaveDireta) return new List<int>();

        // ⚠️ Bye é coisa da PRIMEIRA rodada do mata-mata. Assim que a segunda fase nasce,
        // quem descansou já entrou nela — e daí em diante "não ter jogo" quer dizer outra
        // coisa: eliminado no grupo, ou cortado por uma regra antiga. Sem esta parada, uma
        // categoria com o mata-mata em andamento ressuscitaria gente a cada fase.
        if (partidasDeMataMata.Select(p => p.Fase).Distinct().Count() > 1) return new List<int>();

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
            // Pós-grupos: recalcula a classificação com a MESMA régua da geração do mata-mata
            // (Services/ClassificacaoDeGrupos). Os jogos de grupo estão todos finalizados — o
            // mata-mata só nasce depois deles —, então a conta dá sempre o mesmo resultado.
            var partidasDeGrupo = partidas.Where(p => FasesTorneio.EhFaseDeGrupos(p.Fase)).ToList();
            noQuadro = ClassificacaoDeGrupos.Calcular(
                    duplas, partidasDeGrupo, Math.Max(1, categoria.ClassificadosPorGrupo ?? 2))
                .OrderBy(c => c.Posicao)
                .ThenByDescending(c => c.Vitorias).ThenByDescending(c => c.Saldo).ThenBy(c => c.Grupo)
                .Select(c => c.DuplaId)
                .ToList();
        }

        int vagasNaPrimeiraRodada = partidasDeMataMata.Count * 2;
        int quantosDescansaram = noQuadro.Count - vagasNaPrimeiraRodada;
        if (quantosDescansaram <= 0) return new List<int>();

        // `Take` no fim é a mesma trava vista de outro ângulo: ainda que a lista dos que "não
        // jogaram" venha maior do que devia, saem daqui no máximo os que cabiam de folga.
        return noQuadro
            .Where(id => !jaJogaramMataMata.Contains(id))
            .Take(quantosDescansaram)
            .ToList();
    }
}
