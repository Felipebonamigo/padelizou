using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// Por que o acesso foi negado. Existe porque "não pode" e "não pagou" pedem telas DIFERENTES:
// quem não manda no clube leva Forbid, e quem manda mas está sem plano tem que ser levado pra
// tela de assinatura — mandar um cliente em potencial pra um 403 é perder a venda na porta.
public enum AcessoAoModulo
{
    Liberado,
    SemPermissao,
    SemPlano
}

// Quem pode abrir o módulo do bar e das contas do clube.
//
// Existe como serviço, e não como método copiado em cada controller, porque são três perguntas
// coladas — "o módulo está aberto pra esta pessoa?", "ela manda neste clube?" e "o clube
// assinou?" — e duas cópias de uma regra de permissão divergem no dia em que só uma delas é
// atualizada. A resposta é a mesma pro balcão, pro cardápio e pras contas.
public class ModuloDoBar
{
    private readonly DbPadelContext _context;
    private readonly BarSettings _settings;
    private readonly PlanoClubeSettings _plano;

    public ModuloDoBar(DbPadelContext context,
        Microsoft.Extensions.Options.IOptions<BarSettings> settings,
        Microsoft.Extensions.Options.IOptions<PlanoClubeSettings>? plano = null)
    {
        _context = context;
        _settings = settings.Value;
        _plano = plano?.Value ?? new PlanoClubeSettings();
    }

    // Em construção = só admin do Padelizou. É isto que mantém o módulo invisível pros donos
    // de clube sem precisar de branch separada (ver BarSettings).
    public bool EmConstrucao => !_settings.Habilitado;

    public async Task<bool> PodeUsarAsync(int clubeId, int? usuarioId) =>
        await AcessoAsync(clubeId, usuarioId) == AcessoAoModulo.Liberado;

    // A resposta completa: além de poder ou não, POR QUE não.
    //
    // ⚠️ A ORDEM É PERMISSÃO ANTES DE PLANO, e não é detalhe: quem não manda no clube não pode
    // nem descobrir se ele assinou. Perguntar o plano primeiro transformaria esta função num
    // detector de clientes pagantes pra qualquer pessoa logada.
    public async Task<AcessoAoModulo> AcessoAsync(int clubeId, int? usuarioId)
    {
        if (usuarioId == null) return AcessoAoModulo.SemPermissao;

        var ehAdminDoPadelizou = await EhAdminDoPadelizouAsync(usuarioId);

        if (EmConstrucao && !ehAdminDoPadelizou) return AcessoAoModulo.SemPermissao;

        var mandaNoClube =
            await _context.Clubes.AnyAsync(c => c.Id == clubeId && c.DonoId == usuarioId)
            || await _context.ClubeAdministradores.AnyAsync(a => a.ClubeId == clubeId && a.JogadorId == usuarioId);

        if (!mandaNoClube) return AcessoAoModulo.SemPermissao;

        // ⚠️ O admin do Padelizou entra SEM assinatura, e isso é obrigatório, não cortesia:
        // é ele quem dá suporte ao clube que ligou dizendo que o balcão travou, e um suporte
        // que não consegue ver a tela do cliente não é suporte. Também é o que mantém o
        // módulo utilizável enquanto ele está em construção, antes de existir plano nenhum.
        if (ehAdminDoPadelizou) return AcessoAoModulo.Liberado;

        var clube = await _context.Clubes.FirstOrDefaultAsync(c => c.Id == clubeId);
        if (clube == null) return AcessoAoModulo.SemPermissao;

        return PlanoDoClube.LiberaGestao(clube, DateTime.Now, _plano)
            ? AcessoAoModulo.Liberado
            : AcessoAoModulo.SemPlano;
    }

    // Serve pra decidir se o ATALHO aparece no painel do clube. Esconder o link é cortesia; a
    // trava de verdade é o AcessoAsync repetido em toda ação.
    //
    // ⚠️ Aqui NÃO se pergunta pelo plano de propósito: o atalho é justamente por onde o dono
    // do clube descobre que o bar existe e chega na tela de assinatura. Escondê-lo de quem não
    // assinou seria esconder a vitrine de quem ainda não comprou.
    public async Task<bool> MostrarAtalhoAsync(int? usuarioId) =>
        !EmConstrucao || await EhAdminDoPadelizouAsync(usuarioId);

    private Task<bool> EhAdminDoPadelizouAsync(int? usuarioId) =>
        _context.Jogadores.AnyAsync(j => j.Id == usuarioId && (j.IsAdminGeral || j.IsAdminRaiz));
}
