using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// Manda o "você está inscrito e ainda não pagou" — a regra de QUEM e QUANDO mora em
// Services/LembreteDeInscricaoNaoPaga; aqui só se varre o banco e se enfileira o aviso.
//
// ⚠️ Não confundir com o lembrete do PagamentoExpiradoBackgroundService. Aquele parte de uma
// FATURA pendente; este parte da INSCRIÇÃO. Quem escolheu "pago depois" não tem fatura nenhuma
// — era exatamente essa pessoa que ficava sem receber nada até o dia do fechamento.
public class LembreteInscricaoNaoPagaBackgroundService : BackgroundService
{
    // De hora em hora. Não precisa ser fino: os marcos são em DIAS, e quem decide se já avisou
    // é a coluna, não o relógio. O tick curto serve só pra pegar a primeira hora civilizada do
    // dia sem depender de o processo ter subido às 9h em ponto.
    private static readonly TimeSpan IntervaloTick = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LembreteInscricaoNaoPagaBackgroundService> _logger;

    public LembreteInscricaoNaoPagaBackgroundService(IServiceScopeFactory scopeFactory,
        ILogger<LembreteInscricaoNaoPagaBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Não segurar a subida do host: daqui pra baixo tudo é I/O, mas a primeira varredura
        // acontece antes do primeiro await de verdade se o banco responder de imediato.
        await Task.Yield();

        // Uma varredura JÁ NA SUBIDA, e não só daqui a uma hora: publicar de manhã não pode
        // custar a janela do dia inteiro. Repetir é inofensivo — quem decide se já avisou é a
        // coluna, não o fato de o processo ter reiniciado.
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

            var enviados = await VarrerAsync(context, push, DateTime.Now, stoppingToken);

            if (enviados > 0)
                _logger.LogInformation("{Total} lembrete(s) de inscrição não paga enfileirado(s).", enviados);
        }
        catch (Exception ex)
        {
            // Falhar aqui não pode derrubar o host — na próxima hora tenta de novo.
            _logger.LogError(ex, "Falha ao lembrar inscrições não pagas.");
        }
    }

    // A varredura em si, separada e com o `agora` POR PARÂMETRO: é o que permite exercitá-la
    // inteira num teste — inclusive nos dias que ainda não chegaram — em vez de conferir só a
    // régua e torcer pra ligação estar certa. Devolve quantas inscrições foram lembradas.
    public static async Task<int> VarrerAsync(DbPadelContext context, IPushNotificationService push,
        DateTime agora, CancellationToken stoppingToken = default)
    {
        if (!LembreteDeInscricaoNaoPaga.HoraCivilizada(agora)) return 0;

        // Filtra no banco o que dá pra traduzir em SQL; o resto da régua (que passa por
        // PrazoParaPagar) roda na memória, sobre um punhado de linhas.
        var candidatos = await context.Torneios
            .Where(t => t.CanceladoEm == null && t.PrecoInscricao > 0 && !t.PagamentoObrigatorioNaInscricao)
            .ToListAsync(stoppingToken);

        var torneios = candidatos.Where(LembreteDeInscricaoNaoPaga.VigiaEsteTorneio).ToList();
        if (torneios.Count == 0) return 0;

        int lembradas = 0;

        foreach (var torneio in torneios)
        {
            var prazo = PrazoParaPagar.Ate(torneio)!.Value;

            // ⚠️ TIME NÃO É GENTE: numa categoria de times o Jogador1Id aponta pro ORGANIZADOR
            // que cadastrou a linha, e cobrar dele a inscrição de cada time seria enchê-lo de
            // avisos sobre dívida que não é dele.
            //
            // ⚠️ `NomeTime == null`, e não `!EhTime`: EhTime é [NotMapped] e o EF não traduz
            // propriedade calculada — a consulta estouraria em produção e passaria no teste
            // InMemory, que avalia em C#.
            var duplas = await context.Duplas
                .Where(d => d.Categoria.TorneioId == torneio.Id
                         && !d.Pago && !d.EmListaDeEspera && d.NomeTime == null)
                .ToListAsync(stoppingToken);

            foreach (var dupla in duplas)
            {
                var marco = LembreteDeInscricaoNaoPaga.MarcoDevido(prazo, agora, dupla.UltimoLembreteDePagamento);
                if (marco == null) continue;

                var valor = dupla.ValorInscricao ?? torneio.PrecoInscricao;

                // Os DOIS integrantes: a dívida é da inscrição, e quem paga costuma ser quem
                // lembrou primeiro. Avisar só quem inscreveu deixaria o parceiro descobrindo a
                // pendência no dia do torneio.
                var pessoas = dupla.Jogador2Id is int segundo
                    ? new[] { dupla.Jogador1Id, segundo }
                    : new[] { dupla.Jogador1Id };

                foreach (var jogadorId in pessoas)
                    await EnfileirarAsync(push, jogadorId, torneio, valor, prazo, agora);

                dupla.UltimoLembreteDePagamento = marco;
                lembradas++;
            }

            var americanas = await context.InscricoesAmericanas
                .Where(i => i.Categoria.TorneioId == torneio.Id && !i.Pago && !i.EmListaDeEspera)
                .ToListAsync(stoppingToken);

            foreach (var inscricao in americanas)
            {
                var marco = LembreteDeInscricaoNaoPaga.MarcoDevido(prazo, agora, inscricao.UltimoLembreteDePagamento);
                if (marco == null) continue;

                var valor = inscricao.ValorInscricao ?? torneio.PrecoInscricao;
                await EnfileirarAsync(push, inscricao.JogadorId, torneio, valor, prazo, agora);

                inscricao.UltimoLembreteDePagamento = marco;
                lembradas++;
            }
        }

        // ⚠️ O marco é gravado DEPOIS de enfileirar, no mesmo tick: a fila de avisos é em memória
        // e a entrega acontece fora daqui (ver FilaDeAvisos). Se o processo cair no meio, o pior
        // caso é um aviso repetido — muito melhor que o silêncio, que é o defeito que este
        // serviço existe pra corrigir.
        if (lembradas > 0) await context.SaveChangesAsync(stoppingToken);

        return lembradas;
    }

    // O link é a TELA DO TORNEIO, não uma fatura: quem paga depois pode não ter cobrança nenhuma
    // aberta, e é lá que fica o botão "Pagar agora" que gera uma.
    //
    // Só ENFILEIRA — a entrega (caixa de entrada + e-mail + push) é do funil, num lugar só.
    private static Task EnfileirarAsync(IPushNotificationService push, int jogadorId,
        Torneio torneio, decimal valor, DateTime prazo, DateTime agora) =>
        push.EnviarParaJogadorAsync(jogadorId,
            LembreteDeInscricaoNaoPaga.Titulo,
            LembreteDeInscricaoNaoPaga.Frase(torneio.Nome, valor, prazo, agora),
            $"/Torneios/Details/{torneio.Id}");
}
