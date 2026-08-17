using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// "O TORNEIO ACABOU — VOTE NO MVP", pra todo mundo que jogou.
//
// A régua de quando e a quem mora em Services/MvpDoTorneio; aqui só se varre o banco e se
// enfileira o aviso. Mesmo arranjo do PerguntaSobreNaoPagosBackgroundService.
//
// ⚠️ POR QUE UM VARREDOR, e não um gancho no fim da última partida: `Status = "Finalizado"` é
// carimbado em SEIS lugares (dois no `EncerramentoDaPartida`, quatro no `RoboDoChaveamento` —
// mata-mata, Americano individual, Americano de duplas e a coroação na mão do organizador).
// Pendurar o aviso em cada um seria a mesma regra escrita seis vezes, e o sétimo caminho que
// alguém escrevesse amanhã nasceria SEM aviso, calado. Aqui existe um dono só, e ele cobre
// qualquer caminho que leve o torneio a acabar.
//
// ⚠️ Quem garante UM aviso só é a COLUNA (`Torneio.AvisoDeMvpEnviadoEm`), nunca o relógio: o
// varredor passa a cada poucos minutos, e sem a marca cada passada repetiria o push pras 90 a
// 220 pessoas de um torneio real.
public class AvisoDoMvpBackgroundService : BackgroundService
{
    // Curto de propósito, ao contrário dos varredores de prazo (que rodam de hora em hora): o
    // pedido é "quando finalizar o último jogo", e o valor deste aviso é justamente chegar
    // enquanto todo mundo ainda está no clube. A varredura é uma consulta indexada por status —
    // barata o bastante pra rodar assim.
    private static readonly TimeSpan IntervaloTick = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AvisoDoMvpBackgroundService> _logger;

    public AvisoDoMvpBackgroundService(IServiceScopeFactory scopeFactory,
        ILogger<AvisoDoMvpBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        using var timer = new PeriodicTimer(IntervaloTick);

        // ⚠️ SEM varredura na subida, ao contrário do irmão que pergunta sobre não pagos. Um
        // deploy é um restart, e um restart não é notícia nenhuma pra quem jogou: varrer no
        // start faria o primeiro deploy depois deste código disparar o aviso de todo torneio
        // que tivesse acabado nos últimos 7 dias, de uma vez. Esperar um tick custa 5 minutos.
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

            var avisados = await VarrerAsync(context, push, DateTime.Now, stoppingToken);

            if (avisados > 0)
                _logger.LogInformation("{Total} torneio(s) com o aviso de MVP disparado.", avisados);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao avisar sobre a votação do MVP.");
        }
    }

    // A varredura, com o `agora` POR PARÂMETRO — é o que permite exercitá-la inteira num teste,
    // inclusive em horários que ainda não chegaram. Devolve quantos TORNEIOS foram avisados.
    public static async Task<int> VarrerAsync(DbPadelContext context, IPushNotificationService push,
        DateTime agora, CancellationToken stoppingToken = default)
    {
        if (!MvpDoTorneio.HoraDeAvisar(agora)) return 0;

        // Só o que pode ter votação: finalizado, com o interruptor ligado e ainda sem aviso.
        // O resto (a janela de 7 dias) depende do último jogo e é decidido logo abaixo.
        var candidatos = await context.Torneios
            .Where(t => t.UsaVotacaoDeMvp
                     && t.AvisoDeMvpEnviadoEm == null
                     && t.Status == MvpDoTorneio.StatusFinalizado)
            .Select(t => new { t.Id, t.Nome })
            .ToListAsync(stoppingToken);

        if (candidatos.Count == 0) return 0;

        int avisados = 0;

        foreach (var candidato in candidatos)
        {
            var torneio = await context.Torneios.FindAsync(new object[] { candidato.Id }, stoppingToken);
            if (torneio == null) continue;

            // ⚠️ A MARCA É GRAVADA DE QUALQUER JEITO, mesmo quando não há o que mandar — senão a
            // varredura voltaria a este torneio a cada 5 minutos, pra sempre, só pra descobrir de
            // novo que não há cédula nem eleitor. O que não acontece é o AVISO.
            //
            // ⚠️ E O CARIMBO É SALVO **ANTES** DE ENFILEIRAR, um torneio por vez — não no fim do
            // laço. Com um `SaveChanges` só no fim, um torneio que estourasse no meio perderia o
            // carimbo dos anteriores, e o tick seguinte reenviaria o push pras 90 a 220 pessoas
            // de cada um deles. Salvando antes, o pior caso vira "um torneio ficou sem aviso"
            // em vez de "duzentas pessoas receberam duas vezes" — e entre falhar por falta e
            // falhar por excesso, num canal de massa, a escolha não é difícil.
            torneio.AvisoDeMvpEnviadoEm = agora;
            await context.SaveChangesAsync(stoppingToken);

            var votacao = await MvpDoTorneio.DoTorneioAsync(context, torneio.Id, null, agora);
            if (votacao == null) continue;

            // A eleição de MVP existe neste torneio? Fora da janela (acabou faz mais de uma
            // semana, ou sem jogo nenhum pra contar a partir de), sem campeão pra votar, ou
            // no AMERICANO — que não elege MVP —, ela não existe.
            bool temMvp = votacao.Aberta && votacao.Candidatos.Count > 0;

            // ⚠️ A ENQUETE é a SEGUNDA razão pra avisar, e ela não segue o interruptor: o
            // torneio normal cujo organizador desligou a eleição continua sendo avaliado, e
            // sem esta linha ninguém seria convidado — a enquete dele nasceria morta.
            // (No Americano as duas são falsas desde 17/08/2026, e o torneio não recebe aviso.)
            bool temEnquete = EnqueteDoTorneio.Aberta(
                votacao.StatusDoTorneio, votacao.UltimoJogo, agora, votacao.Formato);

            if (!temMvp && !temEnquete) continue;

            var eleitores = await MvpDoTorneio.EleitoresAsync(context, torneio.Id);
            if (eleitores.Count == 0) continue;

            var corpo = MvpDoTorneio.CorpoDoAviso(torneio.Nome, temMvp);
            var url = $"/Torneios/Mvp/{torneio.Id}";

            foreach (var jogadorId in eleitores)
            {
                // Só ENFILEIRA — a FilaDeAvisos entrega por fora. Entregar aqui dentro seguraria
                // a varredura por centenas de envios.
                await push.EnviarParaJogadorAsync(jogadorId,
                    MvpDoTorneio.TituloDoAvisoPara(temMvp), corpo, url, MvpDoTorneio.CanalDoAviso);
            }

            avisados++;
        }

        return avisados;
    }
}
