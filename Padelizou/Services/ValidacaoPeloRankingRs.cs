using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// A regra de inscrição que consulta o Ranking RS.
//
// Mora num serviço, e não dentro do controller, porque DUAS telas inscrevem gente na mesma
// categoria — o formulário de inscrição (DuplasController.Create) e a troca de parceiro
// (DuplasController.TrocarParceiro). Regra de torneio escrita em dois lugares é a origem
// documentada dos piores defeitos deste projeto: uma cópia é corrigida, a outra não, e o
// sistema passa a responder coisas diferentes conforme a tela por onde a pessoa entrou.
//
// TRÊS COISAS QUE ESTE ARQUIVO NUNCA FAZ:
//  1. Recusar quando não deu pra consultar. Ranking fora do ar, chave estrangulada, categoria
//     que não casa — tudo isso deixa a inscrição passar. O contrário transformaria uma queda
//     na casa dos outros em "ninguém consegue se inscrever" num sábado de manhã.
//  2. Dar a palavra final. A recusa vira LINHA em BloqueioDoRanking, e quem decide se ela fica
//     de pé é o organizador.
//  3. Consultar sem o organizador ter pedido. `Torneio.ValidarPeloRankingRs` nasce desligado.
public class ValidacaoPeloRankingRs
{
    private readonly DbPadelContext _context;
    private readonly IRankingRsService _ranking;
    private readonly ILogger<ValidacaoPeloRankingRs> _logger;

    public ValidacaoPeloRankingRs(DbPadelContext context, IRankingRsService ranking,
        ILogger<ValidacaoPeloRankingRs> logger)
    {
        _context = context;
        _ranking = ranking;
        _logger = logger;
    }

    public record Pessoa(string Nome, string Cpf);

    // null = pode se inscrever. Texto = a recusa que o jogador lê.
    public async Task<string?> MotivoDeRecusaAsync(Torneio torneio, Categoria categoria,
        IReadOnlyList<Pessoa> pessoas, CancellationToken ct = default)
    {
        if (!torneio.ValidarPeloRankingRs) return null;
        if (!_ranking.Configurado) return null;

        // Categoria sem par no ranking não é validada. Ver Services/CategoriaDoRankingRs: o
        // nome no Padelizou é texto livre e a API deles não confere sexo, então casar no chute
        // devolveria um "APROVADO" tranquilo e errado.
        if (categoria.RankingRsCategoriaId is not int categoriaRsId) return null;

        var candidatos = pessoas
            .Where(p => !string.IsNullOrWhiteSpace(p.Cpf) && !string.IsNullOrWhiteSpace(p.Nome))
            .GroupBy(p => p.Cpf).Select(g => g.First())  // a mesma pessoa não é perguntada duas vezes
            .ToList();
        if (candidatos.Count == 0) return null;

        // Quem o organizador já liberou nesta categoria nem chega a ser consultado: a decisão
        // dele vale mais que a resposta da API, e reconsultar só gastaria a cota da chave.
        var cpfs = candidatos.Select(p => p.Cpf).ToList();
        var jaLiberados = await _context.BloqueiosDoRanking
            .Where(b => b.CategoriaId == categoria.Id
                        && cpfs.Contains(b.Cpf)
                        && b.Situacao == SituacaoDoBloqueio.Liberado)
            .Select(b => b.Cpf)
            .ToListAsync(ct);

        var aConsultar = candidatos.Where(p => !jaLiberados.Contains(p.Cpf)).ToList();
        if (aConsultar.Count == 0) return null;

        var resultados = await _ranking.ValidarLoteAsync(
            aConsultar.Select(p => new AtletaParaValidar(p.Nome, categoriaRsId, p.Cpf)).ToList(), ct);

        var reprovados = resultados.Where(r => r.Reprovado && r.Referencia != null).ToList();
        if (reprovados.Count == 0) return null;

        foreach (var r in reprovados)
        {
            await RegistrarAsync(torneio, categoria, categoriaRsId, r, ct);
        }
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Ranking RS barrou {Quantos} pessoa(s) na categoria {Categoria} do torneio {Torneio}.",
            reprovados.Count, categoria.Id, torneio.Id);

        return Frase.Recusa(categoria.Nome, reprovados);
    }

    // Uma linha por pessoa em cada categoria — tentar de novo ATUALIZA, não empilha.
    private async Task RegistrarAsync(Torneio torneio, Categoria categoria, int categoriaRsId,
        ResultadoDoRanking resultado, CancellationToken ct)
    {
        var cpf = resultado.Referencia!;
        var existente = await _context.BloqueiosDoRanking
            .FirstOrDefaultAsync(b => b.CategoriaId == categoria.Id && b.Cpf == cpf, ct);

        var motivo = resultado.Motivo(categoria.Nome);

        if (existente == null)
        {
            _context.BloqueiosDoRanking.Add(new BloqueioDoRanking
            {
                TorneioId = torneio.Id,
                CategoriaId = categoria.Id,
                Cpf = cpf,
                NomeConsultado = resultado.Nome,
                CategoriaRsId = categoriaRsId,
                CategoriaRsNome = CategoriaDoRankingRs.NomeDoId(categoriaRsId),
                Motivo = motivo,
                Situacao = SituacaoDoBloqueio.Pendente,
            });
            return;
        }

        // Já existia. Atualiza o motivo (o ranking muda com o tempo) e a data da tentativa,
        // mas NÃO mexe na Situacao: se o organizador já bateu o martelo em "Mantido", uma nova
        // tentativa do jogador não pode devolver o caso pra fila como se fosse novidade.
        existente.NomeConsultado = resultado.Nome;
        existente.Motivo = motivo;
        existente.CriadoEm = DateTime.Now;
    }

    // As frases ficam separadas e estáticas pra poderem ser testadas sem banco nem rede.
    public static class Frase
    {
        public const string OrganizadorVaiAvaliar =
            "O organizador foi avisado e vai decidir se libera a inscrição.";

        public static string Recusa(string categoriaNoPadelizou, IReadOnlyList<ResultadoDoRanking> reprovados)
        {
            var motivos = string.Join(" ", reprovados.Select(r => r.Motivo(categoriaNoPadelizou)));
            return $"{motivos} {OrganizadorVaiAvaliar}";
        }
    }
}
