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
        // que tivesse acabado dentro da janela, de uma vez. Esperar um tick custa 5 minutos.
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

            var agora = DateTime.Now;
            var avisados = await VarrerAsync(context, push, agora, stoppingToken);

            if (avisados > 0)
                _logger.LogInformation("{Total} torneio(s) com o aviso de MVP disparado.", avisados);

            var lembrados = await LembrarAsync(context, push, agora, stoppingToken);

            if (lembrados > 0)
                _logger.LogInformation("{Total} torneio(s) com o lembrete de MVP disparado.", lembrados);
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
        // O resto (a janela de 24h) depende do último jogo e é decidido logo abaixo.
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

    // ─────────────── O LEMBRETE: "AINDA FALTA O SEU VOTO" (13/10/2026) ───────────────
    //
    // 🗣️ Felipe: *"temos que relembrar também quem ainda não votou, pra votar, por que é apenas
    // permitido 24h após o último jogo"*. Desenho aprovado por ele: UM lembrete por torneio,
    // `HorasAntesDeFecharParaLembrar` antes de fechar, pelo app, só pra quem jogou e ainda não
    // votou — na cédula dividida, o lembrete diz QUAL cédula falta.
    //
    // ⚠️ O RECORTE DE 2 DIAS no `AvisoDeMvpEnviadoEm` não é otimização: sem ele, a primeira passada
    // depois do deploy olharia todo torneio finalizado do banco. A janela inteira é de 24h; quem
    // recebeu o aviso inicial há mais de 2 dias não tem votação aberta pra lembrar.
    //
    // Devolve quantos TORNEIOS tiveram lembrete disparado.
    public static async Task<int> LembrarAsync(DbPadelContext context, IPushNotificationService push,
        DateTime agora, CancellationToken stoppingToken = default)
    {
        if (!MvpDoTorneio.HoraDeAvisar(agora)) return 0;

        var recorte = agora.AddDays(-2);
        var candidatos = await context.Torneios
            .Where(t => t.UsaVotacaoDeMvp
                     && t.LembreteDeMvpEnviadoEm == null
                     && t.AvisoDeMvpEnviadoEm != null && t.AvisoDeMvpEnviadoEm > recorte
                     && t.Status == MvpDoTorneio.StatusFinalizado)
            .Select(t => t.Id)
            .ToListAsync(stoppingToken);

        int lembrados = 0;

        foreach (var torneioId in candidatos)
        {
            var votacao = await MvpDoTorneio.DoTorneioAsync(context, torneioId, null, agora);
            if (votacao == null || !votacao.Aberta || votacao.FechaEm is not DateTime fecha) continue;
            if (votacao.Candidatos.Count == 0) continue;

            // Ainda não é a hora: o varredor volta no próximo tick.
            if (agora < fecha.AddHours(-MvpDoTorneio.HorasAntesDeFecharParaLembrar)) continue;

            var torneio = await context.Torneios.FindAsync(new object[] { torneioId }, stoppingToken);
            if (torneio == null) continue;

            // Carimbo ANTES de enfileirar, pelo mesmo motivo do aviso inicial (ver VarrerAsync):
            // entre "um torneio ficou sem lembrete" e "duzentas pessoas receberam dois", o
            // primeiro é o defeito aceitável.
            torneio.LembreteDeMvpEnviadoEm = agora;
            await context.SaveChangesAsync(stoppingToken);

            var votos = await context.VotosDeMvp.AsNoTracking()
                .Where(v => v.TorneioId == torneioId)
                .Select(v => new { v.VotanteId, v.Naipe })
                .ToListAsync(stoppingToken);

            var eleitores = await MvpDoTorneio.EleitoresAsync(context, torneioId);
            var url = $"/Torneios/Mvp/{torneioId}";

            foreach (var jogadorId in eleitores)
            {
                // As cédulas em que esta pessoa PODE votar e ainda não votou. Quem é o único nome
                // de uma cédula não vota nela (ninguém vota em si mesmo) — e não é cobrado por isso.
                var faltam = votacao.Cedulas
                    .Where(c => c.Candidatos.Any(x => x.JogadorId != jogadorId))
                    .Where(c => !votos.Any(v => v.VotanteId == jogadorId && v.Naipe == c.Naipe))
                    .ToList();
                if (faltam.Count == 0) continue;

                await push.EnviarParaJogadorAsync(jogadorId, MvpDoTorneio.TituloDoLembrete,
                    MvpDoTorneio.CorpoDoLembrete(torneio.Nome, fecha, votacao.Dividida && faltam.Count == 1 ? faltam[0].Titulo : null),
                    url, MvpDoTorneio.CanalDoAviso);
            }

            lembrados++;
        }

        return lembrados;
    }
}
