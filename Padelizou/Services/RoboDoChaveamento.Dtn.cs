using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// A CHAVE DTN DO TRETA (25/11/2026) — ver TRETA.md e Services/ChaveDtn.
//
// Quando a fase de grupos de um módulo com `DuplasNoDtnPorGrupo > 0` FECHA INTEIRA, nasce a
// categoria DTN: cópias dos últimos de cada grupo, numa chave direta invertida.
//
// ⚠️ CÓPIAS, e não as duplas originais. Há ~10 caminhos (W.O., W.O. tardio, correção de
// placar, reabrir) que carimbam `UltimaFase` na dupla do jogo: com a original, o perdedor de
// uma semi DTN apareceria como semifinalista do MÓDULO, no pódio e no perfil. Com a cópia o
// carimbo cai na DTN, que está fora de ranking, dinheiro e MVP (entrega 1). É o caminho do
// `ChaveDireta`, que já remonta duplas numa categoria paralela.
public partial class RoboDoChaveamento
{
    private async Task MontarChaveDtnSeOsGruposFecharamAsync(
        Categoria modulo, int? torneioId, List<Partida> partidasDeGrupo)
    {
        if (modulo.DuplasNoDtnPorGrupo <= 0 || modulo.PapelDaChave != null) return;
        if (partidasDeGrupo.Count == 0 || partidasDeGrupo.Any(p => p.Status != "Finalizada")) return;

        // A DTN pode já existir VAZIA (criada no sorteio pra prévia ter onde prender a reserva de
        // horário — ChaveDtn.GarantirCategoriaAsync). Vazia, é aqui que ela é preenchida; com
        // dupla ou jogo, já foi montada e não se monta de novo.
        var existente = await _context.Categorias.FirstOrDefaultAsync(c =>
            c.CategoriaDeOrigemId == modulo.Id && c.PapelDaChave == ChaveDtn.Dtn);
        if (existente != null
            && (await _context.Duplas.AnyAsync(d => d.CategoriaId == existente.Id)
                || await _context.Partidas.AnyAsync(p => p.CategoriaId == existente.Id)))
            return;

        var grupos = modulo.GruposTorneio.OrderBy(g => g.Nome).ToList();
        var duplasDosGrupos = grupos.SelectMany(g => g.Duplas).ToList();
        var pontos = await ClassificacaoDeGrupos.PontosSePrecisarAsync(
            duplasDosGrupos, partidasDeGrupo, _estatisticas.ObterPontosPorJogadorAsync);
        var rankings = grupos
            .Select(g => (IReadOnlyList<ClassificacaoDeGrupos.Linha>)
                ClassificacaoDeGrupos.Ordenar(g.Duplas, partidasDeGrupo, pontos))
            .ToList();

        var doPiorProMelhor = ChaveDtn.QuemVaiProDtn(
            rankings, modulo.DuplasNoDtnPorGrupo, ClassificacaoDeGrupos.VagasPorGrupo(modulo));
        if (ChaveamentoMataMata.ProblemaNaChaveDireta(doPiorProMelhor.Count) != null) return;

        var dtn = existente ?? await ChaveDtn.GarantirCategoriaAsync(_context, modulo);

        // Uma a uma e salvando: o Id da cópia segue a ordem do pior pro melhor, que é a ordem
        // em que a chave direta (AvancoDaChave.ByesDaCategoriaAsync) lê quem folgou.
        var originais = duplasDosGrupos.ToDictionary(d => d.Id);
        var copias = new List<int>();
        foreach (var id in doPiorProMelhor)
        {
            var original = originais[id];
            var copia = new Dupla
            {
                Categoria = dtn,
                Jogador1Id = original.Jogador1Id,
                Jogador2Id = original.Jogador2Id,
                LadoJogador1 = original.LadoJogador1,
                // Não é inscrição: nasce paga, senão vira cobrança — o mesmo dos times.
                Pago = true,
            };
            _context.Duplas.Add(copia);
            await _context.SaveChangesAsync();
            copias.Add(copia.Id);
        }

        var primeiraRodada = ChaveamentoMataMata.MontarChaveDireta(copias);
        var jogos = primeiraRodada.Confrontos.Select(confronto => new Partida
        {
            TorneioId = torneioId,
            CategoriaId = dtn.Id,
            Dupla1Id = confronto.Dupla1Id,
            Dupla2Id = confronto.Dupla2Id,
            Fase = primeiraRodada.Fase,
            Status = "Agendada",
            Codigo = Guid.NewGuid().ToString().Substring(0, 6).ToUpper(),
        }).ToList();

        await AgendarNaGradeAsync(jogos, torneioId);
        _context.Partidas.AddRange(jogos);
        await _context.SaveChangesAsync();
    }
}
