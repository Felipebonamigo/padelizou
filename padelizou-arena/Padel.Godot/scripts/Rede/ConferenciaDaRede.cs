using Godot;
using Padel.Core.Rede;

namespace Padel.Godot;

/// <summary>
/// Os testes do transporte ENet de verdade (o que o xUnit do Core não alcança: o Padel.Core.Rede é testado com o
/// transporte em memória, e este arquivo testa a ponte com o ENet do Godot). Host e cliente no mesmo processo,
/// em 127.0.0.1, sem tela:
/// <code>godot --headless --path Padel.Godot res://cenas/TesteRede.tscn -- --conferir</code>
/// Cada caso imprime "ok" ou "FALHOU" com o motivo; o processo sai com 0 se tudo passou e 1 se algo falhou.
/// Mesmo executor mínimo da <see cref="Interface.ConferenciaDaInterface"/> (e o mesmo atalho: sem projeto de teste do Godot).
/// </summary>
public static class ConferenciaDaRede
{
    private sealed class Falha(string mensagem) : Exception(mensagem);

    private sealed record Caso(string Nome, Action Rodar);

    // Porta alta e fora da 7777 padrão, pra não brigar com um jogo aberto na mesma máquina.
    private const int PortaBase = 47810;
    private static int _porta = PortaBase;

    public static void RodarESair(Node no)
    {
        int falhas = 0, total = 0;
        foreach (var caso in Casos())
        {
            total++;
            try
            {
                caso.Rodar();
                GD.Print($"  ok      {caso.Nome}");
            }
            catch (Exception e)   // qualquer exceção é o caso falhando, não o executor
            {
                falhas++;
                GD.PrintErr($"  FALHOU  {caso.Nome}: {e.Message}");
            }
        }
        GD.Print($"Conferência da rede: {total} casos, {falhas} falha(s).");
        no.GetTree().Quit(falhas == 0 && total > 0 ? 0 : 1);
    }

    private static IEnumerable<Caso> Casos() =>
    [
        new("host e cliente conectam em 127.0.0.1 e os dois veem o Conectou", () =>
        {
            using var par = Ligar();
        }),
        new("pacote confiável e não confiável chegam inteiros e pelo canal certo", () =>
        {
            using var par = Ligar();
            par.Cliente.Enviar(par.HostVistoPeloCliente, Canal.Confiavel, [1, 2, 3]);
            par.Cliente.Enviar(par.HostVistoPeloCliente, Canal.NaoConfiavel, [9, 8]);
            var recebidos = par.EsperarPacotes(par.Host, 2);
            Conferir(recebidos.Any(e => e.Canal == Canal.Confiavel && e.Dados.Span.SequenceEqual(new byte[] { 1, 2, 3 })), "o confiável não chegou igual");
            Conferir(recebidos.Any(e => e.Canal == Canal.NaoConfiavel && e.Dados.Span.SequenceEqual(new byte[] { 9, 8 })), "o não confiável não chegou igual");
        }),
        new("latência de teste segura o pacote na saída até vencer", () =>
        {
            using var par = Ligar();
            par.Cliente.LatenciaDeTesteMs = 150;
            var relogio = System.Diagnostics.Stopwatch.StartNew();
            par.Cliente.Enviar(par.HostVistoPeloCliente, Canal.Confiavel, [7]);
            par.EsperarPacotes(par.Host, 1);
            Conferir(relogio.ElapsedMilliseconds >= 150, $"chegou em {relogio.ElapsedMilliseconds} ms, antes dos 150 de latência");
        }),
        new("pacote atrasado na fila não é mandado a quem já foi desconectado (o 'Invalid channel' da saída do cliente)", () =>
        {
            using var par = Ligar();
            par.Cliente.LatenciaDeTesteMs = 60;
            for (int i = 0; i < 5; i++) par.Cliente.Enviar(par.HostVistoPeloCliente, Canal.NaoConfiavel, [(byte)i]);
            long enviadosAntes = par.Cliente.PacotesEnviados;
            par.Cliente.Desconectar(par.HostVistoPeloCliente);
            System.Threading.Thread.Sleep(80);   // a latência vence com o par já saindo
            par.Cliente.Processar();
            Conferir(par.Cliente.EnviosRecusados == 0, $"o transporte tentou mandar {par.Cliente.EnviosRecusados} pacote(s) pra um par que está saindo");
            Conferir(par.Cliente.PacotesEnviados == enviadosAntes, "contou como enviado pacote pra quem já saiu");
        }),
        new("envio sem latência logo depois do Desconectar não chega ao ENet (o par está saindo)", () =>
        {
            using var par = Ligar();
            long enviadosAntes = par.Cliente.PacotesEnviados;
            par.Cliente.Desconectar(par.HostVistoPeloCliente);
            par.Cliente.Enviar(par.HostVistoPeloCliente, Canal.Confiavel, [1]);
            par.Cliente.Enviar(par.HostVistoPeloCliente, Canal.NaoConfiavel, [2]);
            Conferir(par.Cliente.EnviosRecusados == 0, $"o ENet recusou {par.Cliente.EnviosRecusados} envio(s) pra um par saindo");
            Conferir(par.Cliente.PacotesEnviados == enviadosAntes, "contou como enviado pacote pra quem está saindo");
        }),
        new("o Dispose do cliente com pacotes atrasados na fila não tenta mandar nada", () =>
        {
            var par = Ligar();
            par.Cliente.LatenciaDeTesteMs = 60;
            for (int i = 0; i < 5; i++) par.Cliente.Enviar(par.HostVistoPeloCliente, Canal.NaoConfiavel, [(byte)i]);
            var cliente = par.Cliente;
            // O caminho do SessaoCliente.Dispose: sai da sala, bombeia uma última vez e fecha.
            cliente.Desconectar(par.HostVistoPeloCliente);
            System.Threading.Thread.Sleep(80);
            cliente.Processar();
            Conferir(cliente.EnviosRecusados == 0, $"tentou mandar {cliente.EnviosRecusados} pacote(s) na saída");
            par.Dispose();
        }),
    ];

    private static void Conferir(bool condicao, string motivo)
    {
        if (!condicao) throw new Falha(motivo);
    }

    private sealed class ParLigado(TransporteEnet host, TransporteEnet cliente, int hostVistoPeloCliente) : IDisposable
    {
        public TransporteEnet Host { get; } = host;
        public TransporteEnet Cliente { get; } = cliente;
        public int HostVistoPeloCliente { get; } = hostVistoPeloCliente;

        public List<EventoDoTransporte> EsperarPacotes(TransporteEnet quem, int quantos)
        {
            var pacotes = new List<EventoDoTransporte>();
            Bombear(() =>
            {
                while (quem.TentarReceber(out var e))
                    if (e.Tipo == TipoDeEventoDoTransporte.Pacote) pacotes.Add(e);
                return pacotes.Count >= quantos;
            }, $"{quantos} pacote(s)");
            return pacotes;
        }

        public void Bombear(Func<bool> pronto, string oQue)
        {
            var relogio = System.Diagnostics.Stopwatch.StartNew();
            while (relogio.ElapsedMilliseconds < 3000)
            {
                Host.Processar();
                Cliente.Processar();
                if (pronto()) return;
                System.Threading.Thread.Sleep(2);
            }
            throw new Falha($"esperou 3 s por {oQue}");
        }

        public void Dispose()
        {
            Cliente.Dispose();
            Host.Dispose();
        }
    }

    private static ParLigado Ligar()
    {
        var host = TransporteEnet.Hospedar(_porta);
        var cliente = TransporteEnet.Conectar("127.0.0.1", _porta);
        _porta++;
        bool hostViu = false;
        int? parDoHost = null;
        var par = new ParLigado(host, cliente, 0);
        try
        {
            par.Bombear(() =>
            {
                while (host.TentarReceber(out var e)) if (e.Tipo == TipoDeEventoDoTransporte.Conectou) hostViu = true;
                while (cliente.TentarReceber(out var e)) if (e.Tipo == TipoDeEventoDoTransporte.Conectou) parDoHost = e.Par;
                return hostViu && parDoHost is not null;
            }, "o Conectou dos dois lados");
        }
        catch
        {
            par.Dispose();
            throw;
        }
        return parDoHost is int visto ? new ParLigado(host, cliente, visto) : throw new Falha("sem par do host");
    }
}
