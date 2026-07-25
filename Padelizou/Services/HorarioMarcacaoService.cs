using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

public class HorarioMarcacaoService : IHorarioMarcacaoService
{
    private readonly DbPadelContext _context;
    private readonly IEmailService _emailService;
    private readonly IPushNotificationService _pushService;
    private readonly IAsaasService _asaas;
    private readonly ILogger<HorarioMarcacaoService> _logger;

    public HorarioMarcacaoService(DbPadelContext context, IEmailService emailService,
        IPushNotificationService pushService, IAsaasService asaas, ILogger<HorarioMarcacaoService> logger)
    {
        _context = context;
        _emailService = emailService;
        _pushService = pushService;
        _asaas = asaas;
        _logger = logger;
    }

    public async Task<List<SlotMarcacao>> ObterSlotsDisponiveisAsync(int clubeId, DateTime data)
    {
        var regras = await _context.HorariosMarcacaoDisponivel
            .Include(h => h.QuadraClube)
            .Where(h => h.ClubeId == clubeId && h.Ativo && h.QuadraClube.Ativa
                     && (int)data.DayOfWeek == h.DiaSemana)
            .ToListAsync();

        if (regras.Count == 0) return new List<SlotMarcacao>();

        // Só há taxa a exibir se o dono do clube ativou o recebimento pelo app; senão o
        // jogador paga a quadra por fora e o valor anunciado é o valor final.
        var dono = await _context.Clubes
            .Where(c => c.Id == clubeId)
            .Select(c => c.Dono)
            .FirstOrDefaultAsync();

        bool cobraOnline = _asaas.Configurado
            && dono is { ReceberPagamentoOnline: true }
            && !string.IsNullOrWhiteSpace(dono.AsaasWalletId);

        var dataBase = data.Date;
        var marcadas = (await _context.MarcacoesJogo
            .Where(m => m.ClubeId == clubeId && m.Status == "Confirmada"
                     && m.DataHora >= dataBase && m.DataHora < dataBase.AddDays(1))
            .Select(m => new { m.QuadraClubeId, m.DataHora })
            .ToListAsync())
            .Select(m => (m.QuadraClubeId, m.DataHora))
            .ToHashSet();

        var slots = new List<SlotMarcacao>();
        foreach (var regra in regras)
        {
            var horario = dataBase.Add(regra.HoraInicio);
            var fimJanela = dataBase.Add(regra.HoraFim);

            while (horario.AddMinutes(regra.DuracaoMinutos) <= fimJanela)
            {
                if (horario > DateTime.Now && !marcadas.Contains((regra.QuadraClubeId, horario)))
                {
                    decimal? precoTotal = regra.Preco;
                    decimal? taxa = null;

                    if (cobraOnline && regra.Preco > 0)
                    {
                        // Mesmo cálculo que MarcarJogoController usa pra gerar a cobrança, pra
                        // o valor da tela bater exatamente com o do checkout.
                        var rateio = _asaas.CalcularRateio(regra.Preco.Value, "Jogo", dono!.ModoComissao);
                        precoTotal = rateio.ValorTotal;
                        taxa = rateio.ValorTotal - regra.Preco.Value;
                    }

                    slots.Add(new SlotMarcacao(regra.QuadraClubeId, regra.QuadraClube.Nome, horario,
                        horario.AddMinutes(regra.DuracaoMinutos), regra.Preco, precoTotal, taxa));
                }
                horario = horario.AddMinutes(regra.DuracaoMinutos);
            }
        }

        return slots.OrderBy(s => s.Inicio).ThenBy(s => s.QuadraNome).ToList();
    }

    public async Task<int> NotificarHorarioVagoAsync(int clubeId)
    {
        var clube = await _context.Clubes.FindAsync(clubeId);
        if (clube == null || clube.CidadeId == null) return 0;

        var elegiveis = await _context.Jogadores
            .Where(j => j.NotificarHorarioVagoRegiao
                     && _context.JogadorCidades.Any(jc => jc.JogadorId == j.Id && jc.CidadeId == clube.CidadeId))
            .ToListAsync();

        var titulo = $"Horário disponível em {clube.Nome}";
        var corpo = "Tem quadra livre hoje — corre lá marcar antes que acabe.";
        var url = $"/MarcarJogo/Clube/{clubeId}";

        foreach (var jogador in elegiveis.Where(j => j.NotificarEmail && !string.IsNullOrWhiteSpace(j.Email)))
        {
            try
            {
                await _emailService.EnviarAsync(jogador.Email!, jogador.Nome, titulo, $"<p>{corpo}</p>");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao enviar e-mail de horário vago do clube {ClubeId} pro jogador {JogadorId}", clubeId, jogador.Id);
            }
        }

        foreach (var jogador in elegiveis)
        {
            try
            {
                await _pushService.EnviarParaJogadorAsync(jogador.Id, titulo, corpo, url);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao enviar push de horário vago do clube {ClubeId} pro jogador {JogadorId}", clubeId, jogador.Id);
            }
        }

        return elegiveis.Count;
    }
}
