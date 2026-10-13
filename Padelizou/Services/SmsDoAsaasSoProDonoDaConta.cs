using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// OS CLIENTES DO ASAAS QUE JÁ EXISTIAM TAMBÉM SAEM DO SMS.
//
// 🗣️ Felipe, 13/10/2026: *"remova os sms, somente eu posso receber sms"*. O cliente novo (e o
// reaproveitado pelo CPF) sai sem SMS no `AsaasService.ObterOuCriarClienteAsync` — mas quem
// já tem cobrança aberta e não vai gerar outra continuaria recebendo, e é justamente o caso
// que trouxe o pedido: o Lucas Fonseca, do NATA, com uma fatura vencida que ninguém vai pagar.
//
// Uma passada na partida do app, depois de cada deploy. Na segunda em diante o Asaas responde
// que já está tudo desligado e nenhum PUT sai — só a leitura.
//
// atalho: sem marca de "já feito" (seria migration). A janela de 90 dias é o teto do que se
// relê a cada partida; cobrança mais antiga que isso o Asaas não está mais lembrando.
public static class SmsDoAsaasSoProDonoDaConta
{
    public static readonly TimeSpan Janela = TimeSpan.FromDays(90);

    public static async Task<List<string>> ClientesParaDesligarAsync(DbPadelContext context, DateTime agora)
    {
        var desde = agora - Janela;

        return await context.Pagamentos
            .Where(p => p.AsaasCustomerId != null && p.AsaasCustomerId != "" && p.CriadoEm >= desde)
            .Select(p => p.AsaasCustomerId!)
            .Distinct()
            .ToListAsync();
    }
}

public class SmsDoAsaasSoProDonoDaContaBackgroundService : BackgroundService
{
    // Espera o app subir e o healthz do deploy passar antes de sair batendo no gateway.
    private static readonly TimeSpan EsperaNaPartida = TimeSpan.FromMinutes(2);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SmsDoAsaasSoProDonoDaContaBackgroundService> _logger;

    public SmsDoAsaasSoProDonoDaContaBackgroundService(IServiceScopeFactory scopeFactory,
        ILogger<SmsDoAsaasSoProDonoDaContaBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(EsperaNaPartida, stoppingToken);

            using var scope = _scopeFactory.CreateScope();
            var asaas = scope.ServiceProvider.GetRequiredService<IAsaasService>();
            if (!asaas.Configurado) return;

            var context = scope.ServiceProvider.GetRequiredService<DbPadelContext>();
            var clientes = await SmsDoAsaasSoProDonoDaConta.ClientesParaDesligarAsync(context, DateTime.Now);

            // Um de cada vez: são poucas centenas, e não há pressa que justifique estourar o
            // limite de chamadas do gateway.
            int falhas = 0;
            foreach (var cliente in clientes)
            {
                if (stoppingToken.IsCancellationRequested) return;
                if (!await asaas.DesligarSmsDoClienteAsync(cliente)) falhas++;
            }

            _logger.LogInformation("SMS do Asaas conferido em {Total} cliente(s); {Falhas} falha(s).",
                clientes.Count, falhas);
        }
        catch (OperationCanceledException)
        {
            // O app está parando — a próxima partida refaz.
        }
        catch (Exception ex)
        {
            // Falhar aqui não pode derrubar o host.
            _logger.LogError(ex, "Falha ao desligar o SMS dos clientes do Asaas.");
        }
    }
}
