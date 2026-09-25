using Padel.Core.Rede;

namespace Padel.Core.Tests;

/// <summary>
/// A sala (lobby): o aperto de mão Ola → BemVindo/Recusado, as vagas na ordem rival, parceiro, segundo rival, e o
/// Iniciar que cria a partida com humano onde há gente e IA no resto.
/// </summary>
public class RedeSalaTests
{
    private sealed class Cenario
    {
        private readonly TransporteEmMemoria _host;

        public Cenario(OpcoesDaPartida? opcoes = null)
        {
            Rede = new RedeEmMemoria(11, new CondicoesDaRede { Latencia = 0.03, Jitter = 0.01 });
            _host = Rede.NovoPonto();
            Servidor = new ServidorDaPartida(_host, "Host", opcoes);
        }

        public RedeEmMemoria Rede { get; }
        public ServidorDaPartida Servidor { get; }
        public List<ClienteDaPartida> Clientes { get; } = [];

        public ClienteDaPartida NovoCliente(string nome)
        {
            var t = Rede.NovoPonto();
            Rede.Conectar(t, _host);
            var c = new ClienteDaPartida(t, nome);
            Clientes.Add(c);
            Rodar(0.3);
            return c;
        }

        /// <summary>Um ponto cru na rede, sem ClienteDaPartida — pra mandar o que um cliente de verdade não manda.</summary>
        public (TransporteEmMemoria ponto, int parDoHost) PontoCru()
        {
            var t = Rede.NovoPonto();
            var (parDoHost, _) = Rede.Conectar(t, _host);
            Rodar(0.3);
            t.Processar();
            while (t.TentarReceber(out _)) { }
            return (t, parDoHost);
        }

        public void Rodar(double segundos)
        {
            int ticks = (int)Math.Round(segundos * Protocolo.TicksPorSegundo);
            for (int i = 0; i < ticks; i++)
            {
                Rede.AvancarRelogio(Protocolo.Passo);
                Servidor.Passo();
                foreach (var c in Clientes) c.Passo();
            }
        }
    }

    private static OpcoesDaPartida Opcoes => new() { Dificuldade = Dificuldade.Dificil, PontoDeOuro = false, SetsParaVencer = 2, ModoDeGolpe = ModoDeGolpe.Automatico, Semente = 77 };

    [Fact]
    public void Tres_clientes_entram_como_rival_parceiro_e_segundo_rival_e_recebem_as_opcoes_e_a_semente()
    {
        var cenario = new Cenario(Opcoes);
        var ana = cenario.NovoCliente("Ana");
        var bia = cenario.NovoCliente("Bia");
        var caio = cenario.NovoCliente("Caio");
        cenario.Rodar(0.2);

        Assert.Equal([2, 1, 3], new[] { ana.Indice, bia.Indice, caio.Indice });
        foreach (var c in cenario.Clientes)
        {
            Assert.Equal(FaseDoCliente.NaSala, c.Fase);
            var o = c.Opcoes;
            Assert.NotNull(o);
            Assert.Equal(Dificuldade.Dificil, o.Dificuldade);
            Assert.False(o.PontoDeOuro);
            Assert.Equal(2, o.SetsParaVencer);
            Assert.Equal(ModoDeGolpe.Automatico, o.ModoDeGolpe);
            Assert.Equal(77u, c.Semente);
            Assert.Equal(["Host", "Bia", "Ana", "Caio"], c.Nomes);
        }
        Assert.Equal(77u, cenario.Servidor.Semente);
        Assert.Equal(["Host", "Bia", "Ana", "Caio"], cenario.Servidor.Nomes);
        Assert.Equal(FaseDoServidor.Sala, cenario.Servidor.Fase);
    }

    [Fact]
    public void O_quarto_cliente_e_recusado_por_sala_cheia_e_os_outros_seguem_na_sala()
    {
        var cenario = new Cenario(Opcoes);
        for (int i = 0; i < 3; i++) cenario.NovoCliente($"C{i}");
        var quarto = cenario.NovoCliente("Sobrando");
        Assert.Equal(FaseDoCliente.Recusado, quarto.Fase);
        Assert.Equal(MotivoDaRecusa.SalaCheia, quarto.MotivoDaRecusa);
        Assert.Equal(-1, quarto.Indice);
        Assert.All(cenario.Clientes.Take(3), c => Assert.Equal(FaseDoCliente.NaSala, c.Fase));
        Assert.DoesNotContain("Sobrando", cenario.Servidor.Nomes);
    }

    [Fact]
    public void Um_cliente_de_outra_versao_e_recusado_com_a_versao_do_host()
    {
        var cenario = new Cenario(Opcoes);
        var (ponto, parDoHost) = cenario.PontoCru();
        ponto.Enviar(parDoHost, Canal.Confiavel, Protocolo.EscreverOla(new Ola("Velho", Versao: Protocolo.Versao + 1)));
        cenario.Rodar(0.3);
        ponto.Processar();
        Recusado? recusa = null;
        while (ponto.TentarReceber(out var e))
            if (e.Tipo == TipoDeEventoDoTransporte.Pacote && Protocolo.TentarLerRecusado(e.Dados.Span, out var r)) recusa = r;
        Assert.NotNull(recusa);
        Assert.Equal(MotivoDaRecusa.VersaoIncompativel, recusa.Motivo);
        Assert.Equal(Protocolo.Versao, recusa.VersaoDoHost);
        Assert.DoesNotContain("Velho", cenario.Servidor.Nomes);
    }

    [Fact]
    public void Quem_sai_da_sala_libera_a_vaga_pro_proximo()
    {
        var cenario = new Cenario(Opcoes);
        var ana = cenario.NovoCliente("Ana");
        var bia = cenario.NovoCliente("Bia");
        ana.Sair();
        cenario.Rodar(0.3);
        Assert.Equal(FaseDoCliente.Desconectado, ana.Fase);
        Assert.Equal(["Host", "Bia", "", ""], cenario.Servidor.Nomes);
        Assert.Equal(["Host", "Bia", "", ""], bia.Nomes);
        var caio = cenario.NovoCliente("Caio");
        Assert.Equal(2, caio.Indice);   // a vaga do rival, que a Ana deixou
    }

    [Fact]
    public void Iniciar_cria_a_partida_com_humano_onde_ha_gente_e_avisa_os_clientes()
    {
        var cenario = new Cenario(Opcoes);
        var ana = cenario.NovoCliente("Ana");
        var partida = cenario.Servidor.Iniciar();
        Assert.Equal(FaseDoServidor.Jogando, cenario.Servidor.Fase);
        Assert.Same(partida, cenario.Servidor.Partida);
        Assert.Equal([true, false, true, false], partida.Opcoes.Humanos);
        Assert.Equal(77u, partida.Opcoes.Semente);
        Assert.Equal(Dificuldade.Dificil, partida.Opcoes.Dificuldade);
        Assert.Equal([true, false, true, false], partida.Jogadores.Select(j => j.Humano));
        cenario.Rodar(0.3);
        Assert.Equal(FaseDoCliente.Jogando, ana.Fase);
        Assert.Equal([true, false, true, false], ana.Humanos);
        Assert.Equal(["Host", "", "Ana", ""], ana.Nomes);
    }

    [Fact]
    public void Depois_de_iniciar_quem_chega_e_recusado_por_partida_em_andamento()
    {
        var cenario = new Cenario(Opcoes);
        cenario.NovoCliente("Ana");
        cenario.Servidor.Iniciar();
        var atrasado = cenario.NovoCliente("Atrasado");
        Assert.Equal(FaseDoCliente.Recusado, atrasado.Fase);
        Assert.Equal(MotivoDaRecusa.PartidaEmAndamento, atrasado.MotivoDaRecusa);
    }

    [Fact]
    public void Um_bem_vindo_com_vaga_fora_da_faixa_e_ignorado_e_o_cliente_segue_sem_quebrar()
    {
        // O cliente não confia no pacote nem com o CRC válido: um BemVindo com índice 4 aceito punha Indice = 4 no
        // cliente, e o primeiro instantâneo derrubava o Passo com IndexOutOfRangeException (Jogadores[4] na predição).
        var rede = new RedeEmMemoria(5);
        var hostCru = rede.NovoPonto();
        var t = rede.NovoPonto();
        var (_, parDoCliente) = rede.Conectar(t, hostCru);
        var cliente = new ClienteDaPartida(t, "Ana");
        var opcoes = new OpcoesDaPartida { Semente = 3 };
        var humano = new EstadoDoJogador(0, 5, 0, 0, 0, 0, BalancoDeLob: false, Humano: true);
        uint tick = 0;

        void Rodar(int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                rede.AvancarRelogio(Protocolo.Passo);
                hostCru.Processar();
                while (hostCru.TentarReceber(out _)) { }
                cliente.Passo(new Entrada(0.5f, -0.5f, AcaoPressionada: false, AcaoSegurada: false));
            }
        }
        void MandarInstantaneo()
        {
            tick += Protocolo.TicksPorInstantaneo;
            var instantaneo = new Instantaneo { Tick = tick, Estado = EstadoDaPartida.Rally, Jogadores = [humano, humano, humano, humano] };
            hostCru.Enviar(parDoCliente, Canal.NaoConfiavel, Protocolo.EscreverInstantaneo(instantaneo));
        }

        Rodar(3);   // conecta e manda o Ola
        Assert.Equal(FaseDoCliente.AguardandoResposta, cliente.Fase);
        hostCru.Enviar(parDoCliente, Canal.Confiavel, Protocolo.EscreverBemVindo(new BemVindo(4, opcoes, 3)));
        hostCru.Enviar(parDoCliente, Canal.Confiavel, Protocolo.EscreverComecou(new Comecou([true, true, true, true], ["Host", "Bia", "Ana", "Caio"])));
        for (int k = 0; k < 5; k++)
        {
            MandarInstantaneo();
            Assert.Null(Record.Exception(() => Rodar(Protocolo.TicksPorInstantaneo)));
        }
        Assert.Equal(-1, cliente.Indice);

        // O mesmo caminho com a vaga certa funciona: a recusa acima foi pelo índice, não pelo roteiro do teste.
        hostCru.Enviar(parDoCliente, Canal.Confiavel, Protocolo.EscreverBemVindo(new BemVindo(2, opcoes, 3)));
        for (int k = 0; k < 5; k++)
        {
            MandarInstantaneo();
            Rodar(Protocolo.TicksPorInstantaneo);
        }
        Assert.Equal(2, cliente.Indice);
        var desenho = cliente.ParaDesenhar();
        Assert.NotNull(desenho);
        Assert.Equal(2, desenho.IndiceLocal);
    }

    [Fact]
    public void Sem_semente_o_host_sorteia_uma_e_todo_mundo_usa_a_mesma()
    {
        var cenario = new Cenario(new OpcoesDaPartida());
        var ana = cenario.NovoCliente("Ana");
        uint semente = cenario.Servidor.Semente;
        Assert.Equal(semente, ana.Semente);
        Assert.Equal(semente, ana.Opcoes?.Semente);
        Assert.Equal(semente, cenario.Servidor.Iniciar().Opcoes.Semente);
    }
}
