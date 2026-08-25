using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// ⚠️ OS DOIS LADOS SÃO EXPLÍCITOS, e não um `bool` só com o outro deduzido por negação: só
// entra partida finalizada, mas "finalizada sem VencedorId" existe no banco (correção de
// placar no meio do caminho), e a negação faria o card coroar a dupla 2 num jogo que ninguém
// venceu. Com os dois, o card sabe também o caso "nenhum": desenha sem destaque.
public sealed record ResultadoDeJogo(
    string Fase, string Dupla1, string Dupla2, int Games1, int Games2,
    bool Dupla1Venceu, bool Dupla2Venceu);

public sealed record DiaDeResultados(
    DateTime Dia,
    int CategoriaId,
    string Categoria,
    List<ResultadoDeJogo> Jogos,
    int Total,
    string Torneio,
    string? Clube)
{
    public bool TemOQueMostrar => Jogos.Count > 0;

    // ⚠️ O QUE FICOU DE FORA É DITO, NÃO ESCONDIDO. Um card intitulado "resultados do dia"
    // mostrando metade deles, calado, é uma meia-verdade que ninguém tem como perceber olhando.
    public int QuantosFicaramDeFora => Math.Max(0, Total - Jogos.Count);
}

// OS RESULTADOS DE UM DIA, numa categoria.
//
// ⚠️ É POR CATEGORIA e não pelo torneio inteiro: um sábado de torneio grande passa de
// cinquenta jogos, e não existe card legível com cinquenta linhas. Numa categoria, um dia
// costuma ter de quatro a dez.
//
// ⚠️ O DIA DE UM JOGO É `HorarioFimReal ?? HorarioPrevisto`, nessa ordem: o primeiro é quando
// a bola parou de verdade, o segundo é a grade. Jogo lançado sem ter sido "colocado no ar" não
// tem fim real — rotina em torneio pequeno, onde o organizador só digita o placar no final —,
// e cair pra grade é o que impede que ele suma do resumo do próprio dia em que aconteceu.
// Sem nenhum dos dois o jogo não tem dia e fica fora: melhor ausente que no dia errado.
public static class ResultadosDoDia
{
    public static async Task<DiaDeResultados?> DaCategoriaAsync(
        DbPadelContext contexto, int torneioId, int categoriaId, DateTime dia)
    {
        var torneio = await contexto.Torneios
            .AsNoTracking()
            .Where(t => t.Id == torneioId)
            .Select(t => new { t.Id, t.Nome, t.Status, t.ClubeId })
            .FirstOrDefaultAsync();

        // Mesma régua dos outros cards de torneio: cancelado não anuncia nada.
        if (torneio == null || !CampeoesDoTorneio.PodeAnunciar(torneio.Status)) return null;

        var categoria = await contexto.Categorias
            .AsNoTracking()
            .Where(c => c.Id == categoriaId && c.TorneioId == torneioId)
            .Select(c => new { c.Id, c.Nome })
            .FirstOrDefaultAsync();

        if (categoria == null) return null;

        var nomeDoClube = await contexto.Clubes
            .AsNoTracking()
            .Where(c => c.Id == torneio.ClubeId)
            .Select(c => c.Nome)
            .FirstOrDefaultAsync();

        // ⚠️ O FILTRO DE DIA É FEITO EM MEMÓRIA, de propósito. `(p.HorarioFimReal ??
        // p.HorarioPrevisto)!.Value.Date` dentro do `Where` é o tipo de expressão que o
        // InMemory dos testes aceita e o Postgres pode recusar — e o volume aqui é o de UMA
        // categoria de UM torneio, dezenas de linhas, não a tabela inteira.
        var partidas = await contexto.Partidas
            .AsNoTracking()
            .Include(p => p.Dupla1).ThenInclude(d => d!.Jogador1)
            .Include(p => p.Dupla1).ThenInclude(d => d!.Jogador2)
            .Include(p => p.Dupla2).ThenInclude(d => d!.Jogador1)
            .Include(p => p.Dupla2).ThenInclude(d => d!.Jogador2)
            .Where(p => p.CategoriaId == categoriaId && p.Status == "Finalizada")
            .ToListAsync();

        var doDia = partidas
            .Select(p => new { Partida = p, Quando = p.HorarioFimReal ?? p.HorarioPrevisto })
            .Where(x => x.Quando != null && x.Quando.Value.Date == dia.Date)
            // Mais recente em cima: num dia de torneio os jogos que interessam (semifinal,
            // final) são os últimos, e é neles que o card precisa pegar quem lê só a
            // primeira linha.
            .OrderByDescending(x => x.Quando)
            .ThenByDescending(x => x.Partida.Id)
            .ToList();

        var jogos = doDia
            .Take(CartaoDosResultados.MaximoDeJogos)
            .Select(x => new ResultadoDeJogo(
                Fase: x.Partida.Fase,
                Dupla1: x.Partida.Dupla1 == null ? "" : NomeDaDupla.Na(x.Partida.Dupla1),
                Dupla2: x.Partida.Dupla2 == null ? "" : NomeDaDupla.Na(x.Partida.Dupla2),
                Games1: x.Partida.GamesDupla1 ?? 0,
                Games2: x.Partida.GamesDupla2 ?? 0,
                // ⚠️ Quem venceu sai de `QuemVenceu.Da`, a MESMA função que grava o
                // `VencedorId` ao finalizar — nunca de `games1 > games2`. As duas divergem
                // quando um lado está sem placar, e foi assim que a tabela de um grupo
                // apontou uma vencedora e o registro do jogo apontou outra (05/08/2026).
                Dupla1Venceu: QuemVenceu.Da(x.Partida) == x.Partida.Dupla1Id,
                Dupla2Venceu: QuemVenceu.Da(x.Partida) == x.Partida.Dupla2Id))
            .ToList();

        return new DiaDeResultados(
            Dia: dia.Date,
            CategoriaId: categoria.Id,
            Categoria: categoria.Nome,
            Jogos: jogos,
            Total: doDia.Count,
            Torneio: torneio.Nome,
            Clube: nomeDoClube);
    }
}
