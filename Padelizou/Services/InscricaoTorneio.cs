using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// Regras de inscrição que valem igual pra dupla e pra americano — ficam aqui pra não
// existirem em duas versões que divergem com o tempo (foi o que já aconteceu com as
// duas grafias de fase de grupos, ver FasesTorneio).
public static class InscricaoTorneio
{
    // Quando o organizador desliga PermiteMultiplasCategorias, cada jogador fica preso a
    // uma categoria só. Devolve a mensagem de bloqueio, ou null se pode seguir.
    //
    // categoriaAlvoId != null exclui a própria categoria da checagem — serve pra troca de
    // parceiro, em que o jogador já está inscrito ali e não pode se auto-bloquear.
    public static async Task<string?> MotivoBloqueioMultiplasCategoriasAsync(
        DbPadelContext context, Torneio torneio, IEnumerable<int> jogadorIds, int? ignorarCategoriaId = null)
    {
        if (torneio.PermiteMultiplasCategorias) return null;

        var ids = jogadorIds.Distinct().ToList();
        if (ids.Count == 0) return null;

        var jaInscritos = await context.Duplas
            .Where(d => d.Categoria.TorneioId == torneio.Id
                     && d.CategoriaId != ignorarCategoriaId
                     && (ids.Contains(d.Jogador1Id) || (d.Jogador2Id != null && ids.Contains(d.Jogador2Id.Value))))
            .Select(d => new
            {
                Categoria = d.Categoria.Nome,
                Nome1 = d.Jogador1.Nome,
                J1 = d.Jogador1Id,
                Nome2 = d.Jogador2 != null ? d.Jogador2.Nome : null,
                J2 = d.Jogador2Id,
            })
            .ToListAsync();

        var conflitos = new List<string>();
        foreach (var d in jaInscritos)
        {
            if (ids.Contains(d.J1)) conflitos.Add($"{d.Nome1} (já está na {d.Categoria})");
            if (d.J2 != null && ids.Contains(d.J2.Value) && d.Nome2 != null)
                conflitos.Add($"{d.Nome2} (já está na {d.Categoria})");
        }

        var americanos = await context.InscricoesAmericanas
            .Where(i => i.Categoria.TorneioId == torneio.Id
                     && i.CategoriaId != ignorarCategoriaId
                     && ids.Contains(i.JogadorId))
            .Select(i => new { Categoria = i.Categoria.Nome, Nome = i.Jogador.Nome })
            .ToListAsync();

        conflitos.AddRange(americanos.Select(a => $"{a.Nome} (já está na {a.Categoria})"));

        if (conflitos.Count == 0) return null;

        return $"Neste torneio cada jogador só pode disputar uma categoria: "
             + string.Join(", ", conflitos.Distinct()) + ".";
    }
}
