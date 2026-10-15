namespace Padel.Core.Tests;

/// <summary>
/// Os níveis de assistência da fatia no Unreal (padelizou-game, docs/ASSISTENCIA.md), primeira parte: a janela do timing.
/// No Pro (o padrão, o jogo de hoje) a raquete só pega a bola no instante do balanço; no Padrão (±20 ms) e no Casual
/// (±40 ms) ela pega em qualquer passo da janela em volta desse instante, naquele em que a bola está mais perto do ponto
/// ideal — a mesma regra da IA (<see cref="Jogador.PodeBaterAgora"/>). O nível é de cada jogador e muda a qualquer hora.
/// </summary>
public class AssistenciaTests
{
    private const float Passo = 1f / 120f;

    private sealed record Resultado(EventoDaPartida? Golpe, float Erro, int PassosDoApertoAoGolpe);

    /// <summary>
    /// A bola baixa do GolpesTests (vem da rede e passa ao lado do humano parado a ~1 m de altura, já quicada). O humano
    /// aperta o A <paramref name="adiantamento"/> segundos antes da hora certa — a hora em que o instante do balanço cai no
    /// passo em que a bola fica mais perto do ponto ideal (negativo = atrasado).
    /// </summary>
    private static Resultado Cena(Assistencia? nivelDoJogador, float adiantamento, Assistencia nivelDaPartida = Assistencia.Pro)
    {
        var partida = new Partida(new OpcoesDaPartida { Semente = 21, Humanos = [true, false, false, false], Assistencia = nivelDaPartida });
        var vazia = Entrada.Vazia;
        partida.Avancar(Passo, [new Entrada(0, 0, true, true), vazia, vazia, vazia]);
        Assert.Equal(EstadoDaPartida.Rally, partida.Estado);
        var humano = partida.Jogadores[0];
        if (nivelDoJogador is Assistencia n) humano.Assistencia = n;

        partida.Arbitro.RegistrarGolpe(1);
        partida.Arbitro.Processar(new EventoDaBola(TipoDeEventoDaBola.Quique, 2.6f, 5.5f, 0, 1));
        humano.X = 2.0f; humano.Y = 8.0f; humano.Vx = 0; humano.Vy = 0; humano.Cooldown = 0;
        partida.Bola.Posicionar(2.6f, 5.5f, 1.0f);
        partida.Bola.Lancar(new Velocidade(0, 7, 1.5f, 0));

        // A hora certa: o passo em que a bola (sem ninguém bater) fica mais perto do ponto ideal do humano parado.
        var clone = partida.Bola.Clonar();
        var eventos = new List<EventoDaBola>();
        int melhor = 0;
        float menor = float.PositiveInfinity;
        for (int i = 1; i <= 240; i++)
        {
            clone.Avancar(Passo, eventos);
            float d = humano.DistanciaAoPontoIdeal(clone.X, clone.Y);
            if (d < menor && humano.Alcanca(clone)) { menor = d; melhor = i; }
        }
        int passoDoAperto = melhor - (int)MathF.Round((Jogador.MomentoIdealDoBalanco + adiantamento) / Passo);

        EventoDaPartida? golpe = null;
        int passoDoGolpe = -1, passo = 0;
        partida.Evento += ev => { if (ev.Tipo == TipoDeEventoDaPartida.Golpe && ev.Jogador == humano && golpe is null) { golpe = ev; passoDoGolpe = passo; } };
        for (passo = 1; passo <= 240 && golpe is null && partida.Estado == EstadoDaPartida.Rally; passo++)
        {
            var entrada = passo == passoDoAperto ? new Entrada(0, 0, AcaoPressionada: true, AcaoSegurada: true) : vazia;
            partida.Avancar(Passo, [entrada, vazia, vazia, vazia]);
        }
        return new Resultado(golpe, golpe is null ? float.NaN : partida.UltimoErroDoHumano, golpe is null ? -1 : passoDoGolpe - passoDoAperto);
    }

    [Fact]
    public void O_padrao_e_o_Pro_o_jogo_de_hoje()
    {
        Assert.Equal(Assistencia.Pro, new OpcoesDaPartida().Assistencia);
        var partida = new Partida(new OpcoesDaPartida { Humanos = [true, true, false, false] });
        Assert.All(partida.Jogadores, j => Assert.Equal(Assistencia.Pro, j.Assistencia));
        Assert.Equal(0f, Partida.JanelaDoContato(Assistencia.Pro));
        Assert.Equal(0.02f, Partida.JanelaDoContato(Assistencia.Padrao));
        Assert.Equal(0.04f, Partida.JanelaDoContato(Assistencia.Casual));
    }

    [Fact]
    public void As_opcoes_dao_o_nivel_inicial_dos_humanos()
    {
        var partida = new Partida(new OpcoesDaPartida { Humanos = [true, false, true, false], Assistencia = Assistencia.Casual });
        Assert.Equal(Assistencia.Casual, partida.Jogadores[0].Assistencia);
        Assert.Equal(Assistencia.Casual, partida.Jogadores[2].Assistencia);
    }

    /// <summary>
    /// Apertando cedo ou tarde dentro da janela, o Casual e o Padrão pegam a bola mais perto do ponto ideal que o Pro, e o
    /// contato fica dentro da janela (o Pro: sempre no mesmo passo do balanço).
    /// </summary>
    [Theory]
    [InlineData(0.04f)]
    [InlineData(0.02f)]
    [InlineData(-0.02f)]
    [InlineData(-0.04f)]
    public void Fora_da_hora_a_janela_pega_a_bola_mais_perto_do_ideal_e_dentro_dela(float adiantamento)
    {
        var pro = Cena(Assistencia.Pro, adiantamento);
        var padrao = Cena(Assistencia.Padrao, adiantamento);
        var casual = Cena(Assistencia.Casual, adiantamento);
        Assert.True(pro.Golpe is not null && padrao.Golpe is not null && casual.Golpe is not null, "a cena devia ter golpe nos três níveis");
        Assert.True(casual.Erro < pro.Erro, $"Casual {casual.Erro:F3} devia errar menos que o Pro {pro.Erro:F3}");
        Assert.True(padrao.Erro < pro.Erro, $"Padrão {padrao.Erro:F3} devia errar menos que o Pro {pro.Erro:F3}");
        Assert.True(casual.Erro <= padrao.Erro, $"Casual {casual.Erro:F3} devia errar no máximo o do Padrão {padrao.Erro:F3}");
        // Janela em passos de 1/120 s: ±40 ms = ±4,8; ±20 ms = ±2,4.
        Assert.InRange(casual.PassosDoApertoAoGolpe - pro.PassosDoApertoAoGolpe, -5, 5);
        Assert.InRange(padrao.PassosDoApertoAoGolpe - pro.PassosDoApertoAoGolpe, -3, 3);
    }

    /// <summary>
    /// Na hora certa a janela não piora nada e mexe no contato no máximo um passo: a "hora certa" em passos de 1/120 s pode
    /// deixar o instante do Pro a um passo do melhor, e a janela acha o melhor.
    /// </summary>
    [Fact]
    public void Na_hora_certa_a_janela_nao_piora_e_mexe_no_maximo_um_passo()
    {
        var pro = Cena(Assistencia.Pro, 0);
        var casual = Cena(Assistencia.Casual, 0);
        Assert.NotNull(pro.Golpe);
        Assert.True(casual.Erro <= pro.Erro, $"Casual {casual.Erro:F4} piorou o Pro {pro.Erro:F4}");
        Assert.InRange(casual.PassosDoApertoAoGolpe - pro.PassosDoApertoAoGolpe, -1, 1);
    }

    [Fact]
    public void Muito_cedo_nem_a_janela_do_Casual_salva_a_raquete_passa()
    {
        Assert.Null(Cena(Assistencia.Casual, 0.3f).Golpe);
        Assert.Null(Cena(Assistencia.Pro, 0.3f).Golpe);
    }

    [Fact]
    public void O_nivel_e_de_cada_jogador_e_muda_a_qualquer_hora()
    {
        // A partida começou no Pro; o jogador passa pro Casual no meio dela e já joga com a janela.
        var pro = Cena(null, 0.04f, nivelDaPartida: Assistencia.Pro);
        var mudou = Cena(Assistencia.Casual, 0.04f, nivelDaPartida: Assistencia.Pro);
        var casual = Cena(null, 0.04f, nivelDaPartida: Assistencia.Casual);
        Assert.Equal(casual.Erro, mudou.Erro);
        Assert.True(mudou.Erro < pro.Erro);
    }
}
