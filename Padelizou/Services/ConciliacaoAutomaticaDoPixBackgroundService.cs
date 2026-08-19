using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Padelizou.Models;

namespace Padelizou.Services;

// Varre o extrato do Inter de tempos em tempos e faz o que o admin faria olhando o extrato
// na mão: confirma sozinho o que já bate com uma cobrança nossa (mesmo TxId, mesmo valor) e
// deixa como SUGESTÃO em /Admin/PixDireto o que só bate por CPF (Pix combinado por fora,
// sem cobrança) — o clique final continua sendo humano nesse segundo caso. A regra de quem
// casa com quem mora em ConciliacaoAutomaticaDoPix; aqui só busca dados e persiste.
//
// Nasce DESLIGADO junto com InterPixSettings.Habilitado. Enquanto a conta do Inter não
// existir, este serviço nem começa a rodar — a conferência manual continua sendo o caminho.
public class ConciliacaoAutomaticaDoPixBackgroundService : BackgroundService
{
    // Guarda até onde já foi varrido, pra não sugerir o mesmo Pix duas vezes a cada tick.
    // Vive em ConfiguracaoDoSistema — mesma prateleira da chave Pix (ChavePixDoPadelizou).
    private const string ChaveDoMarcador = "PixInter.UltimoRecebidoEm";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly InterPixSettings _cfg;
    private readonly ILogger<ConciliacaoAutomaticaDoPixBackgroundService> _logger;

    public ConciliacaoAutomaticaDoPixBackgroundService(IServiceScopeFactory scopeFactory,
        IOptions<InterPixSettings> cfg, ILogger<ConciliacaoAutomaticaDoPixBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _cfg = cfg.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_cfg.Habilitado)
        {
            _logger.LogInformation("Conciliação automática do Pix (Inter) desligada — sem client_id configurado.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, _cfg.IntervaloDeVarreduraMinutos)));
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await VarrerAsync(stoppingToken);
        }
    }

    private async Task VarrerAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbPadelContext>();
            var provedor = scope.ServiceProvider.GetRequiredService<IProvedorDeExtratoPix>();
            var pagamentos = scope.ServiceProvider.GetRequiredService<IPagamentoInscricaoService>();
            var planoCfg = scope.ServiceProvider.GetRequiredService<IOptions<PlanoProfessorSettings>>().Value;

            var marcador = await LerMarcadorAsync(context) ?? DateTime.Now.AddHours(-_cfg.JanelaInicialHoras);
            var agora = DateTime.Now;

            var recebidos = await provedor.ListarRecebidosAsync(marcador, agora, ct);
            // Só o que é estritamente novo — o marcador é inclusivo do lado do banco (a API
            // trabalha por dia, não por instante), então refiltra aqui pra não reprocessar
            // o mesmo Pix do fim da janela anterior.
            var novos = recebidos.Where(r => r.Horario > marcador).ToList();

            if (novos.Count > 0)
            {
                var pendentes = await context.Pagamentos
                    .Where(p => p.MetodoPagamento == PixDireto.Metodo
                        && (p.Status == "Pendente" || p.Status == PixDireto.AguardandoConfirmacao))
                    .ToListAsync(ct);
                var professores = await context.Jogadores.Where(j => j.IsProfessor).ToListAsync(ct);

                var resultado = ConciliacaoAutomaticaDoPix.Conciliar(novos, pendentes, professores);

                foreach (var (pendente, credito) in resultado.ParaConfirmar)
                {
                    // O mesmo webhook humano de sempre (AdminController.ConfirmarPix), só que
                    // quem conferiu o extrato foi o próprio Inter — por isso ConfirmadoPorJogadorId
                    // fica nulo, do mesmo jeito que o Asaas confirmando pelo webhook.
                    pendente.Status = "Confirmado";
                    pendente.ConfirmadoEm = credito.Horario;
                    await pagamentos.EfetivarAsync(pendente);

                    _logger.LogInformation(
                        "Pix direto {Id} confirmado automaticamente pelo extrato do Inter (e2e {E2e}, R$ {Valor}).",
                        pendente.Id, credito.EndToEndId, credito.Valor);
                }

                foreach (var (professor, credito) in resultado.ParaSugerir)
                {
                    var ehAnual = Math.Abs(credito.Valor - planoCfg.AnuidadeAssinante) < 1m;
                    var ciclo = ehAnual ? PlanoDoProfessor.CicloAnual : PlanoDoProfessor.CicloMensal;
                    var dados = new DadosAssinaturaProfessor(professor.Id, ciclo);

                    context.Pagamentos.Add(new Pagamento
                    {
                        Tipo = PixDireto.TipoAssinatura,
                        JogadorId = professor.Id,
                        RecebedorId = null,
                        Valor = credito.Valor,
                        ValorRepasse = 0m,
                        Comissao = credito.Valor,
                        MetodoPagamento = PixDireto.Metodo,
                        DadosInscricao = System.Text.Json.JsonSerializer.Serialize(dados),
                        Status = PixDireto.AguardandoConfirmacao,
                        CriadoEm = credito.Horario,
                    });

                    _logger.LogInformation(
                        "Pix de {Cpf} (R$ {Valor}) casou pelo CPF com o professor {ProfessorId} — " +
                        "sugestão criada, esperando confirmação em /Admin/PixDireto.",
                        credito.CpfCnpjPagador, credito.Valor, professor.Id);
                }

                await context.SaveChangesAsync(ct);
            }

            var novoMarcador = novos.Count > 0 ? novos.Max(r => r.Horario) : agora;
            await GravarMarcadorAsync(context, novoMarcador);
        }
        catch (Exception ex)
        {
            // Nunca deixa uma falha aqui (Inter fora do ar, token expirado, JSON mudou de
            // formato) travar o app — a conferência manual em /Admin/PixDireto segue valendo
            // exatamente como hoje.
            _logger.LogError(ex, "Falha ao varrer o extrato do Inter para conciliação automática do Pix.");
        }
    }

    private static async Task<DateTime?> LerMarcadorAsync(DbPadelContext context)
    {
        var linha = await context.ConfiguracoesDoSistema.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Chave == ChaveDoMarcador);
        return linha != null && DateTime.TryParse(linha.Valor, out var data) ? data : null;
    }

    private static async Task GravarMarcadorAsync(DbPadelContext context, DateTime valor)
    {
        var linha = await context.ConfiguracoesDoSistema.FirstOrDefaultAsync(c => c.Chave == ChaveDoMarcador);
        if (linha == null)
        {
            context.ConfiguracoesDoSistema.Add(new ConfiguracaoDoSistema
            {
                Chave = ChaveDoMarcador,
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
