using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// AS TAÇAS: as do time, e a do jogador que mais ganhou dentro dele.
//
// ⚠️ Lê o carimbo único de campeão do sistema — `Dupla.UltimaFase == CampeoesDoTorneio.
// FaseDeCampeao` — e não uma regra própria. Decidir campeão em dois lugares é a cicatriz que
// o projeto já tem com "quem venceu", e aqui seria pior: a tela do time contaria uma
// contagem de títulos diferente da página do torneio que os deu.
//
// ⚠️ Torneio CANCELADO não conta, pela mesma régua do CampeoesDoTorneio.PodeAnunciar: se a
// página do torneio não anuncia aquele campeão, a estante do time não pode exibi-lo.
public static class TitulosDoTime
{
    // Um título do TIME: veio de uma categoria de times, onde quem disputa é o time inteiro
    // (a `Dupla` com NomeTime preenchido e TimeId apontando pro cadastro).
    public sealed record Titulo(int TorneioId, string Torneio, string Categoria, DateTime? Data);

    // Um membro do elenco e quantas taças ele tem na carreira.
    public sealed record Vencedor(Jogador Jogador, int Trofeus);

    public static async Task<List<Titulo>> DoTimeAsync(DbPadelContext context, int timeId)
    {
        var titulos = await context.Duplas
            .AsNoTracking()
            .Where(d => d.TimeId == timeId
                     && d.UltimaFase == CampeoesDoTorneio.FaseDeCampeao
                     && d.Categoria.Torneio.Status != CancelamentoDoTorneio.Status)
            .Select(d => new Titulo(
                d.Categoria.TorneioId,
                d.Categoria.Torneio.Nome,
                d.Categoria.Nome,
                d.Categoria.Torneio.DataInicio))
            .ToListAsync();

        // O mais recente primeiro — é a conquista que a pessoa veio ver. Sem data (torneio
        // antigo, importado) desce pro fim em vez de fingir ser o mais novo.
        return titulos
            .OrderByDescending(t => t.Data ?? DateTime.MinValue)
            .ThenBy(t => t.Torneio)
            .ToList();
    }

    // Quantas taças cada um destes jogadores tem na CARREIRA (não só no time atual — a taça
    // é da pessoa, e ela a trouxe na mala). Quem não tem nenhuma fica fora do dicionário.
    public static async Task<Dictionary<int, int>> TrofeusPorJogadorAsync(
        DbPadelContext context, IEnumerable<int> jogadorIds)
    {
        var ids = jogadorIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, int>();

        // ⚠️ `NomeTime == null` de propósito: na dupla-TIME o Jogador1Id aponta pro
        // organizador que CADASTROU o time, não pra quem jogou. Sem este filtro, quem digitou
        // a lista de times de um torneio colecionaria troféus que não ganhou — é a mesma
        // armadilha que já tirou a dupla-time de todo somatório de ranking do sistema.
        var campeas = await context.Duplas
            .AsNoTracking()
            .Where(d => d.UltimaFase == CampeoesDoTorneio.FaseDeCampeao
                     && d.NomeTime == null
                     && d.Categoria.Torneio.Status != CancelamentoDoTorneio.Status
                     && (ids.Contains(d.Jogador1Id)
                         || (d.Jogador2Id != null && ids.Contains(d.Jogador2Id.Value))))
            .Select(d => new { d.Jogador1Id, d.Jogador2Id })
            .ToListAsync();

        var procurados = ids.ToHashSet();
        var contagem = new Dictionary<int, int>();

        foreach (var dupla in campeas)
        {
            // Os DOIS lados contam: a taça é da dupla, e os dois a levantaram.
            if (procurados.Contains(dupla.Jogador1Id))
                contagem[dupla.Jogador1Id] = contagem.GetValueOrDefault(dupla.Jogador1Id) + 1;

            if (dupla.Jogador2Id is int segundo && procurados.Contains(segundo))
                contagem[segundo] = contagem.GetValueOrDefault(segundo) + 1;
        }

        return contagem;
    }

    // Quem, dentre estes jogadores, mais levantou taça.
    //
    // Devolve TODOS os empatados no topo, e não um escolhido a dedo: com dois jogadores de
    // cinco títulos cada, eleger um pelo nome faria o destaque mentir sobre o outro. Lista
    // vazia = ninguém do elenco tem título, e aí a seção inteira não aparece na tela.
    //
    // Função pura de propósito: a contagem já veio do banco em TrofeusPorJogadorAsync, e quem
    // decide o pódio é regra — dá pra testar numa lista na memória.
    public static List<Vencedor> MaioresVencedores(
        IEnumerable<Jogador> membros, IReadOnlyDictionary<int, int> trofeus)
    {
        var comTaca = membros
            .Select(j => new Vencedor(j, trofeus.GetValueOrDefault(j.Id)))
            .Where(v => v.Trofeus > 0)
            .ToList();

        if (comTaca.Count == 0) return new List<Vencedor>();

        var maximo = comTaca.Max(v => v.Trofeus);

        return comTaca
            .Where(v => v.Trofeus == maximo)
            .OrderBy(v => v.Jogador.ComoChamar)
            .ToList();
    }
}
