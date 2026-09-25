using Padel.Core.Rede;

namespace Padel.Core.Tests;

/// <summary>
/// O transporte em memória: relógio virtual, latência, jitter, perda, duplicação e reordenação — tudo com semente.
/// O canal confiável entrega tudo, em ordem, uma vez só; o não confiável sofre o que foi configurado.
/// </summary>
public class RedeTransporteTests
{
    private static List<EventoDoTransporte> Drenar(ITransporte t)
    {
        t.Processar();
        var eventos = new List<EventoDoTransporte>();
        while (t.TentarReceber(out var e)) eventos.Add(e);
        return eventos;
    }

    private static byte[] Numero(int n) => BitConverter.GetBytes(n);
    private static int Numero(EventoDoTransporte e) => BitConverter.ToInt32(e.Dados.Span);

    [Fact]
    public void Conectar_avisa_os_dois_lados_depois_da_latencia_e_nao_antes()
    {
        var rede = new RedeEmMemoria(1, new CondicoesDaRede { Latencia = 0.05 });
        var cliente = rede.NovoPonto();
        var host = rede.NovoPonto();
        var (parDoHost, parDoCliente) = rede.Conectar(cliente, host);
        Assert.Empty(Drenar(host));
        Assert.Empty(Drenar(cliente));
        rede.AvancarRelogio(0.049);
        Assert.Empty(Drenar(host));
        rede.AvancarRelogio(0.002);
        var noHost = Assert.Single(Drenar(host));
        Assert.Equal(new EventoDoTransporte(TipoDeEventoDoTransporte.Conectou, parDoCliente), noHost);
        rede.AvancarRelogio(0.06);
        var noCliente = Assert.Single(Drenar(cliente));
        Assert.Equal(TipoDeEventoDoTransporte.Conectou, noCliente.Tipo);
        Assert.Equal(parDoHost, noCliente.Par);
    }

    [Fact]
    public void Nada_chega_sem_Processar()
    {
        var rede = new RedeEmMemoria(1, new CondicoesDaRede { Latencia = 0.01 });
        var a = rede.NovoPonto();
        var b = rede.NovoPonto();
        var (parDeB, _) = rede.Conectar(a, b);
        rede.AvancarRelogio(0.1);
        a.Enviar(parDeB, Canal.NaoConfiavel, Numero(7));
        rede.AvancarRelogio(0.1);
        Assert.False(b.TentarReceber(out _));
        b.Processar();
        Assert.True(b.TentarReceber(out var conectou));
        Assert.Equal(TipoDeEventoDoTransporte.Conectou, conectou.Tipo);
        Assert.True(b.TentarReceber(out var pacote));
        Assert.Equal(7, Numero(pacote));
        Assert.Equal(Canal.NaoConfiavel, pacote.Canal);
    }

    [Fact]
    public void O_canal_confiavel_entrega_tudo_em_ordem_uma_vez_so_e_com_latencia_mesmo_com_rede_ruim()
    {
        var ruim = new CondicoesDaRede { Latencia = 0.04, Jitter = 0.03, Perda = 0.5, Duplicacao = 0.3, Reordenacao = 0.5 };
        var rede = new RedeEmMemoria(3, ruim);
        var a = rede.NovoPonto();
        var b = rede.NovoPonto();
        var (parDeB, _) = rede.Conectar(a, b);
        rede.AvancarRelogio(0.2);
        Drenar(a); Drenar(b);
        var enviadoEm = new Dictionary<int, double>();
        var recebidos = new List<int>();
        for (int n = 0; n < 600; n++)
        {
            if (n < 300)
            {
                a.Enviar(parDeB, Canal.Confiavel, Numero(n));
                enviadoEm[n] = rede.Agora;
            }
            rede.AvancarRelogio(0.001);
            foreach (var e in Drenar(b))
            {
                Assert.Equal(TipoDeEventoDoTransporte.Pacote, e.Tipo);
                Assert.Equal(Canal.Confiavel, e.Canal);
                int numero = Numero(e);
                Assert.True(rede.Agora - enviadoEm[numero] >= ruim.Latencia - 1e-9, $"a mensagem {numero} chegou antes da latência");
                recebidos.Add(numero);
            }
        }
        Assert.Equal(Enumerable.Range(0, 300), recebidos);
    }

    private static List<(double quando, int numero)> MandarNaoConfiavel(uint semente, CondicoesDaRede condicoes, int quantos, out RedeEmMemoria rede)
    {
        rede = new RedeEmMemoria(semente, condicoes);
        var a = rede.NovoPonto();
        var b = rede.NovoPonto();
        var (parDeB, _) = rede.Conectar(a, b);
        rede.AvancarRelogio(1);
        Drenar(a); Drenar(b);
        var recebidos = new List<(double, int)>();
        double duracao = quantos * 0.002 + condicoes.Latencia + condicoes.Jitter + condicoes.AtrasoDaReordenacao + 0.1;
        for (int passo = 0; passo * 0.002 < duracao; passo++)
        {
            if (passo < quantos) a.Enviar(parDeB, Canal.NaoConfiavel, Numero(passo));
            rede.AvancarRelogio(0.002);
            foreach (var e in Drenar(b)) recebidos.Add((rede.Agora, Numero(e)));
        }
        return recebidos;
    }

    [Fact]
    public void O_canal_nao_confiavel_perde_duplica_e_reordena_na_proporcao_configurada()
    {
        var condicoes = new CondicoesDaRede { Latencia = 0.075, Jitter = 0.02, Perda = 0.10, Duplicacao = 0.05, Reordenacao = 0.10, AtrasoDaReordenacao = 0.03 };
        const int quantos = 5000;
        var recebidos = MandarNaoConfiavel(42, condicoes, quantos, out var rede);
        int unicos = recebidos.Select(r => r.numero).Distinct().Count();
        int duplicados = recebidos.Count - unicos;
        int foraDeOrdem = recebidos.Zip(recebidos.Skip(1)).Count(p => p.Second.numero < p.First.numero);
        Assert.InRange(1 - unicos / (double)quantos, 0.08, 0.12);
        Assert.InRange(duplicados / (double)quantos, 0.03, 0.07);
        Assert.True(foraDeOrdem > quantos * 0.05, $"quase nada fora de ordem: {foraDeOrdem}");
        Assert.Equal(quantos, rede.PacotesEnviados);
        Assert.Equal(quantos - unicos, rede.PacotesPerdidos);
        Assert.Equal(duplicados, rede.PacotesDuplicados);
        // Cada pacote chega entre a latência e a latência + jitter + atraso de reordenação.
        foreach (var (quando, numero) in recebidos)
        {
            double enviado = 1 + numero * 0.002;   // o relógio começou em 1 s e anda 2 ms por envio
            double atraso = quando - enviado;
            Assert.InRange(atraso, condicoes.Latencia - 1e-9, condicoes.Latencia + condicoes.Jitter + condicoes.AtrasoDaReordenacao + 0.0021);
        }
    }

    [Fact]
    public void A_mesma_semente_reproduz_a_mesma_rede_e_outra_semente_nao()
    {
        var condicoes = new CondicoesDaRede { Latencia = 0.03, Jitter = 0.02, Perda = 0.2, Duplicacao = 0.1, Reordenacao = 0.2 };
        var a = MandarNaoConfiavel(7, condicoes, 800, out _);
        var b = MandarNaoConfiavel(7, condicoes, 800, out _);
        var c = MandarNaoConfiavel(8, condicoes, 800, out _);
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void Desconectar_avisa_o_outro_lado_depois_do_que_ja_estava_a_caminho_e_para_de_entregar()
    {
        var rede = new RedeEmMemoria(5, new CondicoesDaRede { Latencia = 0.05 });
        var a = rede.NovoPonto();
        var b = rede.NovoPonto();
        var (parDeB, parDeA) = rede.Conectar(a, b);
        rede.AvancarRelogio(0.2);
        Drenar(a); Drenar(b);

        a.Enviar(parDeB, Canal.Confiavel, Numero(1));
        a.Desconectar(parDeB);
        a.Enviar(parDeB, Canal.Confiavel, Numero(2));   // depois de desconectar: some sem erro
        var noA = Assert.Single(Drenar(a));
        Assert.Equal(new EventoDoTransporte(TipoDeEventoDoTransporte.Desconectou, parDeB), noA);

        rede.AvancarRelogio(0.1);
        b.Enviar(parDeA, Canal.NaoConfiavel, Numero(3));   // b ainda não sabe; a não recebe mais nada
        var noB = Drenar(b);
        Assert.Equal(2, noB.Count);
        Assert.Equal(1, Numero(noB[0]));
        Assert.Equal(new EventoDoTransporte(TipoDeEventoDoTransporte.Desconectou, parDeA), noB[1]);
        rede.AvancarRelogio(0.1);
        Assert.Empty(Drenar(a));
    }

    [Fact]
    public void Quem_escuta_a_rede_ve_cada_envio_com_o_tamanho()
    {
        var rede = new RedeEmMemoria(9, new CondicoesDaRede { Latencia = 0.01, Perda = 1 });
        var a = rede.NovoPonto();
        var b = rede.NovoPonto();
        var (parDeB, _) = rede.Conectar(a, b);
        var vistos = new List<PacoteNaRede>();
        rede.AoEnviar += vistos.Add;
        a.Enviar(parDeB, Canal.NaoConfiavel, new byte[] { 1, 2, 3 });
        var visto = Assert.Single(vistos);
        Assert.Equal(a.Id, visto.De);
        Assert.Equal(b.Id, visto.Para);
        Assert.Equal(3, visto.Dados.Length);
        Assert.Equal(1, rede.PacotesPerdidos);
    }
}
