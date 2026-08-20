using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// A régua e o texto do RESUMO SEMANAL, puros — a varredura fica no serviço de fundo.
public static class ResumoSemanalDeTorneios
{
    public const string ChaveDoMarcador = "ResumoSemanalDeTorneiosEnviadoEm";

    // Segunda a partir do meio-dia: é quando a semana de jogo se planeja. O marcador (e não o
    // relógio) é quem garante UM envio — a varredura passa de meia em meia hora a segunda
    // inteira, e sem ele cada passada repetiria o push pra base.
    public static bool HoraDeEnviar(DateTime agora, DateTime? ultimoEnvio) =>
        agora.DayOfWeek == DayOfWeek.Monday
        && agora.Hour >= 12
        && (ultimoEnvio == null || agora - ultimoEnvio.Value >= TimeSpan.FromDays(6));

    // A "semana" do resumo: o que foi ANUNCIADO (AvisoDeTorneioNovoEm) nos últimos 7 dias.
    public static readonly TimeSpan Janela = TimeSpan.FromDays(7);

    // Nulo quando não há torneio — semana parada não manda resumo nenhum (mandar "nenhum
    // torneio novo" é o aviso que ensina a desligar o canal).
    //
    // Até 3 nomes por extenso; mais que isso vira "e mais N" — a notificação corta o que
    // passa de duas linhas, e um rosário de nomes não informa mais que o número.
    public static TextoDoAviso? Texto(IReadOnlyList<string> nomes)
    {
        if (nomes.Count == 0) return null;

        if (nomes.Count == 1)
            return new("Torneio novo esta semana 🎾",
                $"{nomes[0]} está com inscrições abertas. Garanta sua vaga.");

        var mostrados = nomes.Take(3).ToList();
        var lista = string.Join(", ", mostrados.Take(mostrados.Count - 1))
                    + " e " + mostrados[^1];
        var resto = nomes.Count - mostrados.Count;
        if (resto > 0) lista += $" (e mais {resto})";

        return new($"{nomes.Count} torneios novos esta semana 🎾",
            $"{lista} estão com inscrições abertas. Escolha o seu.");
    }
}

// O RESUMO SEMANAL DE TORNEIOS: toda segunda ao meio-dia, um push por pessoa com os torneios
// anunciados na semana que ainda estão com inscrições abertas — na região dela.
//
// Ele NÃO substitui o "Novo torneio aberto" (que sai na hora, um por torneio): é o
// repescagem de quem viu o aviso avulso passar no meio do dia e esqueceu. Por isso a régua
// de audiência é exatamente a mesma (NotificarTorneiosAbertos + UF do AvisoDeTorneioNovo) —
// quem desligou o avulso não ganha o resumo pela porta dos fundos.
public class ResumoSemanalDeTorneiosBackgroundService : BackgroundService
{
    private static readonly TimeSpan IntervaloTick = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ResumoSemanalDeTorneiosBackgroundService> _logger;

    public ResumoSemanalDeTorneiosBackgroundService(IServiceScopeFactory scopeFactory,
        ILogger<ResumoSemanalDeTorneiosBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        using var timer = new PeriodicTimer(IntervaloTick);

        // Sem varredura na subida (mesma escolha do AvisoDoMvp): deploy não é segunda-feira.
        // O marcador seguraria a repetição de qualquer jeito, mas esperar um tick é de graça.
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<DbPadelContext>();
                var push = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();

                var avisados = await VarrerAsync(context, push, DateTime.Now, stoppingToken);
                if (avisados > 0)
                    _logger.LogInformation("Resumo semanal de torneios enviado pra {Total} pessoa(s).", avisados);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao enviar o resumo semanal de torneios.");
            }
        }
    }

    // A varredura com o `agora` por parâmetro, pro teste alcançar qualquer segunda-feira.
    // Devolve quantas PESSOAS receberam o resumo.
    public static async Task<int> VarrerAsync(DbPadelContext context, IPushNotificationService push,
        DateTime agora, CancellationToken stoppingToken = default)
    {
        if (!ResumoSemanalDeTorneios.HoraDeEnviar(agora, await LerMarcadorAsync(context))) return 0;

        // ⚠️ O MARCADOR VEM ANTES DO ENVIO, mesmo numa semana sem torneio — as duas lições do
        // AvisoDoMvp: duas passadas quase juntas não podem mandar dobrado, e semana vazia não
        // pode ser reperguntada de meia em meia hora até meia-noite.
        await GravarMarcadorAsync(context, agora);

        // O que foi anunciado na semana E ainda dá pra entrar. A régua da vitrine
        // (aprovado, não oculto) já foi aplicada quando o anúncio saiu — o carimbo
        // AvisoDeTorneioNovoEm só existe em quem passou por ela.
        var corte = agora - ResumoSemanalDeTorneios.Janela;
        var daSemana = await context.Torneios
            .Where(t => t.AvisoDeTorneioNovoEm != null && t.AvisoDeTorneioNovoEm > corte
                     && !t.Oculto && t.Status == "Inscrições Abertas")
            .Select(t => new { t.Id, t.Nome })
            .ToListAsync(stoppingToken);

        if (daSemana.Count == 0) return 0;

        // A UF de cada torneio, uma vez só — a mesma mira por estado do aviso avulso.
        var ufPorTorneio = new Dictionary<int, string?>();
        foreach (var t in daSemana)
            ufPorTorneio[t.Id] = await UfDoTorneio.DescobrirAsync(context, t.Id);

        var candidatos = await context.Jogadores
            .Where(j => j.NotificarTorneiosAbertos && j.ExcluidoEm == null)
            .Select(j => new { j.Id, j.Estado })
            .ToListAsync(stoppingToken);

        int avisados = 0;
        foreach (var jogador in candidatos)
        {
            // Em memória, como no AvisoDeTorneioNovo: Estado é texto livre e quem casa é o
            // UnidadeFederativa. Torneio sem UF vai pra todo mundo; jogador sem estado recebe
            // tudo — as mesmas portas de escape, pelo mesmo motivo.
            var nomes = daSemana
                .Where(t => ufPorTorneio[t.Id] == null
                            || UnidadeFederativa.Combina(jogador.Estado, ufPorTorneio[t.Id]!))
                .Select(t => t.Nome)
                .ToList();

            var texto = ResumoSemanalDeTorneios.Texto(nomes);
            if (texto == null) continue;

            await push.EnviarParaJogadorAsync(jogador.Id, texto.Titulo, texto.Corpo,
                "/Torneios", AlcanceDoAviso.AppSemEmail);
            avisados++;
        }

        return avisados;
    }

    private static async Task<DateTime?> LerMarcadorAsync(DbPadelContext context)
    {
        var linha = await context.ConfiguracoesDoSistema.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Chave == ResumoSemanalDeTorneios.ChaveDoMarcador);
        return linha != null && DateTime.TryParse(linha.Valor, out var data) ? data : null;
    }

    private static async Task GravarMarcadorAsync(DbPadelContext context, DateTime valor)
    {
        var linha = await context.ConfiguracoesDoSistema
            .FirstOrDefaultAsync(c => c.Chave == ResumoSemanalDeTorneios.ChaveDoMarcador);
        if (linha == null)
        {
            context.ConfiguracoesDoSistema.Add(new ConfiguracaoDoSistema
            {
                Chave = ResumoSemanalDeTorneios.ChaveDoMarcador,
                Valor = valor.ToString("O"),
            });
        }
        else
        {
            linha.Valor = valor.ToString("O");
            linha.AtualizadoEm = DateTime.Now;
        }
        await context.SaveChangesAsync();
    }
}
