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
    private readonly PlanoClubeSettings _plano;

    public ModuloFiscal(DbPadelContext context, ModuloDoBar bar,
        Microsoft.Extensions.Options.IOptions<FiscalSettings> settings,
        Microsoft.Extensions.Options.IOptions<PlanoClubeSettings>? plano = null)
    {
        _context = context;
        _bar = bar;
        _settings = settings.Value;
        _plano = plano?.Value ?? new PlanoClubeSettings();
    }

    public bool EmConstrucao => !_settings.Habilitado;

    public async Task<bool> PodeUsarAsync(int clubeId, int? usuarioId) =>
        await AcessoAsync(clubeId, usuarioId) == AcessoAoModulo.Liberado;

    // A resposta completa, como no ModuloDoBar — e aqui a distinção vale ainda mais.
    //
    // ⚠️ EM CONSTRUÇÃO É "SEM PERMISSÃO", NUNCA "SEM PLANO", e essa linha existe porque
    // confundir as duas quase virou um defeito de verdade: mandar o dono do clube pra tela de
    // assinatura enquanto o módulo está em construção seria oferecer a ele um plano Fiscal que
    // ainda não emite nada. Vender o que não existe é pior do que não vender.
    public async Task<AcessoAoModulo> AcessoAsync(int clubeId, int? usuarioId)
    {
        var acessoAoBar = await _bar.AcessoAsync(clubeId, usuarioId);
        if (acessoAoBar != AcessoAoModulo.Liberado) return acessoAoBar;

        var ehAdminDoPadelizou = await EhAdminDoPadelizouAsync(usuarioId);

        if (EmConstrucao)
            return ehAdminDoPadelizou ? AcessoAoModulo.Liberado : AcessoAoModulo.SemPermissao;

        // Admin do Padelizou entra sem plano — é ele quem atende o clube que ligou dizendo que
        // a nota não sai. Mesma razão do ModuloDoBar.
        if (ehAdminDoPadelizou) return AcessoAoModulo.Liberado;

        // O plano Gestão não inclui emissão: quem está nele e clica em "Fiscal" tem que ver o
        // convite pra subir de plano, e é o SemPlano que leva o controller até lá.
        var clube = await _context.Clubes.FirstOrDefaultAsync(c => c.Id == clubeId);

        return clube != null && PlanoDoClube.LiberaFiscal(clube, DateTime.Now, _plano)
            ? AcessoAoModulo.Liberado
            : AcessoAoModulo.SemPlano;
    }

    // Esconder o link é cortesia; a trava de verdade é o PodeUsarAsync repetido em toda ação.
    public Task<bool> MostrarAtalhoAsync(int clubeId, int? usuarioId) => PodeUsarAsync(clubeId, usuarioId);

    // Verdadeiro só quando a ÚNICA coisa entre esta pessoa e a tela é o módulo não existir
    // ainda — ou seja: ela manda no bar deste clube e entraria hoje se o interruptor
    // estivesse ligado.
    //
    // Existe pra que a tela de acesso negado possa dizer "ainda não está no ar" em vez de
    // "essa parte não é sua". A distinção não é estética: pro estranho que nunca teria acesso,
    // "em breve" é uma promessa falsa, e ele fica esperando uma porta que não vai abrir.
    //
    // Repete a pergunta do bar em vez de reaproveitar a resposta do AcessoAsync porque o
    // AcessoAoModulo não carrega a CAUSA — "sem permissão" tanto vale pro estranho quanto pra
    // obra. Custa uma consulta a mais, e só no caminho de quem já levou recusa.
    public async Task<bool> SoFaltaEntrarNoArAsync(int clubeId, int? usuarioId) =>
        EmConstrucao && await _bar.AcessoAsync(clubeId, usuarioId) == AcessoAoModulo.Liberado;

    private Task<bool> EhAdminDoPadelizouAsync(int? usuarioId) =>
        _context.Jogadores.AnyAsync(j => j.Id == usuarioId && (j.IsAdminGeral || j.IsAdminRaiz));
}
