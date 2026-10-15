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

    // ───────────── Parte 2: o movimento assistido ─────────────

    /// <summary>
    /// A cena do movimento: o rival acabou de bater uma bola funda pro lado direito do humano (jogador 0, lado +1); o
    /// parceiro IA está longe, na outra ponta; a IA do time planeja (como a partida faz no golpe do rival) e manda o humano
    /// na bola. O humano fica com o analógico em <paramref name="analogico"/> por <paramref name="passos"/> passos.
    /// </summary>
    private static (Partida Partida, Jogador Humano, float XAntes, float YAntes) CenaDoMovimento(Assistencia nivel, float humanoX, float humanoY,
        int passos, float analogico = 0, float parceiroX = -3.5f, float parceiroY = 6.5f)
    {
        var partida = new Partida(new OpcoesDaPartida { Semente = 21, Humanos = [true, false, false, false] });
        var vazia = Entrada.Vazia;
        partida.Avancar(Passo, [new Entrada(0, 0, true, true), vazia, vazia, vazia]);
        var humano = partida.Jogadores[0];
        var parceiro = partida.Jogadores[1];
        humano.Assistencia = nivel;
        humano.X = humanoX; humano.Y = humanoY; humano.Vx = 0; humano.Vy = 0;
        parceiro.X = parceiroX; parceiro.Y = parceiroY; parceiro.Vx = 0; parceiro.Vy = 0;
        partida.Arbitro.RegistrarGolpe(1);
        partida.Bola.Posicionar(2.5f, -6f, 1.0f);
        partida.Bola.Lancar(new Velocidade(0, 14, 3.5f, 0));
        partida.IAs[0].AoGolpear(partida, new Golpe(2.5f, 6, 1, TipoDeGolpe.Normal), partida.Jogadores[2]);
        float xAntes = humano.X, yAntes = humano.Y;
        for (int i = 0; i < passos && partida.Estado == EstadoDaPartida.Rally; i++)
            partida.Avancar(Passo, [new Entrada(analogico, 0, false, false), vazia, vazia, vazia]);
        return (partida, humano, xAntes, yAntes);
    }

    private static float Distancia(float ax, float ay, float bx, float by) => MathF.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));

    [Fact]
    public void No_Casual_com_o_analogico_solto_o_corpo_vai_sozinho_ate_o_ponto_de_contato()
    {
        // 100 passos: a bola ainda vem (quica perto do passo 110, e aí o plano muda); a 9 m/s², 1,7 m levam uns 90.
        var (partida, humano, x0, y0) = CenaDoMovimento(Assistencia.Casual, 0.5f, 7.0f, passos: 100);
        Assert.Same(humano, partida.IAs[0].Responsavel);
        Assert.True(partida.IAs[0].AlvoDoCorpo(humano) is (float ax, float ay), "a IA do time devia mandar o humano na bola");
        var (tx, ty) = partida.IAs[0].AlvoDoCorpo(humano)!.Value;
        Assert.True(Distancia(x0, y0, tx, ty) > 1.0f, "a cena devia pôr o humano longe do ponto de contato");
        Assert.True(Distancia(humano.X, humano.Y, tx, ty) < 0.15f,
            $"o Casual devia levar o humano até ({tx:F2}, {ty:F2}); está em ({humano.X:F2}, {humano.Y:F2})");
    }

    [Fact]
    public void No_Pro_com_o_analogico_solto_o_corpo_fica_parado()
    {
        var (_, humano, x0, y0) = CenaDoMovimento(Assistencia.Pro, 0.5f, 7.0f, passos: 60);
        Assert.Equal(x0, humano.X);
        Assert.Equal(y0, humano.Y);
    }

    [Fact]
    public void No_Padrao_so_o_ajuste_final_dentro_de_um_metro()
    {
        // Longe (mais de 1 m do ponto): o Padrão não leva.
        var (_, longe, x0, y0) = CenaDoMovimento(Assistencia.Padrao, 0.5f, 7.0f, passos: 60);
        Assert.Equal(x0, longe.X);
        Assert.Equal(y0, longe.Y);
        // Perto: o mesmo ponto, com o humano a 0,7 m dele — o Padrão leva.
        var (referencia, humanoDaReferencia, _, _) = CenaDoMovimento(Assistencia.Casual, 0.5f, 7.0f, passos: 0);
        var (tx, ty) = referencia.IAs[0].AlvoDoCorpo(humanoDaReferencia)!.Value;
        var (partida, perto, px, py) = CenaDoMovimento(Assistencia.Padrao, tx - 0.7f, ty, passos: 70);
        Assert.Same(perto, partida.IAs[0].Responsavel);
        var (tx2, ty2) = partida.IAs[0].AlvoDoCorpo(perto)!.Value;
        Assert.True(Distancia(px, py, tx2, ty2) < 1.0f, "a cena devia pôr o humano a menos de 1 m");
        Assert.True(Distancia(perto.X, perto.Y, tx2, ty2) < 0.15f, $"o Padrão devia fazer o ajuste final até ({tx2:F2}, {ty2:F2}); está em ({perto.X:F2}, {perto.Y:F2})");
    }

    [Fact]
    public void Com_o_analogico_mexido_quem_manda_e_o_jogador()
    {
        // Analógico pra esquerda (no referencial dele; no lado +1, −x): ele vai pra esquerda, longe do ponto à direita.
        // 45 passos (0,375 s) saindo do repouso a 9 m/s²: uns 0,6 m.
        var (_, humano, x0, _) = CenaDoMovimento(Assistencia.Casual, 0.5f, 7.0f, passos: 45, analogico: -1);
        Assert.True(humano.X < x0 - 0.3f, $"devia ter ido pra esquerda: x de {x0:F2} pra {humano.X:F2}");
    }

    [Fact]
    public void Quem_nao_vai_na_bola_nao_e_levado()
    {
        // O parceiro IA perto do ponto, o humano longe na esquerda: a IA manda o parceiro, e o humano fica.
        var (partida, humano, x0, y0) = CenaDoMovimento(Assistencia.Casual, -3.5f, 6.5f, passos: 40, parceiroX: 2.0f, parceiroY: 7.0f);
        Assert.NotSame(humano, partida.IAs[0].Responsavel);
        Assert.Null(partida.IAs[0].AlvoDoCorpo(humano));
        Assert.Equal(x0, humano.X);
        Assert.Equal(y0, humano.Y);
    }
}
