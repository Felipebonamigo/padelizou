using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// O PÓDIO DE UMA CATEGORIA: campeão, vice e semifinalistas.
//
// O card de campeão (13/08/2026) anuncia quem levantou a taça; este conta a etapa inteira —
// que é o que o organizador posta no Instagram quando o dia acaba, e o que dá lugar às outras
// três duplas que chegaram longe e hoje não aparecem em lugar nenhum.
//
// ⚠️ ELE NÃO CALCULA NADA. Os carimbos já existem: quem perde no mata-mata recebe
// `UltimaFase = partida.Fase` (PartidasController, TorneiosController.Placar), então o
// perdedor da final fica em "Final" e os das semis em "Semifinal". Recalcular isso a partir
// das partidas seria a SEGUNDA régua a decidir pódio — e este projeto já pagou por ter duas
// contas de "quem venceu" (Interno de 05/08/2026, dupla errada classificada).
//
// ⚠️ AMERICANO NÃO ENTRA, e é decisão: lá o campeão sai da CLASSIFICAÇÃO e não de uma final,
// ninguém é carimbado "Final", e as outras linhas ficam em "Grupos". Montar um pódio a partir
// da tabela seria uma terceira régua de classificação. O card de CAMPEÃO já cobre o formato —
// e cobre melhor, porque lá quem ganha é uma pessoa, não uma dupla.
public static class PodioDaCategoria
{
    public const string Campea = "Campeao";
    public const string Vice = "Final";
    public const string Semi = "Semifinal";

    // Já tem alguma categoria com pódio fechado? Pergunta da PÁGINA do torneio, que decide se
    // mostra o botão — e que já carregou categorias e duplas de qualquer jeito, então não vale
    // uma segunda ida ao banco.
    //
    // ⚠️ Existe pra a view não escrever `UltimaFase == "Campeao"` na mão, pelo mesmo motivo do
    // `CampeoesDoTorneio.TemCampeao`: seria a segunda cópia da regra, no lugar mais fácil de
    // esquecer no dia em que ela mudar — uma tela que some sozinha e ninguém relaciona.
    public static bool TemPodio(Torneio torneio) =>
        CampeoesDoTorneio.PodeAnunciar(torneio.Status)
        && torneio.Categorias.Any(c => c.Duplas.Any(d => d.UltimaFase == Campea)
                                    && c.Duplas.Any(d => d.UltimaFase == Vice));

    // Os pódios de TODAS as categorias do torneio, na mesma ordem das outras telas. Lista
    // vazia = o torneio não pode anunciar nada (inexistente ou cancelado).
    //
    // ⚠️ Uma consulta pras categorias e UMA pras duplas — não uma por categoria. Um torneio de
    // dez categorias com a versão ingênua faria 21 idas ao banco pra montar uma página.
    public static async Task<List<PodioDeCategoria>> DoTorneioAsync(DbPadelContext contexto, int torneioId)
    {
        var vazio = new List<PodioDeCategoria>();

        // ⚠️ SÓ COLUNAS DO PRÓPRIO TORNEIO, pelo mesmo motivo do CampeoesDoTorneio: um
        // `Clube = t.Clube.Nome` aqui vira INNER JOIN numa FK obrigatória, e o torneio inteiro
        // sumiria da consulta quando o clube não casasse — devolvendo "não tem pódio" em vez
        // de estourar. O nome do clube vem numa consulta à parte, que não pode derrubar nada.
        var torneio = await contexto.Torneios
            .AsNoTracking()
            .Where(t => t.Id == torneioId)
            .Select(t => new { t.Id, t.Nome, t.Status, t.ClubeId, t.DataInicio })
            .FirstOrDefaultAsync();

        // A MESMA régua do card de campeão: torneio cancelado não anuncia, mesmo tendo tido
        // final jogada antes de cancelar.
        if (torneio == null || !CampeoesDoTorneio.PodeAnunciar(torneio.Status)) return vazio;

        var nomeDoClube = await contexto.Clubes
            .AsNoTracking()
            .Where(c => c.Id == torneio.ClubeId)
            .Select(c => c.Nome)
            .FirstOrDefaultAsync();

        var categorias = await contexto.Categorias
            .AsNoTracking()
            .Where(c => c.TorneioId == torneioId)
            .Select(c => new { c.Id, c.Nome })
            .ToListAsync();

        var duplas = await contexto.Duplas
            .AsNoTracking()
            .Include(d => d.Jogador1)
            .Include(d => d.Jogador2)
            .Where(d => d.Categoria.TorneioId == torneioId
                     && (d.UltimaFase == Campea || d.UltimaFase == Vice || d.UltimaFase == Semi))
            .ToListAsync();

        return categorias
            .Select(categoria =>
            {
                var daCategoria = duplas.Where(d => d.CategoriaId == categoria.Id).ToList();

                // O maior Id ganha entre carimbos repetidos, igual ao CampeoesDoTorneio: "não
                // deveria haver dois" não é "não pode", e um acerto na mão no banco basta.
                string? UmSo(string fase) => daCategoria
                    .Where(d => d.UltimaFase == fase)
                    .OrderByDescending(d => d.Id)
                    .Select(NomeDaDupla.Na)
                    .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));

                return new PodioDeCategoria(
                    CategoriaId: categoria.Id,
                    Categoria: categoria.Nome,
                    Campeao: UmSo(Campea),
                    Vice: UmSo(Vice),
                    Semifinalistas: daCategoria
                        .Where(d => d.UltimaFase == Semi)
                        .Select(NomeDaDupla.Na)
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
                        .ToList(),
                    Torneio: torneio.Nome,
                    Clube: nomeDoClube,
                    Data: torneio.DataInicio);
            })
            // A MESMA ordem das outras telas do torneio (masculinas da mais forte pra mais
            // fraca, depois femininas, depois mista/casais).
            .OrderBy(p => CategoriaNaTela.Ordem(p.Categoria))
            .ToList();
    }

    // O pódio de UMA categoria — o que o desenho do card precisa. Nulo quando o torneio não
    // pode anunciar ou a categoria não é dele. Um pódio com `TemOQueMostrar` falso é outra
    // coisa: a categoria existe e ainda não terminou.
    public static async Task<PodioDeCategoria?> DaCategoriaAsync(
        DbPadelContext contexto, int torneioId, int categoriaId)
    {
        var todos = await DoTorneioAsync(contexto, torneioId);
        return todos.FirstOrDefault(p => p.CategoriaId == categoriaId);
    }
}

public sealed record PodioDeCategoria(
    int CategoriaId,
    string Categoria,
    string? Campeao,
    string? Vice,
    List<string> Semifinalistas,
    string Torneio,
    string? Clube,
    DateTime? Data)
{
    // ⚠️ SEM VICE NÃO É PÓDIO. Um degrau só já tem card próprio (o de campeão), e é o formato
    // certo pro Americano e pra categoria que parou no meio. Publicar "pódio" com um nome
    // faria a arte prometer três lugares e entregar um.
    public bool TemOQueMostrar =>
        !string.IsNullOrWhiteSpace(Campeao) && !string.IsNullOrWhiteSpace(Vice);
}
