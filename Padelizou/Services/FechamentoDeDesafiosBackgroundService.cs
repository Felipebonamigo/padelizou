using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// Placar lançado que passou das 72h sem contestação vira Confirmado, com os pontos congelados.
//
// ⚠️ A TELA NÃO DEPENDE DESTE JOB. Quem responde "esse placar já valeu?" pras pessoas é
// `EstadoDoDesafio.StatusNaTela`, lendo o relógio. Este varredor só MATERIALIZA no banco o que
// o relógio já decidiu — porque o ponto precisa ser congelado numa linha pra o ranking somar.
//
// A ordem importa e é de propósito: se este serviço morrer, a tela continua certa e o que
// atrasa é o ranking. O contrário — tela dependendo do job — é como um job que falha calado
// transforma jogo fechado em jogo pendurado, e ninguém descobre até alguém reclamar.
public class FechamentoDeDesafiosBackgroundService : BackgroundService
{
    // De hora em hora: o prazo é de 72h, e apurar isso a cada 15 minutos seria varrer a tabela
    // 96 vezes por dia pra adiantar um fechamento em minutos que ninguém está esperando.
    private static readonly TimeSpan IntervaloTick = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FechamentoDeDesafiosBackgroundService> _logger;

    public FechamentoDeDesafiosBackgroundService(IServiceScopeFactory scopeFactory,
        ILogger<FechamentoDeDesafiosBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(IntervaloTick);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await FecharVencidosAsync(stoppingToken);
        }
    }

    private async Task FecharVencidosAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbPadelContext>();
            var fechamento = scope.ServiceProvider.GetRequiredService<FechamentoDoDesafio>();
            var push = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();

            var agora = DateTime.Now;
            var limite = agora - EstadoDoDesafio.PrazoParaContestar;

            var vencidos = await context.Desafios
                .Where(d => d.Status == Desafio.PlacarLancado
                    && d.LancadoEm != null && d.LancadoEm < limite)
                .ToListAsync(stoppingToken);

            foreach (var desafio in vencidos)
            {
                // Um a um, com o fechamento salvando cada linha: um desafio que estoure não
                // pode levar junto os outros do lote.
                await fechamento.FecharAsync(desafio, confirmadoPorId: null, agora, stoppingToken);

                var placar = PlacarDoDesafio.Texto(desafio);
                var aviso = AvisoDoDesafio.PlacarConfirmado(placar);

                foreach (var jogadorId in desafio.Envolvidos.Distinct())
                {
                    await push.EnviarParaJogadorAsync(jogadorId, aviso.Titulo, aviso.Corpo,
                        $"/Desafios/Meus", AlcanceDoAviso.AppSemEmail);
                }
            }

            if (vencidos.Count > 0)
                _logger.LogInformation("Desafios fechados pelo prazo: {Quantidade}", vencidos.Count);
        }
        catch (Exception ex)
        {
            // Log e segue: o próximo tick tenta de novo. Derrubar o serviço faria o fechamento
            // parar de vez por causa de uma linha ruim.
            _logger.LogError(ex, "Falha ao fechar desafios vencidos");
        }
    }
}
