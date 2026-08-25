using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// Uma linha da tabela do grupo, já pronta pra desenhar.
public sealed record LinhaDaClassificacao(int Posicao, string Dupla, int Jogos, int Vitorias, int Saldo);

public sealed record GrupoClassificado(
    string Grupo,
    int CategoriaId,
    string Categoria,
    List<LinhaDaClassificacao> Linhas,
    string Torneio,
    string? Clube,
    DateTime? Data)
{
    // ⚠️ TABELA ZERADA NÃO VIRA ARTE. A classificação existe desde o sorteio, com todo mundo
    // em zero, e isso é o CERTO na tela (decisão de 12/08/2026: quem entra no começo do
    // torneio procura o próprio nome). Mas um card postado no Instagram com a coluna de
    // vitórias toda zerada anuncia uma etapa que não aconteceu.
    //
    // ⚠️ A PERGUNTA É POR VITÓRIA, E NÃO POR `Jogos`: `ClassificacaoDeGrupos.Ordenar` conta em
    // `Jogos` as partidas em que a dupla APARECE — inclusive as agendadas e ainda sem placar.
    // Um grupo recém-sorteado já nasce com `Jogos > 0` em todo mundo, e a régua ingênua
    // liberaria o card exatamente no estado que ela existe pra barrar.
    public bool TemOQueMostrar => Linhas.Any(l => l.Vitorias > 0);
}

// A CLASSIFICAÇÃO DA FASE DE GRUPOS, no formato que o card precisa.
//
// ⚠️ A ORDEM NÃO É CALCULADA AQUI — sai de `ClassificacaoDeGrupos.Ordenar`, a régua única do
// sistema, a MESMA que o chaveamento usa pra montar a chave. Uma segunda ordenação publicaria
// um card dizendo que a dupla A passou enquanto o chaveamento coloca a B na semifinal. É o
// defeito de 13/08/2026 (a tela ordenava por conta própria, sem o terceiro critério de
// desempate) numa versão pior: impressa, postada e fora do nosso alcance pra corrigir.
public static class ClassificacaoParaCard
{
    public static async Task<List<GrupoClassificado>> DaCategoriaAsync(
        DbPadelContext contexto, int torneioId, int categoriaId)
    {
        var vazio = new List<GrupoClassificado>();

        // Só colunas do próprio torneio: `Clube = t.Clube.Nome` aqui viraria INNER JOIN numa
        // FK obrigatória e sumiria com o torneio inteiro se o clube não casasse.
        var torneio = await contexto.Torneios
            .AsNoTracking()
            .Where(t => t.Id == torneioId)
            .Select(t => new { t.Id, t.Nome, t.Status, t.ClubeId, t.DataInicio })
            .FirstOrDefaultAsync();

        // Mesma régua dos outros cards de torneio: cancelado não anuncia nada.
        if (torneio == null || !CampeoesDoTorneio.PodeAnunciar(torneio.Status)) return vazio;

        var categoria = await contexto.Categorias
            .AsNoTracking()
            .Where(c => c.Id == categoriaId && c.TorneioId == torneioId)
            .Select(c => new { c.Id, c.Nome })
            .FirstOrDefaultAsync();

        if (categoria == null) return vazio;

        var nomeDoClube = await contexto.Clubes
            .AsNoTracking()
            .Where(c => c.Id == torneio.ClubeId)
            .Select(c => c.Nome)
            .FirstOrDefaultAsync();

        var duplas = await contexto.Duplas
            .AsNoTracking()
            .Include(d => d.Jogador1)
            .Include(d => d.Jogador2)
            .Where(d => d.CategoriaId == categoriaId && d.Grupo != null)
            .ToListAsync();

        if (duplas.Count == 0) return vazio;

        // ⚠️ SÓ PARTIDA FINALIZADA, e é a MESMA condição da tela de classificação
        // (`TorneiosController.Classificacao`). Sem ela o card somaria o placar PARCIAL de um
        // jogo ao vivo: o 6x0 do primeiro set entraria como saldo, e o card sairia discordando
        // da tela que o gerou — impresso e postado, fora do nosso alcance pra corrigir.
        //
        // ⚠️ A EXPRESSÃO DA FASE É INLINE, e não `FasesTorneio.EhFaseDeGrupos(p.Fase)`: o EF
        // traduz isto pra SQL, e uma chamada de método C# dentro do `Where` não tem tradução.
        // É a mesma forma que o próprio `FasesTorneio` manda usar em consulta.
        var partidas = await contexto.Partidas
            .AsNoTracking()
            .Where(p => p.CategoriaId == categoriaId && p.Status == "Finalizada"
                     && (p.Fase == "Fase de Grupos" || p.Fase.StartsWith("Grupo ")))
            .ToListAsync();

        return duplas
            .GroupBy(d => d.Grupo!)
            .OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(g =>
            {
                var ordenadas = ClassificacaoDeGrupos.Ordenar(g.ToList(), partidas);

                return new GrupoClassificado(
                    Grupo: g.Key,
                    CategoriaId: categoria.Id,
                    Categoria: categoria.Nome,
                    Linhas: ordenadas
                        .Select((linha, i) => new LinhaDaClassificacao(
                            Posicao: i + 1,
                            Dupla: NomeDaDupla.Na(linha.Dupla),
                            Jogos: linha.Jogos,
                            Vitorias: linha.Vitorias,
                            Saldo: linha.Saldo))
                        .ToList(),
                    Torneio: torneio.Nome,
                    Clube: nomeDoClube,
                    Data: torneio.DataInicio);
            })
            .ToList();
    }
}
