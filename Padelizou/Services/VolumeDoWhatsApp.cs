namespace Padelizou.Services;

// QUANTO pode sair pelo canal de WhatsApp.
//
// ⚠️ Não confundir com `RitmoDoWhatsApp`, que é outra coisa: ele espaça uma mensagem da outra
// (7–16s) e mata a RAJADA. Espaçamento não limita TOTAL, e foi essa fresta que sobrou do
// conserto de 04/08/2026 — a rajada morreu, o volume não.
//
// ⚠️ OS NÚMEROS SAEM DA PLANILHA DO PRODUTO, NÃO DE PRÁTICA GENÉRICA DE ANTI-SPAM. A primeira
// versão disto (60/hora, 300/dia) foi dimensionada só pelo risco de ban e **estava errada**: um
// torneio de 100 participantes manda ~450 mensagens no dia, e só o aviso de "chaves saíram" são
// 100 de uma vez — o teto de 60/hora cortava 40 avisos logo na primeira ação do dia, e o mais
// importante deles ("seu jogo é o próximo") ia junto.
//
// A conta, medida no código que dispara (`AlcanceDoAviso.AppEWhatsApp`), pra 100 participantes:
//
//   chaves saíram ....... 1 por JOGADOR ............................. 100 (de uma vez)
//   seu jogo é o próximo  4 por partida × ~88 partidas ............... ~350
//   lembrete de 24h ..... 1 por jogador com jogo ..................... 100 (na véspera)
//
// Daí 250/hora (o `WHATSAPP.md` põe a zona de risco em ~500/h, então isto é metade dela, com
// folga sobre o pico real de ~150) e 1.200/dia (quase 3× um torneio cheio). O teto não existe
// pra apertar o uso normal: existe pra um laço infinito não torrar o número numa madrugada.
public static class TetoDoWhatsApp
{
    public const int PorHora = 250;
    public const int PorDia = 1200;

    // ⚠️ NÃO EXISTE AQUECIMENTO, e é decisão consciente (Felipe, 07/08/2026). A versão anterior
    // segurava o canal em 30/dia por uma semana depois de reconectar — o que faz sentido pra
    // número NOVO e não pro nosso, que só levou uma restrição e tem histórico de uso real. Com
    // um torneio marcado, a rampa custaria avisos de verdade pra ganhar reputação que o número
    // já tem.
    //
    // Se um dia o chip for trocado por um número realmente novo, a rampa volta a valer a pena —
    // o código dela está no histórico (commit `3d4dc8d`), não precisa ser reinventado.

    // A partir de quanto o Felipe é avisado de que está perto do limite. Descobrir pelo contador
    // de barradas é descobrir DEPOIS de já ter perdido aviso.
    public const int PorcentoParaAvisar = 80;

    public static bool PertoDoTeto(int naUltimaHora, int noDia) =>
        naUltimaHora >= PorHora * PorcentoParaAvisar / 100
        || noDia >= PorDia * PorcentoParaAvisar / 100;
}

// A contagem do que saiu.
//
// Singleton, em memória, e isso é uma escolha com consequência: **um restart zera a contagem**.
// Aceito de propósito — persistir cada envio numa tabela por causa de um teto seria caro pro
// que entrega, e restart é gesto deliberado (deploy), não coisa que acontece no meio de uma
// rajada.
public class VolumeDoWhatsApp
{
    private readonly object _tranca = new();
    private readonly Queue<DateTime> _saidas = new();
    private readonly ILogger<VolumeDoWhatsApp> _logger;

    public VolumeDoWhatsApp(ILogger<VolumeDoWhatsApp> logger) => _logger = logger;

    // Quantas foram barradas pelo teto desde que o app subiu. Nunca é zero calado: cada barrada
    // escreve no log, o painel mostra o total, e o vigia avisa ANTES, aos 80%.
    public int BloqueadasPeloTeto { get; private set; }

    public DateTime? UltimaBloqueada { get; private set; }

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

            if (naHora >= TetoDoWhatsApp.PorHora || noDia >= TetoDoWhatsApp.PorDia)
            {
                BloqueadasPeloTeto++;
                UltimaBloqueada = agora;

                // Warning e não Debug: mensagem barrada é aviso que NÃO chegou em ninguém, e
                // isso precisa doer no log. Silêncio aqui repetiria o erro de 03/08, em que
                // ~200 avisos falharam sem ninguém saber.
                _logger.LogWarning("Teto do WhatsApp atingido ({NaHora}/{TetoHora} na hora, "
                    + "{NoDia}/{TetoDia} no dia) — mensagem descartada. Total barrado: {Total}.",
                    naHora, TetoDoWhatsApp.PorHora, noDia, TetoDoWhatsApp.PorDia, BloqueadasPeloTeto);

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

    public bool PertoDoTeto(DateTime agora) =>
        TetoDoWhatsApp.PertoDoTeto(NaUltimaHora(agora), NoDia(agora));

    // A fila só cresce enquanto interessa: o que é de antes de ontem não conta em nenhuma das
    // duas janelas e só ocuparia memória.
    private void Esquecer(DateTime agora)
    {
        var limite = agora.Date.AddDays(-1);
        while (_saidas.Count > 0 && _saidas.Peek() < limite) _saidas.Dequeue();
    }

    private int ContarDesde(DateTime inicio) => _saidas.Count(q => q >= inicio);

    // O dia é o CIVIL (de meia-noite a meia-noite no fuso de Brasília), não as últimas 24h: é
    // assim que a pessoa do outro lado percebe o volume, e é assim que se lê "o número mandou
    // 300 mensagens hoje".
    private int ContarDoDia(DateTime agora) => _saidas.Count(q => q.Date == agora.Date);
}
