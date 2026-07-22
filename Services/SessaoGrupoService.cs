using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;

namespace Padelizou.Services;

public class SessaoGrupoService : ISessaoGrupoService
{
    private readonly DbPadelContext _context;

    public SessaoGrupoService(DbPadelContext context)
    {
        _context = context;
    }

    public async Task<SessaoGrupo> ObterOuCriarSessaoAsync(GrupoPrivado grupo, DateTime? dataSolicitada)
    {
        var dataHora = dataSolicitada ?? ProximaOcorrencia(grupo.DiaSemanaFixo!.Value, grupo.HorarioFixo!.Value);

        var sessao = await _context.SessoesGrupo
            .Include(s => s.Confirmacoes).ThenInclude(c => c.Jogador)
            .FirstOrDefaultAsync(s => s.GrupoId == grupo.Id && s.DataHora == dataHora);

        if (sessao != null) return sessao;

        sessao = new SessaoGrupo { GrupoId = grupo.Id, DataHora = dataHora };
        _context.SessoesGrupo.Add(sessao);
        await _context.SaveChangesAsync();

        var membros = await _context.JogadoresGrupo
            .Include(jg => jg.Jogador)
            .Where(jg => jg.GrupoId == grupo.Id)
            .Select(jg => jg.Jogador)
            .ToListAsync();

        foreach (var membro in membros)
        {
            _context.ConfirmacoesSessao.Add(new ConfirmacaoSessao
            {
                SessaoId = sessao.Id,
                JogadorId = membro.Id,
                Status = "Pendente",
                Lado = membro.LadoQuadra,
                Avulso = false
            });
        }
        await _context.SaveChangesAsync();

        return await _context.SessoesGrupo
            .Include(s => s.Confirmacoes).ThenInclude(c => c.Jogador)
            .FirstAsync(s => s.Id == sessao.Id);
    }

    private static DateTime ProximaOcorrencia(int diaSemanaFixo, TimeSpan horarioFixo)
    {
        var hoje = DateTime.Today;
        var diasAteAlvo = (diaSemanaFixo - (int)hoje.DayOfWeek + 7) % 7;
        var data = hoje.AddDays(diasAteAlvo).Add(horarioFixo);
        if (data < DateTime.Now) data = data.AddDays(7);
        return data;
    }
}
