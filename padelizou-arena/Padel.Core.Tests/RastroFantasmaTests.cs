namespace Padel.Core.Tests;

/// <summary>
/// O rastro-fantasma da saída de parede (pilar 1 da fatia no Unreal): a previsão é a própria física rodada adiante, então
/// o rastro tem de ser, bit a bit, o caminho que a bola faz quando ninguém bate — e acabar onde a bola deixa de ter jogo
/// do lado de quem recebe.
/// </summary>
public class RastroFantasmaTests
{
    private const float Passo = 1f / 120f;   // o passo da partida, escrito aqui: o do RastroFantasma é o que se confere

    // Do fundo do lado −1 rumo ao +1 (eventos conferidos no Core de 15/10):
    // (20; 3) quica a 6,4 m, bate no vidro de fundo e quica de novo a 4,6 m; (11; 4) quica duas vezes antes do vidro;
    // (28; 3) bate no vidro de fundo antes de quicar.
    private static Bola Lancada(float vy, float vz)
    {
        var b = new Bola();
        b.Posicionar(1, -8, 1);
        b.Lancar(new Velocidade(0, vy, vz, 0));
        return b;
    }

    /// <summary>O caminho real: a cada passo, a posição e os eventos do passo.</summary>
    private static List<(float X, float Y, float Z, float T, List<EventoDaBola> Eventos)> Caminho(Bola bola, int passos)
    {
        var b = bola.Clonar();
        var caminho = new List<(float, float, float, float, List<EventoDaBola>)>();
        float t = 0;
        for (int i = 0; i < passos; i++)
        {
            var eventos = new List<EventoDaBola>();
            b.Avancar(Passo, eventos);
            t += Passo;
            caminho.Add((b.X, b.Y, b.Z, t, eventos));
        }
        return caminho;
    }

    private static int PassoDo(List<(float X, float Y, float Z, float T, List<EventoDaBola> Eventos)> caminho, TipoDeEventoDaBola tipo, int ocorrencia = 1)
    {
        int vistas = 0;
        for (int i = 0; i < caminho.Count; i++)
            foreach (var e in caminho[i].Eventos)
                if (e.Tipo == tipo && ++vistas == ocorrencia) return i;
        return -1;
    }

    /// <summary>
    /// O rastro contra a bola DA PARTIDA (quatro humanos parados, ninguém bate), passo a passo — e não contra um laço igual
    /// ao do próprio Prever, que passaria com o passo errado (revisão de 15/10: com o rastro a 1/60 s, o teste antigo
    /// seguia verde).
    /// </summary>
    [Theory]
    [InlineData(20f, 3f)]
    [InlineData(11f, 4f)]
    [InlineData(28f, 3f)]
    public void O_rastro_e_o_caminho_da_bola_da_partida_quando_ninguem_bate(float vy, float vz)
    {
        var partida = new Partida(new OpcoesDaPartida { Semente = 3, Humanos = [true, true, true, true] });
        var entradas = new Entrada[4];
        entradas[partida.Sacador.Time * 2 + partida.Sacador.Indice] = new Entrada(0, 0, true, true);
        partida.Avancar(Passo, entradas);
        Assert.Equal(EstadoDaPartida.Rally, partida.Estado);
        entradas = new Entrada[4];
        partida.Arbitro.RegistrarGolpe(1);   // bateu o time do lado −1
        partida.Bola.Posicionar(1, -8, 1);
        partida.Bola.Lancar(new Velocidade(0, vy, vz, 0));
        var rastro = RastroFantasma.Prever(partida.Bola, lado: 1, jaQuicou: false);
        Assert.True(rastro.Pontos.Count > 10, $"rastro curto demais: {rastro.Pontos.Count} pontos");
        Assert.True(partida.Bola.X == 1 && partida.Bola.Y == -8 && partida.Bola.Z == 1, "a previsão mudou a bola de verdade");
        for (int i = 0; i < rastro.Pontos.Count; i++)
        {
            Assert.Equal(EstadoDaPartida.Rally, partida.Estado);
            partida.Avancar(Passo, entradas);
            var p = rastro.Pontos[i];
            var b = partida.Bola;
            Assert.True(p.X == b.X && p.Y == b.Y && p.Z == b.Z,
                $"ponto {i}: rastro ({p.X:R}, {p.Y:R}, {p.Z:R}), bola da partida ({b.X:R}, {b.Y:R}, {b.Z:R})");
        }
        // O rastro acaba onde a partida decide o ponto.
        Assert.NotEqual(EstadoDaPartida.Rally, partida.Estado);
    }

    /// <summary>
    /// Revisão de 15/10: com o segundo quique e o vidro no mesmo passo, a bola já morreu no quique — o vidro depois dele não
    /// é saída de parede. Antes, o laço seguia lendo os eventos do passo e marcava a saída.
    /// </summary>
    [Fact]
    public void Quique_e_vidro_no_mesmo_passo_a_bola_ja_morreu_e_nao_ha_saida()
    {
        Bola? achada = null;
        for (float y0 = 9.80f; y0 < 9.999f && achada is null; y0 += 0.0005f)
        for (float z0 = 0.005f; z0 < 0.2f && achada is null; z0 += 0.005f)
        {
            var b = new Bola();
            b.Posicionar(0, y0, z0);
            b.Lancar(new Velocidade(0, 12, -3, 0));
            var eventos = new List<EventoDaBola>();
            b.Clonar().Avancar(Passo, eventos);
            if (eventos.Count >= 2 && eventos[0].Tipo == TipoDeEventoDaBola.Quique && eventos[1].Tipo == TipoDeEventoDaBola.Parede) achada = b;
        }
        Assert.NotNull(achada);
        var rastro = RastroFantasma.Prever(achada, lado: 1, jaQuicou: true);
        Assert.Single(rastro.Pontos);
        Assert.Equal(-1, rastro.PrimeiroDepoisDaParede);
    }

    [Fact]
    public void A_saida_de_parede_comeca_no_passo_da_batida_no_vidro_e_o_rastro_acaba_no_segundo_quique()
    {
        var bola = Lancada(20, 3);
        var rastro = RastroFantasma.Prever(bola, lado: 1, jaQuicou: false);
        var caminho = Caminho(bola, 400);
        int parede = PassoDo(caminho, TipoDeEventoDaBola.Parede);
        int segundoQuique = PassoDo(caminho, TipoDeEventoDaBola.Quique, ocorrencia: 2);
        Assert.True(PassoDo(caminho, TipoDeEventoDaBola.Quique) < parede && parede < segundoQuique, "a cena devia ser quique, vidro, quique");
        Assert.Equal(parede, rastro.PrimeiroDepoisDaParede);
        Assert.Equal(segundoQuique, rastro.Pontos.Count - 1);
        // Depois do vidro de fundo do lado +1, a bola volta rumo à rede.
        Assert.True(rastro.Pontos[parede + 1].Y < rastro.Pontos[parede].Y, "depois do vidro a bola devia voltar");
    }

    [Fact]
    public void Bola_que_quica_duas_vezes_antes_do_vidro_nao_tem_saida_de_parede()
    {
        var bola = Lancada(11, 4);
        var rastro = RastroFantasma.Prever(bola, lado: 1, jaQuicou: false);
        var caminho = Caminho(bola, 400);
        Assert.Equal(-1, rastro.PrimeiroDepoisDaParede);
        Assert.Equal(PassoDo(caminho, TipoDeEventoDaBola.Quique, ocorrencia: 2), rastro.Pontos.Count - 1);
    }

    [Fact]
    public void Vidro_antes_do_quique_e_bola_morta_e_nao_saida_de_parede()
    {
        var bola = Lancada(28, 3);
        var rastro = RastroFantasma.Prever(bola, lado: 1, jaQuicou: false);
        var caminho = Caminho(bola, 400);
        int parede = PassoDo(caminho, TipoDeEventoDaBola.Parede);
        Assert.True(parede >= 0 && parede < PassoDo(caminho, TipoDeEventoDaBola.Quique), "a cena devia ser vidro antes do quique");
        Assert.Equal(-1, rastro.PrimeiroDepoisDaParede);
        Assert.Equal(parede, rastro.Pontos.Count - 1);
    }

    /// <summary>
    /// O jeito do nível Padrão ("só na primeira batida no vidro"): pede o rastro no passo em que a bola bateu no vidro, já
    /// quicada. O próximo quique é o segundo e encerra o rastro, que inteiro é a saída.
    /// </summary>
    [Fact]
    public void Pedido_no_passo_da_batida_no_vidro_o_rastro_inteiro_e_a_saida_ate_o_proximo_quique()
    {
        var bola = Lancada(20, 3);
        var caminho = Caminho(bola, 400);
        int parede = PassoDo(caminho, TipoDeEventoDaBola.Parede);
        int segundoQuique = PassoDo(caminho, TipoDeEventoDaBola.Quique, ocorrencia: 2);
        var b = bola.Clonar();
        var eventos = new List<EventoDaBola>();
        for (int i = 0; i <= parede; i++) b.Avancar(Passo, eventos);
        var rastro = RastroFantasma.Prever(b, lado: 1, jaQuicou: true);
        Assert.Equal(-1, rastro.PrimeiroDepoisDaParede);   // o vidro já passou: não há outro pela frente
        Assert.Equal(segundoQuique - parede - 1, rastro.Pontos.Count - 1);
        Assert.True(rastro.Pontos[^1].X == caminho[segundoQuique].X && rastro.Pontos[^1].Y == caminho[segundoQuique].Y);
    }

    [Fact]
    public void Bola_fora_de_jogo_nao_tem_rastro()
    {
        var rastro = RastroFantasma.Prever(new Bola(), lado: 1, jaQuicou: false);
        Assert.Empty(rastro.Pontos);
        Assert.Equal(-1, rastro.PrimeiroDepoisDaParede);
    }

    [Fact]
    public void O_rastro_nao_passa_de_tres_segundos()
    {
        // Uma bola alta e lenta que fica no ar: o rastro para nos 3 s.
        var b = new Bola();
        b.Posicionar(0, 5, 1);
        b.Lancar(new Velocidade(0, 0.5f, 16, 0));
        var rastro = RastroFantasma.Prever(b, lado: 1, jaQuicou: false);
        Assert.Equal(RastroFantasma.PassosMaximos, rastro.Pontos.Count);
    }
}
