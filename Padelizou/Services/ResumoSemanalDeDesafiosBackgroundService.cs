using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// Manda o resumo semanal do mural: "7 duplas abertas em Gravataí nesta semana".
//
// Regras (quando, pra quem, quantas) em Services/ResumoSemanalDoMural; as consultas, em
// Services/ConsultasDoResumoSemanal — aqui só a varredura.
//
// ⚠️ MÓDULO FECHADO, AVISO CALADO. Enquanto `Desafios__Habilitado` for false, o mural responde
// 404 pra jogador comum: o aviso levaria a pessoa a uma tela que não abre, e aviso que leva a
// lugar nenhum queima a permissão de notificação — ela desliga tudo e não volta. É a mesma
// trava do HorarioVagoBackgroundService, pelo mesmo motivo.
public class ResumoSemanalDeDesafiosBackgroundService : BackgroundService
{
    // De hora em hora, como o HorarioVago: o disparo é uma vez por semana, e o tick só existe
    // pra encontrar a primeira hora útil da quinta.
    private static readonly TimeSpan IntervaloTick = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ResumoSemanalDeDesafiosBackgroundService> _logger;
    private readonly DesafiosSettings _desafios;

    public ResumoSemanalDeDesafiosBackgroundService(IServiceScopeFactory scopeFactory,
        ILogger<ResumoSemanalDeDesafiosBackgroundService> logger,
        Microsoft.Extensions.Options.IOptions<DesafiosSettings> desafios)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _desafios = desafios.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(IntervaloTick);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await EnviarSeForQuintaAsync(stoppingToken);
        }
    }

    private async Task EnviarSeForQuintaAsync(CancellationToken stoppingToken)
    {
        if (!_desafios.Habilitado) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbPadelContext>();
            var push = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();

            var agora = DateTime.Now;
            var ultimoEnvio = await LerUltimoEnvioAsync(context, stoppingToken);
            if (!ResumoSemanalDoMural.EhHoraDeEnviar(agora, ultimoEnvio)) return;

            var noMural = await ConsultasDoResumoSemanal.NoMural(context, agora)
                .ToListAsync(stoppingToken);

            // Mural fraco não gasta o empurrão da semana — e nem a marca, pra que uma quinta sem
            // anúncio nenhum não conte como "já enviado".
            if (noMural.Count < ResumoSemanalDoMural.MinimoDeDuplas) return;

            var jaUsaram = await ConsultasDoResumoSemanal.QuemJaUsouAsync(context, stoppingToken);
            if (jaUsaram.Count == 0) return;

            var candidatos = await context.Jogadores
                .AsNoTracking()
                .Where(j => jaUsaram.Contains(j.Id))
                .ToListAsync(stoppingToken);

            // ⚠️ A MARCA É GRAVADA DEPOIS DAS CONSULTAS E ANTES DO LAÇO DE ENVIO, e a ordem é
            // uma correção de rumo: na primeira versão ela vinha antes de tudo, e uma consulta
            // que estourasse (foi o que aconteceu — ver ConsultasDoResumoSemanal) marcava a
            // semana como enviada sem mandar nada. A perda não era "alguns não receberam": era
            // 100%, toda semana, calada.
            //
            // Aqui: se a leitura falha, a marca não foi escrita e o próximo tick tenta de novo.
            // Se o envio falha no meio, a marca já está lá — e o pior caso volta a ser "alguns
            // não receberam", que é melhor que a base receber duas vezes.
            await MarcarEnvioAsync(context, agora, stoppingToken);

            var quantos = await EnviarAsync(push, candidatos, noMural, stoppingToken);
            _logger.LogInformation("Resumo semanal dos Desafios enviado para {Quantidade} jogadores", quantos);
        }
        catch (Exception ex)
        {
            // Log e segue: o resumo é acessório, e derrubar o serviço por causa dele pararia
            // junto qualquer coisa que rode no mesmo host.
            _logger.LogError(ex, "Falha ao enviar o resumo semanal dos Desafios");
        }
    }

    private static async Task<int> EnviarAsync(IPushNotificationService push,
        List<Jogador> candidatos, List<AnuncioDeDesafio> noMural, CancellationToken stoppingToken)
    {
        // Quem está no mural AGORA não recebe: pra ela o aviso é ruído, e ruído é o que ensina
        // a ignorar o remetente.
        var jaAnunciando = noMural
            .SelectMany(a => new[] { a.Jogador1Id, a.Jogador2Id ?? 0 })
            .ToHashSet();

        var enviados = 0;
        foreach (var jogador in candidatos)
        {
            if (stoppingToken.IsCancellationRequested) break;

            var duplas = ResumoSemanalDoMural.QuantasDuplasPara(noMural, jogador.Id, jogador.Cidade);

            // `jaUsouDesafios: true` porque a lista de candidatos JÁ saiu de quem usou — a régua
            // continua morando em ResumoSemanalDoMural, e a consulta só a alimenta.
            if (!ResumoSemanalDoMural.VaiReceber(jogador, jaUsouDesafios: true,
                    temAnuncioNoMural: jaAnunciando.Contains(jogador.Id), duplasDisponiveis: duplas))
                continue;

            var aviso = AvisoDoDesafio.ResumoDoMural(duplas, NomeDeCidade.ComoTitulo(jogador.Cidade));

            // SoApp: app + e-mail, sem WhatsApp. O e-mail se justifica porque a lista é curta e
            // é gente que já usou — mas o canal do WhatsApp não, porque nada aqui é urgente.
            await push.EnviarParaJogadorAsync(jogador.Id, aviso.Titulo, aviso.Corpo,
                "/Desafios", AlcanceDoAviso.SoApp);

            enviados++;
        }

        return enviados;
    }

    private static async Task<DateTime?> LerUltimoEnvioAsync(DbPadelContext context,
        CancellationToken stoppingToken)
    {
        var linha = await context.ConfiguracoesDoSistema
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Chave == ResumoSemanalDoMural.ChaveDoUltimoEnvio, stoppingToken);

        return DateTime.TryParse(linha?.Valor, out var quando) ? quando : null;
    }

    private static async Task MarcarEnvioAsync(DbPadelContext context, DateTime agora,
        CancellationToken stoppingToken)
    {
        var linha = await context.ConfiguracoesDoSistema
            .FirstOrDefaultAsync(c => c.Chave == ResumoSemanalDoMural.ChaveDoUltimoEnvio, stoppingToken);

        if (linha == null)
        {
            linha = new ConfiguracaoDoSistema { Chave = ResumoSemanalDoMural.ChaveDoUltimoEnvio };
            context.ConfiguracoesDoSistema.Add(linha);
        }

        linha.Valor = agora.ToString("O");
        linha.AtualizadoEm = agora;

        await context.SaveChangesAsync(stoppingToken);
    }
}
