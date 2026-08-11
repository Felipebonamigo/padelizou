using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// O dono que não defende perde o cinturão. Este é o varredor que executa isso.
//
// ⚠️ A REGRA É A METADE MAIS IMPORTANTE DO CINTURÃO. Sem ela, ganhar e sumir seria a estratégia
// ótima: o campeão viraria um nome parado numa tela, e o convite pra desafiá-lo — que é o motivo
// de o cinturão existir — deixaria de valer alguma coisa.
//
// ⚠️ A TELA NÃO DEPENDE DESTE VIGIA. Quantas faltam pro dono perder é lido do relógio
// (Cinturao.QuantasFaltamParaPerder), então um vigia parado ATRASA a troca em vez de esconder o
// estado. É a mesma ordem de prioridade do fechamento de placar: primeiro a tela conta a
// verdade, depois o job materializa.
public class VigiaDoCinturaoBackgroundService : BackgroundService
{
    // De 6 em 6 horas. A janela da regra é de 14 dias e o gatilho é uma recusa — apurar isso de
    // hora em hora seria varrer a tabela 24 vezes por dia pra adiantar uma troca em minutos que
    // ninguém está cronometrando.
    private static readonly TimeSpan IntervaloTick = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VigiaDoCinturaoBackgroundService> _logger;

    public VigiaDoCinturaoBackgroundService(IServiceScopeFactory scopeFactory,
        ILogger<VigiaDoCinturaoBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(IntervaloTick);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CobrarDefesaAsync(stoppingToken);
        }
    }

    private async Task CobrarDefesaAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbPadelContext>();
            var cinturao = scope.ServiceProvider.GetRequiredService<MovimentacaoDoCinturao>();

            var agora = DateTime.Now;

            var donos = await context.ReinadosNoCinturao
                .Where(r => r.TerminouEm == null)
                .ToListAsync(stoppingToken);

            var trocas = 0;
            foreach (var dono in donos)
            {
                // Um a um, com a movimentação salvando cada troca: um cinturão que estoure não
                // pode levar junto os outros da varredura.
                if (await cinturao.TransferirPorOmissaoAsync(dono, agora, stoppingToken) != null)
                    trocas++;
            }

            if (trocas > 0)
                _logger.LogInformation("Cinturões que mudaram de mão por falta de defesa: {Quantidade}", trocas);
        }
        catch (Exception ex)
        {
            // Log e segue: o próximo tick tenta de novo. Derrubar o serviço faria a defesa
            // obrigatória parar de vez por causa de uma linha ruim.
            _logger.LogError(ex, "Falha ao cobrar a defesa dos cinturões");
        }
    }
}
