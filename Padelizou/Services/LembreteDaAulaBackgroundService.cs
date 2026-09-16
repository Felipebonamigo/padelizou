using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;

namespace Padelizou.Services;

// Manda o "sua aula é amanhã" e o "sua aula é daqui a pouco" — a régua de QUANDO e O QUE mora
// em Services/LembreteDaAula; aqui só se varre o banco e se enfileira o aviso.
//
// ⚠️ CANAL: só o app (caixa de avisos + push), sem e-mail e sem WhatsApp. Decisão do Felipe em
// 16/09/2026, respondendo ao pedido do Maickel: a aula é compromisso que a própria pessoa
// marcou, então não vale a cota de e-mail (que já morreu num dia, levando 130 mensagens junto)
// nem o chip do WhatsApp (que já foi restrito pela Meta uma vez). Ver Services/AlcanceDoAviso.
public class LembreteDaAulaBackgroundService : BackgroundService
{
    // 15 minutos, e não uma hora como o lembrete de inscrição: o marco de 1h precisa de um tick
    // fino pra não chegar faltando cinco minutos. O de 24h não se importa.
    private static readonly TimeSpan IntervaloTick = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LembreteDaAulaBackgroundService> _logger;

    public LembreteDaAulaBackgroundService(IServiceScopeFactory scopeFactory,
        ILogger<LembreteDaAulaBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Não segurar a subida do host.
        await Task.Yield();

        // Uma varredura já na subida: um deploy no meio da tarde não pode custar os lembretes
        // da janela. Repetir é inofensivo — quem decide se já avisou é a coluna, não o restart.
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

            var avisos = await VarrerAsync(context, push, DateTime.Now, stoppingToken);

            if (avisos > 0)
                _logger.LogInformation("{Total} lembrete(s) de aula enfileirado(s).", avisos);
        }
        catch (Exception ex)
        {
            // Nunca derruba o host: o que não saiu neste tick sai no próximo — a coluna diz o
            // que falta.
            _logger.LogError(ex, "Falha ao lembrar as aulas que estão chegando.");
        }
    }

    // A varredura, separada e com o `agora` POR PARÂMETRO: é o que permite exercitá-la inteira
    // num teste — inclusive em horas que ainda não chegaram — em vez de conferir só a régua e
    // torcer pra ligação estar certa. Devolve quantos avisos foram enfileirados.
    public static async Task<int> VarrerAsync(DbPadelContext context, IPushNotificationService push,
        DateTime agora, CancellationToken stoppingToken = default)
    {
        var avisos = await VarrerAulasAsync(context, push, agora, stoppingToken);
        avisos += await VarrerJogosAulaAsync(context, push, agora, stoppingToken);

        if (avisos > 0 || context.ChangeTracker.HasChanges())
            await context.SaveChangesAsync(stoppingToken);

        return avisos;
    }

    // As aulas que podem precisar de lembrete AGORA.
    //
    // ⚠️ PÚBLICA E SEPARADA porque o EF InMemory da suíte NÃO TRADUZ SQL: uma consulta que o
    // Postgres recusaria passaria lisa por 7 mil testes verdes e estouraria no primeiro tick em
    // produção. Assim ela é compilável sozinha no `ToQueryString()` — ver
    // TraducaoDoLembreteDeAulaTests e a lição de 19/08/2026.
    //
    // ⚠️ SÓ `Confirmada`, e não `PoliticaAula.ContaComoAtiva` — que também aceita "Pendente".
    // Pendente aqui quer dizer "o professor ainda não aceitou", e mandar "sua aula é amanhã" pra
    // uma aula que ainda pode ser recusada é prometer o que o sistema não tem. Cancelada,
    // Recusada, Realizada e "A recuperar" ficam de fora por construção.
    public static IQueryable<Aula> ConsultaDasAulas(DbPadelContext context, DateTime agora)
    {
        // O teto sai dos próprios marcos: marco novo na régua estica a janela sozinho.
        var teto = agora.AddHours(LembreteDaAula.Marcos.Max());

        return context.Aulas
            .Include(a => a.Professor)
            .Include(a => a.Aluno)
            .Include(a => a.LocalAula)
            .Where(a => a.Status == PoliticaAula.Confirmada && a.DataHora > agora && a.DataHora <= teto);
    }

    // "Ativo" é o status de nascença do jogo-aula; cancelar grava "Cancelado"
    // (ver JogoAulaController).
    public static IQueryable<JogoAula> ConsultaDosJogosAula(DbPadelContext context, DateTime agora)
    {
        var teto = agora.AddHours(LembreteDaAula.Marcos.Max());

        return context.JogosAula
            .Include(j => j.Professor)
            .Include(j => j.LocalAula)
            .Where(j => j.Status == "Ativo" && j.DataHora > agora && j.DataHora <= teto);
    }

    // ⚠️ QUEM ESTÁ NA LISTA DE ESPERA NÃO TEM VAGA: dizer "seu jogo-aula é amanhã" pra quem não
    // entrou é prometer o que não existe.
    public static IQueryable<InscricaoJogoAula> ConsultaDosInscritos(DbPadelContext context, int jogoAulaId) =>
        context.InscricoesJogoAula
            .Include(i => i.Jogador)
            .Where(i => i.JogoAulaId == jogoAulaId && !i.EmListaDeEspera);

    private static async Task<int> VarrerAulasAsync(DbPadelContext context, IPushNotificationService push,
        DateTime agora, CancellationToken stoppingToken)
    {
        var aulas = await ConsultaDasAulas(context, agora).ToListAsync(stoppingToken);

        var avisos = 0;

        // ⚠️ AGRUPADO POR (PROFESSOR, HORÁRIO), e isso não é economia: a turma são TRÊS LINHAS
        // de Aula no mesmo horário, uma por aluno com a cobrança dele. Sem agrupar, o professor
        // levaria três avisos idênticos no mesmo minuto — e é assim que alguém desliga as
        // notificações do sistema inteiro.
        foreach (var turma in aulas.GroupBy(a => new { a.ProfessorId, a.DataHora }))
        {
            var devidas = turma
                .Select(a => (Aula: a, Marco: LembreteDaAula.MarcoDevido(a.DataHora, agora, a.UltimoLembreteEnviado)))
                .Where(x => x.Marco != null)
                .ToList();

            if (devidas.Count == 0) continue;

            var primeira = turma.First();
            var marco = devidas.Min(x => x.Marco!.Value);
            var local = primeira.LocalAula.Nome;
            var quando = primeira.DataHora;

            foreach (var (aula, marcoDaAula) in devidas)
            {
                // Marcado por ter sido DESPACHADO, não entregue — e marcado mesmo quando não há
                // ninguém pra avisar (aluno avulso, conta excluída, preferência desligada):
                // senão o varredor tentaria de novo a cada 15 minutos até a aula acontecer.
                aula.UltimoLembreteEnviado = marcoDaAula;

                if (aula.Aluno is not { ExcluidoEm: null, NotificarLembreteDeAula: true } aluno) continue;

                await push.EnviarParaJogadorAsync(aluno.Id,
                    LembreteDaAula.Titulo(LembreteDaAula.UmaAula, quando, agora),
                    LembreteDaAula.Frase(primeira.Professor.ComoChamar, quando, agora, local,
                        ofereceDesmarcar: marcoDaAula == LembreteDaAula.MarcoDaVespera),
                    // A tela do aluno é a que tem o botão de desmarcar — aviso que cai numa
                    // tela sem o botão que ele mesmo sugere não é aviso.
                    "/Aulas/MinhasAulas", AlcanceDoAviso.AppSemEmail);

                avisos++;
            }

            if (primeira.Professor is not { ExcluidoEm: null, NotificarLembreteDeAula: true }) continue;

            var quantos = turma.Count();
            var comQuem = quantos == 1 ? NomeDoAluno(primeira) : $"{quantos} alunos";

            await push.EnviarParaJogadorAsync(primeira.ProfessorId,
                LembreteDaAula.Titulo(LembreteDaAula.UmaAula, quando, agora),
                // O professor não desmarca por aqui: ele reorganiza a agenda dele, e a tela
                // dele é outra.
                LembreteDaAula.Frase(comQuem, quando, agora, local, ofereceDesmarcar: false),
                "/Aulas/MinhaAgenda", AlcanceDoAviso.AppSemEmail);

            avisos++;
        }

        return avisos;
    }

    // Quem o professor vai encontrar na quadra. O nome do CADASTRO é o último recurso: o aluno
    // avulso não tem conta e é conhecido só pelo que o professor digitou.
    private static string NomeDoAluno(Aula aula) =>
        aula.Aluno?.ComoChamar
        ?? (string.IsNullOrWhiteSpace(aula.NomeAlunoAvulso) ? "seu aluno" : aula.NomeAlunoAvulso.Trim());

    private static async Task<int> VarrerJogosAulaAsync(DbPadelContext context, IPushNotificationService push,
        DateTime agora, CancellationToken stoppingToken)
    {
        var jogos = await ConsultaDosJogosAula(context, agora).ToListAsync(stoppingToken);

        var avisos = 0;

        foreach (var jogo in jogos)
        {
            var marco = LembreteDaAula.MarcoDevido(jogo.DataHora, agora, jogo.UltimoLembreteEnviado);
            if (marco == null) continue;

            var inscritos = await ConsultaDosInscritos(context, jogo.Id).ToListAsync(stoppingToken);

            // ⚠️ SEM NINGUÉM INSCRITO NÃO HÁ O QUE LEMBRAR, e o marco fica NULO de propósito: se
            // alguém se inscrever depois, o lembrete de 1h ainda sai. Avisar o professor de que
            // ninguém se inscreveu é outro aviso, e não é este.
            if (inscritos.Count == 0) continue;

            jogo.UltimoLembreteEnviado = marco;

            var local = jogo.LocalAula.Nome;

            foreach (var inscricao in inscritos)
            {
                if (inscricao.Jogador is not { ExcluidoEm: null, NotificarLembreteDeAula: true } jogador) continue;

                await push.EnviarParaJogadorAsync(jogador.Id,
                    LembreteDaAula.Titulo(LembreteDaAula.UmJogoAula, jogo.DataHora, agora),
                    LembreteDaAula.Frase($"Prof. {jogo.Professor.ComoChamar}", jogo.DataHora, agora, local,
                        ofereceDesmarcar: false),
                    $"/JogoAula/Detalhes/{jogo.Id}", AlcanceDoAviso.AppSemEmail);

                avisos++;
            }

            if (jogo.Professor is not { ExcluidoEm: null, NotificarLembreteDeAula: true }) continue;

            var comQuem = inscritos.Count == 1 ? "1 inscrito" : $"{inscritos.Count} inscritos";

            await push.EnviarParaJogadorAsync(jogo.ProfessorId,
                LembreteDaAula.Titulo(LembreteDaAula.UmJogoAula, jogo.DataHora, agora),
                LembreteDaAula.Frase(comQuem, jogo.DataHora, agora, local, ofereceDesmarcar: false),
                $"/JogoAula/Detalhes/{jogo.Id}", AlcanceDoAviso.AppSemEmail);

            avisos++;
        }

        return avisos;
    }
}
