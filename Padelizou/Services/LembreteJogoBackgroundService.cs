using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;

namespace Padelizou.Services;

// Não existia nenhum job de background no projeto — este é o primeiro. Roda periodicamente e
// avisa, pelo app, TODO MUNDO QUE ESTÁ NA LISTA do jogo fixo da semana quando faltam menos de
// 24h e o dono ativou a opção em Configuracoes.
//
// ⚠️ ELE MUDOU DE PAPEL EM 21/08/2026. Antes cobrava confirmação de quem não tinha respondido;
// com a presença presumida, quem não respondeu JÁ ESTÁ dentro — o aviso deixou de perguntar e
// passou a informar, oferecendo a saída. Ver Services/AvisoDoJogoDaSemana.
//
// ⚠️ E ELE CONTINUA LENDO `grupo.EnviarLembrete24h`. Não tirar essa flag do Where lá em cima:
// `ProcessarGrupoAsync` chama `ObterOuCriarSessaoAsync` ANTES da checagem de janela, e pedir a
// sessão CRIA — sem a flag, o timer escalaria a panelinha inteira de todo grupo do sistema pra
// dentro da lista, a cada 15 minutos, sem nenhum humano na frente.
public class LembreteJogoBackgroundService : BackgroundService
{
    private static readonly TimeSpan IntervaloTick = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LembreteJogoBackgroundService> _logger;

    public LembreteJogoBackgroundService(IServiceScopeFactory scopeFactory, ILogger<LembreteJogoBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(IntervaloTick);

        // Roda uma vez logo na subida do app, sem esperar o primeiro tick do timer.
        await ProcessarAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessarAsync(stoppingToken);
        }
    }

    private async Task ProcessarAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbPadelContext>();
            var sessaoGrupoService = scope.ServiceProvider.GetRequiredService<ISessaoGrupoService>();
            var whatsAppService = scope.ServiceProvider.GetRequiredService<IWhatsAppService>();
            var pushService = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();

            var grupos = await context.GruposPrivados
                .Include(g => g.Clube)
                .Where(g => g.EnviarLembrete24h && g.DiaSemanaFixo != null && g.HorarioFixo != null)
                .ToListAsync(stoppingToken);

            foreach (var grupo in grupos)
            {
                await ProcessarGrupoAsync(grupo, context, sessaoGrupoService, whatsAppService, pushService, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao processar lembretes de 24h.");
        }
    }

    private async Task ProcessarGrupoAsync(
        GrupoPrivado grupo, DbPadelContext context, ISessaoGrupoService sessaoGrupoService,
        IWhatsAppService whatsAppService, IPushNotificationService pushService, CancellationToken stoppingToken)
    {
        var sessao = await sessaoGrupoService.ObterOuCriarSessaoAsync(grupo, null);

        var agora = DateTime.Now;
        if (agora < sessao.DataHora.AddHours(-24) || agora >= sessao.DataHora)
        {
            return; // fora da janela de 24h (cedo demais ou o jogo já começou/passou)
        }

        // ⚠️ TRÊS FILTROS MUDARAM EM 21/08/2026, e dois deles já estavam errados antes:
        //
        //   • `Status != NaoVai` — NÃO EXISTIA. O job mandava "confirma presença" até pra quem
        //     tinha acabado de apertar "Não vou". Já era bug, e a presunção só deixa mais visível.
        //   • SAIU `Celular` — resto da era do WhatsApp. O aviso sai por push/caixa/e-mail desde
        //     09/08, então quem não tem celular cadastrado era CONTADO como avisado e nunca
        //     recebia nada.
        //   • SAIU `AceitaConvitesJogo`, ENTROU `NotificarAvisoJogo` — aquela é a chave de
        //     "estranhos podem me chamar pra jogar", não tem nada a ver com o jogo fixo da
        //     própria panelinha. Esta é a preferência certa, e mantém um opt-out de verdade.
        //   • ENTROU `ExcluidoEm == null` — cinto e suspensório: conta anonimizada não recebe.
        var aAvisar = sessao.Confirmacoes
            .Where(c => !c.Avulso
                     && c.LembreteEnviadoEm == null
                     && c.Status != PresencaNaSessao.NaoVai
                     && c.Jogador.ExcluidoEm == null
                     && c.Jogador.NotificarAvisoJogo)
            .ToList();

        foreach (var confirmacao in aAvisar)
        {
            bool confirmou = confirmacao.Status == PresencaNaSessao.Confirmado;

            // Um aviso só, por um caminho só. Até 04/08/2026 este lembrete mandava WhatsApp
            // DIRETO e depois chamava o push — que, desde que o fan-out entrou, também manda
            // WhatsApp. Resultado: a mesma pessoa recebia a mesma coisa duas vezes, e mensagem
            // repetida é o que faz alguém apertar "bloquear" (e "denunciar" logo em seguida).
            await pushService.EnviarParaJogadorAsync(
                confirmacao.JogadorId,
                AvisoDoJogoDaSemana.Titulo(confirmou),
                AvisoDoJogoDaSemana.Corpo(confirmou, grupo.Nome, sessao.DataHora, grupo.Clube?.Nome),
                // ⚠️ CAI NA TELA DA SEMANA, e não em /Agenda: a Agenda lista só os JogosSemanais
                // JÁ REGISTRADOS, ou seja, o aviso caía numa página SEM o botão de sair. Um aviso
                // que pede pra avisar e leva pra tela errada não é aviso.
                //
                // Fora do WhatsApp por decisão do Felipe (09/08/2026): o jogo fixo é o mesmo dia
                // e a mesma hora toda semana, e quem está no grupo já sabe que ele existe. Mais
                // ainda agora, que ele sai pra TODA a lista e não só pra quem não respondeu — é
                // a forma de rajada que já custou o número uma vez.
                $"/Grupos/Semana?grupoId={grupo.Id}&data={sessao.DataHora:s}");

            // Marcado como enviado por ter sido DESPACHADO, não entregue: a fila entrega
            // depois, e insistir a cada passada transformaria um lembrete em perseguição.
            confirmacao.LembreteEnviadoEm = agora;
        }

        if (aAvisar.Any())
        {
            await context.SaveChangesAsync(stoppingToken);
        }
    }
}
