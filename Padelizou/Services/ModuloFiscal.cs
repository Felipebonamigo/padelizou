using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// Quem pode abrir a parte fiscal do clube.
//
// São DUAS perguntas, e as duas precisam responder sim: "esta pessoa manda neste clube?" —
// que é exatamente a mesma pergunta do bar, e por isso é respondida pelo ModuloDoBar em vez
// de recopiada aqui — e "o plano fiscal está ligado pra ela?", que é a pergunta nova.
//
// A ordem importa: o fiscal é um degrau ACIMA do bar. Não existe emitir nota de uma venda que
// o sistema não registra, então quem não pode usar o bar nunca pode usar o fiscal.
public class ModuloFiscal
{
    private readonly DbPadelContext _context;
    private readonly ModuloDoBar _bar;
    private readonly FiscalSettings _settings;

    public ModuloFiscal(DbPadelContext context, ModuloDoBar bar,
        Microsoft.Extensions.Options.IOptions<FiscalSettings> settings)
    {
        _context = context;
        _bar = bar;
        _settings = settings.Value;
    }

    public bool EmConstrucao => !_settings.Habilitado;

    public async Task<bool> PodeUsarAsync(int clubeId, int? usuarioId)
    {
        if (!await _bar.PodeUsarAsync(clubeId, usuarioId)) return false;

        return !EmConstrucao || await EhAdminDoPadelizouAsync(usuarioId);
    }

    // Esconder o link é cortesia; a trava de verdade é o PodeUsarAsync repetido em toda ação.
    public Task<bool> MostrarAtalhoAsync(int clubeId, int? usuarioId) => PodeUsarAsync(clubeId, usuarioId);

    private Task<bool> EhAdminDoPadelizouAsync(int? usuarioId) =>
        _context.Jogadores.AnyAsync(j => j.Id == usuarioId && (j.IsAdminGeral || j.IsAdminRaiz));
}
