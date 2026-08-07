using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// QUANTO pode sair pelo canal de WhatsApp. Puro e testável, porque é ele que decide se o
// número sobrevive ao próximo sábado de torneio.
//
// ⚠️ Não confundir com `RitmoDoWhatsApp`, que é outra coisa: ele espaça uma mensagem da outra
// (7–16s) e mata a RAJADA. Espaçamento não limita TOTAL — 11,5s de média são ~313 mensagens
// por hora ininterruptas, e o `WHATSAPP.md` já chama ~500/h de zona de risco. Foi essa fresta
// que sobrou do conserto de 04/08/2026: a rajada morreu, o volume não.
public static class TetoDoWhatsApp
{
    // Por hora, sempre. É o freio contra o pico: uma noite de inscrições ou um torneio
    // publicando chaves gera dezenas de avisos legítimos em minutos, e legítimo ou não, é o
    // pico que a Meta enxerga.
    public const int PorHora = 60;

    // Por dia, depois que o número está estabelecido.
    public const int PorDiaEmRegime = 300;

    // Por dia, enquanto o número é novo — ou está voltando de uma restrição, que é o mesmo
    // problema: reputação zerada. Voltar direto no volume de antes é o caminho mais curto pra
    // segunda restrição, e a segunda costuma ser mais dura que a primeira.
    public const int PorDiaAquecendo = 30;

    public static readonly TimeSpan DuracaoDoAquecimento = TimeSpan.FromDays(7);

    // ⚠️ Sem data de início, o teto é o BAIXO, não o alto. "Não sei desde quando este número
    // envia" e "este número é veterano" não podem dar no mesmo resultado — no primeiro caso o
    // barato é esperar uma semana, e o caro é perder o número.
    public static int PorDia(DateTime agora, DateTime? aquecimentoComecouEm)
    {
        if (aquecimentoComecouEm == null) return PorDiaAquecendo;

        return agora - aquecimentoComecouEm.Value >= DuracaoDoAquecimento
            ? PorDiaEmRegime
            : PorDiaAquecendo;
    }

    public static bool AindaAquecendo(DateTime agora, DateTime? aquecimentoComecouEm) =>
        PorDia(agora, aquecimentoComecouEm) == PorDiaAquecendo;
}

// A contagem do que saiu, e a memória de quando o número começou a esquentar.
//
// Singleton, em memória, e isso é uma escolha com consequência: **um restart zera a contagem
// da hora e do dia**. Aceito de propósito — persistir cada envio numa tabela por causa de um
// teto seria caro pro que entrega, e restart não acontece no meio de uma rajada (deploy é
// gesto deliberado). O que NÃO pode ser esquecido é o começo do aquecimento, e esse mora no
// banco (`ConfiguracaoDoSistema`), igual ao portão.
public class VolumeDoWhatsApp
{
    public const string ChaveDoAquecimento = "WhatsApp.AquecimentoComecouEm";

    private readonly object _tranca = new();
    private readonly Queue<DateTime> _saidas = new();
    private readonly ILogger<VolumeDoWhatsApp> _logger;

    public VolumeDoWhatsApp(ILogger<VolumeDoWhatsApp> logger) => _logger = logger;

    // Quando este número começou a enviar. Null = nunca conectou desde que passamos a marcar,
    // e aí vale o teto de aquecimento.
    public DateTime? AquecimentoComecouEm { get; private set; }

    // Quantas foram barradas pelo teto desde que o app subiu. Nunca é zero calado: cada
    // barrada escreve no log, e o painel mostra o total.
    public int BloqueadasPeloTeto { get; private set; }

    public DateTime? UltimaBloqueada { get; private set; }

    public async Task CarregarAsync(DbPadelContext context)
    {
        var linha = await context.ConfiguracoesDoSistema
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Chave == ChaveDoAquecimento);

        if (linha != null && DateTime.TryParse(linha.Valor, out var quando))
            AquecimentoComecouEm = quando;
    }

    // Chamado pelo vigia toda vez que o canal aparece conectado. Grava só na PRIMEIRA —
    // reescrever a cada checagem empurraria o fim do aquecimento pra sempre, e o número
    // nunca sairia do teto baixo.
    public async Task MarcarConectadoAsync(DbPadelContext context, DateTime agora)
    {
        if (AquecimentoComecouEm != null) return;

        var linha = await context.ConfiguracoesDoSistema
            .FirstOrDefaultAsync(c => c.Chave == ChaveDoAquecimento);

        if (linha == null)
        {
            linha = new ConfiguracaoDoSistema { Chave = ChaveDoAquecimento };
            context.ConfiguracoesDoSistema.Add(linha);
        }

        linha.Valor = agora.ToString("o");
        linha.AtualizadoEm = agora;

        await context.SaveChangesAsync();

        // Só depois de gravar, mesmo motivo do portão: memória adiantada em relação ao banco
        // faz o aquecimento recomeçar sozinho no próximo restart.
        AquecimentoComecouEm = agora;

        _logger.LogWarning("Canal de WhatsApp conectado — aquecimento começa agora, teto de "
            + "{Teto} mensagens/dia por {Dias} dias.",
            TetoDoWhatsApp.PorDiaAquecendo, TetoDoWhatsApp.DuracaoDoAquecimento.TotalDays);
    }

    // A pergunta do entregador, logo antes de mandar. Registra a saída no mesmo passo em que
    // autoriza: separar as duas coisas abriria janela pra duas mensagens passarem pelo mesmo
    // último lugar do teto.
    public bool RegistrarSePuder(DateTime agora)
    {
        lock (_tranca)
        {
            Esquecer(agora);

            var naHora = ContarDesde(agora.AddHours(-1));
            var noDia = ContarDoDia(agora);
            var tetoDoDia = TetoDoWhatsApp.PorDia(agora, AquecimentoComecouEm);

            if (naHora >= TetoDoWhatsApp.PorHora || noDia >= tetoDoDia)
            {
                BloqueadasPeloTeto++;
                UltimaBloqueada = agora;

                // Warning e não Debug: mensagem barrada é aviso que NÃO chegou em ninguém, e
                // isso precisa doer no log. Silêncio aqui seria repetir o erro de 03/08, em
                // que ~200 avisos falharam sem ninguém saber.
                _logger.LogWarning("Teto do WhatsApp atingido ({NaHora}/{TetoHora} na hora, "
                    + "{NoDia}/{TetoDia} no dia) — mensagem descartada. Total barrado: {Total}.",
                    naHora, TetoDoWhatsApp.PorHora, noDia, tetoDoDia, BloqueadasPeloTeto);

                return false;
            }

            _saidas.Enqueue(agora);
            return true;
        }
    }

    public int NaUltimaHora(DateTime agora)
    {
        lock (_tranca) { Esquecer(agora); return ContarDesde(agora.AddHours(-1)); }
    }

    public int NoDia(DateTime agora)
    {
        lock (_tranca) { Esquecer(agora); return ContarDoDia(agora); }
    }

    public int TetoDoDia(DateTime agora) => TetoDoWhatsApp.PorDia(agora, AquecimentoComecouEm);

    public bool AindaAquecendo(DateTime agora) => TetoDoWhatsApp.AindaAquecendo(agora, AquecimentoComecouEm);

    // A fila só cresce enquanto interessa: o que é de antes de ontem não conta em nenhuma das
    // duas janelas e só ocuparia memória.
    private void Esquecer(DateTime agora)
    {
        var limite = agora.Date.AddDays(-1);
        while (_saidas.Count > 0 && _saidas.Peek() < limite) _saidas.Dequeue();
    }

    private int ContarDesde(DateTime inicio) => _saidas.Count(q => q >= inicio);

    // O dia é o CIVIL (de meia-noite a meia-noite no fuso de Brasília), não as últimas 24h:
    // é assim que a pessoa do outro lado percebe o volume, e é assim que se conta "o número
    // mandou 300 mensagens hoje".
    private int ContarDoDia(DateTime agora) => _saidas.Count(q => q.Date == agora.Date);
}
