using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// Pergunta ao ORGANIZADOR o que fazer com quem não pagou, no fechamento das inscrições.
// A régua de quando e a quem mora em Services/DecisaoSobreQuemNaoPagou; aqui só se varre o
// banco e se enfileira o aviso.
//
// ⚠️ Este serviço existe pra que a caixinha "quem não pagar perde a vaga" deixe de ser uma
// promessa vazia — ela vivia só no modelo e na tela de criação, sem NINGUÉM a ler.
//
// ⚠️ E ele PERGUNTA, não executa. Apagar inscrição de gente real por conta própria é o desastre
// que já aconteceu aqui (as 8 pessoas que sumiram sem deixar rastro): um robô que remove no
// horário errado, ou por causa de um pagamento que o webhook ainda não confirmou, transforma um
// atraso de Pix em vaga perdida.
public class PerguntaSobreNaoPagosBackgroundService : BackgroundService
{
    // De hora em hora, como o lembrete de inscrição não paga: os marcos são em dias e quem
    // decide se já perguntou é a coluna, não o relógio.
    private static readonly TimeSpan IntervaloTick = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PerguntaSobreNaoPagosBackgroundService> _logger;

    public PerguntaSobreNaoPagosBackgroundService(IServiceScopeFactory scopeFactory,
        ILogger<PerguntaSobreNaoPagosBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        // Uma varredura já na subida: publicar de manhã não pode custar a janela do dia.
        // Repetir é inofensivo — quem decide se já perguntou é a coluna.
        await UmaVarreduraAsync(stoppingToken);

        using var timer = new PeriodicTimer(IntervaloTick);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await UmaVarreduraAsync(stoppingToken);
        }
    }

    private async Task UmaVarreduraAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbPadelContext>();
            var push = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();

            var perguntados = await VarrerAsync(context, push, DateTime.Now, stoppingToken);

            if (perguntados > 0)
                _logger.LogInformation("{Total} organizador(es) perguntado(s) sobre quem não pagou.", perguntados);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao perguntar sobre inscrições não pagas.");
        }
    }

    // A varredura, com o `agora` POR PARÂMETRO — é o que permite exercitá-la inteira num teste,
    // inclusive em datas que ainda não chegaram. Devolve quantos TORNEIOS foram perguntados.
    public static async Task<int> VarrerAsync(DbPadelContext context, IPushNotificationService push,
        DateTime agora, CancellationToken stoppingToken = default)
    {
        // Mesma janela civilizada do lembrete: ninguém decide sobre a vaga dos outros às 3h.
        if (!LembreteDeInscricaoNaoPaga.HoraCivilizada(agora)) return 0;

        var candidatos = await context.Torneios
            .Where(t => t.ExcluirSeNaoPagar && t.PerguntaDeNaoPagosEm == null
                     && t.CanceladoEm == null && t.PrecoInscricao > 0)
            .ToListAsync(stoppingToken);

        var naHora = candidatos
            .Where(t => DecisaoSobreQuemNaoPagou.HoraDePerguntar(t, agora, t.PerguntaDeNaoPagosEm))
            .ToList();

        if (naHora.Count == 0) return 0;

        int perguntados = 0;

        foreach (var torneio in naHora)
        {
            var devendo = await ContarDevendoAsync(context, torneio.Id, stoppingToken);

            // ⚠️ A marca é gravada MESMO quando ninguém deve — senão a varredura voltaria a este
            // torneio de hora em hora, pra sempre, só pra descobrir de novo que não há o que
            // perguntar. O que não acontece é o AVISO: torneio com todo mundo pago não precisa
            // receber uma pergunta sem assunto.
            torneio.PerguntaDeNaoPagosEm = agora;

            if (devendo == 0) continue;

            var organizadores = await context.TorneioOrganizadores
                .Where(o => o.TorneioId == torneio.Id)
                .Select(o => o.JogadorId)
                .ToListAsync(stoppingToken);

            var corpo = devendo == 1
                ? $"1 inscrição do {torneio.Nome} está sem pagamento. Você decide: liberar a vaga ou acertar por fora."
                : $"{devendo} inscrições do {torneio.Nome} estão sem pagamento. Você decide: liberar as vagas ou acertar por fora.";

            foreach (var organizadorId in organizadores)
            {
                await push.EnviarParaJogadorAsync(organizadorId,
                    DecisaoSobreQuemNaoPagou.TituloDoAviso, corpo,
                    $"/Torneios/NaoPagos/{torneio.Id}");
            }

            perguntados++;
        }

        await context.SaveChangesAsync(stoppingToken);

        return perguntados;
    }

    // Quantas inscrições estão devendo. Duplas e americanas, com as mesmas exclusões do
    // lembrete: time não é gente, lista de espera não deve nada ainda, e no Americano individual
    // a tabela Dupla guarda o par da rodada, não a inscrição.
    private static async Task<int> ContarDevendoAsync(DbPadelContext context, int torneioId,
        CancellationToken stoppingToken)
    {
        var torneio = await context.Torneios.FindAsync(new object[] { torneioId }, stoppingToken);

        int duplas = torneio != null && LembreteDeInscricaoNaoPaga.AInscricaoEhDupla(torneio)
            ? await context.Duplas.CountAsync(d => d.Categoria.TorneioId == torneioId
                && !d.Pago && !d.EmListaDeEspera && d.NomeTime == null, stoppingToken)
            : 0;

        int americanas = await context.InscricoesAmericanas.CountAsync(
            i => i.Categoria.TorneioId == torneioId && !i.Pago && !i.EmListaDeEspera, stoppingToken);

        return duplas + americanas;
    }
}
