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

        var avancam = partidasDaFase.Select(p => p.VencedorId!.Value).ToList();
        avancam.AddRange(await ByesAsync(context, categoriaId));
        return avancam;
    }

    // Duplas de uma chave direta que ainda não jogaram nada nesta categoria.
    //
    // Não ter partida é a definição de bye: quem perdeu tem jogo, quem venceu tem jogo, quem
    // passou direto não tem. Por isso a consulta se esgota sozinha — depois da primeira
    // rodada todo mundo que segue vivo já jogou, e daí em diante ela volta vazia sem
    // precisar saber em que fase estamos.
    private static async Task<List<int>> ByesAsync(DbPadelContext context, int categoriaId)
    {
        var categoria = await context.Categorias
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == categoriaId);

        if (categoria is not { ChaveDireta: true }) return new List<int>();

        var confrontos = await context.Partidas
            .Where(p => p.CategoriaId == categoriaId)
            .Select(p => new { p.Dupla1Id, p.Dupla2Id })
            .ToListAsync();

        var jaJogaram = confrontos
            .SelectMany(c => new[] { c.Dupla1Id, c.Dupla2Id })
            .ToHashSet();

        // O mesmo filtro do sorteio: dupla sem parceiro ou na lista de espera nunca entrou
        // na chave, e não pode entrar por esta porta.
        var candidatas = await context.Duplas
            .Where(d => d.CategoriaId == categoriaId)
            .ToListAsync();

        return candidatas
            .Where(d => !ForaDoSorteio.FicaDeFora(d) && !jaJogaram.Contains(d.Id))
            .OrderBy(d => d.Id)
            .Select(d => d.Id)
            .ToList();
    }
}
