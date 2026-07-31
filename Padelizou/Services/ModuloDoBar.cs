using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// Quem pode abrir o módulo do bar e das contas do clube.
//
// Existe como serviço, e não como método copiado em cada controller, porque são duas
// perguntas coladas — "o módulo está aberto pra esta pessoa?" e "ela manda neste clube?" — e
// duas cópias de uma regra de permissão divergem no dia em que só uma delas é atualizada.
// A resposta é a mesma pro balcão, pro cardápio e pras contas.
public class ModuloDoBar
{
    private readonly DbPadelContext _context;
    private readonly BarSettings _settings;

    public ModuloDoBar(DbPadelContext context, Microsoft.Extensions.Options.IOptions<BarSettings> settings)
    {
        _context = context;
        _settings = settings.Value;
    }

    // Em construção = só admin do Padelizou. É isto que mantém o módulo invisível pros donos
    // de clube sem precisar de branch separada (ver BarSettings).
    public bool EmConstrucao => !_settings.Habilitado;

    public async Task<bool> PodeUsarAsync(int clubeId, int? usuarioId)
    {
        if (usuarioId == null) return false;

        if (EmConstrucao && !await EhAdminDoPadelizouAsync(usuarioId)) return false;

        return await _context.Clubes.AnyAsync(c => c.Id == clubeId && c.DonoId == usuarioId)
            || await _context.ClubeAdministradores.AnyAsync(a => a.ClubeId == clubeId && a.JogadorId == usuarioId);
    }

    // Serve também pra decidir se o ATALHO aparece no painel do clube. Esconder o link é
    // cortesia; a trava de verdade é o PodeUsarAsync repetido em toda ação.
    public async Task<bool> MostrarAtalhoAsync(int? usuarioId) =>
        !EmConstrucao || await EhAdminDoPadelizouAsync(usuarioId);

    private Task<bool> EhAdminDoPadelizouAsync(int? usuarioId) =>
        _context.Jogadores.AnyAsync(j => j.Id == usuarioId && (j.IsAdminGeral || j.IsAdminRaiz));
}
