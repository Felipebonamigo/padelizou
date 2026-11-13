namespace Padelizou.Services;

// O QUE ESPERA PRA SAIR: a hora de um jogo mudou com a chave pública, e o aviso ainda não foi.
// Quem anota são as ações que remarcam (TorneiosController.AnotarHorariosMudados); quem tira e
// manda é o relógio (AvisoDeHorarioMudadoBackgroundService → AvisoDeHorarioMudado).
//
// ⏱️ A ESPERA É DO TORNEIO, contada da ÚLTIMA mudança nele: arrumar a grade é uma rajada de
// cliques (as setas sobem o mesmo jogo três horários em 20 segundos, e mexem nos vizinhos).
// Aviso por clique encheria o celular de todo mundo e queimaria a cota de e-mail (100/dia).
// Três minutos sem mexer no torneio = o organizador terminou; e quem está em dois jogos mexidos
// recebe UM aviso com os dois.
//
// 🧯 COM TETO DE 10 MINUTOS desde a mudança mais antiga que espera: no dia do torneio o
// organizador remarca sem parar, e o jogo que foi pra mais cedo não pode esperar ele parar.
//
// 🧠 O "ANTES" É O DA PRIMEIRA MUDANÇA: é o horário que o jogador conhece. Quem foi de 18:00 pra
// 18:50 e depois pra 19:40 recebe "de 18:00 pra 19:40"; quem voltou pras 18:00 não recebe nada
// (isso o envio decide, lendo a hora de agora no banco).
//
// 💨 EM MEMÓRIA, como a FilaDeAvisos: aviso é perecível. Um restart dentro da espera perde o
// aviso daquela mexida — o mesmo preço que a fila já paga, e a tela continua com a hora certa.
// Persistir pediria tabela nova pra um aviso que vale minutos.
public class AvisosDeHorarioPendentes
{
    public static readonly TimeSpan Quietude = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan Teto = TimeSpan.FromMinutes(10);

    public sealed record Vencido(int TorneioId, int PartidaId, DateTime? Antes);

    private sealed class DoTorneio
    {
        public required DateTime PrimeiraMudanca { get; init; }
        public required DateTime UltimaMudanca { get; set; }
        public Dictionary<int, DateTime?> AntesPorJogo { get; } = new();
    }

    private readonly object _trava = new();
    private readonly Dictionary<int, DoTorneio> _porTorneio = new();

    public void Anotar(int torneioId, IEnumerable<AvisoDeHorarioMudado.Mudanca> mudancas, DateTime agora)
    {
        lock (_trava)
        {
            foreach (var mudanca in mudancas)
            {
                if (!_porTorneio.TryGetValue(torneioId, out var doTorneio))
                {
                    doTorneio = new DoTorneio { PrimeiraMudanca = agora, UltimaMudanca = agora };
                    _porTorneio[torneioId] = doTorneio;
                }
                doTorneio.UltimaMudanca = agora;
                doTorneio.AntesPorJogo.TryAdd(mudanca.PartidaId, mudanca.Antes);
            }
        }
    }

    // Tira (e esquece) o que já pode sair: um aviso não sai duas vezes.
    public List<Vencido> Tirar(DateTime agora)
    {
        lock (_trava)
        {
            var vencidos = new List<Vencido>();
            foreach (var (torneioId, doTorneio) in _porTorneio.OrderBy(par => par.Key).ToList())
            {
                if (agora - doTorneio.UltimaMudanca < Quietude && agora - doTorneio.PrimeiraMudanca < Teto) continue;

                vencidos.AddRange(doTorneio.AntesPorJogo.OrderBy(par => par.Key)
                    .Select(par => new Vencido(torneioId, par.Key, par.Value)));
                _porTorneio.Remove(torneioId);
            }
            return vencidos;
        }
    }
}
