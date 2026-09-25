using Godot;
using Padel.Core;
using Padel.Core.Rede;

namespace Padel.Godot;

/// <summary>
/// Os testes do transporte ENet de verdade (o que o xUnit do Core não alcança: o Padel.Core.Rede é testado com o
/// transporte em memória, e este arquivo testa a ponte com o ENet do Godot) e das sessões online por cima dele
/// (Dispose, queda, sala cheia, passo fixo, o fim do cliente pelo placar desenhado, o ResumoParaLog). Host e cliente
/// no mesmo processo, em 127.0.0.1, sem tela:
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
    /// <summary>O delta que o Godot entrega ao _PhysicsProcess a 120 Hz (physics_ticks_per_second): double, não o float do Protocolo.Passo.</summary>
    private const double UmQuadroDaFisica = 1.0 / 120;
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
        new("o Dispose do cliente com pacotes atrasados na fila não manda a fila, e o host vê o Desconectou", () =>
        {
            var par = Ligar();
            try
            {
                par.Cliente.LatenciaDeTesteMs = 60;
                for (int i = 0; i < 5; i++) par.Cliente.Enviar(par.HostVistoPeloCliente, Canal.NaoConfiavel, [(byte)i]);
                long enviadosAntes = par.Cliente.PacotesEnviados;
                par.Cliente.Dispose();   // direto, com a fila cheia: nada de Desconectar antes
                // A prova é do lado do cliente: um Dispose que despejasse a fila contaria os envios (ou o ENet os recusaria).
                // Do lado do host não dá pra ver — o ENet joga fora o que chega no mesmo lote do aviso de desconexão.
                Conferir(par.Cliente.PacotesEnviados == enviadosAntes, $"o Dispose mandou {par.Cliente.PacotesEnviados - enviadosAntes} pacote(s) da fila atrasada");
                Conferir(par.Cliente.EnviosRecusados == 0, $"o Dispose tentou mandar {par.Cliente.EnviosRecusados} pacote(s) que o ENet recusou");
                bool desconectou = false;
                Esperar(() =>
                {
                    par.Host.Processar();
                    while (par.Host.TentarReceber(out var e)) desconectou |= e.Tipo == TipoDeEventoDoTransporte.Desconectou;
                    return desconectou;
                }, "o Desconectou do cliente no host");
            }
            finally
            {
                par.Host.Dispose();
            }
        }),
        new("o Dispose da SessaoCliente (Sair → Processar → Dispose) libera a vaga no host", () =>
        {
            int porta = ProximaPorta();
            using var host = new SessaoHost(porta, new OpcoesDaPartida(), "Host", esperarSegundos: 1000);
            var cliente = new SessaoCliente("127.0.0.1", porta, "Ana");
            try
            {
                Esperar(() =>
                {
                    host.Avancar(UmQuadroDaFisica, []);
                    cliente.Avancar(UmQuadroDaFisica, []);
                    return cliente.Cliente.Fase == FaseDoCliente.NaSala && !string.IsNullOrEmpty(host.Servidor.Nomes[cliente.Cliente.Indice]);
                }, "a Ana entrar na sala");
            }
            catch
            {
                cliente.Dispose();
                throw;
            }
            int vaga = cliente.Cliente.Indice;
            cliente.Dispose();
            Esperar(() =>
            {
                host.Avancar(UmQuadroDaFisica, []);
                return string.IsNullOrEmpty(host.Servidor.Nomes[vaga]);
            }, $"o host liberar a vaga {vaga}");
            Conferir(cliente.Transporte.EnviosRecusados == 0, $"a saída tentou mandar {cliente.Transporte.EnviosRecusados} pacote(s) que o ENet recusou");
        }),
        new("host.Dispose: o cliente vê o Desconectou do host e sai da sala", () =>
        {
            int porta = ProximaPorta();
            var host = TransporteEnet.Hospedar(porta);
            using var transporteDoCliente = TransporteEnet.Conectar("127.0.0.1", porta);
            var cliente = new ClienteDaPartida(transporteDoCliente, "Ana");
            bool hostViu = false;
            try
            {
                // Os dois lados: o host só conhece o par (e só o avisa no Dispose) depois do Conectou dele — um par ainda
                // no meio do aperto do ENet não recebe aviso nenhum e fica pro prazo do cliente.
                Esperar(() =>
                {
                    host.Processar();
                    while (host.TentarReceber(out var e)) hostViu |= e.Tipo == TipoDeEventoDoTransporte.Conectou;
                    cliente.Passo();
                    return hostViu && cliente.Fase == FaseDoCliente.AguardandoResposta;
                }, "o cliente conectar");
            }
            finally
            {
                host.Dispose();
            }
            Esperar(() =>
            {
                cliente.Passo();
                return cliente.Fase != FaseDoCliente.AguardandoResposta;
            }, "o cliente perceber que o host fechou");
            Conferir(cliente.Fase == FaseDoCliente.Desconectado, $"o cliente ficou {cliente.Fase}, e não Desconectado");
        }),
        new("Conectar numa porta sem host: o transporte desiste com Desconectou e o cliente sai de Conectando", () =>
        {
            using var transporte = TransporteEnet.Conectar("127.0.0.1", ProximaPorta(), prazoDoEnetMs: 1500);
            var cliente = new ClienteDaPartida(transporte, "Ana");
            Esperar(() =>
            {
                cliente.Passo();
                return cliente.Fase != FaseDoCliente.Conectando;
            }, "o cliente sair de Conectando", ms: 6000);
            Conferir(cliente.Fase == FaseDoCliente.Desconectado, $"o cliente ficou {cliente.Fase}, e não Desconectado");
        }),
        new("sala cheia: o 4º cliente recebe o Recusado \"sala cheia\" (o ENet com 3 vagas o ignorava em silêncio)", () =>
        {
            int porta = ProximaPorta();
            using var transporteDoHost = TransporteEnet.Hospedar(porta);
            var servidor = new ServidorDaPartida(transporteDoHost, "Host", new OpcoesDaPartida());
            var transportes = new List<TransporteEnet>();
            try
            {
                var clientes = new List<ClienteDaPartida>();
                for (int i = 0; i < 4; i++)
                {
                    // Prazo curto do ENet: sem ele, o ignorado levaria mais de 30 s pra desistir.
                    var t = TransporteEnet.Conectar("127.0.0.1", porta, prazoDoEnetMs: 2000);
                    transportes.Add(t);
                    clientes.Add(new ClienteDaPartida(t, $"Cliente {i + 1}"));
                }
                Esperar(() =>
                {
                    servidor.Passo();
                    foreach (var c in clientes) c.Passo();
                    return clientes.All(c => c.Fase is not (FaseDoCliente.Conectando or FaseDoCliente.AguardandoResposta));
                }, "os quatro terem resposta", ms: 6000);
                string fases = string.Join(", ", clientes.Select(c => $"{c.Fase}{(c.MotivoDaRecusa is MotivoDaRecusa m ? $"({m})" : "")}"));
                Conferir(clientes.Count(c => c.Fase == FaseDoCliente.NaSala) == 3, $"não entraram 3: {fases}");
                Conferir(clientes.Count(c => c.Fase == FaseDoCliente.Recusado && c.MotivoDaRecusa == MotivoDaRecusa.SalaCheia) == 1,
                    $"o que sobrou não ouviu \"sala cheia\": {fases}");
            }
            finally
            {
                foreach (var t in transportes) t.Dispose();
            }
        }),
        // ---- Problemas do ENet à vista: aviso limitado no log e contadores no "Saindo após" ----
        new("aviso limitado: o 1º avisa na hora, os seguintes no máximo um por intervalo dizendo quantos calou, e todos contam", () =>
        {
            var aviso = new AvisoLimitado("erro de teste", intervaloMs: 5000);
            Conferir(aviso.Registrar(0) is string primeiro && primeiro.Contains("erro de teste"), "o primeiro não avisou (ou avisou sem dizer o quê)");
            for (long agora = 1; agora < 5000; agora += 100)
                Conferir(aviso.Registrar(agora) is null, $"avisou de novo aos {agora} ms, antes do intervalo de 5 s");
            string? depois = aviso.Registrar(5000);
            Conferir(depois is not null, "passado o intervalo, não avisou");
            Conferir(depois?.Contains("mais 50") == true && depois.Contains("52 no total"), $"o aviso não diz quantos calou e o total: {depois}");
            Conferir(aviso.Ocorrencias == 52, $"contou {aviso.Ocorrencias}, e não 52");
            Conferir(aviso.Registrar(5001) is null, "avisou logo depois do aviso anterior");
        }),
        new("o ResumoParaLog do host e do cliente mostra envios recusados, erros do ENet e descartes da rede ruim", () =>
        {
            int porta = ProximaPorta();
            using var host = new SessaoHost(porta, new OpcoesDaPartida(), "Host", esperarSegundos: 1000);
            var cliente = new SessaoCliente("127.0.0.1", porta, "Ana");
            try
            {
                Esperar(() =>
                {
                    host.Avancar(UmQuadroDaFisica, []);
                    cliente.Avancar(UmQuadroDaFisica, []);
                    return cliente.Cliente.Fase == FaseDoCliente.NaSala;
                }, "a Ana entrar na sala");
                // Perda de teste de 100 %: os não confiáveis viram descarte (o par 1 é o primeiro de cada lado).
                host.Transporte.PerdaDeTeste = 1;
                cliente.Transporte.PerdaDeTeste = 1;
                for (int i = 0; i < 3; i++) host.Transporte.Enviar(1, Canal.NaoConfiavel, [(byte)i]);
                for (int i = 0; i < 2; i++) cliente.Transporte.Enviar(1, Canal.NaoConfiavel, [(byte)i]);
                foreach (var (quem, resumo, descartes) in new[] { ("host", host.ResumoParaLog(), 3), ("cliente", cliente.ResumoParaLog(), 2) })
                    foreach (var campo in new[] { "recusados=0", "errosDoEnet=0", $"descartadosNoTeste={descartes}" })
                        Conferir(resumo.Contains(campo), $"o resumo do {quem} não tem \"{campo}\": {resumo}");
            }
            finally
            {
                cliente.Dispose();
            }
        }),
        // ---- Passo fixo das sessões (PassoFixo, no Padel.Core): o delta da física vira passos de 1/120 s ----
        new("SessaoHost: 3000 quadros de 1/120 s dão 3000 passos da partida (o quadro 2301 ficava sem passo)", () =>
        {
            using var host = new SessaoHost(ProximaPorta(), new OpcoesDaPartida { Semente = 7 }, "Host", esperarSegundos: 0);
            for (int quadro = 0; quadro < 3000; quadro++) host.Avancar(UmQuadroDaFisica, []);
            Conferir(host.Servidor.Tick == 3000, $"3000 quadros deram {host.Servidor.Tick} passos");
        }),
        new("SessaoHost: o aperto do host num quadro sem passo vale no passo seguinte", () =>
        {
            using var host = new SessaoHost(ProximaPorta(), new OpcoesDaPartida { Semente = 7 }, "Host", esperarSegundos: 0);
            host.Avancar(UmQuadroDaFisica, []);   // a sala fecha e a partida nasce
            int apertosAplicados = 0;
            host.Servidor.EntradaAplicada += (jogador, entrada) => { if (jogador == 0 && entrada.AcaoPressionada) apertosAplicados++; };
            var aperto = new Entrada(0, 0, AcaoPressionada: true, AcaoSegurada: true);
            host.Avancar(UmQuadroDaFisica / 2, [aperto]);         // física a 240 Hz: este quadro não dá passo
            host.Avancar(UmQuadroDaFisica / 2, [Entrada.Vazia]);  // este dá
            Conferir(apertosAplicados == 1, $"o aperto do quadro sem passo chegou {apertosAplicados} vez(es) à partida, e não 1");
        }),
        new("SessaoCliente: 3000 quadros de 1/120 s dão 3000 passos do cliente", () =>
        {
            var cliente = new SessaoCliente("127.0.0.1", ProximaPorta(), "Ana");   // ninguém hospedando: o relógio anda igual
            try
            {
                for (int quadro = 0; quadro < 3000; quadro++) cliente.Avancar(UmQuadroDaFisica, []);
                long passos = (long)Math.Round(cliente.Cliente.Relogio / Protocolo.Passo);
                Conferir(passos == 3000, $"3000 quadros deram {passos} passos");
            }
            finally
            {
                cliente.Dispose();
            }
        }),
        // ---- Fim da partida no cliente: pelo placar DESENHADO, não pelo instantâneo mais novo ----
        new("SessaoCliente: quando Acabou, o retrato já tem o vencedor (o instantâneo do fim chega 100 ms antes do desenho dele); o host fechar depois não vira motivo", () =>
        {
            int porta = ProximaPorta();
            var host = new SessaoHost(porta, new OpcoesDaPartida { Semente = 7 }, "Host", esperarSegundos: 1000);
            var cliente = new SessaoCliente("127.0.0.1", porta, "Ana");
            bool hostFechado = false;
            try
            {
                void Quadro()
                {
                    host.Avancar(UmQuadroDaFisica, []);
                    cliente.Avancar(UmQuadroDaFisica, []);
                }
                Esperar(() =>
                {
                    Quadro();
                    return cliente.Cliente.Fase == FaseDoCliente.NaSala && !string.IsNullOrEmpty(host.Servidor.Nomes[cliente.Cliente.Indice]);
                }, "a Ana entrar na sala");
                var partida = host.Servidor.Iniciar();   // fecha a sala já (o prazo de 1000 s nunca vence); o SessaoHost segue jogando
                // O placar feito à mão até 5-0 e 40-0: a partida inteira levaria minutos. Pro que se confere dá no mesmo — o
                // instantâneo seguinte ao último ponto já leva o vencedor, e o desenho do cliente (100 ms atrás) ainda não.
                for (int ponto = 0; ponto < 23; ponto++) partida.Placar.PontoPara(0);
                Esperar(() =>
                {
                    Quadro();
                    return cliente.Retrato.Placar.Resumo == "5-0";
                }, "o cliente desenhar o 5-0");
                while (!partida.Placar.Acabou) partida.Placar.PontoPara(0);   // o último ponto
                bool instantaneoAntesDoDesenho = false;
                Esperar(() =>
                {
                    Quadro();
                    if (!cliente.Acabou)
                    {
                        instantaneoAntesDoDesenho |= cliente.Cliente.UltimoInstantaneo?.Placar.Acabou == true;
                        return false;
                    }
                    Conferir(cliente.Retrato.Placar.Vencedor == 0,
                        $"acabou com o retrato sem o vencedor (placar desenhado \"{cliente.Retrato.Placar.Resumo}\"): a tela de fim diria \"Partida encerrada\" sem o set final");
                    return true;
                }, "o cliente ver o fim");
                Conferir(instantaneoAntesDoDesenho, "o instantâneo do fim chegou junto com o desenho dele: o caso não passou pelo atraso de interpolação");
                Conferir(cliente.Retrato.Placar.Resumo == "6-0", $"o retrato do fim mostra \"{cliente.Retrato.Placar.Resumo}\", e não \"6-0\"");
                Conferir(cliente.MotivoDoFim is null, $"fim pelo placar com motivo de queda: {cliente.MotivoDoFim}");
                // O host fecha o jogo depois da tela de fim: o cliente cai, mas a partida já tinha acabado no placar — a tela
                // dele segue "Derrota", e não "A conexão com o host caiu".
                host.Dispose();
                hostFechado = true;
                Esperar(() =>
                {
                    cliente.Avancar(UmQuadroDaFisica, []);
                    return cliente.Cliente.Fase == FaseDoCliente.Desconectado;
                }, "o cliente perceber que o host fechou");
                Conferir(cliente.Acabou, "o cliente caiu e não acabou");
                Conferir(cliente.MotivoDoFim is null, $"a queda depois do fim virou o motivo do fim: {cliente.MotivoDoFim}");
            }
            finally
            {
                cliente.Dispose();
                if (!hostFechado) host.Dispose();
            }
        }),
        new("Hospedar numa porta já aberta: o erro diz a porta", () =>
        {
            // O próprio Godot imprime "ERROR: Couldn't create an ENet host." aqui: é o caso, não uma falha dele.
            int porta = ProximaPorta();
            using var primeiro = TransporteEnet.Hospedar(porta);
            try
            {
                using var segundo = TransporteEnet.Hospedar(porta);
            }
            catch (InvalidOperationException e)
            {
                Conferir(e.Message.Contains($"porta {porta}"), $"a mensagem não diz a porta: {e.Message}");
                return;
            }
            throw new Falha($"abriu duas salas na porta {porta}");
        }),
    ];

    private static void Conferir(bool condicao, string motivo)
    {
        if (!condicao) throw new Falha(motivo);
    }

    private static int ProximaPorta() => _porta++;

    /// <summary>Repete o passo até ele dizer "pronto" (com 2 ms de folga entre as voltas); estoura o prazo = falha.</summary>
    private static void Esperar(Func<bool> pronto, string oQue, int ms = 3000)
    {
        var relogio = System.Diagnostics.Stopwatch.StartNew();
        while (relogio.ElapsedMilliseconds < ms)
        {
            if (pronto()) return;
            System.Threading.Thread.Sleep(2);
        }
        throw new Falha($"esperou {ms / 1000.0:0.#} s por {oQue}");
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

        public void Bombear(Func<bool> pronto, string oQue) => Esperar(() =>
        {
            Host.Processar();
            Cliente.Processar();
            return pronto();
        }, oQue);

        public void Dispose()
        {
            Cliente.Dispose();
            Host.Dispose();
        }
    }

    private static ParLigado Ligar()
    {
        int porta = ProximaPorta();
        var host = TransporteEnet.Hospedar(porta);
        var cliente = TransporteEnet.Conectar("127.0.0.1", porta);
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
